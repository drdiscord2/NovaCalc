using System;
using System.Collections.Generic;
using System.Globalization;

namespace NovaCalc.Converters
{
    /// <summary>Системы счисления: DEC/HEX/OCT/BIN и переводы между ними.</summary>
    public static class NumberSystems
    {
        public enum Base { Dec = 10, Hex = 16, Oct = 8, Bin = 2 }

        /// <summary>Парсит целое число в указанной системе счисления (знаковое long).</summary>
        public static long Parse(string text, Base b)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) throw new FormatException("empty");
            bool neg = false;
            int start = 0;
            if (text[0] == '-') { neg = true; start = 1; }
            else if (text[0] == '+') start = 1;
            string body = text.Substring(start);
            // префиксы вида 0x / 0b / 0o допустимы для соответствующих баз
            if (b == Base.Hex && body.Length > 2 && body[0] == '0' && (body[1] == 'x' || body[1] == 'X')) body = body.Substring(2);
            if (b == Base.Bin && body.Length > 2 && body[0] == '0' && (body[1] == 'b' || body[1] == 'B')) body = body.Substring(2);
            if (b == Base.Oct && body.Length > 2 && body[0] == '0' && (body[1] == 'o' || body[1] == 'O')) body = body.Substring(2);
            if (body.Length == 0) throw new FormatException("empty digits");
            ulong u;
            NumberStyles st = NumberStyles.AllowLeadingSign;
            switch (b)
            {
                case Base.Hex:
                    // AllowHexSpecifier нельзя комбинировать с AllowLeadingSign — знак уже снят выше
                    u = ulong.Parse(body, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture); break;
                case Base.Oct:
                    u = ParseRadix(body, 8); break;
                case Base.Bin:
                    u = ParseRadix(body, 2); break;
                default:
                    // decimal — через ulong, чтобы поддержать значения больше long.MaxValue
                    u = ulong.Parse(body, st, CultureInfo.InvariantCulture); break;
            }
            if (neg) return unchecked((long)(0UL - u));
            if (u > long.MaxValue) throw new OverflowException("out of int64 range");
            return unchecked((long)u);
        }

        /// <summary>Форматирует целое в строку для базы (отрицательные — со знаком).</summary>
        public static string Format(long value, Base b)
        {
            switch (b)
            {
                case Base.Hex:
                    if (value < 0) return "-" + (-value).ToString("X", CultureInfo.InvariantCulture);
                    return value.ToString("X", CultureInfo.InvariantCulture);
                case Base.Oct:
                    return ToBaseString(value, 8);
                case Base.Bin:
                    return ToBaseString(value, 2);
                default:
                    return value.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Разбор строки в произвольной системе (2/8) в ulong без зависимости от Convert.</summary>
        private static ulong ParseRadix(string s, int radix)
        {
            bool neg = false;
            int i = 0;
            if (s.Length > 0 && (s[0] == '-' || s[0] == '+')) { neg = s[0] == '-'; i = 1; }
            ulong acc = 0UL;
            for (; i < s.Length; i++)
            {
                int d = (int)char.GetNumericValue(s[i]);
                if (d < 0 || d >= radix) throw new FormatException("bad digit");
                acc = acc * (ulong)radix + (ulong)d;
            }
            return neg ? unchecked(0UL - acc) : acc;
        }

        private static string ToBaseString(long value, int radix)
        {
            bool neg = value < 0;
            // обработка long.MinValue без переполнения
            ulong uv = neg ? (ulong)(-(value + 1)) + 1UL : (ulong)value;
            if (uv == 0) return "0";
            var chars = new List<char>();
            while (uv > 0)
            {
                chars.Add((char)('0' + (int)(uv % (ulong)radix)));
                uv /= (ulong)radix;
            }
            var sb = new System.Text.StringBuilder();
            if (neg) sb.Append('-');
            for (int i = chars.Count - 1; i >= 0; i--) sb.Append(chars[i]);
            return sb.ToString();
        }

        /// <summary>Конвертирует строку из одной системы в другую.</summary>
        public static string Convert(string text, Base from, Base to)
        {
            return Format(Parse(text, from), to);
        }
    }

    /// <summary>Статистика по списку чисел: сумма, среднее, min, max, медиана, станд. отклонение.</summary>
    public static class Statistics
    {
        public class Result
        {
            public int Count;
            public decimal Sum;
            public decimal Mean;
            public decimal Min;
            public decimal Max;
            public decimal Median;
            public decimal StdDev; // выборочное (n-1) при n>1, иначе 0
        }

        public static Result Compute(List<decimal> values)
        {
            if (values == null || values.Count == 0)
                throw new ArgumentException("no data");
            var r = new Result { Count = values.Count };
            decimal sum = 0m;
            decimal min = values[0], max = values[0];
            foreach (var v in values)
            {
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            r.Sum = sum;
            r.Mean = sum / values.Count;
            r.Min = min;
            r.Max = max;

            var sorted = new List<decimal>(values);
            sorted.Sort();
            int n = sorted.Count;
            r.Median = (n % 2 == 1) ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2m;

            if (n > 1)
            {
                double mean = (double)r.Mean;
                double acc = 0.0;
                foreach (var v in sorted)
                {
                    double d = (double)v - mean;
                    acc += d * d;
                }
                double sd = Math.Sqrt(acc / (n - 1));
                // округляем до 15 значащих цифр, как остальной движок
                r.StdDev = decimal.Round((decimal)sd, Math.Min(15, 28), MidpointRounding.AwayFromZero);
            }
            return r;
        }

        /// <summary>Разбирает список чисел из текста (разделители: любая запятая/точка с запятой/новая строка/пробел).</summary>
        public static List<decimal> ParseList(string text)
        {
            var list = new List<decimal>();
            if (string.IsNullOrEmpty(text)) return list;
            string[] parts = text.Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                decimal v;
                if (Localization.Strings.TryParseNumber(p.Trim(), out v)) list.Add(v);
                else if (decimal.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) list.Add(v);
            }
            return list;
        }
    }

    /// <summary>Дата-калькулятор: разница дат, прибавление/вычитание дней.</summary>
    public static class DateCalculator
    {
        public struct DateDiff
        {
            public int TotalDays;
            public int Years;
            public int Months;
            public int Days;
        }

        public static bool TryParseDate(string text, out DateTime dt)
        {
            text = (text ?? "").Trim();
            if (DateTime.TryParseExact(text, new[] { "yyyy-MM-dd", "dd.MM.yyyy", "MM/dd/yyyy", "dd/MM/yyyy", "yyyy.M.d", "d.M.yyyy" },
                                        CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return true;
            return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt);
        }

        /// <summary>Разница между датами (to - from).</summary>
        public static DateDiff Difference(DateTime from, DateTime to)
        {
            int total = (int)(to.Date - from.Date).TotalDays;
            int years = to.Year - from.Year;
            int months = to.Month - from.Month;
            int days = to.Day - from.Day;
            if (days < 0)
            {
                months--;
                // дни в предыдущем месяце относительно 'to'
                days += DateTime.DaysInMonth(to.Year, to.Month == 1 ? 12 : to.Month - 1);
            }
            if (months < 0) { months += 12; years--; }
            return new DateDiff { TotalDays = total, Years = years, Months = months, Days = days };
        }

        /// <summary>Прибавляет (или вычитает при отрицательном) количество дней.</summary>
        public static DateTime AddDays(DateTime date, int days)
        {
            try { return date.AddDays(days); }
            catch (ArgumentOutOfRangeException) { return date; }
        }
    }

    /// <summary>Физические константы (CODATA 2018, точные значения СИ где определены).</summary>
    public static class PhysicalConstants
    {
        public class Constant
        {
            public string Key;       // ключ локализации имени
            public string Symbol;
            public decimal Value;
            public string Unit;      // единица (не локализуется, физическая запись)

            public Constant(string key, string symbol, decimal value, string unit)
            {
                Key = key; Symbol = symbol; Value = value; Unit = unit;
            }
        }

        public static readonly List<Constant> All = new List<Constant>
        {
            new Constant("const_c_speedoflight", "c", 299792458m, "m/s"),
            new Constant("const_c_gravity", "G", 0.0000000000667430m, "m^3/(kg*s^2)"),
            new Constant("const_c_planck", "h", 0.00000000000000000000000000000662607015m, "J*s"),
            new Constant("const_c_boltzmann", "k", 0.00000000000001380649m, "J/K"),
            new Constant("const_c_avogadro", "N_A", 602214076000000000000000m, "1/mol"),
            new Constant("const_c_echarge", "e", 0.0000000000000000001602176634m, "C"),
            new Constant("const_c_electronmass", "m_e", 0.00000000000000000000000000091093837015m, "kg"),
            new Constant("const_c_gas", "R", 8.314462618m, "J/(mol*K)"),
        };
    }
}
