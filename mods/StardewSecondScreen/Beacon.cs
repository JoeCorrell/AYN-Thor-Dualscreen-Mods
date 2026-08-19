using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StardewSecondScreen
{
    internal sealed class Beacon : IDisposable
    {
        private const int BeaconPort = 7787;
        private const int IntervalMillis = 3000;

        private readonly int _port;
        private readonly Func<string> _describe;
        private readonly Action<string> _log;
        private readonly CancellationTokenSource _cancel = new();

        public Beacon(int port, Func<string> describe, Action<string> log)
        {
            _port = port;
            _describe = describe;
            _log = log;
        }

        public void Start()
        {
            _ = Task.Run(Announce);
        }

        private async Task Announce()
        {
            UdpClient? socket = null;
            try
            {
                socket = new UdpClient { EnableBroadcast = true };
                var target = new IPEndPoint(IPAddress.Broadcast, BeaconPort);
                _log($"Announcing on the network every {IntervalMillis / 1000} seconds.");

                while (!_cancel.IsCancellationRequested)
                {
                    try
                    {
                        var payload = Encoding.UTF8.GetBytes(_describe());
                        await socket.SendAsync(payload, payload.Length, target)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                    try
                    {
                        await Task.Delay(IntervalMillis, _cancel.Token).ConfigureAwait(false);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            catch (Exception failure)
            {
                _log("Could not announce on the network: " + failure.Message);
            }
            finally
            {
                socket?.Dispose();
            }
        }

        public void Dispose()
        {
            _cancel.Cancel();
        }
    }
}
