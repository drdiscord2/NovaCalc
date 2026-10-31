using System;
using System.Collections.Generic;
using System.Globalization;
using NovaCalc.Services;

namespace NovaCalc.Converters
{
    /// <summary>Одна валюта с курсом к базовой (USD).</summary>
    public class Currency
    {
        public string Code;
        public string Name;
        public decimal RateToBase;   // 1 USD = RateToBase <currency>
    }

    /// <summary>
    /// Офлайн-конвертер валют. Курсы хранятся локально в JSON (rates.json),
    /// пользователь может редактировать вручную; отображается дата последней редакции.
    /// Онлайн-обновление — только по явному нажатию, без интернета приложение не падает.
    /// </summary>
    public static class CurrencyService
    {
        /// <summary>Базовая валюта списка курсов.</summary>
        public const string BaseCode = "USD";

        // Стартовые курсы (условные, офлайн; пользователь может отредактировать)
        private static readonly Dictionary<string, string[]> Defaults = new Dictionary<string, string[]>
        {
            // code: name, rate to USD
            { "USD", new[] { "US Dollar", "1" } },
            { "RUB", new[] { "Russian Ruble", "90.5" } },
            { "EUR", new[] { "Euro", "0.92" } },
            { "CNY", new[] { "Chinese Yuan", "7.24" } },
            { "KZT", new[] { "Kazakhstani Tenge", "480.0" } },
            { "BYN", new[] { "Belarusian Ruble", "3.25" } },
            { "GBP", new[] { "British Pound", "0.79" } },
            { "JPY", new[] { "Japanese Yen", "150.5" } },
        };

        public static List<Currency> Load()
        {
            var list = new List<Currency>();
            try
            {
                string text = StorageService.ReadAllTextSafe(StorageService.RatesFilePath);
                if (!string.IsNullOrEmpty(text))
                {
                    var root = MiniJson.GetObj(MiniJson.Parse(text));
                    foreach (var it in MiniJson.GetArray(root, "rates"))
                    {
                        var d = MiniJson.GetObj(it);
                        var c = new Currency
                        {
                            Code = MiniJson.GetStr(d, "code", "").ToUpperInvariant(),
                            Name = MiniJson.GetStr(d, "name", ""),
                            RateToBase = MiniJson.GetDec(d, "rate", 0m)
                        };
                        if (c.Code.Length > 0 && c.RateToBase > 0m) list.Add(c);
                    }
                }
            }
            catch (Exception) { /* поврежденный файл — используем дефолты */ }

            if (list.Count == 0) list = DefaultList();
            // Гарантируем наличие базовой валюты
            if (list.Find(c => c.Code == BaseCode) == null)
                list.Insert(0, new Currency { Code = BaseCode, Name = "US Dollar", RateToBase = 1m });
            return list;
        }

        public static List<Currency> DefaultList()
        {
            var list = new List<Currency>();
            foreach (var kv in Defaults)
                list.Add(new Currency { Code = kv.Key, Name = kv.Value[0], RateToBase = decimal.Parse(kv.Value[1], CultureInfo.InvariantCulture) });
            return list;
        }

        public static void Save(List<Currency> rates, out string lastEditedIso)
        {
            lastEditedIso = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            var arr = new List<object>();
            foreach (var c in rates)
                arr.Add(new Dictionary<string, object> { { "code", c.Code }, { "name", c.Name }, { "rate", c.RateToBase } });
            var root = new Dictionary<string, object>
            {
                { "base", BaseCode },
                { "updated", lastEditedIso },
                { "rates", arr }
            };
            StorageService.TryWriteJson(StorageService.RatesFilePath, MiniJson.Serialize(root));
        }

        /// <summary>Дата последнего изменения курсов из файла (или пустая строка).</summary>
        public static string GetLastEditedDate()
        {
            try
            {
                string text = StorageService.ReadAllTextSafe(StorageService.RatesFilePath);
                if (!string.IsNullOrEmpty(text))
                {
                    var root = MiniJson.GetObj(MiniJson.Parse(text));
                    return MiniJson.GetStr(root, "updated", "");
                }
            }
            catch (Exception) { }
            return "";
        }

        /// <summary>Конвертация: amount из fromCode в toCode.</summary>
        public static decimal Convert(List<Currency> rates, string fromCode, string toCode, decimal amount)
        {
            var from = rates.Find(c => c.Code == fromCode);
            var to = rates.Find(c => c.Code == toCode);
            if (from == null || to == null || from.RateToBase <= 0m || to.RateToBase <= 0m)
                throw new FormatException("unknown currency");
            decimal usd = amount / from.RateToBase;
            return usd * to.RateToBase;
        }

        /// <summary>
        /// Пытается обновить курсы онлайн. Вызывается ТОЛЬКО по явному нажатию кнопки.
        /// Без интернета или при любой сетевой ошибке возвращает false — приложение не падает.
        /// Использует открытый бесплатный источник; формат парсится максимально защищённо.
        /// </summary>
        public static bool TryUpdateOnline(List<Currency> rates)
        {
            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    var proxy = System.Net.WebRequest.DefaultWebProxy;
                    if (proxy != null) { proxy.Credentials = System.Net.CredentialCache.DefaultCredentials; wc.Proxy = proxy; }
                    wc.Timeout = 6000; // не блокируем UI надолго
                    string json = wc.DownloadString("https://open.er-api.com/v6/latest/USD");
                    var root = MiniJson.GetObj(MiniJson.Parse(json));
                    var ratesObj = MiniJson.GetObj(root["rates"]);
                    bool any = false;
                    foreach (var c in rates)
                    {
                        object v;
                        if (c.Code != BaseCode && ratesObj.TryGetValue(c.Code, out v))
                        {
                            decimal r = MiniJson.GetDec(ratesObj, c.Code, 0m);
                            if (r > 0m) { c.RateToBase = r; any = true; }
                        }
                    }
                    if (any)
                    {
                        string tmp;
                        Save(rates, out tmp);
                    }
                    return any;
                }
            }
            catch (Exception)
            {
                return false; // нет сети / недоступно — молча, без падения
            }
        }
    }
}
