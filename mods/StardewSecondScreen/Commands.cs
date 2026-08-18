using System;
using System.Collections.Concurrent;

namespace StardewSecondScreen
{

    internal readonly struct Command
    {
        public readonly string Type;
        public readonly int A;
        public readonly int B;

        public readonly string Key;

        public Command(string type, int a, int b, string key = "")
        {
            Type = type;
            A = a;
            B = b;
            Key = key;
        }
    }

    internal sealed class CommandQueue
    {
        private const int Limit = 64;

        private readonly ConcurrentQueue<Command> _pending = new();

        public void Accept(string raw)
        {
            if (_pending.Count >= Limit) return;

            var type = Field(raw, "type");
            if (string.IsNullOrEmpty(type)) return;

            _pending.Enqueue(new Command(
                type!,
                Number(raw, "slot") ?? Number(raw, "from") ?? Number(raw, "value") ?? -1,
                Number(raw, "to") ?? -1,
                Field(raw, "key") ?? ""));
        }

        public void Drain(Action<Command> apply)
        {
            while (_pending.TryDequeue(out var command))
            {
                try { apply(command); }
                catch {  }
            }
        }

        private static string? Field(string raw, string key)
        {
            var marker = "\"" + key + "\"";
            var at = raw.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) return null;
            var colon = raw.IndexOf(':', at + marker.Length);
            if (colon < 0) return null;
            var open = raw.IndexOf('"', colon);
            if (open < 0) return null;
            var close = raw.IndexOf('"', open + 1);
            if (close < 0) return null;
            return raw.Substring(open + 1, close - open - 1);
        }

        private static int? Number(string raw, string key)
        {
            var marker = "\"" + key + "\"";
            var at = raw.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) return null;
            var colon = raw.IndexOf(':', at + marker.Length);
            if (colon < 0) return null;

            var start = colon + 1;
            while (start < raw.Length && (raw[start] == ' ' || raw[start] == '"')) start++;
            var end = start;
            while (end < raw.Length && (char.IsDigit(raw[end]) || raw[end] == '-')) end++;
            return int.TryParse(raw.Substring(start, end - start), out var value) ? value : null;
        }
    }
}
