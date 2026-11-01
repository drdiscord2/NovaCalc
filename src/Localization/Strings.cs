using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NovaCalc.Engine;

namespace NovaCalc.Localization
{
    /// <summary>Культура, используемая для всех вычислений и хранения чисел (инвариантная).</summary>
    public static class InvariantCultureHelper
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Локализация: статичные встроенные словари переводов. Без внешних файлов и API.
    /// Запасной язык — English. Числовые разделители берутся из CultureFor(),
    /// но математический движок всегда работает на инвариантной культуре.
    /// </summary>
    public static class Strings
    {
        // ===== Список поддерживаемых языков =====
        // Ключ — код языка, Value — (родное название, culture code)
        public static readonly List<string> SupportedLanguages = new List<string>
        {
            "en", "ru", "uk", "kk", "de", "fr", "es", "it", "pt", "tr", "zh", "ar"
        };

        private static readonly Dictionary<string, string[]> LangNames = new Dictionary<string, string[]>
        {
            // { английское название, местное название }
            { "en", new[] { "English", "English" } },
            { "ru", new[] { "Russian", "Русский" } },
            { "uk", new[] { "Ukrainian", "Українська" } },
            { "kk", new[] { "Kazakh", "Қазақша" } },
            { "de", new[] { "German", "Deutsch" } },
            { "fr", new[] { "French", "Français" } },
            { "es", new[] { "Spanish", "Español" } },
            { "it", new[] { "Italian", "Italiano" } },
            { "pt", new[] { "Portuguese", "Português" } },
            { "tr", new[] { "Turkish", "Türkçe" } },
            { "zh", new[] { "Chinese", "中文" } },
            { "ar", new[] { "Arabic", "العربية" } },
        };

        public static string LanguageDisplayName(string code)
        {
            string[] names;
            if (LangNames.TryGetValue(code, out names)) return names[1] + " (" + names[0] + ")";
            return code;
        }

        /// <summary>Нейтральное отображаемое имя языка (для списков настроек).</summary>
        public static string LanguageNativeName(string code)
        {
            string[] names;
            if (LangNames.TryGetValue(code, out names)) return names[1];
            return code;
        }

        /// <summary>CultureInfo для форматирования чисел в UI по коду языка.</summary>
        public static CultureInfo CultureFor(string lang)
        {
            try
            {
                switch (lang)
                {
                    case "en": return CultureInfo.GetCultureInfo("en-US");
                    case "ru": return CultureInfo.GetCultureInfo("ru-RU");
                    case "uk": return CultureInfo.GetCultureInfo("uk-UA");
                    case "kk": return CultureInfo.GetCultureInfo("kk-KZ");
                    case "de": return CultureInfo.GetCultureInfo("de-DE");
                    case "fr": return CultureInfo.GetCultureInfo("fr-FR");
                    case "es": return CultureInfo.GetCultureInfo("es-ES");
                    case "it": return CultureInfo.GetCultureInfo("it-IT");
                    case "pt": return CultureInfo.GetCultureInfo("pt-PT");
                    case "tr": return CultureInfo.GetCultureInfo("tr-TR");
                    case "zh": return CultureInfo.GetCultureInfo("zh-CN");
                    case "ar": return CultureInfo.GetCultureInfo("ar-SA");
                    default: return CultureInfo.GetCultureInfo("en-US");
                }
            }
            catch (Exception)
            {
                // Если культура недоступна в системе — не падаем
                return CultureInfo.InvariantCulture;
            }
        }

        /// <summary>Определяет язык по системе; если недоступен — English.</summary>
        public static string DetectSystemLanguage()
        {
            try
            {
                string twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                if (SupportedLanguages.Contains(twoLetter)) return twoLetter;
            }
            catch (Exception) { }
            return "en";
        }

        // ===== Словарь переводов =====
        // Формат: ключ -> массив [en, ru, uk, kk, de, fr, es, it, pt, tr, zh, ar]
        // Порядок кодов соответствует SupportedLanguages.
        private static readonly Dictionary<string, string[]> Table = BuildTable();

        public static string T(string key)
        {
            return T(key, Current);
        }

        public static string T(string key, string lang)
        {
            string[] row;
            if (!Table.TryGetValue(key, out row)) return key;
            int idx = SupportedLanguages.IndexOf(lang);
            if (idx < 0 || idx >= row.Length || string.IsNullOrEmpty(row[idx]))
                idx = 0; // запасной — English
            return row[idx];
        }

        /// <summary>Текущий язык интерфейса.</summary>
        public static string Current = "en";

        /// <summary>Безопасное приведение decimal к double (без переполнения на краях диапазона).</summary>
        public static double SafeToDouble(decimal value)
        {
            try { return (double)value; }
            catch (OverflowException) { return value > 0m ? double.MaxValue : double.MinValue; }
        }

        /// <summary>Форматирует decimal для отображения в UI с учётом локали и настройки числа знаков.</summary>
        public static string FormatNumber(decimal value, int maxDigits, bool groupSeparators, bool scientific)
        {
            CultureInfo ci = CultureFor(Current);
            double dv = SafeToDouble(value);
            if (scientific && !double.IsNaN(dv) && !double.IsInfinity(dv) && (Math.Abs(dv) >= 1e16 ||
                (value != 0m && Math.Abs(dv) < 1e-9)))
            {
                // нормализуем экспоненциальную запись: "1,23456E+021" -> "1,23456E+21"
                string s = dv.ToString("E" + Math.Max(2, Math.Min(maxDigits, 9)), ci);
                int eIdx = s.LastIndexOfAny(new[] { 'E', 'e' });
                if (eIdx >= 0)
                {
                    string mant = s.Substring(0, eIdx + 1);
                    string expPart = s.Substring(eIdx + 1);
                    char sign = '+';
                    if (expPart.Length > 0 && (expPart[0] == '+' || expPart[0] == '-')) { sign = expPart[0]; expPart = expPart.Substring(1); }
                    long ev;
                    if (long.TryParse(expPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out ev))
                        s = mant + sign + ev.ToString(CultureInfo.InvariantCulture);
                }
                return s;
            }
            decimal v = value;
            if (maxDigits >= 0 && maxDigits < 28)
            {
                try { v = Math.Round(v, maxDigits, MidpointRounding.AwayFromZero); }
                catch (ArgumentOutOfRangeException) { }
            }
            // убираем хвостовые нули scale'а decimal (20.0 -> 20), иначе вывод неаккуратный
            v = NovaCalc.Engine.CalculatorEngine.Normalize(v);
            string fmt = groupSeparators ? "#,##0.###############################" : "0.###############################";
            try
            {
                return v.ToString(fmt, ci);
            }
            catch (Exception)
            {
                return v.ToString(ci);
            }
        }

        /// <summary>Парсит число из строки UI в любой локали; при неудаче пробует инвариантную.</summary>
        public static bool TryParseNumber(string text, out decimal value)
        {
            text = (text ?? "").Trim();
            value = 0m;
            if (text.Length == 0) return false;
            CultureInfo ci = CultureFor(Current);
            if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, ci, out value)) return true;
            if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value)) return true;
            // нормализация: убираем пробелы и групповые разделители
            string cleaned = text.Replace(" ", "").Replace("\u00A0", "");
            if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowExponent, ci, out value)) return true;
            if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value)) return true;
            return false;
        }

        // ===== Переводы =====
        private static Dictionary<string, string[]> BuildTable()
        {
            var d = new Dictionary<string, string[]>();
            // en, ru, uk, kk, de, fr, es, it, pt, tr, zh, ar
            Add(d, "app_title", "NovaCalc", "NovaCalc", "NovaCalc", "NovaCalc", "NovaCalc", "NovaCalc", "NovaCalc", "NovaCalc", "NovaCalc", "NovaCalc", "NovaCalc", "NovaCalc");
            Add(d, "mode_standard", "Standard", "Обычный", "Звичайний", "Стандартты", "Standard", "Standard", "Estándar", "Standard", "Padrão", "Standart", "标准", "قياسي");
            Add(d, "mode_scientific", "Scientific", "Инженерный", "Інженерний", "Инженерлік", "Wissenschaftlich", "Scientifique", "Científico", "Scientifico", "Científico", "Bilimsel", "科学型", "علمي");
            Add(d, "mode_laboratory", "Laboratory", "Лабораторный", "Лабораторний", "Зертханалық", "Labor", "Laboratoire", "Laboratorio", "Laboratorio", "Laboratório", "Laboratuvar", "实验室", "مختبر");

            Add(d, "btn_equals", "=", "=", "=", "=", "=", "=", "=", "=", "=", "=", "=", "=");
            Add(d, "btn_clear", "C", "С", "С", "С", "C", "C", "C", "C", "C", "C", "C", "C");
            Add(d, "btn_clear_entry", "CE", "CE", "CE", "CE", "CE", "CE", "CE", "CE", "CE", "CE", "CE", "CE");
            Add(d, "btn_backspace", "⌫", "⌫", "⌫", "⌫", "⌫", "⌫", "⌫", "⌫", "⌫", "⌫", "⌫", "⌫");
            Add(d, "btn_negate", "+/-", "+/-", "+/-", "+/-", "+/-", "+/-", "+/-", "+/-", "+/-", "+/-", "+/-", "+/-");
            Add(d, "btn_percent", "%", "%", "%", "%", "%", "%", "%", "%", "%", "%", "%", "%");
            Add(d, "btn_sqrt", "√x", "√x", "√x", "√x", "√x", "√x", "√x", "√x", "√x", "√x", "√x", "√x");
            Add(d, "btn_square", "x²", "x²", "x²", "x²", "x²", "x²", "x²", "x²", "x²", "x²", "x²", "x²");
            Add(d, "btn_inverse", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x");
            Add(d, "btn_reciprocal", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x", "1/x");

            Add(d, "mem_mc", "MC", "MC", "MC", "MC", "MC", "MC", "MC", "MC", "MC", "MC", "MC", "MC");
            Add(d, "mem_mr", "MR", "MR", "MR", "MR", "MR", "MR", "MR", "MR", "MR", "MR", "MR", "MR");
            Add(d, "mem_mplus", "M+", "M+", "M+", "M+", "M+", "M+", "M+", "M+", "M+", "M+", "M+", "M+");
            Add(d, "mem_mminus", "M-", "M-", "M-", "M-", "M-", "M-", "M-", "M-", "M-", "M-", "M-", "M-");
            Add(d, "mem_ms", "MS", "MS", "MS", "MS", "MS", "MS", "MS", "MS", "MS", "MS", "MS", "MS");
            Add(d, "mem_indicator", "M", "M", "M", "M", "M", "M", "M", "M", "M", "M", "M", "M");

            Add(d, "history", "History", "История", "Історія", "Тарих", "Verlauf", "Historique", "Historial", "Cronologia", "Histórico", "Geçmiş", "历史记录", "السجل");
            Add(d, "history_empty", "No calculations yet", "Пока нет вычислений", "Ще немає обчислень", "Есептеулер жоқ", "Noch keine Berechnungen", "Aucun calcul", "Sin cálculos", "Nessun calcolo", "Sem cálculos", "Hesaplama yok", "暂无计算", "لا توجد عمليات");
            Add(d, "history_clear", "Clear history", "Очистить историю", "Очистити історію", "Тарихты өшіру", "Verlauf löschen", "Effacer l'historique", "Borrar historial", "Cancella cronologia", "Limpar histórico", "Geçmişi temizle", "清除历史", "مسح السجل");
            Add(d, "copy_result", "Copy result", "Копировать результат", "Копіювати результат", "Нәтижені көшіру", "Ergebnis kopieren", "Copier le résultat", "Copiar resultado", "Copia risultato", "Copiar resultado", "Sonucu kopyala", "复制结果", "نسخ النتيجة");
            Add(d, "paste", "Paste", "Вставить", "Вставити", "Қою", "Einfügen", "Coller", "Pegar", "Incolla", "Colar", "Yapıştır", "粘贴", "لصق");
            Add(d, "copy_expression", "Copy expression", "Копировать выражение", "Копіювати вираз", "Өрнекті көшіру", "Ausdruck kopieren", "Copier l'expression", "Copiar expresión", "Copia espressione", "Copiar expressão", "İfadeyi kopyala", "复制表达式", "نسخ الصيغة");

            Add(d, "settings", "Settings", "Настройки", "Налаштування", "Баптаулар", "Einstellungen", "Paramètres", "Ajustes", "Impostazioni", "Configurações", "Ayarlar", "设置", "الإعدادات");
            Add(d, "settings_theme", "Theme", "Тема", "Тема", "Тақырып", "Thema", "Thème", "Tema", "Tema", "Tema", "Tema", "主题", "المظهر");
            Add(d, "theme_dark", "Dark", "Тёмная", "Темна", "Күңгірт", "Dunkel", "Sombre", "Oscuro", "Scuro", "Escuro", "Koyu", "深色", "داكن");
            Add(d, "theme_light", "Light", "Светлая", "Світла", "Ашық", "Hell", "Clair", "Claro", "Chiaro", "Claro", "Açık", "浅色", "فاتح");
            Add(d, "settings_language", "Language", "Язык", "Мова", "Тіл", "Sprache", "Langue", "Idioma", "Lingua", "Idioma", "Dil", "语言", "اللغة");
            Add(d, "settings_startup_mode", "Startup mode", "Режим запуска", "Режим запуску", "Бастапқы режим", "Startmodus", "Mode de démarrage", "Modo inicial", "Modalità iniziale", "Modo inicial", "Başlangıç modu", "启动模式", "وضع البداية");
            Add(d, "settings_max_digits", "Displayed digits", "Число отображаемых знаков", "Кількість знаків", "Көрсетілетін цифрлар", "Anzeigeziffern", "Chiffres affichés", "Dígitos mostrados", "Cifre visualizzate", "Dígitos exibidos", "Gösterilen basamak", "显示位数", "عدد الخانات");
            Add(d, "settings_topmost", "Always on top", "Поверх всех окон", "Поверх усіх вікон", "Барлық терезелер үстінде", "Immer im Vordergrund", "Toujours au premier plan", "Siempre arriba", "Sempre in primo piano", "Sempre no topo", "Her zaman üstte", "总在最前", "دائمًا في الأعلى");
            Add(d, "settings_group_sep", "Thousands separators", "Разделители тысяч", "Роздільники тисяч", "Мыңдық бөлгіштер", "Tausendertrennzeichen", "Séparateurs de milliers", "Separadores de miles", "Separatori migliaia", "Separadores de milhar", "Binlik ayırıcı", "千位分隔符", "فاصل الآلاف");
            Add(d, "settings_scientific", "Scientific notation", "Экспоненциальная запись", "Експоненційний запис", "Экспонента жазуы", "Wissenschaftliche Schreibweise", "Notation scientifique", "Notación científica", "Notazione scientifica", "Notação científica", "Bilimsel gösterim", "科学计数法", "الترميز العلمي");
            Add(d, "settings_keep_mode", "Remember last mode", "Запоминать последний режим", "Запам'ятовувати останній режим", "Соңғы режимді есте сақтау", "Letzten Modus merken", "Mémoriser le dernier mode", "Recordar último modo", "Ricorda ultima modalità", "Lembrar último modo", "Son modu hatırla", "记住上次模式", "تذكر الوضع الأخير");
            Add(d, "settings_close", "Close", "Закрыть", "Закрити", "Жабу", "Schließen", "Fermer", "Cerrar", "Chiudi", "Fechar", "Kapat", "关闭", "إغلاق");
            Add(d, "settings_save", "Save", "Сохранить", "Зберегти", "Сақтау", "Speichern", "Enregistrer", "Guardar", "Salva", "Salvar", "Kaydet", "保存", "حفظ");

            Add(d, "angle_deg", "Deg", "Град", "Град", "Град", "Grad", "Deg", "Grados", "Gradi", "Graus", "Derece", "度", "درجة");
            Add(d, "angle_rad", "Rad", "Рад", "Рад", "Рад", "Bogenmaß", "Rad", "Radianes", "Radianti", "Radianos", "Radyan", "弧度", "راديان");
            Add(d, "angle_grad", "Grad", "Градианы", "Градiани", "Градиан", "Neugrad", "Grade", "Gradianes", "Gradianti", "Gradianos", "Gradyan", "百分度", "غراد");

            Add(d, "numsys_dec", "DEC", "DEC", "DEC", "DEC", "DEZ", "DEC", "DEC", "DEC", "DEC", "DEC", "十进制", "عشري");
            Add(d, "numsys_hex", "HEX", "HEX", "HEX", "HEX", "HEX", "HEX", "HEX", "HEX", "HEX", "HEX", "十六进制", "ست عشري");
            Add(d, "numsys_oct", "OCT", "OCT", "OCT", "OCT", "OKT", "OCT", "OCT", "OCT", "OCT", "OCT", "八进制", "ثماني");
            Add(d, "numsys_bin", "BIN", "BIN", "BIN", "BIN", "BIN", "BIN", "BIN", "BIN", "BIN", "BIN", "二进制", "ثنائي");

            Add(d, "lab_converter", "Unit converter", "Конвертер единиц", "Конвертер одиниць", "Өлшем бірліктер конвертері", "Einheitenumrechner", "Convertisseur d'unités", "Conversor de unidades", "Convertitore di unità", "Conversor de unidades", "Birim dönüştürücü", "单位换算", "محول الوحدات");
            Add(d, "lab_currency", "Currency converter", "Конвертер валют", "Конвертер валют", "Валюта конвертері", "Währungsumrechner", "Convertisseur de devises", "Conversor de divisas", "Convertitore di valute", "Conversor de moedas", "Para birimi çevirici", "货币换算", "محول العملات");
            Add(d, "lab_constants", "Physical constants", "Физические константы", "Фізичні константи", "Физикалық тұрақтылар", "Physikalische Konstanten", "Constantes physiques", "Constantes físicas", "Costanti fisiche", "Constantes físicas", "Fiziksel sabitler", "物理常数", "الثوابت الفيزيائية");
            Add(d, "lab_datecalc", "Date calculator", "Дата-калькулятор", "Калькулятор дат", "Күні калькуляторы", "Datumsrechner", "Calculateur de dates", "Calculadora de fechas", "Calcolatore di date", "Calculadora de datas", "Tarih hesaplayıcı", "日期计算器", "حاسبة التواريخ");
            Add(d, "lab_statistics", "Statistics", "Статистика", "Статистика", "Статистика", "Statistik", "Statistiques", "Estadística", "Statistica", "Estatística", "İstatistik", "统计", "الإحصاء");
            Add(d, "lab_edit_rates", "Edit exchange rates", "Редактировать курсы валют", "Редагувати курси валют", "Валюта бағамдарын өңдеу", "Wechselkurse bearbeiten", "Modifier les taux de change", "Editar tipos de cambio", "Modifica tassi di cambio", "Editar taxas de câmbio", "Döviz kurlarını düzenle", "编辑汇率", "تحرير أسعار الصرف");

            Add(d, "cat_length", "Length", "Длина", "Довжина", "Ұзындық", "Länge", "Longueur", "Longitud", "Lunghezza", "Comprimento", "Uzunluk", "长度", "الطول");
            Add(d, "cat_mass", "Mass", "Масса", "Маса", "Масса", "Masse", "Masse", "Masa", "Massa", "Massa", "Kütle", "质量", "الكتلة");
            Add(d, "cat_temperature", "Temperature", "Температура", "Температура", "Температура", "Temperatur", "Température", "Temperatura", "Temperatura", "Temperatura", "Sıcaklık", "温度", "الحرارة");
            Add(d, "cat_volume", "Volume", "Объём", "Об'єм", "Көлем", "Volumen", "Volume", "Volumen", "Volume", "Volume", "Hacim", "体积", "الحجم");
            Add(d, "cat_area", "Area", "Площадь", "Площа", "Аудан", "Fläche", "Surface", "Área", "Area", "Área", "Alan", "面积", "المساحة");
            Add(d, "cat_speed", "Speed", "Скорость", "Швидкість", "Жылдамдық", "Geschwindigkeit", "Vitesse", "Velocidad", "Velocità", "Velocidade", "Hız", "速度", "السرعة");
            Add(d, "cat_time", "Time", "Время", "Час", "Уақыт", "Zeit", "Temps", "Tiempo", "Tempo", "Tempo", "Zaman", "时间", "الوقت");
            Add(d, "cat_pressure", "Pressure", "Давление", "Тиск", "Қысым", "Druck", "Pression", "Presión", "Pressione", "Pressão", "Basınç", "压强", "الضغط");
            Add(d, "cat_energy", "Energy", "Энергия", "Енергія", "Энергия", "Energie", "Énergie", "Energía", "Energia", "Energia", "Enerji", "能量", "الطاقة");
            Add(d, "cat_power", "Power", "Мощность", "Потужність", "Қуат", "Leistung", "Puissance", "Potencia", "Potenza", "Potência", "Güç", "功率", "القدرة");
            Add(d, "cat_data", "Data / Information", "Информация/данные", "Інформація/дані", "Ақпарат/деректер", "Daten", "Données", "Datos", "Dati", "Dados", "Veri", "数据", "البيانات");

            Add(d, "date_from", "From date", "Дата от", "Дата від", "Бастапқы дата", "Von", "De", "Desde", "Da", "De", "Den", "起始日期", "من");
            Add(d, "date_to", "To date", "Дата до", "Дата до", "Аяқталу датасы", "Bis", "À", "Hasta", "A", "Até", "Kadar", "结束日期", "إلى");
            Add(d, "date_diff", "Difference", "Разница", "Різниця", "Айырмашылық", "Differenz", "Différence", "Diferencia", "Differenza", "Diferença", "Fark", "差值", "الفرق");
            Add(d, "date_add", "Add / subtract days", "Прибавить/вычесть дни", "Додати/відняти дні", "Күн қосу/алу", "Tage addieren/subtrahieren", "Ajouter/soustraire des jours", "Sumar/restar días", "Aggiungi/sottrai giorni", "Somar/subtrair dias", "Gün ekle/çıkar", "加/减天数", "إضافة/طرح أيام");
            Add(d, "date_days", "days", "дней", "днів", "күн", "Tage", "jours", "días", "giorni", "dias", "gün", "天", "أيام");
            Add(d, "date_result", "Result", "Результат", "Результат", "Нәтиже", "Ergebnis", "Résultat", "Resultado", "Risultato", "Resultado", "Sonuç", "结果", "النتيجة");

            Add(d, "stats_input", "Numbers list (comma or newline separated)", "Список чисел (через запятую или с новой строки)", "Список чисел (коми або новий рядок)", "Сандар тізімі (үтір немесе жаңа жол)", "Zahlenliste (Komma oder neue Zeile)", "Liste de nombres (virgule ou nouvelle ligne)", "Lista de números (coma o nueva línea)", "Elenco numeri (virgola o nuova riga)", "Lista de números (vírgula ou nova linha)", "Sayı listesi (virgül veya yeni satır)", "数字列表（逗号或换行分隔）", "قائمة الأرقام (فاصلة أو سطر جديد)");
            Add(d, "stats_sum", "Sum", "Сумма", "Сума", "Қосынды", "Summe", "Somme", "Suma", "Somma", "Soma", "Toplam", "总和", "المجموع");
            Add(d, "stats_mean", "Mean", "Среднее", "Середнє", "Орташа мәні", "Mittelwert", "Moyenne", "Media", "Media", "Média", "Ortalama", "平均值", "المتوسط");
            Add(d, "stats_min", "Min", "Минимум", "Мінімум", "Минимум", "Minimum", "Minimum", "Mínimo", "Minimo", "Mínimo", "Min", "最小值", "الأصغر");
            Add(d, "stats_max", "Max", "Максимум", "Максимум", "Максимум", "Maximum", "Maximum", "Máximo", "Massimo", "Máximo", "Max", "最大值", "الأكبر");
            Add(d, "stats_median", "Median", "Медиана", "Медіана", "Медиана", "Median", "Médiane", "Mediana", "Mediana", "Mediana", "Medyan", "中位数", "الوسيط");
            Add(d, "stats_stdev", "Std deviation", "Стандартное отклонение", "Стандартне відхилення", "Стандартты ауытқу", "Standardabweichung", "Écart-type", "Desviación estándar", "Deviazione standard", "Desvio padrão", "Standart sapma", "标准差", "الانحراف المعياري");
            Add(d, "stats_count", "Count", "Количество", "Кількість", "Саны", "Anzahl", "Nombre", "Cantidad", "Numero", "Quantidade", "Adet", "数量", "العدد");

            Add(d, "rates_updated", "Rates updated", "Курсы обновлены", "Курси оновлено", "Бағамдар жаңартылды", "Kurse aktualisiert", "Taux mis à jour", "Tipos actualizados", "Tassi aggiornati", "Taxas atualizadas", "Kurlar güncellendi", "汇率已更新", "تم تحديث الأسعار");
            Add(d, "rates_last_edit", "Last edited", "Последняя редакция", "Остання редакція", "Соңғы өңдеу", "Zuletzt bearbeitet", "Dernière modification", "Última edición", "Ultima modifica", "Última edição", "Son düzenleme", "最后编辑", "آخر تعديل");
            Add(d, "rates_online", "Update online", "Обновить онлайн", "Оновити онлайн", "Онлайн жаңарту", "Online aktualisieren", "Mettre à jour en ligne", "Actualizar en línea", "Aggiorna online", "Atualizar online", "Çevrimiçi güncelle", "在线更新", "تحديث عبر الإنترنت");
            Add(d, "rates_no_internet", "No internet connection. Rates were not updated.", "Нет подключения к интернету. Курсы не обновлены.", "Немає інтернету. Курси не оновлені.", "Интернет жоқ. Бағамдар жаңартылмады.", "Keine Internetverbindung. Kurse nicht aktualisiert.", "Pas de connexion Internet. Taux non mis à jour.", "Sin conexión a Internet. Tipos no actualizados.", "Nessuna connessione. Tassi non aggiornati.", "Sem conexão com a Internet. Taxas não atualizadas.", "İnternet bağlantısı yok. Kurlar güncellenmedi.", "无网络连接，汇率未更新。", "لا يوجد اتصال بالإنترنت. لم يتم التحديث.");
            Add(d, "rates_saved", "Rates saved", "Курсы сохранены", "Курси збережено", "Бағамдар сақталды", "Kurse gespeichert", "Taux enregistrés", "Tipos guardados", "Tassi salvati", "Taxas salvas", "Kurlar kaydedildi", "汇率已保存", "تم حفظ الأسعار");

            Add(d, "err_divzero", "Cannot divide by zero", "Деление на ноль", "Ділення на нуль", "Нөлге бөлу", "Division durch Null", "Division par zéro", "División por cero", "Divisione per zero", "Divisão por zero", "Sıfıra bölme", "除以零", "القسمة على صفر");
            Add(d, "err_overflow", "Overflow", "Переполнение", "Переповнення", "Тасу", "Überlauf", "Dépassement", "Desbordamiento", "Overflow", "Estouro", "Taşma", "溢出", "تجاوز الحد");
            Add(d, "err_syntax", "Invalid expression", "Неверное выражение", "Невірний вираз", "Қате өрнек", "Ungültiger Ausdruck", "Expression invalide", "Expresión inválida", "Espressione non valida", "Expressão inválida", "Geçersiz ifade", "无效表达式", "تعبير غير صالح");
            Add(d, "err_invalidop", "Invalid operation", "Недопустимая операция", "Недопустима операція", "Жарайсыз амал", "Ungültige Operation", "Opération invalide", "Operación no válida", "Operazione non valida", "Operação inválida", "Geçersiz işlem", "无效操作", "عملية غير صالحة");
            Add(d, "err_logdomain", "Logarithm of non-positive number", "Логарифм неположительного числа", "Логарифм недодатного числа", "Логарифм теріс санның", "Logarithmus einer nicht positiven Zahl", "Logarithme d'un nombre négatif", "Logaritmo de número no positivo", "Logaritmo di numero non positivo", "Logaritmo de número não positivo", "Pozitif olmayan sayının logaritması", "非正数的对数", "لوغاريتم عدد غير موجب");
            Add(d, "err_asindomain", "asin domain error [-1..1]", "Ошибка области asin [-1..1]", "Помилка області asin [-1..1]", "asin аймақ қатесі [-1..1]", "asin Bereichsfehler [-1..1]", "Erreur de domaine asin [-1..1]", "Error de dominio asin [-1..1]", "Errore di dominio asin [-1..1]", "Erro de domínio asin [-1..1]", "asin alan hatası [-1..1]", "asin 定义域错误 [-1..1]", "خطأ مجال asin");
            Add(d, "err_acosdomain", "acos domain error [-1..1]", "Ошибка области acos [-1..1]", "Помилка області acos [-1..1]", "acos аймақ қатесі [-1..1]", "acos Bereichsfehler [-1..1]", "Erreur de domaine acos [-1..1]", "Error de dominio acos [-1..1]", "Errore di dominio acos [-1..1]", "Erro de domínio acos [-1..1]", "acos alan hatası [-1..1]", "acos 定义域错误", "خطأ مجال acos");
            Add(d, "err_factdomain", "Factorial requires non-negative integer", "Факториал требует целое неотрицательное число", "Факторіал потребує ціле недодатне число", "Факториал бүтін теріс емес сан талап етеді", "Fakultät benötigt nichtnegative ganze Zahl", "Factorielle exige entier non négatif", "Factorial requiere entero no negativo", "Fattoriale richiede intero non negativo", "Fatorial requer inteiro não negativo", "Faktöriyel negatif olmayan tam sayı ister", "阶乘要求非负整数", "العاملي يتطلب عددًا صحيحًا غير سالب");
            Add(d, "err_tanundef", "Undefined value", "Неопределённое значение", "Невизначене значення", "Анықталмаған мән", "Undefinierter Wert", "Valeur indéfinie", "Valor indefinido", "Valore indefinito", "Valor indefinido", "Tanımsız değer", "未定义值", "قيمة غير معرفة");
            Add(d, "err_intrequired", "Integer required", "Требуется целое число", "Потрібне ціле число", "Бүтін сан қажет", "Ganzzahl erforderlich", "Nombre entier requis", "Se requiere entero", "Richiesto numero intero", "Número inteiro necessário", "Tam sayı gerekli", "需要整数", "يلزم عدد صحيح");
            Add(d, "err_shiftcount", "Shift count must be 0..63", "Сдвиг должен быть 0..63", "Зсув має бути 0..63", "Ыжыту 0..63 болуы керек", "Verschiebung muss 0..63 sein", "Décalage doit être 0..63", "Desplazamiento debe ser 0..63", "Spostamento deve essere 0..63", "Deslocamento deve ser 0..63", "Kaydırma 0..63 olmalı", "移位必须为 0..63", "الإزاحة يجب أن تكون 0..63");

            Add(d, "const_c_speedoflight", "Speed of light", "Скорость света", "Швидкість світла", "Жарық жылдамдығы", "Lichtgeschwindigkeit", "Vitesse de la lumière", "Velocidad de la luz", "Velocità della luce", "Velocidade da luz", "Işık hızı", "光速", "سرعة الضوء");
            Add(d, "const_c_gravity", "Gravitational constant", "Гравитационная постоянная", "Гравітаційна стала", "Гравитациялық тұрақты", "Gravitationskonstante", "Constante gravitationnelle", "Constante gravitacional", "Costante gravitazionale", "Constante gravitacional", "Yerçekimi sabiti", "引力常数", "الثابت الجاذبي");
            Add(d, "const_c_planck", "Planck constant", "Постоянная Планка", "Стала Планка", "Планк тұрақтысы", "Planck-Konstante", "Constante de Planck", "Constante de Planck", "Costante di Planck", "Constante de Planck", "Planck sabiti", "普朗克常数", "ثابت بلانك");
            Add(d, "const_c_boltzmann", "Boltzmann constant", "Постоянная Больцмана", "Стала Больцмана", "Больцман тұрақтысы", "Boltzmann-Konstante", "Constante de Boltzmann", "Constante de Boltzmann", "Costante di Boltzmann", "Constante de Boltzmann", "Boltzmann sabiti", "玻尔兹曼常数", "ثابت بولتزمان");
            Add(d, "const_c_avogadro", "Avogadro constant", "Число Авогадро", "Число Авогадро", "Авогадро саны", "Avogadro-Konstante", "Nombre d'Avogadro", "Número de Avogadro", "Numero di Avogadro", "Número de Avogadro", "Avogadro sayısı", "阿伏伽德罗常数", "عدد أفوغادرو");
            Add(d, "const_c_echarge", "Elementary charge", "Элементарный заряд", "Елементарний заряд", "Элементар заряд", "Elementarladung", "Charge élémentaire", "Carga elemental", "Carica elementare", "Carga elementar", "Temel yük", "元电荷", "الشحنة الأولية");
            Add(d, "const_c_electronmass", "Electron mass", "Масса электрона", "Маса електрона", "Электрон массасы", "Elektronenmasse", "Masse de l'électron", "Masa del electrón", "Massa dell'elettrone", "Massa do elétron", "Elektron kütlesi", "电子质量", "كتلة الإلكترون");
            Add(d, "const_c_gas", "Gas constant", "Универсальная газовая постоянная", "Універсальна газова стала", "Газ тұрақтысы", "Gaskonstante", "Constante des gaz parfaits", "Constante de los gases", "Costante dei gas", "Constante dos gases", "Gaz sabiti", "气体常数", "الثابت الغازي");

            Add(d, "lbl_input", "Input", "Ввод", "Вхід", "Енгізу", "Eingabe", "Entrée", "Entrada", "Input", "Entrada", "Giriş", "输入", "الإدخال");
            Add(d, "lbl_result", "Result", "Результат", "Результат", "Нәтиже", "Ergebnis", "Résultat", "Resultado", "Risultato", "Resultado", "Sonuç", "结果", "النتيجة");
            Add(d, "lbl_from", "From", "Из", "З", "Берілген", "Von", "De", "De", "Da", "De", "Kaynak", "从", "من");
            Add(d, "lbl_to", "To", "В", "У", "Алынатын", "Nach", "Vers", "A", "A", "Para", "Hedef", "到", "إلى");
            Add(d, "lbl_convert", "Convert", "Конвертировать", "Конвертувати", "Түрлендіру", "Umrechnen", "Convertir", "Convertir", "Converti", "Converter", "Dönüştür", "换算", "تحويل");
            Add(d, "ok", "OK", "ОК", "ОК", "ОК", "OK", "OK", "OK", "OK", "OK", "Tamam", "确定", "موافق");
            Add(d, "cancel", "Cancel", "Отмена", "Скасування", "Болдырмау", "Abbrechen", "Annuler", "Cancelar", "Annulla", "Cancelar", "İptal", "取消", "إلغاء");
            return d;
        }

        private static void Add(Dictionary<string, string[]> d, string key, params string[] values)
        {
            if (values.Length != SupportedLanguages.Count)
                throw new InvalidOperationException("Translation row length mismatch for key: " + key);
            d[key] = values;
        }

        /// <summary>Проверяет наличие перевода для языка (используется selftest).</summary>
        public static bool HasTranslations(string lang)
        {
            return SupportedLanguages.Contains(lang);
        }

        /// <summary>Возвращает все ключи (для диагностики).</summary>
        public static IEnumerable<string> Keys { get { return Table.Keys; } }
    }
}
