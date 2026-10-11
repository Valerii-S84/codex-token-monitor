using System;
using System.Globalization;
using System.Text;

namespace CodexTokenMonitor
{
    internal static class JsonLite
    {
        public static int IndexOf(byte[] data, int length, string ascii, int start)
        {
            if (data == null || ascii == null || start < 0) return -1;
            byte[] needle = Encoding.ASCII.GetBytes(ascii);
            int last = length - needle.Length;
            for (int i = start; i <= last; i++)
            {
                int j = 0;
                while (j < needle.Length && data[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        public static string GetString(byte[] data, int length, string key, int start)
        {
            int keyAt = IndexOf(data, length, "\"" + key + "\"", Math.Max(0, start));
            if (keyAt < 0) return null;
            int i = keyAt + key.Length + 2;
            while (i < length && data[i] != (byte)':') i++;
            if (i >= length) return null;
            i++;
            while (i < length && IsSpace(data[i])) i++;
            if (i >= length || data[i] != (byte)'\"') return null;
            int valueStart = ++i;
            bool escaped = false;
            while (i < length)
            {
                byte b = data[i];
                if (b == (byte)'\"' && !escaped)
                {
                    string raw = Encoding.UTF8.GetString(data, valueStart, i - valueStart);
                    return DecodeString(raw);
                }
                if (b == (byte)'\\') escaped = !escaped;
                else escaped = false;
                i++;
            }
            return null;
        }

        public static long GetLong(byte[] data, int length, string key, int start, int end, long fallback)
        {
            if (end <= 0 || end > length) end = length;
            int keyAt = IndexOf(data, end, "\"" + key + "\"", Math.Max(0, start));
            if (keyAt < 0 || keyAt >= end) return fallback;
            int i = keyAt + key.Length + 2;
            while (i < end && data[i] != (byte)':') i++;
            if (i >= end) return fallback;
            i++;
            while (i < end && IsSpace(data[i])) i++;
            int numberStart = i;
            if (i < end && data[i] == (byte)'-') i++;
            while (i < end && data[i] >= (byte)'0' && data[i] <= (byte)'9') i++;
            if (i <= numberStart) return fallback;
            long result;
            string value = Encoding.ASCII.GetString(data, numberStart, i - numberStart);
            return Int64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }

        public static int FindObjectStart(byte[] data, int length, string key, int start)
        {
            int keyAt = IndexOf(data, length, "\"" + key + "\"", Math.Max(0, start));
            if (keyAt < 0) return -1;
            int i = keyAt + key.Length + 2;
            while (i < length && data[i] != (byte)':') i++;
            while (i < length && data[i] != (byte)'{') i++;
            return i < length ? i : -1;
        }

        public static int FindObjectEnd(byte[] data, int length, int objectStart)
        {
            if (objectStart < 0 || objectStart >= length || data[objectStart] != (byte)'{') return -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = objectStart; i < length; i++)
            {
                byte b = data[i];
                if (inString)
                {
                    if (b == (byte)'\"' && !escaped) inString = false;
                    if (b == (byte)'\\') escaped = !escaped;
                    else escaped = false;
                    continue;
                }
                if (b == (byte)'\"') inString = true;
                else if (b == (byte)'{') depth++;
                else if (b == (byte)'}' && --depth == 0) return i + 1;
            }
            return -1;
        }

        private static bool IsSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static string DecodeString(string raw)
        {
            if (raw.IndexOf('\\') < 0) return raw;
            StringBuilder result = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c != '\\' || i + 1 >= raw.Length)
                {
                    result.Append(c);
                    continue;
                }
                char esc = raw[++i];
                if (esc == '\"' || esc == '\\' || esc == '/') result.Append(esc);
                else if (esc == 'b') result.Append('\b');
                else if (esc == 'f') result.Append('\f');
                else if (esc == 'n') result.Append('\n');
                else if (esc == 'r') result.Append('\r');
                else if (esc == 't') result.Append('\t');
                else if (esc == 'u' && i + 4 < raw.Length)
                {
                    int code;
                    if (Int32.TryParse(raw.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                    {
                        result.Append((char)code);
                        i += 4;
                    }
                    else result.Append(esc);
                }
                else result.Append(esc);
            }
            return result.ToString();
        }
    }
}
