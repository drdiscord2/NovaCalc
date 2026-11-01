using System;
using System.Collections.Generic;
using System.Globalization;
using NovaCalc.Engine;
using NovaCalc.Converters;
using NovaCalc.Localization;

namespace NovaCalc.Services
{
    /// <summary>
    /// Встроенный самотест (NovaCalc.exe --selftest).
    /// Не открывает интерфейс, возвращает 0 при успехе, 1 при ошибках.
    /// </summary>
    public static class SelfTest
    {
        private static int _passed;
        private static readonly List<string> _failures = new List<string>();

        public static int Run()
        {
            _passed = 0;
            _failures.Clear();
            var inv = CultureInfo.InvariantCulture;
            var e = new CalculatorEngine { AngleMode = AngleMode.Degrees };

            // ===== Арифметика и точность =====
            Check("0.1 + 0.2 == 0.3", Eval(e, "0.1 + 0.2") == 0.3m);
            Check("2 + 2 * 2 == 6", Eval(e, "2 + 2 * 2") == 6m);
            Check("(2 + 2) * 2 == 8", Eval(e, "(2 + 2) * 2") == 8m);
            Check("-5 + 3 == -2", Eval(e, "-5 + 3") == -2m);
            Check("10% == 0.1", Eval(e, "10%") == 0.1m);
            Check("2^10 == 1024", Eval(e, "2^10") == 1024m);
            Check("sqrt(9) == 3", Eval(e, "sqrt(9)") == 3m);
            Check("Ans works", AnsCheck());
            Check("pi constant sane", Math.Abs((double)Eval(e, "pi") - Math.PI) < 1e-12);
            Check("nested functions root(3,27)==3", Eval(e, "root(27, 3)") == 3m);
            Check("factorial 5! == 120", Eval(e, "5!") == 120m);
            Check("abs(-7) == 7", Eval(e, "abs(-7)") == 7m);
            Check("mod(10,3) == 1", Eval(e, "mod(10, 3)") == 1m);
            Check("sin(30deg) == 0.5", Eval(e, "sin(30)") == 0.5m);
            Check("ln(e) == 1", Eval(e, "ln(e)") == 1m);
            Check("log(1000) == 3", Eval(e, "log(1000)") == 3m);
            Check("hex literal 0x1F == 31", Eval(e, "0x1F") == 31m);
            Check("bin literal 0b1010 == 10", Eval(e, "0b1010") == 10m);
            Check("scientific 1.5e3 == 1500", Eval(e, "1.5e3") == 1500m);
            Check("bitand(12,10) == 8", Eval(e, "and(12, 10)") == 8m);
            Check("bitor(12,10) == 14", Eval(e, "or(12, 10)") == 14m);
            Check("bitxor(12,10) == 6", Eval(e, "xor(12, 10)") == 6m);
            Check("shl(1,4) == 16", Eval(e, "shl(1, 4)") == 16m);
            Check("shr(16,2) == 4", Eval(e, "shr(16, 2)") == 4m);

            // ===== Проценты Windows-семантика (полный путь через парсер) =====
            Check("200 + 10% == 220", Eval(e, "200 + 10%") == 220m);
            Check("200 - 10% == 180", Eval(e, "200 - 10%") == 180m);
            Check("200 * 10% == 20", Eval(e, "200 * 10%") == 20m);
            Check("200 / 10% == 2000", Eval(e, "200 / 10%") == 2000m);
            Check("root(16,2) == 4", Eval(e, "root(16, 2)") == 4m);
            Check("pi*2 constant no-parens", Math.Abs((double)Eval(e, "pi * 2") - 2 * Math.PI) < 1e-12);
            Check("e^2 constant no-parens", Math.Abs((double)Eval(e, "e^2") - Math.E * Math.E) < 1e-9);
            Check("pow10(3) == 1000", Eval(e, "pow10(3)") == 1000m);
            Check("pow2(10) == 1024", Eval(e, "pow2(10)") == 1024m);

            // ===== Ошибки =====
            bool divZeroCaught = false;
            try { Eval(e, "1/0"); } catch (DivideByZeroCalcException) { divZeroCaught = true; }
            catch (CalcException) { divZeroCaught = true; }
            Check("division by zero raises error", divZeroCaught);

            bool badExprCaught = false;
            try { Eval(e, "2++*"); } catch (CalcException) { badExprCaught = true; }
            Check("bad syntax raises error", badExprCaught);

            bool sqrtNegCaught = false;
            try { Eval(e, "sqrt(-1)"); } catch (CalcException) { sqrtNegCaught = true; }
            Check("sqrt(-1) raises error", sqrtNegCaught);

            // ===== Системы счисления =====
            Check("HEX 1F -> DEC 31", NumberSystems.Parse("1F", NumberSystems.Base.Hex) == 31);
            Check("BIN 1010 -> DEC 10", NumberSystems.Parse("1010", NumberSystems.Base.Bin) == 10);
            Check("DEC 31 -> HEX 1F", NumberSystems.Format(31, NumberSystems.Base.Hex) == "1F");
            Check("DEC 10 -> BIN 1010", NumberSystems.Format(10, NumberSystems.Base.Bin) == "1010");
            Check("OCT 17 -> DEC 15", NumberSystems.Parse("17", NumberSystems.Base.Oct) == 15);

            // ===== Конвертеры единиц =====
            Check("1 km == 1000 m", UnitConverter.Convert("length", "km", "m", 1m) == 1000m);
            Check("1 kg == 1000 g", UnitConverter.Convert("mass", "kg", "g", 1m) == 1000m);
            Check("0 C == 32 F", UnitConverter.Convert("temperature", "C", "F", 0m) == 32m);
            Check("100 C == 212 F", UnitConverter.Convert("temperature", "C", "F", 100m) == 212m);
            Check("1 GiB == 1024 MiB", UnitConverter.Convert("data", "gib", "mib", 1m) == 1024m);
            Check("1 GB == 1e9 bytes", UnitConverter.Convert("data", "gb", "byte", 1m) == 1000000000m);
            Check("1 h == 3600 s", UnitConverter.Convert("time", "h", "s", 1m) == 3600m);

            // ===== Статистика =====
            var stats = Statistics.Compute(new List<decimal> { 1m, 2m, 3m, 4m, 5m });
            Check("mean of 1..5 == 3", stats.Mean == 3m);
            Check("sum of 1..5 == 15", stats.Sum == 15m);
            Check("median of 1..5 == 3", stats.Median == 3m);
            Check("min==1 max==5", stats.Min == 1m && stats.Max == 5m);
            Check("stdev of 1..5 ~ 1.5811", Math.Abs((double)stats.StdDev - 1.5811388300841898) < 1e-12);

            // ===== Дата-калькулятор =====
            DateTime d1 = new DateTime(2026, 1, 1), d2 = new DateTime(2026, 1, 31);
            Check("date diff 30 days", DateCalculator.Difference(d1, d2).TotalDays == 30);
            Check("date add 10 days", DateCalculator.AddDays(d1, 10) == new DateTime(2026, 1, 11));

            // ===== Валюты =====
            var rates = CurrencyService.DefaultList();
            Check("currency RUB present", rates.Exists(c => c.Code == "RUB"));
            decimal rub = CurrencyService.Convert(rates, "USD", "RUB", 1m);
            Check("currency USD->RUB positive", rub > 0m);
            decimal back = CurrencyService.Convert(rates, "RUB", "USD", rub);
            Check("currency roundtrip", Math.Abs((double)(back - 1m)) < 1e-9);

            // ===== Локализация =====
            Check("English resources present", Strings.HasTranslations("en"));
            Check("Russian resources present", Strings.HasTranslations("ru"));
            Strings.Current = "en";
            string en = Strings.T("mode_standard");
            Strings.Current = "ru";
            string ru = Strings.T("mode_standard");
            Strings.Current = "en";
            Check("en/ru differ", en != ru && !string.IsNullOrEmpty(en) && !string.IsNullOrEmpty(ru));
            Check("fallback for missing lang uses en", Strings.T("mode_standard", "xx") == en);
            foreach (var lang in Strings.SupportedLanguages)
                Check("lang '" + lang + "' has all keys", AllKeysPresent(lang));

            // ===== Форматирование =====
            Strings.Current = "ru";
            string fmt = Strings.FormatNumber(1234.5m, 16, true, true);
            Strings.Current = "en";
            Check("localized formatting does not crash", !string.IsNullOrEmpty(fmt));
            Check("format 0.1+0.2 shows 0.3", Strings.FormatNumber(0.1m + 0.2m, 16, false, true) == "0.3");

            // ===== JSON =====
            bool jsonOk = false;
            try
            {
                var dict = new Dictionary<string, object> { { "a", 1.25m }, { "b", "тест" }, { "c", true } };
                string js = MiniJson.Serialize(dict);
                var parsed = MiniJson.GetObj(MiniJson.Parse(js));
                jsonOk = MiniJson.GetDec(parsed, "a", 0m) == 1.25m && MiniJson.GetStr(parsed, "b", "") == "тест" && MiniJson.GetBool(parsed, "c", false);
            }
            catch (Exception) { jsonOk = false; }
            Check("MiniJson roundtrip", jsonOk);

            // ===== Итог =====
            Console.WriteLine("NovaCalc selftest: passed " + _passed + ", failed " + _failures.Count);
            foreach (var f in _failures) Console.WriteLine("  FAIL: " + f);
            return _failures.Count == 0 ? 0 : 1;
        }

        private static decimal Eval(CalculatorEngine e, string expr)
        {
            return e.Evaluate(expr);
        }

        private static bool AnsCheck()
        {
            var e = new CalculatorEngine();
            e.Evaluate("40+2");
            return e.Ans == 42m;
        }

        private static bool AllKeysPresent(string lang)
        {
            Strings.Current = lang;
            foreach (var key in Strings.Keys)
            {
                string v = Strings.T(key, lang);
                if (string.IsNullOrEmpty(v)) { Strings.Current = "en"; return false; }
            }
            Strings.Current = "en";
            return true;
        }

        private static void Check(string name, bool ok)
        {
            if (ok) _passed++;
            else _failures.Add(name);
        }
    }
}
