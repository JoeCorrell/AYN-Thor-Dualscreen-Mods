using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StardewSecondScreen
{

    internal static class Json
    {
        public static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var sb = new StringBuilder(value!.Length + 8);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:

                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public static string Str(string key, string? value) =>
            $"\"{key}\":\"{Escape(value)}\"";

        public static string Num(string key, int value) =>
            $"\"{key}\":{value.ToString(CultureInfo.InvariantCulture)}";

        public static string Flag(string key, bool value) =>
            $"\"{key}\":\"{(value ? "1" : "0")}\"";

        public static string Object(params string[] fields) =>
            "{" + string.Join(",", fields) + "}";

        public static string Array(string key, IEnumerable<string> objects) =>
            $"\"{key}\":[" + string.Join(",", objects) + "]";

        public static string Message(string type, params string[] fields)
        {
            var all = new List<string> { Str("type", type) };
            all.AddRange(fields);
            return "{" + string.Join(",", all) + "}";
        }
    }
}
