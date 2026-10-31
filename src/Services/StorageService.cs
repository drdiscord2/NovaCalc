using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NovaCalc.Services
{
    /// <summary>Настройки приложения (сериализуются в локальный JSON, без реестра).</summary>
    public class AppSettings
    {
        public string Theme = "dark";              // dark | light
        public string Language = null;              // null = авто по системе при первом запуске
        public int StartupMode = 0;                 // 0 обычный, 1 инженерный, 2 лабораторный
        public int MaxDigits = 16;                  // число отображаемых знаков (0..28)
        public bool TopMost = false;                // поверх всех окон
        public bool GroupSeparators = true;         // разделители тысяч
        public bool ScientificNotation = true;      // экспоненциальная запись для огромных/малых чисел
        public bool RememberLastMode = true;        // сохранять последний режим

        public Dictionary<string, object> ToJsonDict()
        {
            return new Dictionary<string, object>
            {
                { "theme", Theme },
                { "language", Language ?? "" },
                { "startupMode", StartupMode },
                { "maxDigits", MaxDigits },
                { "topMost", TopMost },
                { "groupSeparators", GroupSeparators },
                { "scientific", ScientificNotation },
                { "rememberLastMode", RememberLastMode },
            };
        }

        public static AppSettings FromJson(Dictionary<string, object> d)
        {
            var s = new AppSettings();
            s.Theme = MiniJson.GetStr(d, "theme", "dark");
            string lang = MiniJson.GetStr(d, "language", "");
            s.Language = string.IsNullOrEmpty(lang) ? null : lang;
            s.StartupMode = Clamp(MiniJson.GetInt(d, "startupMode", 0), 0, 2);
            s.MaxDigits = Clamp(MiniJson.GetInt(d, "maxDigits", 16), 0, 28);
            s.TopMost = MiniJson.GetBool(d, "topMost", false);
            s.GroupSeparators = MiniJson.GetBool(d, "groupSeparators", true);
            s.ScientificNotation = MiniJson.GetBool(d, "scientific", true);
            s.RememberLastMode = MiniJson.GetBool(d, "rememberLastMode", true);
            return s;
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }

    /// <summary>Одна запись истории вычислений.</summary>
    public class HistoryItem
    {
        public string Expression;
        public string Result;   // инвариантное строковое представление decimal

        public Dictionary<string, object> ToJsonDict()
        {
            return new Dictionary<string, object> { { "expr", Expression }, { "res", Result } };
        }

        public static HistoryItem FromJson(Dictionary<string, object> d)
        {
            return new HistoryItem
            {
                Expression = MiniJson.GetStr(d, "expr", ""),
                Result = MiniJson.GetStr(d, "res", "")
            };
        }
    }

    /// <summary>
    /// Хранилище настроек/истории/курсов. Локальный JSON, без реестра.
    /// Портативный режим: если рядом с exe лежит portable.flag — папка данных возле exe.
    /// Иначе %APPDATA%\NovaCalc. При недоступности основной папки — безопасный запасной путь
    /// (LocalAppData, затем Temp), приложение не падает.
    /// </summary>
    public static class StorageService
    {
        private const int MaxHistoryItems = 200;

        private static string _dataDir;

        /// <summary>Папка данных (создаётся при необходимости). Не бросает исключений.</summary>
        public static string DataDir
        {
            get
            {
                if (_dataDir != null) return _dataDir;
                try
                {
                    string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    if (exeDir == null) exeDir = ".";
                    if (File.Exists(Path.Combine(exeDir, "portable.flag")))
                    {
                        _dataDir = exeDir;
                        EnsureDir(_dataDir);
                        return _dataDir;
                    }
                }
                catch (Exception) { /* нет доступа к папке exe — идем дальше */ }

                string[] candidates =
                {
                    SafeEnv("APPDATA") != null ? Path.Combine(SafeEnv("APPDATA"), "NovaCalc") : null,
                    SafeEnv("LOCALAPPDATA") != null ? Path.Combine(SafeEnv("LOCALAPPDATA"), "NovaCalc") : null,
                    Path.Combine(Path.GetTempPath(), "NovaCalc")
                };
                foreach (var c in candidates)
                {
                    if (c == null) continue;
                    if (EnsureDir(c)) { _dataDir = c; return c; }
                }
                _dataDir = "."; // крайний случай — текущая папка
                return _dataDir;
            }
        }

        private static string SafeEnv(string name)
        {
            try { return Environment.GetFolderPath(name == "APPDATA" ? Environment.SpecialFolder.ApplicationData : Environment.SpecialFolder.LocalApplicationData); }
            catch (Exception) { return null; }
        }

        private static bool EnsureDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                // проверка записи
                string probe = Path.Combine(dir, ".novacalc_probe");
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string SettingsPath { get { return Path.Combine(DataDir, "settings.json"); } }
        private static string HistoryPath { get { return Path.Combine(DataDir, "history.json"); } }
        private static string RatesPath { get { return Path.Combine(DataDir, "rates.json"); } }

        /// <summary>Читает настройки; при любой ошибке возвращает значения по умолчанию.</summary>
        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string text = File.ReadAllText(SettingsPath, Encoding.UTF8);
                    var obj = MiniJson.GetObj(MiniJson.Parse(text));
                    return AppSettings.FromJson(obj);
                }
            }
            catch (Exception) { /* поврежденный файл — молча используем дефолт */ }
            return new AppSettings();
        }

        public static void SaveSettings(AppSettings s)
        {
            TryWriteJson(SettingsPath, MiniJson.Serialize(s.ToJsonDict()));
        }

        public static List<HistoryItem> LoadHistory()
        {
            var list = new List<HistoryItem>();
            try
            {
                if (File.Exists(HistoryPath))
                {
                    string text = File.ReadAllText(HistoryPath, Encoding.UTF8);
                    var arr = MiniJson.GetArray(MiniJson.GetObj(MiniJson.Parse("{\"items\":" + text + "}")), "items");
                    foreach (var it in arr)
                    {
                        var d = it as Dictionary<string, object>;
                        if (d != null) list.Add(HistoryItem.FromJson(d));
                    }
                }
            }
            catch (Exception) { }
            return list;
        }

        public static void SaveHistory(List<HistoryItem> items)
        {
            if (items.Count > MaxHistoryItems)
                items.RemoveRange(0, items.Count - MaxHistoryItems);
            var arr = new List<object>();
            foreach (var it in items) arr.Add(it.ToJsonDict());
            var root = new Dictionary<string, object> { { "items", arr } };
            TryWriteJson(HistoryPath, MiniJson.Serialize(root));
        }

        public static void AddHistory(List<HistoryItem> items, string expression, decimal result)
        {
            items.Add(new HistoryItem { Expression = expression, Result = result.ToString(CultureInfo.InvariantCulture) });
            SaveHistory(items);
        }

        /// <summary>Попытка записи файла; ошибки проглатываем (только для чтения и т.п.) — приложение не падает.</summary>
        public static void TryWriteJson(string path, string json)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // Запасной путь: temp-копия, чтобы данные не потерялись совсем
                try
                {
                    string alt = Path.Combine(Path.GetTempPath(), "NovaCalc", Path.GetFileName(path));
                    Directory.CreateDirectory(Path.GetDirectoryName(alt));
                    File.WriteAllText(alt, json, new UTF8Encoding(false));
                }
                catch (Exception) { /* окончательно не можем сохранить — молча продолжаем работу */ }
            }
        }

        public static string ReadAllTextSafe(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null; }
            catch (Exception) { return null; }
        }

        public static string RatesFilePath { get { return RatesPath; } }
    }
}
