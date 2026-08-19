using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StardewSecondScreen
{

    internal sealed class Bridge : IDisposable
    {

        private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly int _port;
        private readonly Action<string> _log;
        private readonly CancellationTokenSource _cancel = new();
        private readonly List<TcpClient> _clients = new();
        private readonly object _lock = new();

        private readonly Dictionary<string, string> _latest = new();

        private readonly ConcurrentDictionary<TcpClient, ConcurrentQueue<byte[]>> _outbound = new();
        private readonly ConcurrentDictionary<TcpClient, bool> _writing = new();

        private const int OutboundLimit = 32;

        private TcpListener? _listener;

        private readonly bool _remote;

        public Bridge(int port, Action<string> log, bool remote = false)
        {
            _port = port;
            _log = log;
            _remote = remote;
        }

        public bool HasClients
        {
            get { lock (_lock) { return _clients.Count > 0; } }
        }

        public void Start()
        {
            try
            {

                _listener = new TcpListener(_remote ? IPAddress.Any : IPAddress.Loopback, _port);
                _listener.Start();
                _log(_remote
                    ? $"Listening on ws://0.0.0.0:{_port} (this machine and the network)"
                    : $"Listening on ws://127.0.0.1:{_port}");
                _ = Task.Run(AcceptLoop);
            }
            catch (Exception e)
            {
                _log($"Could not open the socket: {e.Message}");
            }
        }

        private async Task AcceptLoop()
        {
            while (!_cancel.IsCancellationRequested && _listener != null)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch { return; }

                _ = Task.Run(() => Serve(client));
            }
        }

        private async Task Serve(TcpClient client)
        {
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var key = await ReadHandshakeKey(stream).ConfigureAwait(false);
                if (key == null) { client.Close(); return; }

                using var sha = SHA1.Create();
                var accept = Convert.ToBase64String(
                    sha.ComputeHash(Encoding.ASCII.GetBytes(key + HandshakeGuid)));
                var response =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
                var bytes = Encoding.ASCII.GetBytes(response);
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);

                lock (_lock) { _clients.Add(client); }
                _log("wemu connected");

                List<string> backlog;
                lock (_lock) { backlog = new List<string>(_latest.Values); }
                foreach (var frame in backlog) Send(client, frame);

                await DrainUntilClosed(client, stream).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log($"Handshake failed: {e.Message}");
            }
            finally
            {
                lock (_lock) { _clients.Remove(client); }
                try { client.Close(); } catch { }
            }
        }

        private static async Task<string?> ReadHandshakeKey(NetworkStream stream)
        {
            var buffer = new byte[4096];
            var read = 0;

            while (read < buffer.Length)
            {
                var got = await stream.ReadAsync(buffer, read, buffer.Length - read).ConfigureAwait(false);
                if (got <= 0) return null;
                read += got;
                var soFar = Encoding.ASCII.GetString(buffer, 0, read);
                if (!soFar.Contains("\r\n\r\n")) continue;

                foreach (var line in soFar.Split(new[] { "\r\n" }, StringSplitOptions.None))
                {
                    if (!line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                        continue;
                    return line.Substring("Sec-WebSocket-Key:".Length).Trim();
                }
                return null;
            }
            return null;
        }

        public event Action<string>? MessageReceived;

        private async Task DrainUntilClosed(TcpClient client, NetworkStream stream)
        {
            var header = new byte[8];
            try
            {
                while (client.Connected && !_cancel.IsCancellationRequested)
                {
                    if (!await ReadExactly(stream, header, 2).ConfigureAwait(false)) break;

                    var opcode = header[0] & 0x0F;
                    if (opcode == 0x8) break;

                    var masked = (header[1] & 0x80) != 0;
                    long length = header[1] & 0x7F;

                    if (length == 126)
                    {
                        if (!await ReadExactly(stream, header, 2).ConfigureAwait(false)) break;
                        length = (header[0] << 8) | header[1];
                    }
                    else if (length == 127)
                    {
                        if (!await ReadExactly(stream, header, 8).ConfigureAwait(false)) break;
                        length = 0;
                        for (var i = 0; i < 8; i++) length = (length << 8) | header[i];
                    }

                    if (length < 0 || length > 64 * 1024) break;

                    var mask = new byte[4];
                    if (masked && !await ReadExactly(stream, mask, 4).ConfigureAwait(false)) break;

                    var payload = new byte[length];
                    if (length > 0 && !await ReadExactly(stream, payload, (int)length).ConfigureAwait(false)) break;
                    if (masked)
                    {
                        for (var i = 0; i < payload.Length; i++) payload[i] ^= mask[i % 4];
                    }

                    if (opcode == 0x1 && payload.Length > 0)
                    {
                        var text = Encoding.UTF8.GetString(payload);
                        try { MessageReceived?.Invoke(text); } catch { }
                    }
                }
            }
            catch {  }
            _log("wemu disconnected");
        }

        private static async Task<bool> ReadExactly(NetworkStream stream, byte[] buffer, int count)
        {
            var read = 0;
            while (read < count)
            {
                var got = await stream.ReadAsync(buffer, read, count - read).ConfigureAwait(false);
                if (got <= 0) return false;
                read += got;
            }
            return true;
        }

        public void Send(string payload)
        {
            List<TcpClient> targets;
            lock (_lock) { targets = new List<TcpClient>(_clients); }
            foreach (var client in targets) Send(client, payload);
        }

        public void Broadcast(string kind, string payload)
        {
            List<TcpClient> targets;
            lock (_lock)
            {
                _latest[kind] = payload;
                targets = new List<TcpClient>(_clients);
            }
            foreach (var client in targets) Send(client, payload);
        }

        private void Send(TcpClient client, string payload)
        {
            try
            {
                if (!client.Connected) return;
                var body = Encoding.UTF8.GetBytes(payload);

                using var frame = new MemoryStream(body.Length + 10);
                frame.WriteByte(0x81);

                if (body.Length < 126)
                {
                    frame.WriteByte((byte)body.Length);
                }
                else if (body.Length <= ushort.MaxValue)
                {
                    frame.WriteByte(126);
                    frame.WriteByte((byte)(body.Length >> 8));
                    frame.WriteByte((byte)(body.Length & 0xFF));
                }
                else
                {
                    frame.WriteByte(127);
                    for (var shift = 56; shift >= 0; shift -= 8)
                        frame.WriteByte((byte)((long)body.Length >> shift));
                }

                frame.Write(body, 0, body.Length);
                Enqueue(client, frame.ToArray());
            }
            catch
            {
                lock (_lock) { _clients.Remove(client); }
            }
        }

        private void Enqueue(TcpClient client, byte[] frame)
        {
            var queue = _outbound.GetOrAdd(client, _ => new ConcurrentQueue<byte[]>());
            while (queue.Count >= OutboundLimit) queue.TryDequeue(out _);
            queue.Enqueue(frame);

            if (_writing.TryAdd(client, true)) _ = Task.Run(() => WriteLoop(client, queue));
        }

        private async Task WriteLoop(TcpClient client, ConcurrentQueue<byte[]> queue)
        {
            try
            {
                var stream = client.GetStream();
                while (client.Connected && !_cancel.IsCancellationRequested)
                {
                    if (!queue.TryDequeue(out var frame))
                    {
                        await Task.Delay(15, _cancel.Token).ConfigureAwait(false);
                        continue;
                    }
                    await stream.WriteAsync(frame, 0, frame.Length).ConfigureAwait(false);
                }
            }
            catch {  }
            finally
            {

                _outbound.TryRemove(client, out _);
                _writing.TryRemove(client, out _);
            }
        }

        public void Dispose()
        {
            _cancel.Cancel();
            List<TcpClient> targets;
            lock (_lock) { targets = new List<TcpClient>(_clients); _clients.Clear(); }
            foreach (var client in targets)
            {
                try { client.Close(); } catch { }
            }
            try { _listener?.Stop(); } catch { }
        }
    }
}
