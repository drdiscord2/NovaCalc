using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NovaCalc.Services
{
    /// <summary>
    /// Минимальный JSON-сериализатор/парсер для нужд NovaCalc (настройки, история, курсы валют).
    /// Без внешних зависимостей и NuGet. Поддерживает: строки, числа, true/false, null,
    /// объекты (словари string->value), массивы. Экранирование — стандартное JSON.
    /// </summary>
    public static class MiniJson
    {
        // ===== Сериализация =====

        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is int || v is long) { sb.Append(Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is decimal) { sb.Append(((decimal)v).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is double) { sb.Append(((double)v).ToString("R", CultureInfo.InvariantCulture)); return; }
            var dict = v as Dictionary<string, object>;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key);
                    sb.Append(':');
                    Write(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            var list = v as List<object>;
            if (list != null)
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Write(sb, list[i]);
                }
                sb.Append(']');
                return;
            }
            WriteString(sb, v.ToString());
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ===== Парсинг =====

        /// <summary>Разбирает JSON в Dictionary&lt;string,object&gt;/List&lt;object&gt;/примитивы. При ошибке бросает FormatException.</summary>
        public static object Parse(string json)
        {
            int pos = 0;
            object result = ParseValue(json, ref pos);
            SkipWs(json, ref pos);
            return result;
        }

        private static object ParseValue(string s, ref int pos)
        {
            SkipWs(s, ref pos);
            if (pos >= s.Length) throw new FormatException("unexpected end of json");
            char c = s[pos];
            switch (c)
            {
                case '{': return ParseObject(s, ref pos);
                case '[': return ParseArray(s, ref pos);
                case '"': return ParseString(s, ref pos);
                case 't': Expect(s, ref pos, "true"); return true;
                case 'f': Expect(s, ref pos, "false"); return false;
                case 'n': Expect(s, ref pos, "null"); return null;
                default: return ParseNumber(s, ref pos);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int pos)
        {
            var d = new Dictionary<string, object>();
            pos++; // {
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == '}') { pos++; return d; }
            while (true)
            {
                SkipWs(s, ref pos);
                string key = ParseString(s, ref pos);
                SkipWs(s, ref pos);
                if (pos >= s.Length || s[pos] != ':') throw new FormatException("expected ':'");
                pos++;
                object val = ParseValue(s, ref pos);
                d[key] = val;
                SkipWs(s, ref pos);
                if (pos >= s.Length) throw new FormatException("unterminated object");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == '}') { pos++; return d; }
                throw new FormatException("expected ',' or '}'");
            }
        }

        private static List<object> ParseArray(string s, ref int pos)
        {
            var l = new List<object>();
            pos++; // [
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == ']') { pos++; return l; }
            while (true)
            {
                l.Add(ParseValue(s, ref pos));
                SkipWs(s, ref pos);
                if (pos >= s.Length) throw new FormatException("unterminated array");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == ']') { pos++; return l; }
                throw new FormatException("expected ',' or ']'");
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            if (s[pos] != '"') throw new FormatException("expected string");
            pos++;
            var sb = new StringBuilder();
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (pos >= s.Length) break;
                    char e = s[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (pos + 4 > s.Length) throw new FormatException("bad unicode escape");
                            string hex = s.Substring(pos, 4);
                            pos += 4;
                            int code;
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                throw new FormatException("bad unicode escape");
                            sb.Append((char)code);
                            break;
                        default: throw new FormatException("bad escape");
                    }
                }
                else sb.Append(c);
            }
            throw new FormatException("unterminated string");
        }

        private static object ParseNumber(string s, ref int pos)
        {
            int start = pos;
            if (pos < s.Length && (s[pos] == '-' || s[pos] == '+')) pos++;
            bool isFloat = false;
            while (pos < s.Length)
            {
                char c = s[pos];
                if (char.IsDigit(c)) { pos++; }
                else if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') { isFloat |= (c == '.' || c == 'e' || c == 'E'); pos++; }
                else break;
            }
            string text = s.Substring(start, pos - start);
            decimal dec;
            if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out dec))
                return dec; // все числа храним как decimal — точность
            double dbl;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out dbl))
                return dbl;
            throw new FormatException("bad number '" + text + "'");
        }

        private static void Expect(string s, ref int pos, string word)
        {
            if (pos + word.Length > s.Length || s.Substring(pos, word.Length) != word)
                throw new FormatException("expected '" + word + "'");
            pos += word.Length;
        }

        private static void SkipWs(string s, ref int pos)
        {
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t' || s[pos] == '\n' || s[pos] == '\r')) pos++;
        }

        // ===== Удобные извлечения =====

        public static string GetStr(Dictionary<string, object> d, string key, string def)
        {
            object v;
            if (d.TryGetValue(key, out v))
            {
                string s = v as string;
                if (s != null) return s;
                if (v != null) return v.ToString();
            }
            return def;
        }

        public static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            object v;
            if (d.TryGetValue(key, out v))
            {
                if (v is decimal) return (int)(decimal)v;
                if (v is double) return (int)(double)v;
                int r;
                if (int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out r)) return r;
            }
            return def;
        }

        public static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            object v;
            if (d.TryGetValue(key, out v))
            {
                if (v is bool) return (bool)v;
                string s = v as string;
                if (s == "true") return true;
                if (s == "false") return false;
            }
            return def;
        }

        public static decimal GetDec(Dictionary<string, object> d, string key, decimal def)
        {
            object v;
            if (d.TryGetValue(key, out v))
            {
                if (v is decimal) return (decimal)v;
                if (v is double) return (decimal)(double)v;
                decimal r;
                if (decimal.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
            }
            return def;
        }

        public static List<object> GetArray(Dictionary<string, object> d, string key)
        {
            object v;
            if (d.TryGetValue(key, out v))
            {
                var l = v as List<object>;
                if (l != null) return l;
            }
            return new List<object>();
        }

        public static Dictionary<string, object> GetObj(object v)
        {
            var d = v as Dictionary<string, object>;
            return d ?? new Dictionary<string, object>();
        }
    }
}
