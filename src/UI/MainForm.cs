using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using NovaCalc.Engine;
using NovaCalc.Localization;
using NovaCalc.Services;
using NovaCalc.Themes;

namespace NovaCalc.UI
{
    /// <summary>Три режима калькулятора.</summary>
    public enum CalcMode { Standard = 0, Scientific = 1, Laboratory = 2 }

    /// <summary>
    /// Главное окно NovaCalc: без стандартной рамки, скруглённые углы (Win11 DWM + fallback),
    /// градиентный фон, тёмная/светлая темы без перезапуска, три режима, история, настройки.
    /// </summary>
    public class MainForm : Form
    {
        // ===== Состояние калькулятора =====
        private readonly CalculatorEngine _engine = new CalculatorEngine();
        private AppSettings _settings = new AppSettings();
        private ThemePalette _theme = ThemePalette.Dark();
        private List<HistoryItem> _history = new List<HistoryItem>();
        private int _historyNavIndex = -1;   // навигация стрелками по истории

        // Цепочечное состояние обычного режима
        private decimal _accValue;            // накопленное значение / левый операнд
        private char _pendingOp;              // ожидающий оператор
        private bool _startNewEntry = true;   // следующая цифра начинает новый ввод
        private bool _justEvaluated;          // только что нажали "="
        private bool _percentPending;         // в текущем вводе нажат % — применять семантику Windows
        private string _expressionText = "";  // отображение выражения над дисплеем
        private string _chainExpr = "";       // накопленное строковое представление цепочки (для истории)

        private CalcMode _mode = CalcMode.Standard;
        private NumberSystemsBaseNum _numBase = NumberSystemsBaseNum.Dec;

        // ===== UI элементы =====
        private GradientPanel _root;
        private ThemedLabel _lblExpression;
        private Label _lblDisplay;
        private ThemedLabel _lblMemIndicator;
        private RoundButton _btnMin, _btnClose, _btnSettings, _btnHistoryToggle, _btnTopMost;
        private FlowLayoutPanel _modeSwitch;
        private RoundButton[] _modeButtons;
        private GlassPanel _displayPanel;
        private TableLayoutPanel _keypad;
        private Panel _sciPanel;      // научная панель (левая колонка)
        private Panel _labPanel;      // содержимое лабораторного режима
        private ListBox _historyList;
        private Panel _historyPanel;
        private ToolTip _toolTip = new ToolTip();          // подсказки к кнопкам режимов/операций

        /// <summary>Показать короткую подсказку над кнопкой (используется при наведении).</summary>
        private void ShowTip(Control c, string text)
        {
            if (_toolTip.GetToolTip(c) != text) _toolTip.SetToolTip(c, text);
            _toolTip.Show(text, c, 0, -c.Height - 4, 1200);
        }

        // Подвеска подсказки на конкретную кнопку один раз (не дублировать обработчики при смене темы)
        private readonly HashSet<Control> _tipHooked = new HashSet<Control>();
        private void ShowTipOnHover(Control c, string text)
        {
            _toolTip.SetToolTip(c, text);
            if (_tipHooked.Add(c))
                c.MouseEnter += (a, ev) => { var t = c as Control; if (t != null && t.IsHandleCreated) ShowTip(t, text); };
        }

        // Все темизируемые кнопки для перекраски (пересобирается при rebuild, чтобы не рос бесконечно)
        private readonly List<RoundButton> _allButtons = new List<RoundButton>();
        // Статичные кнопки (заголовок/капцион), добавляются один раз
        private readonly List<RoundButton> _staticButtons = new List<RoundButton>();

        private float _dpiScale = 1f;

        public MainForm()
        {
            SuspendLayout();
            // ===== Окно без рамки =====
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(16, 17, 26);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            KeyPreview = true;
            MinimumSize = new Size(340, 520);
            Font = new Font("Segoe UI", 9.5f);

            Load += MainForm_Load;
            ResumeLayout(false);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // DPI: берём масштаб экрана и пересобираем компоновку под него
            using (var g = CreateGraphics())
            {
                float dx = g.DpiX;
                if (dx > 96f) _dpiScale = dx / 96f;
            }

            _settings = StorageService.LoadSettings();
            Strings.Current = _settings.Language ?? Strings.DetectSystemLanguage();
            _theme = ThemePalette.For(_settings.Theme);
            _history = StorageService.LoadHistory();
            _mode = (CalcMode)(_settings.RememberLastMode ? LastModeOrDefault() : _settings.StartupMode);

            BuildUi();
            ApplyTheme();
            SwitchMode(_mode);
            UpdateDisplay();

            // Скругление углов через DWM (Windows 11); на Win10 остаётся прямоугольник — стабильно.
            try
            {
                int radius = (int)(12 * _dpiScale);
                NativeMethods.DwmSetWindowAttribute(Handle, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, ref radius, sizeof(int));
                var margins = new NativeMethods.MARGINS { Left = 1, Right = 1, Top = 1, Bottom = 1 };
                NativeMethods.DwmExtendFrameIntoClientArea(Handle, ref margins); // тень окна
            }
            catch (Exception) { /* Win7/8 или недоступно — не критично */ }

            TopMost = _settings.TopMost;
        }

        private int LastModeOrDefault()
        {
            return _settings.StartupMode; // StartupMode используется и как "последний режим" при RememberLastMode
        }

        // ============================================================
        // ПОСТРОЕНИЕ UI
        // ============================================================

        private void BuildUi()
        {
            _root = new GradientPanel { Dock = DockStyle.Fill };
            Controls.Add(_root);

            float s = _dpiScale;
            int W = LayoutWidth();
            // Высота подстраивается под режим: обычный/инженерный — компактно, лабораторный — больше
            ClientSize = new Size(W, LayoutHeight());

            // ---------- Верхняя строка: название + кнопка "поверх окон" + настройки + history + свернуть/закрыть
            var topRow = new Panel { Dock = DockStyle.Top, Height = (int)(40 * s) };
            _root.Controls.Add(topRow);

            var lblTitle = new ThemedLabel
            {
                Text = "NovaCalc",
                Font = new Font("Segoe UI Semibold", 10.5f * s),
                Location = new Point((int)(14 * s), (int)(10 * s)),
                AutoSize = true,
                Name = "lblTitle"
            };
            topRow.Controls.Add(lblTitle);

            // Кнопки заголовка: одинаковый размер, единая формула позиционирования справа налево
            _btnTopMost = MakeCaptionButton("📌");   // поверх всех окон
            _btnTopMost.AccessibleName = "topmost";
            _btnTopMost.Click += (a, b) => ToggleTopMost();
            _btnHistoryToggle = MakeCaptionButton("≡");
            _btnHistoryToggle.AccessibleName = "history";
            _btnHistoryToggle.Click += (a, b) => ToggleHistory();
            _btnSettings = MakeCaptionButton("⚙");
            _btnSettings.AccessibleName = "settings";
            _btnSettings.Click += (a, b) => OpenSettings();
            _btnMin = MakeCaptionButton("—");
            _btnMin.AccessibleName = "minimize";
            _btnMin.Click += (a, b) => WindowState = FormWindowState.Minimized;
            _btnClose = MakeCaptionButton("✕");
            _btnClose.AccessibleName = "close";
            _btnClose.Click += (a, b) => Close();
            topRow.Controls.AddRange(new Control[] { _btnTopMost, _btnHistoryToggle, _btnSettings, _btnMin, _btnClose });
            LayoutCaptionButtons();

            // Перетаскивание окна за верхнюю область
            AttachDrag(topRow);
            AttachDrag(lblTitle);

            // ---------- Переключатель режимов ----------
            _modeSwitch = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Location = new Point((int)(12 * s), (int)(44 * s)),
                Size = new Size(ClientSize.Width - (int)(24 * s), (int)(34 * s)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _modeButtons = new RoundButton[3];
            string[] modeKeys = { "mode_standard", "mode_scientific", "mode_laboratory" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var mb = new RoundButton
                {
                    Text = Strings.T(modeKeys[i]),
                    Width = (int)((ClientSize.Width - 24 * s - 16 * s) / 3),
                    Height = (int)(32 * s),
                    Margin = new Padding((int)(4 * s), 0, (int)(4 * s), 0),
                    Radius = 8 * s,
                    Tag = idx
                };
                mb.Click += (a, b) => SwitchMode((CalcMode)idx);
                _modeButtons[i] = mb;
                _allButtons.Add(mb);
                _modeSwitch.Controls.Add(mb);
            }
            _root.Controls.Add(_modeSwitch);

            // ---------- Дисплей ----------
            _displayPanel = new GlassPanel
            {
                Location = new Point((int)(12 * s), (int)(84 * s)),
                Size = new Size(ClientSize.Width - (int)(24 * s), (int)(92 * s)),
                Radius = 12 * s,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _root.Controls.Add(_displayPanel);

            _lblMemIndicator = new ThemedLabel
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5f * s),
                Location = new Point((int)(10 * s), (int)(6 * s)),
                AutoSize = true
            };
            _displayPanel.Controls.Add(_lblMemIndicator);

            _lblExpression = new ThemedLabel
            {
                Text = "",
                Font = new Font("Segoe UI", 9.5f * s),
                Location = new Point((int)(10 * s), (int)(8 * s)),
                Size = new Size(_displayPanel.Width - (int)(20 * s), (int)(22 * s)),
                TextAlign = ContentAlignment.TopRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _displayPanel.Controls.Add(_lblExpression);

            _lblDisplay = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI Light", 30f * s),
                Location = new Point((int)(6 * s), (int)(30 * s)),
                Size = new Size(_displayPanel.Width - (int)(12 * s), (int)(56 * s)),
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _displayPanel.Controls.Add(_lblDisplay);

            // ---------- История (панель над клавиатурой, скрыта по умолчанию) ----------
            _historyPanel = new Panel
            {
                Location = HistoryLocation(),
                Size = HistorySize(),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _historyList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f * s),
                IntegralHeight = false
            };
            _historyList.MouseDoubleClick += HistoryActivate;
            _historyList.KeyDown += (a, ev) =>
            {
                if (ev.KeyCode == Keys.Enter) { HistoryActivate(a, ev); ev.SuppressKeyPress = true; }
            };
            _historyPanel.Controls.Add(_historyList);
            var btnClearHist = MakeFlatButton(Strings.T("history_clear"));
            btnClearHist.AccessibleName = "clear_history";
            btnClearHist.Dock = DockStyle.Bottom;
            btnClearHist.Click += (a, b) => { _history.Clear(); StorageService.SaveHistory(_history); RefreshHistory(); };
            _historyPanel.Controls.Add(btnClearHist);
            _root.Controls.Add(_historyPanel);

            // ---------- Область тела: клавиатура (стандарт/научный) или лабораторная панель ----------
            // Единая формула геометрии: всё ниже дисплея вычисляется из ClientSize, без магических чисел.
            _keypad = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 7,
                Location = BodyLocation(),
                Size = BodySize(),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            for (int c = 0; c < 4; c++) _keypad.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            for (int r = 0; r < 7; r++) _keypad.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7f));
            _root.Controls.Add(_keypad);

            // Научная дополнительная панель (видна в инженерном режиме) — слева, окно шире
            _sciPanel = new Panel
            {
                Location = BodyLocation(),
                Size = SciBodySize(),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };
            _root.Controls.Add(_sciPanel);

            // Лабораторная панель
            _labPanel = new Panel
            {
                Location = BodyLocation(),
                Size = BodySize(),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _root.Controls.Add(_labPanel);

            BuildStandardKeypad();
            BuildScientificExtras();
            BuildLaboratory();
            RefreshHistory();
        }

        // ===== Единая геометрия компоновки (учитывает DPI и состояние истории) =====
        private const double StdW = 380, SciW = 600, LabW = 440;   // ширины в DIP
        private const double TopBarH = 40, ModeRowY = 44, ModeRowH = 34, DisplayY = 84, DisplayH = 92, Pad = 12, Gap = 10;

        private int LayoutWidth()
        {
            float s = _dpiScale;
            double w = _mode == CalcMode.Scientific ? SciW : (_mode == CalcMode.Laboratory ? LabW : StdW);
            return (int)(w * s);
        }

        /// <summary>Минимальная высота окна под текущий режим (клавиатуре нужно ~7 рядов).</summary>
        private int LayoutHeight()
        {
            float s = _dpiScale;
            double bodyRows = _mode == CalcMode.Laboratory ? 300 : 330; // высота тела в DIP
            double hist = _historyPanel != null && _historyPanel.Visible ? HistH + 6 : 0;
            return (int)((DisplayY + DisplayH + Gap + hist + bodyRows + 14) * s);
        }

        /// <summary>Точка начала области тела (под дисплеем/историей).</summary>
        private Point BodyLocation()
        {
            float s = _dpiScale;
            double topY = DisplayY + DisplayH + Gap;
            if (_historyPanel != null && _historyPanel.Visible) topY += HistH + 6;
            return new Point((int)(Pad * s), (int)(topY * s));
        }

        private const double HistH = 140; // высота панели истории в DIP

        private Point HistoryLocation()
        {
            float s = _dpiScale;
            return new Point((int)(Pad * s), (int)((DisplayY + DisplayH + Gap) * s));
        }

        private Size HistorySize()
        {
            float s = _dpiScale;
            return new Size(ClientSize.Width - (int)(2 * Pad * s), (int)(HistH * s));
        }

        private Size BodySize()
        {
            Point p = BodyLocation();
            return new Size(ClientSize.Width - 2 * p.X, Math.Max((int)(120 * _dpiScale), ClientSize.Height - p.Y - (int)(Gap * _dpiScale)));
        }

        /// <summary>Размер sci-панели: фиксированная ширина, остальное — клавиатуре.</summary>
        private Size SciBodySize()
        {
            Point p = BodyLocation();
            return new Size((int)(206 * _dpiScale), Math.Max((int)(120 * _dpiScale), ClientSize.Height - p.Y - (int)(Gap * _dpiScale)));
        }

        private Size KeypadSizeForSci()
        {
            Point p = BodyLocation();
            int sciW = (int)(206 * _dpiScale);
            return new Size(Math.Max((int)(150 * _dpiScale), ClientSize.Width - sciW - p.X - (int)(Gap * _dpiScale)),
                            Math.Max((int)(120 * _dpiScale), ClientSize.Height - p.Y - (int)(Gap * _dpiScale)));
        }

        private void LayoutCaptionButtons()
        {
            float s = _dpiScale;
            int bs = (int)(30 * s);          // размер квадратной кнопки
            int y = (int)(5 * s);
            int right = ClientSize.Width - (int)(8 * s);
            Action<RoundButton, int> place = (b, idx) => { if (b != null) b.SetBounds(right - bs - idx * (bs + (int)(2 * s)), y, bs, bs); };
            place(_btnClose, 0);
            place(_btnMin, 1);
            place(_btnSettings, 2);
            place(_btnHistoryToggle, 3);
            place(_btnTopMost, 4);
        }

        private RoundButton MakeCaptionButton(string text)
        {
            var b = new RoundButton
            {
                Text = text,
                Height = (int)(30 * _dpiScale),
                Width = (int)(30 * _dpiScale),
                Radius = 8f * _dpiScale,
                Font = new Font("Segoe UI", 9.5f * _dpiScale),
                CaptionStyle = true
            };
            _staticButtons.Add(b);
            _allButtons.Add(b);
            return b;
        }

        private RoundButton MakeFlatButton(string text)
        {
            var b = new RoundButton
            {
                Text = text,
                Height = (int)(28 * _dpiScale),
                Radius = 8f * _dpiScale,
                Font = new Font("Segoe UI", 9f * _dpiScale)
            };
            _staticButtons.Add(b);
            _allButtons.Add(b);
            return b;
        }

        private RoundButton MakeKey(string text, string tag, EventHandler onClick, int col, int row, int colspan = 1)
        {
            var b = new RoundButton
            {
                Text = text,
                Tag = tag,
                Dock = DockStyle.Fill,
                Margin = new Padding((int)(3 * _dpiScale)),
                Radius = 9f * _dpiScale,
                Font = new Font("Segoe UI", 12.5f * _dpiScale)
            };
            b.Click += onClick;
            if (colspan > 1) _keypad.SetColumnSpan(b, colspan);
            _keypad.Controls.Add(b, col, row);
            _allButtons.Add(b);
            return b;
        }

        // ============================================================
        // ОБЫЧНЫЙ РЕЖИМ: КЛАВИАТУРА
        // ============================================================

        private void BuildStandardKeypad()
        {
            // Пересборка: чистим список темизируемых кнопок до статичных (без утечки при смене языка)
            _allButtons.RemoveRange(_staticButtons.Count, _allButtons.Count - _staticButtons.Count);
            _keypad.Controls.Clear();
            _keypad.RowCount = 7;
            _keypad.RowStyles.Clear();
            for (int r = 0; r < 7; r++) _keypad.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7f));

            // строка памяти
            AddMemRow();

            // строка: MC MR M+ M- MS  -> уже в AddMemRow (5 кнопок в 4 колонках не влезает ровно; делаем 2 ряда: память + функции)
            // ряд функций: %, CE, C, ⌫
            MakeKey("%", "pct", OnPercentClick, 0, 1);
            MakeKey("CE", "ce", (a, b) => ClearEntry(), 1, 1);
            MakeKey(Strings.T("btn_clear"), "clr", (a, b) => ClearAll(), 2, 1);
            MakeKey("⌫", "back", (a, b) => Backspace(), 3, 1);

            MakeKey("1/x", "inv", (a, b) => UnaryOp(v => SafeDivLocal(1m, v), "1/(", ")"), 0, 2);
            MakeKey("x²", "sq", (a, b) => UnaryOp(v => SafeMulLocal(v, v), "(", ")²"), 1, 2);
            MakeKey("√x", "sqrt", (a, b) => UnaryOp(v => SqrtLocal(v), "√(", ")"), 2, 2);
            MakeKey("÷", "op/", (a, b) => OperatorPressed('/'), 3, 2);

            MakeKey("7", "d7", OnDigit, 0, 3);
            MakeKey("8", "d8", OnDigit, 1, 3);
            MakeKey("9", "d9", OnDigit, 2, 3);
            MakeKey("×", "op*", (a, b) => OperatorPressed('*'), 3, 3);

            MakeKey("4", "d4", OnDigit, 0, 4);
            MakeKey("5", "d5", OnDigit, 1, 4);
            MakeKey("6", "d6", OnDigit, 2, 4);
            MakeKey("−", "op-", (a, b) => OperatorPressed('-'), 3, 4);

            MakeKey("1", "d1", OnDigit, 0, 5);
            MakeKey("2", "d2", OnDigit, 1, 5);
            MakeKey("3", "d3", OnDigit, 2, 5);
            MakeKey("+", "op+", (a, b) => OperatorPressed('+'), 3, 5);

            MakeKey("+/-", "neg", (a, b) => Negate(), 0, 6);
            MakeKey("0", "d0", OnDigit, 1, 6);
            MakeKey(".", "dot", (a, b) => AppendDecimalSeparator(), 2, 6);
            var eq = MakeKey("=", "eq", (a, b) => DoEquals(), 3, 6);
            eq.IsAccent = true;
            eq.Invalidate();
        }

        private void AddMemRow()
        {
            // Пять кнопок памяти в один ряд: используем вложенную таблицу
            var memPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0)
            };
            for (int c = 0; c < 5; c++) memPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            memPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            string[][] mem =
            {
                new[] { "MC", "mc" }, new[] { "MR", "mr" }, new[] { "M+", "mp" }, new[] { "M-", "mm" }, new[] { "MS", "ms" }
            };
            for (int c = 0; c < 5; c++)
            {
                var b = new RoundButton
                {
                    Text = mem[c][0],
                    Tag = mem[c][1],
                    Dock = DockStyle.Fill,
                    Margin = new Padding((int)(3 * _dpiScale)),
                    Radius = 9f * _dpiScale,
                    Font = new Font("Segoe UI", 10.5f * _dpiScale)
                };
                string t = mem[c][1];
                b.Click += (a, ev) => MemoryAction(t);
                memPanel.Controls.Add(b, c, 0);
                _allButtons.Add(b);
            }
            _keypad.Controls.Add(memPanel, 0, 0);
            _keypad.SetColumnSpan(memPanel, 4);
        }

        // ============================================================
        // ИНЖЕНЕРНЫЙ РЕЖИМ
        // ============================================================

        private void BuildScientificExtras()
        {
            _sciPanel.Controls.Clear();
            float s = _dpiScale;
            // сетка доп. кнопок 4 колонки x N рядов + нижний ряд систем счисления
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                AutoScroll = false,
                Padding = new Padding(0)
            };
            for (int c = 0; c < 4; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _sciPanel.Controls.Add(grid);

            string[][] sciKeys =
            {
                new[] { "sin", "sin(" }, new[] { "cos", "cos(" }, new[] { "tan", "tan(" }, new[] { "π", "pi(" },
                new[] { "asin", "asin(" }, new[] { "acos", "acos(" }, new[] { "atan", "atan(" }, new[] { "eˣ", "e(" },
                new[] { "sinh", "sinh(" }, new[] { "cosh", "cosh(" }, new[] { "tanh", "tanh(" }, new[] { "√y", "root(" },
                new[] { "log", "log(" }, new[] { "ln", "ln(" }, new[] { "exp", "exp(" }, new[] { "|x|", "abs(" },
                new[] { "xʸ", "^" }, new[] { "10ˣ", "pow10(" }, new[] { "2ˣ", "pow2(" }, new[] { "n!", "!" },
                new[] { "mod", "mod(" }, new[] { "Ans", "ans" }, new[] { "(", "(" }, new[] { ")", ")" },
                new[] { "AND", "and(" }, new[] { "OR", "or(" }, new[] { "XOR", "xor(" }, new[] { "NOT", "not(" },
                new[] { "<<", "shl(" }, new[] { ">>", "shr(" }, new[] { ",", "," }, new[] { "E", "E" },
                new[] { "Deg", "deg" }, new[] { "Rad", "rad" }, new[] { "Grad", "grad" }, new[] { "⌫", "back" },
            };
            int rows = (sciKeys.Length + 3) / 4;
            for (int r = 0; r < rows; r++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            int row = 0, col = 0;
            foreach (var k in sciKeys)
            {
                var b = new RoundButton
                {
                    Text = LocalizeSci(k[0]),
                    Tag = "sci:" + k[1],
                    AccessibleName = k[1],
                    Dock = DockStyle.Fill,
                    Margin = new Padding((int)(2 * s)),
                    Radius = 8f * s,
                    Font = new Font("Segoe UI", 9f * s)
                };
                string ins = k[1]; string raw = k[0];
                b.Click += (a, ev) => SciInsert(ins, raw);
                grid.Controls.Add(b, col, row);
                _allButtons.Add(b);
                col++;
                if (col >= 4) { col = 0; row++; }
            }

            // Ряд систем счисления DEC HEX OCT BIN
            var baseRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(34 * s),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            foreach (var nb in new[] { "DEC", "HEX", "OCT", "BIN" })
            {
                var bb = new RoundButton
                {
                    Text = nb,
                    Width = (int)(46 * s),
                    Height = (int)(28 * s),
                    Margin = new Padding((int)(2 * s)),
                    Radius = 8f * s,
                    Font = new Font("Segoe UI", 9f * s),
                    Tag = "base:" + nb
                };
                string n = nb;
                bb.Click += (a, ev) => SetNumberBase(n);
                baseRow.Controls.Add(bb);
                _allButtons.Add(bb);
            }
            _sciPanel.Controls.Add(baseRow);
        }

        private string LocalizeSci(string key)
        {
            switch (key)
            {
                case "Deg": return Strings.T("angle_deg");
                case "Rad": return Strings.T("angle_rad");
                case "Grad": return Strings.T("angle_grad");
                default: return key;
            }
        }

        // Ввод выражения в инженерном режиме: строка _exprInput
        private string _exprInput = "";

        // После «=» следующий символ начинает новое выражение; оператор — продолжает с Ans
        private bool _exprJustEvaluated;

        private void SciInsert(string token, string raw)
        {
            switch (raw)
            {
                case "deg": _engine.AngleMode = AngleMode.Degrees; break;
                case "rad": _engine.AngleMode = AngleMode.Radians; break;
                case "grad": _engine.AngleMode = AngleMode.Gradians; break;
                case "ans": AppendExpr("Ans"); break;
                case "pi(": AppendExpr("pi"); break;    // константа π: вставляется как значение
                case "e(": AppendExpr("e"); break;     // константа e: вставляется как значение
                case "E": AppendExpr("E"); break;
                case "(": AppendExpr("("); break;
                case ")": AppendExpr(")"); break;
                case ",": AppendExpr(", "); break;
                case "^": AppendExpr("^"); break;
                case "!": AppendExpr("!"); break;
                case "pow10(": AppendExpr("10^("); break;
                case "pow2(": AppendExpr("2^("); break;
                case "back": Backspace(); return;   // кнопка ⌫ в sci-панели
                default: AppendExpr(token); break;
            }
            UpdateDisplay();
        }

        /// <summary>
        /// Backspace в инженерном режиме: удаляет не один символ, а целый «токен» —
        /// имя функции вместе со скобкой ("sin(" → ""), оператор с пробелом (", " → ""), обычный символ.
        /// </summary>
        private void BackspaceExpr()
        {
            if (_exprInput.Length == 0) return;
            // многобуквенные функции/константы с открытой скобкой
            string[] funcTokens = { "asin(", "acos(", "atan(", "sinh(", "cosh(", "tanh(", "sin(", "cos(", "tan(", "log(", "mod(", "not(", "shl(", "shr(", "abs(", "exp(", "ln(", "root(", "xor(" };
            foreach (var f in funcTokens)
                if (_exprInput.EndsWith(f)) { _exprInput = _exprInput.Substring(0, _exprInput.Length - f.Length); return; }
            if (_exprInput.EndsWith(", ")) { _exprInput = _exprInput.Substring(0, _exprInput.Length - 2); return; }
            _exprInput = _exprInput.Substring(0, _exprInput.Length - 1);
        }

        private void AppendExpr(string t)
        {
            // После «=»: оператор продолжает вычисление от Ans, остальное начинает новое выражение
            if (_exprJustEvaluated)
            {
                _exprJustEvaluated = false;
                string op = t.Trim();
                if (op.Length == 1 && ("+-*/^%".IndexOf(op[0]) >= 0))
                    _exprInput = "Ans";
                else
                    _exprInput = "";
            }
            _exprInput += t;
        }

        private void SetNumberBase(string name)
        {
            NumberSystemsBaseNum b;
            switch (name)
            {
                case "HEX": b = NumberSystemsBaseNum.Hex; break;
                case "OCT": b = NumberSystemsBaseNum.Oct; break;
                case "BIN": b = NumberSystemsBaseNum.Bin; break;
                default: b = NumberSystemsBaseNum.Dec; break;
            }
            _numBase = b;
            // конвертируем текущее значение в выбранную систему счисления
            try
            {
                long cur = ConvertDisplayToInt64();
                _exprInput = NovaCalc.Converters.NumberSystems.Format(cur, MapBase(b));
                _exprJustEvaluated = false;
            }
            catch (Exception) { ShowError(Strings.T("err_invalidop")); return; }
            UpdateNumBaseButtons();
            UpdateDisplay();
        }

        /// <summary>Подсветка активной кнопки DEC/HEX/OCT/BIN в sci-панели.</summary>
        private void UpdateNumBaseButtons()
        {
            if (_allButtons == null) return;
            foreach (var btn in _allButtons)
            {
                var t = btn.Tag as string;
                if (t == null || !t.StartsWith("base:")) continue;
                bool active = ("base:" + NumBaseName()) == t;
                btn.NormalColor = active ? _theme.Accent : _theme.FuncButton;
                btn.TextColor = active ? _theme.AccentText : _theme.FuncText;
                btn.HoverColor = ThemePalette.HoverOf(_theme, btn.NormalColor);
                btn.PressColor = ThemePalette.PressOf(_theme, btn.NormalColor);
                btn.Invalidate();
            }
        }

        private string NumBaseName()
        {
            switch (_numBase)
            {
                case NumberSystemsBaseNum.Hex: return "HEX";
                case NumberSystemsBaseNum.Oct: return "OCT";
                case NumberSystemsBaseNum.Bin: return "BIN";
                default: return "DEC";
            }
        }

        private long ConvertDisplayToInt64()
        {
            decimal v = CurrentInputValue();
            if (v != Math.Truncate(v)) throw new FormatException();
            return (long)v;
        }

        private static NovaCalc.Converters.NumberSystems.Base MapBase(NumberSystemsBaseNum b)
        {
            switch (b)
            {
                case NumberSystemsBaseNum.Hex: return NovaCalc.Converters.NumberSystems.Base.Hex;
                case NumberSystemsBaseNum.Oct: return NovaCalc.Converters.NumberSystems.Base.Oct;
                case NumberSystemsBaseNum.Bin: return NovaCalc.Converters.NumberSystems.Base.Bin;
                default: return NovaCalc.Converters.NumberSystems.Base.Dec;
            }
        }

        private enum NumberSystemsBaseNum { Dec, Hex, Oct, Bin }

        // ============================================================
        // ЛАБОРАТОРНЫЙ РЕЖИМ
        // ============================================================

        private ComboBox _labTabs;
        private Panel _labContent;

        private void BuildLaboratory()
        {
            _labPanel.Controls.Clear();
            float s = _dpiScale;

            _labTabs = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 0),
                Width = (int)(220 * s),
                Font = new Font("Segoe UI", 9.5f * s)
            };
            _labTabs.Items.Add(Strings.T("lab_converter"));
            _labTabs.Items.Add(Strings.T("lab_currency"));
            _labTabs.Items.Add(Strings.T("lab_constants"));
            _labTabs.Items.Add(Strings.T("lab_datecalc"));
            _labTabs.Items.Add(Strings.T("lab_statistics"));
            _labTabs.SelectedIndexChanged += (a, b) => ShowLabTab(_labTabs.SelectedIndex);
            _labPanel.Controls.Add(_labTabs);

            _labContent = new Panel
            {
                Location = new Point(0, (int)(32 * s)),
                Size = new Size(_labPanel.Width, _labPanel.Height - (int)(32 * s)),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _labPanel.Controls.Add(_labContent);
            ShowLabTab(0);
        }

        private TextBox _unitValueBox;
        private ComboBox _unitCategory, _unitFrom, _unitTo;
        private Label _unitResult;

        private TextBox _curValueBox;
        private ComboBox _curFrom, _curTo;
        private Label _curResult, _curDateLabel;

        private ListView _constList;
        private DateTimePicker _dateFrom, _dateTo;
        private Label _dateResult;
        private TextBox _daysBox;
        private Label _dateAddResult;
        private TextBox _statsBox;
        private Label _statsResult;

        private void ShowLabTab(int index)
        {
            _labContent.Controls.Clear();
            float s = _dpiScale;
            switch (index)
            {
                case 0: BuildUnitConverter(s); break;
                case 1: BuildCurrencyConverter(s); break;
                case 2: BuildConstants(s); break;
                case 3: BuildDateCalc(s); break;
                case 4: BuildStats(s); break;
            }
        }

        private void BuildUnitConverter(float s)
        {
            _unitCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(0, (int)(6 * s)), Width = (int)(160 * s) };
            foreach (var cat in NovaCalc.Converters.UnitConverter.All)
                _unitCategory.Items.Add(Strings.T("cat_" + cat.Key));
            _unitCategory.SelectedIndex = 0;
            _unitCategory.SelectedIndexChanged += (a, b) => PopulateUnitLists();

            _unitValueBox = new TextBox { Location = new Point(0, (int)(36 * s)), Width = (int)(110 * s), Text = "1" };
            _unitFrom = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point((int)(115 * s), (int)(36 * s)), Width = (int)(130 * s) };
            _unitTo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point((int)(115 * s), (int)(66 * s)), Width = (int)(130 * s) };
            var btnConv = MakeActionButton(Strings.T("lbl_convert"), (int)(6 * s), (int)(96 * s), (int)(110 * s), () => DoUnitConvert());
            _unitResult = MakeResultLabel((int)(130 * s), (int)(96 * s));

            PopulateUnitLists();
            _labContent.Controls.AddRange(new Control[] { _unitCategory, _unitValueBox, _unitFrom, _unitTo, btnConv, _unitResult });
        }

        private void PopulateUnitLists()
        {
            var cats = NovaCalc.Converters.UnitConverter.All;
            int idx = _unitCategory.SelectedIndex;
            if (idx < 0) idx = 0;
            var cat = cats[idx];
            _unitFrom.Items.Clear(); _unitTo.Items.Clear();
            foreach (var u in cat.Units) { _unitFrom.Items.Add(u.NameKey); _unitTo.Items.Add(u.NameKey); }
            if (cat.Units.Count > 0)
            {
                _unitFrom.SelectedIndex = 0;
                _unitTo.SelectedIndex = Math.Min(1, cat.Units.Count - 1);
            }
        }

        private void DoUnitConvert()
        {
            try
            {
                decimal v;
                if (!Strings.TryParseNumber(_unitValueBox.Text, out v)) { ShowError(Strings.T("err_syntax")); return; }
                var cats = NovaCalc.Converters.UnitConverter.All;
                var cat = cats[Math.Max(0, _unitCategory.SelectedIndex)];
                string fromKey = cat.Units[_unitFrom.SelectedIndex].Key;
                string toKey = cat.Units[_unitTo.SelectedIndex].Key;
                decimal res = NovaCalc.Converters.UnitConverter.Convert(cat.Key, fromKey, toKey, v);
                _unitResult.Text = FormatOut(res);
                _engine.Ans = res;
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void BuildCurrencyConverter(float s)
        {
            _rates = NovaCalc.Converters.CurrencyService.Load();
            _curValueBox = new TextBox { Location = new Point(0, (int)(6 * s)), Width = (int)(110 * s), Text = "100" };
            _curFrom = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point((int)(115 * s), (int)(6 * s)), Width = (int)(110 * s) };
            _curTo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point((int)(230 * s), (int)(6 * s)), Width = (int)(110 * s) };
            foreach (var c in _rates) { _curFrom.Items.Add(c.Code); _curTo.Items.Add(c.Code); }
            _curFrom.SelectedIndex = 0;
            int ruIdx = _rates.FindIndex(c => c.Code == "RUB");
            _curTo.SelectedIndex = ruIdx >= 0 ? ruIdx : Math.Min(1, _rates.Count - 1);

            var btnConv = MakeActionButton(Strings.T("lbl_convert"), (int)(6 * s), (int)(36 * s), (int)(110 * s), DoCurrencyConvert);
            var btnEdit = MakeActionButton(Strings.T("lab_edit_rates"), (int)(120 * s), (int)(36 * s), (int)(150 * s), OpenRatesEditor);
            var btnOnline = MakeActionButton(Strings.T("rates_online"), (int)(6 * s), (int)(66 * s), (int)(150 * s), DoOnlineUpdate);
            _curResult = MakeResultLabel((int)(230 * s), (int)(36 * s));
            _curDateLabel = new ThemedLabel
            {
                Text = Strings.T("rates_last_edit") + ": " + NovaCalc.Converters.CurrencyService.GetLastEditedDate(),
                Location = new Point((int)(6 * s), (int)(96 * s)),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f * s)
            };
            _labContent.Controls.AddRange(new Control[] { _curValueBox, _curFrom, _curTo, btnConv, btnEdit, btnOnline, _curResult, _curDateLabel });
        }

        private List<NovaCalc.Converters.Currency> _rates;

        private void DoCurrencyConvert()
        {
            try
            {
                decimal v;
                if (!Strings.TryParseNumber(_curValueBox.Text, out v)) { ShowError(Strings.T("err_syntax")); return; }
                decimal res = NovaCalc.Converters.CurrencyService.Convert(_rates, _curFrom.SelectedItem.ToString(), _curTo.SelectedItem.ToString(), v);
                _curResult.Text = FormatOut(res);
                _engine.Ans = res;
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void DoOnlineUpdate()
        {
            Cursor prev = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            bool ok = NovaCalc.Converters.CurrencyService.TryUpdateOnline(_rates);
            Cursor.Current = prev;
            if (ok)
            {
                _curDateLabel.Text = Strings.T("rates_last_edit") + ": " + NovaCalc.Converters.CurrencyService.GetLastEditedDate();
                MessageBox.Show(this, Strings.T("rates_updated"), "NovaCalc", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, Strings.T("rates_no_internet"), "NovaCalc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenRatesEditor()
        {
            using (var dlg = new RatesEditorForm(_rates, _theme, _dpiScale))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _rates = dlg.ResultRates;
                    _curFrom.Items.Clear(); _curTo.Items.Clear();
                    foreach (var c in _rates) { _curFrom.Items.Add(c.Code); _curTo.Items.Add(c.Code); }
                    _curFrom.SelectedIndex = 0; _curTo.SelectedIndex = Math.Min(1, _rates.Count - 1);
                    _curDateLabel.Text = Strings.T("rates_last_edit") + ": " + NovaCalc.Converters.CurrencyService.GetLastEditedDate();
                }
            }
        }

        private void BuildConstants(float s)
        {
            _constList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Segoe UI", 9f * s)
            };
            _constList.Columns.Add("", (int)(60 * s));
            _constList.Columns.Add(Strings.T("lbl_result"), (int)(230 * s));
            _constList.Columns.Add("", (int)(120 * s));
            foreach (var c in NovaCalc.Converters.PhysicalConstants.All)
            {
                var item = new ListViewItem(c.Symbol);
                item.SubItems.Add(c.Value.ToString("G12", CultureInfo.InvariantCulture) + " " + c.Unit);
                item.SubItems.Add(Strings.T(c.Key));
                item.Tag = c.Value;
                _constList.Items.Add(item);
            }
            _constList.DoubleClick += (a, b) =>
            {
                if (_constList.SelectedItems.Count > 0)
                {
                    var val = (decimal)_constList.SelectedItems[0].Tag;
                    InsertIntoCurrent(val);
                }
            };
            _labContent.Controls.Add(_constList);
        }

        private void BuildDateCalc(float s)
        {
            var lblFrom = new ThemedLabel { Text = Strings.T("date_from"), Location = new Point(0, (int)(6 * s)), AutoSize = true };
            _dateFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Location = new Point((int)(110 * s), (int)(4 * s)), Width = (int)(120 * s), Value = DateTime.Today };
            var lblTo = new ThemedLabel { Text = Strings.T("date_to"), Location = new Point(0, (int)(36 * s)), AutoSize = true };
            _dateTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Location = new Point((int)(110 * s), (int)(34 * s)), Width = (int)(120 * s), Value = DateTime.Today };
            var btnDiff = MakeActionButton(Strings.T("date_diff"), (int)(240 * s), (int)(4 * s), (int)(120 * s), DoDateDiff);
            _dateResult = MakeResultLabel((int)(240 * s), (int)(36 * s));

            var lblAdd = new ThemedLabel { Text = Strings.T("date_add"), Location = new Point(0, (int)(70 * s)), AutoSize = true };
            _daysBox = new TextBox { Location = new Point((int)(110 * s), (int)(68 * s)), Width = (int)(80 * s), Text = "30" };
            var btnAdd = MakeActionButton(Strings.T("date_result"), (int)(200 * s), (int)(68 * s), (int)(100 * s), DoDateAdd);
            _dateAddResult = MakeResultLabel((int)(240 * s), (int)(70 * s));

            _labContent.Controls.AddRange(new Control[] { lblFrom, _dateFrom, lblTo, _dateTo, btnDiff, _dateResult, lblAdd, _daysBox, btnAdd, _dateAddResult });
        }

        private void DoDateDiff()
        {
            var d = NovaCalc.Converters.DateCalculator.Difference(_dateFrom.Value, _dateTo.Value);
            _dateResult.Text = d.TotalDays + " " + Strings.T("date_days") + " (" + d.Years + "y " + d.Months + "m " + d.Days + "d)";
        }

        private void DoDateAdd()
        {
            int days;
            if (!int.TryParse(_daysBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out days))
            { ShowError(Strings.T("err_syntax")); return; }
            var res = NovaCalc.Converters.DateCalculator.AddDays(_dateFrom.Value, days);
            _dateAddResult.Text = res.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private void BuildStats(float s)
        {
            var lbl = new ThemedLabel { Text = Strings.T("stats_input"), Location = new Point(0, 0), AutoSize = true };
            _statsBox = new TextBox
            {
                Location = new Point(0, (int)(22 * s)),
                Size = new Size(_labContent.Width - (int)(4 * s), (int)(80 * s)),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = "1, 2, 3, 4, 5"
            };
            var btn = MakeActionButton(Strings.T("lab_statistics"), (int)(0), (int)(110 * s), (int)(140 * s), DoStats);
            _statsResult = new Label
            {
                Location = new Point((int)(150 * s), (int)(110 * s)),
                Size = new Size(_labContent.Width - (int)(150 * s), _labContent.Height - (int)(115 * s)),
                Font = new Font("Segoe UI", 9f * s),
                ForeColor = _theme.TextPrimary,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _labContent.Controls.AddRange(new Control[] { lbl, _statsBox, btn, _statsResult });
        }

        private void DoStats()
        {
            try
            {
                var values = NovaCalc.Converters.Statistics.ParseList(_statsBox.Text);
                if (values.Count == 0) { ShowError(Strings.T("err_syntax")); return; }
                var r = NovaCalc.Converters.Statistics.Compute(values);
                _statsResult.Text =
                    Strings.T("stats_count") + ": " + r.Count + "\n" +
                    Strings.T("stats_sum") + ": " + FormatOut(r.Sum) + "\n" +
                    Strings.T("stats_mean") + ": " + FormatOut(r.Mean) + "\n" +
                    Strings.T("stats_min") + ": " + FormatOut(r.Min) + "\n" +
                    Strings.T("stats_max") + ": " + FormatOut(r.Max) + "\n" +
                    Strings.T("stats_median") + ": " + FormatOut(r.Median) + "\n" +
                    Strings.T("stats_stdev") + ": " + FormatOut(r.StdDev);
                _engine.Ans = r.Mean;
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private RoundButton MakeActionButton(string text, int x, int y, int w, Action action)
        {
            var b = new RoundButton
            {
                Text = text,
                Location = new Point(x, y),
                Width = w,
                Height = (int)(30 * _dpiScale),
                Radius = 8f * _dpiScale,
                Font = new Font("Segoe UI", 9f * _dpiScale)
            };
            b.Click += (a, ev) => action();
            _allButtons.Add(b);
            return b;
        }

        private Label MakeResultLabel(int x, int y)
        {
            return new ThemedLabel
            {
                Text = "",
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10f * _dpiScale)
            };
        }

        private void InsertIntoCurrent(decimal val)
        {
            if (_mode == CalcMode.Scientific)
            {
                AppendExpr(val.ToString(CultureInfo.InvariantCulture));
                UpdateDisplay();
            }
            else
            {
                _entryBuffer = val.ToString(CultureInfo.InvariantCulture);
                _startNewEntry = false;
                UpdateDisplay();
            }
        }

        // ============================================================
        // ЛОГИКА ОБЫЧНОГО РЕЖИМА (цепочка)
        // ============================================================

        private string _entryBuffer = "";

        private void OnDigit(object sender, EventArgs e)
        {
            var b = sender as RoundButton;
            if (b == null) return;
            AppendChar(b.Text);
        }

        private void AppendChar(string ch)
        {
            if (_mode == CalcMode.Scientific)
            {
                _exprInput += ch;
                UpdateDisplay();
                return;
            }
            // Процент — часть текущего ввода: новый символ «срезает» постфиксный %,
            // как в калькуляторе Windows (10% затем 5 -> 10.5).
            if (_percentPending && _entryBuffer.EndsWith("%"))
            {
                _entryBuffer = _entryBuffer.Substring(0, _entryBuffer.Length - 1);
                _percentPending = false;
            }
            // Новый ввод: после старта/смены значения ИЛИ сразу после «=» без нового оператора
            // (иначе «6×6==» дублировало бы результат, а «= затем 7» портил последний ответ).
            if (_startNewEntry || _justEvaluated)
            {
                if (_justEvaluated && _pendingOp == '\0') { _accValue = 0m; _chainExpr = ""; _expressionText = ""; }
                _entryBuffer = "";
                _startNewEntry = false;
                _justEvaluated = false;
            }
            if (_entryBuffer == "0" && ch != ".") _entryBuffer = "";
            _entryBuffer += ch;
            UpdateDisplay();
        }

        private void AppendDecimalSeparator()
        {
            if (_mode == CalcMode.Scientific) { AppendExpr("."); UpdateDisplay(); return; }
            if (_percentPending && _entryBuffer.EndsWith("%"))
            {
                _entryBuffer = _entryBuffer.Substring(0, _entryBuffer.Length - 1);
                _percentPending = false;
            }
            if (_startNewEntry || _justEvaluated)
            {
                if (_justEvaluated && _pendingOp == '\0') { _accValue = 0m; _chainExpr = ""; _expressionText = ""; }
                _entryBuffer = "0"; _startNewEntry = false; _justEvaluated = false;
            }
            if (!_entryBuffer.Contains(".")) _entryBuffer += ".";
            UpdateDisplay();
        }

        private decimal CurrentInputValue()
        {
            if (_mode == CalcMode.Scientific)
            {
                // попытка вычислить текущее выражение; иначе 0
                try { return _engine.Evaluate(_exprInput); }
                catch (Exception) { return 0m; }
            }
            string s = _entryBuffer;
            bool pct = s.EndsWith("%");
            if (pct) s = s.Substring(0, s.Length - 1);
            decimal v;
            if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return pct ? v / 100m : v;
            return 0m;
        }

        private void OperatorPressed(char op)
        {
            if (_mode == CalcMode.Scientific)
            {
                AppendExpr(op == '-' ? "-" : op.ToString());
                UpdateDisplay();
                return;
            }
            if (_pendingOp == '\0')
            {
                // Первый оператор в цепочке: левый операнд = текущий ввод (или прошлый результат)
                _accValue = CurrentInputValue();
                _engine.Ans = _accValue;
            }
            else if (!_startNewEntry)
            {
                // 6 + 7 × ... — промежуточное вычисление с процентной семантикой Windows
                _accValue = PercentChainStep(_pendingOp, _accValue, _exprOfEntry(), op);
                _engine.Ans = _accValue;
            }
            else
            {
                // Просто замена ожидающего оператора: 6 + ... → 6 × ...
                _chainExpr = FormatOut(_accValue) + " " + OpSymbol(op);
            }
            _expressionText = _chainExpr;
            _pendingOp = op;
            _startNewEntry = true;
            _justEvaluated = false;
            _percentPending = false;
            _entryBuffer = "";
            UpdateDisplay();
        }

        /// <summary>Строковое представление текущего ввода для движка (с постфиксным %, если он был нажат).</summary>
        private string _exprOfEntry()
        {
            string s = _entryBuffer.Length > 0 ? _entryBuffer : "0";
            return _percentPending ? s + "%" : s;
        }

        /// <summary>
        /// Шаг цепочки «a op b% c» через основной парсер движка — единая реализация
        /// процентов Windows (200+10%=220, 200*10%=20, 200/10%=2000), без дублирования логики в UI.
        /// </summary>
        private decimal PercentChainStep(char prevOp, decimal a, string bExpr, char nextOp)
        {
            try
            {
                return _engine.Evaluate(a.ToString(CultureInfo.InvariantCulture) + " " + prevOp + " " +
                                        bExpr + " " + nextOp + " 0");
            }
            catch (CalcException ex) { ShowError(LocalizedError(ex)); throw; }
        }

        private decimal ParseEntryAndApplyPercent()
        {
            // Реликтовый путь сохранён для совместимости; сейчас вся процентная
            // логика живёт в строке ввода ('%'-постфикс) и в движке.
            return CurrentInputValue();
        }

        private void OnPercentClick(object sender, EventArgs e)
        {
            if (_mode == CalcMode.Scientific) { AppendExpr("%"); UpdateDisplay(); return; }
            // Процент — часть текущего ввода: просто добавляем постфикс '%' к строке ввода.
            // Значение пересчитает движок при операторе или «=» (единая логика процентов).
            if (_startNewEntry && _entryBuffer.Length == 0)
                _entryBuffer = FormatOut(_pendingOp != '\0' ? _accValue : _engine.Ans);
            _justEvaluated = false;
            if (!_entryBuffer.EndsWith("%")) _entryBuffer += "%";
            _percentPending = true;
            _startNewEntry = false;
            UpdateDisplay();
        }

        private string OpSymbol(char op)
        {
            switch (op)
            {
                case '+': return "+";
                case '-': return "−";
                case '*': return "×";
                case '/': return "÷";
                default: return op.ToString();
            }
        }

        private void DoEquals()
        {
            if (_mode == CalcMode.Scientific)
            {
                try
                {
                    decimal res = _engine.Evaluate(_exprInput);
                    _engine.Ans = res;
                    AddToHistory(_exprInput, res);
                    _expressionText = _exprInput + " =";
                    _exprInput = res.ToString(CultureInfo.InvariantCulture);
                    _exprJustEvaluated = true;
                    ShowOnDisplay(res);
                }
                catch (CalcException ex) { ShowError(LocalizedError(ex)); }
                catch (Exception ex) { ShowError(ex.Message); }
                UpdateDisplay();
                return;
            }
            // Единый путь: собираем строку цепочки и вычисляем её основным парсером движка.
            // Это гарантирует идентичность результатов обычного и инженерного режимов
            // и корректную процентную семантику Windows на всех шагах цепочки.
            string expr;      // машиночитаемое выражение для движка
            string shown;     // человекочитаемое выражение для истории/строки
            if (_pendingOp != '\0')
            {
                expr = _accValue.ToString(CultureInfo.InvariantCulture) + " " + _pendingOp + " " + _exprOfEntry();
                shown = _chainExpr + " " + _exprOfEntry();
            }
            else if (_entryBuffer.Length > 0)
            {
                expr = _entryBuffer; shown = _entryBuffer;
            }
            else
            {
                expr = _justEvaluated ? _engine.Ans.ToString(CultureInfo.InvariantCulture) : "0";
                shown = expr;
            }
            decimal result;
            try { result = _engine.Evaluate(expr); }
            catch (CalcException ex) { ShowError(LocalizedError(ex)); return; }
            catch (Exception ex) { ShowError(ex.Message); return; }
            _accValue = result;
            _engine.Ans = result;
            _expressionText = shown + " =";
            _chainExpr = FormatOut(result);   // «=» продолжает цепочку от результата
            _pendingOp = '\0';
            _startNewEntry = true;
            _justEvaluated = true;
            _percentPending = false;
            _entryBuffer = "";
            AddToHistory(shown, result);
            ShowOnDisplay(result);
            UpdateDisplay();
        }

        private void Negate()
        {
            if (_mode == CalcMode.Scientific) { AppendExpr("-("); UpdateDisplay(); return; }
            decimal v = CurrentInputValue();
            _entryBuffer = (-v).ToString(CultureInfo.InvariantCulture);
            _startNewEntry = false;
            _justEvaluated = false;
            _percentPending = false;
            UpdateDisplay();
        }

        private void UnaryOp(Func<decimal, decimal> f, string exprPrefix, string exprSuffix)
        {
            try
            {
                decimal v = CurrentInputValue();
                decimal res = f(v);
                if (_mode == CalcMode.Scientific)
                {
                    _exprInput = exprPrefix + _exprInput + exprSuffix;
                }
                else
                {
                    AddToHistory(exprPrefix + FormatOut(v) + exprSuffix, res);
                    _entryBuffer = res.ToString(CultureInfo.InvariantCulture);
                    _startNewEntry = true;
                    _justEvaluated = false;
                    _percentPending = false;
                }
                _engine.Ans = res;
                ShowOnDisplay(res);
            }
            catch (CalcException ex) { ShowError(LocalizedError(ex)); }
            catch (Exception ex) { ShowError(ex.Message); }
            UpdateDisplay();
        }

        private decimal SafeDivLocal(decimal a, decimal b) { return CalculatorEngine.SafeDiv(a, b); }
        private decimal SafeMulLocal(decimal a, decimal b) { return CalculatorEngine.SafeMul(a, b); }
        private decimal SqrtLocal(decimal v)
        {
            if (v < 0m) throw new InvalidOperationCalcException("err_invalidop");
            return _engine.Evaluate("sqrt(" + v.ToString(CultureInfo.InvariantCulture) + ")");
        }

        private void ClearAll()
        {
            // Полная очистка: значение, цепочка, ввод, выражение, движок (Ans)
            _accValue = 0m; _pendingOp = '\0'; _entryBuffer = ""; _startNewEntry = true;
            _justEvaluated = false; _percentPending = false; _expressionText = ""; _chainExpr = "";
            _exprInput = ""; _exprJustEvaluated = false;
            _engine.Reset();
            _historyNavIndex = -1;
            ShowOnDisplayRaw("");
            UpdateDisplay();
        }

        private void ClearEntry()
        {
            // CE чистит ТЕКУЩИЙ ВВОД целиком (как в Windows): 36 → 0, а не 36 → 6.
            // Накопленная цепочка (левый операнд и оператор) сохраняется.
            _entryBuffer = ""; _percentPending = false; _startNewEntry = true; _justEvaluated = false;
            _exprInput = ""; _exprJustEvaluated = false;
            UpdateDisplay();
        }

        private void Backspace()
        {
            if (_mode == CalcMode.Scientific)
            {
                BackspaceExpr();
                UpdateDisplay();
                return;
            }
            // «Сброс» (как в калькуляторе Windows): очистка текущего ввода целиком.
            _entryBuffer = "";
            _percentPending = false;
            _startNewEntry = true;
            _justEvaluated = false;
            UpdateDisplay();
        }

        // Память
        private void MemoryAction(string what)
        {
            try
            {
                decimal v = CurrentInputValue();
                switch (what)
                {
                    case "mc": _engine.Memory = 0m; break;
                    case "mr":
                        _entryBuffer = _engine.Memory.ToString(CultureInfo.InvariantCulture);
                        _startNewEntry = true;
                        break;
                    case "mp": _engine.Memory = CalculatorEngine.SafeAdd(_engine.Memory, v); break;
                    case "mm": _engine.Memory = CalculatorEngine.SafeSub(_engine.Memory, v); break;
                    case "ms": _engine.Memory = v; break;
                }
            }
            catch (CalcException ex) { ShowError(LocalizedError(ex)); }
            UpdateDisplay();
        }

        // ============================================================
        // ОТОБРАЖЕНИЕ
        // ============================================================

        private void ShowOnDisplay(decimal v)
        {
            _lblDisplay.Text = FormatOut(v);
            _lblDisplay.ForeColor = _theme.TextPrimary;
        }

        private void ShowOnDisplayRaw(string t)
        {
            _lblDisplay.Text = t.Length == 0 ? "0" : t;
        }

        private string FormatOut(decimal v)
        {
            return Strings.FormatNumber(v, _settings.MaxDigits, _settings.GroupSeparators, _settings.ScientificNotation);
        }

        private void ShowError(string msg)
        {
            _lblDisplay.Text = msg;
            _lblDisplay.ForeColor = _theme.ErrorText;
            _lblExpression.Text = "";
        }

        private string LocalizedError(CalcException ex)
        {
            return Strings.T(ex.MessageKey);
        }

        private void UpdateDisplay()
        {
            _lblExpression.Text = _expressionText;
            _lblMemIndicator.Text = _engine.Memory != 0m ? Strings.T("mem_indicator") : "";
            _lblMemIndicator.Location = new Point((int)(10 * _dpiScale), (int)(6 * _dpiScale));
            if (_mode == CalcMode.Scientific)
            {
                _lblDisplay.Text = _exprInput.Length > 0 ? _exprInput : "0";
                _lblDisplay.ForeColor = _theme.TextPrimary;
                return;
            }
            if (_startNewEntry && _entryBuffer.Length == 0)
            {
                _lblDisplay.Text = _pendingOp != '\0' ? FormatOut(_accValue) : (_justEvaluated ? FormatOut(_accValue) : "0");
            }
            else
            {
                _lblDisplay.Text = _entryBuffer.Length == 0 ? "0" : _entryBuffer;
            }
            _lblDisplay.ForeColor = _theme.TextPrimary;
        }

        private void AddToHistory(string expression, decimal result)
        {
            _history.Add(new HistoryItem { Expression = expression, Result = result.ToString(CultureInfo.InvariantCulture) });
            StorageService.SaveHistory(_history);
            RefreshHistory();
        }

        private void RefreshHistory()
        {
            _historyList.BeginUpdate();
            _historyList.Items.Clear();
            for (int i = _history.Count - 1; i >= 0 && i >= _history.Count - 100; i--)
            {
                var it = _history[i];
                decimal r;
                string rs = decimal.TryParse(it.Result, NumberStyles.Float, CultureInfo.InvariantCulture, out r)
                    ? FormatOut(r) : it.Result;
                _historyList.Items.Add(it.Expression + " = " + rs);
            }
            if (_historyList.Items.Count == 0)
                _historyList.Items.Add(Strings.T("history_empty"));
            _historyList.EndUpdate();
        }

        private void HistoryActivate(object sender, EventArgs e)
        {
            int idx = _historyList.SelectedIndex;
            if (idx < 0) return;
            int realIdx = _history.Count - 1 - idx;
            if (realIdx < 0 || realIdx >= _history.Count) return;
            var it = _history[realIdx];
            decimal res;
            if (decimal.TryParse(it.Result, NumberStyles.Float, CultureInfo.InvariantCulture, out res))
            {
                if (_mode == CalcMode.Scientific) { _exprInput = it.Expression; UpdateDisplay(); }
                else { _entryBuffer = it.Result; _startNewEntry = true; ShowOnDisplay(res); }
            }
        }

        private void ToggleHistory()
        {
            _historyPanel.Visible = !_historyPanel.Visible;
            // при открытии истории увеличиваем окно, чтобы клавиатура не обрезалась
            int needH = LayoutHeight();
            if (ClientSize.Height < needH) ClientSize = new Size(ClientSize.Width, needH);
            RelayoutBody();
        }

        private void RelayoutBody()
        {
            // Единая перекладка: история, тело (клавиатура/sci/lab) — всё из формул геометрии
            if (_historyPanel == null || _keypad == null) return;
            _historyPanel.Location = HistoryLocation();
            _historyPanel.Size = HistorySize();
            Point p = BodyLocation();
            if (_mode == CalcMode.Scientific)
            {
                _sciPanel.SetBounds(p.X, p.Y, SciBodySize().Width, SciBodySize().Height);
                Size ks = KeypadSizeForSci();
                _keypad.SetBounds(p.X + ks.Width + (int)(Gap * _dpiScale), p.Y, ks.Width, ks.Height);
            }
            else
            {
                Size bs = BodySize();
                _keypad.SetBounds(p.X, p.Y, bs.Width, bs.Height);
                _sciPanel.SetBounds(p.X, p.Y, SciBodySize().Width, bs.Height);
                _labPanel.SetBounds(p.X, p.Y, bs.Width, bs.Height);
            }
            LayoutCaptionButtons();
            Invalidate(false);
        }

        // ============================================================
        // РЕЖИМЫ
        // ============================================================

        private void SwitchMode(CalcMode m)
        {
            _mode = m;
            _historyNavIndex = -1;
            int W = LayoutWidth();
            int H = LayoutHeight();
            if (ClientSize.Width != W || ClientSize.Height != H)
            {
                int cx = Left + ClientSize.Width / 2, cy = Top + ClientSize.Height / 2;
                SuspendLayout();
                ClientSize = new Size(W, H);
                Left = cx - W / 2; Top = cy - H / 2;
                ResumeLayout(true);
            }
            _keypad.Visible = m == CalcMode.Standard || m == CalcMode.Scientific;
            _sciPanel.Visible = m == CalcMode.Scientific;
            _labPanel.Visible = m == CalcMode.Laboratory;
            RelayoutBody();
            UpdateNumBaseButtons();

            // подсветка активной кнопки режима
            for (int i = 0; i < 3; i++)
            {
                _modeButtons[i].IsAccent = (i == (int)m);
                _modeButtons[i].NormalColor = (i == (int)m) ? _theme.Accent : _theme.FuncButton;
                _modeButtons[i].TextColor = (i == (int)m) ? _theme.AccentText : _theme.FuncText;
                _modeButtons[i].HoverColor = ThemePalette.HoverOf(_theme, _modeButtons[i].NormalColor);
                _modeButtons[i].PressColor = ThemePalette.PressOf(_theme, _modeButtons[i].NormalColor);
                _modeButtons[i].Invalidate();
            }

            if (_settings.RememberLastMode)
            {
                _settings.StartupMode = (int)m;
                StorageService.SaveSettings(_settings);
            }
            UpdateDisplay();
        }

        private void ToggleTopMost()
        {
            TopMost = !TopMost;
            _settings.TopMost = TopMost;
            StorageService.SaveSettings(_settings);
        }

        // ============================================================
        // ТЕМЫ И ЯЗЫКИ
        // ============================================================

        private void ApplyTheme()
        {
            _theme = ThemePalette.For(_settings.Theme);
            _root.ColorTop = _theme.BackgroundTop;
            _root.ColorBottom = _theme.BackgroundBottom;
            BackColor = _theme.BackgroundBottom;
            _displayPanel.FillColor = _theme.Panel;
            _displayPanel.BorderColor = _theme.Border;
            _lblDisplay.ForeColor = _theme.TextPrimary;
            _lblExpression.ForeColor = _theme.TextSecondary;
            _lblMemIndicator.ForeColor = _theme.Accent;
            _historyPanel.BackColor = Color.Transparent;
            _historyList.BackColor = _theme.HistoryBg;
            _historyList.ForeColor = _theme.TextPrimary;
            if (_constList != null) { _constList.BackColor = _theme.HistoryBg; _constList.ForeColor = _theme.TextPrimary; }

            foreach (var b in _allButtons)
            {
                bool accent = b.IsAccent;
                b.NormalColor = accent ? _theme.Accent : ClassifyButtonColor(b);
                b.TextColor = accent ? _theme.AccentText : ClassifyTextColor(b);
                b.HoverColor = ThemePalette.HoverOf(_theme, b.NormalColor);
                b.PressColor = ThemePalette.PressOf(_theme, b.NormalColor);
                // Подсказки при наведении: для sci-кнопок — их «сырое» имя функции
                var tag = b.Tag as string;
                if (tag != null && tag.StartsWith("sci:"))
                    ShowTipOnHover(b, tag.Substring(4));
                b.Invalidate();
            }
            // режимные кнопки перекрасит SwitchMode
            SwitchModeVisualOnly();
            Invalidate(true);
        }

        private void SwitchModeVisualOnly()
        {
            for (int i = 0; i < 3; i++)
            {
                if (_modeButtons == null) return;
                _modeButtons[i].IsAccent = (i == (int)_mode);
                _modeButtons[i].NormalColor = (i == (int)_mode) ? _theme.Accent : _theme.FuncButton;
                _modeButtons[i].TextColor = (i == (int)_mode) ? _theme.AccentText : _theme.FuncText;
                _modeButtons[i].HoverColor = ThemePalette.HoverOf(_theme, _modeButtons[i].NormalColor);
                _modeButtons[i].PressColor = ThemePalette.PressOf(_theme, _modeButtons[i].NormalColor);
                _modeButtons[i].Invalidate();
            }
        }

        private Color ClassifyButtonColor(RoundButton b)
        {
            object tag = b.Tag;
            string t = tag as string ?? "";
            if (t.StartsWith("sci:") || t.StartsWith("base:")) return _theme.FuncButton;
            string txt = b.Text;
            if (txt == "+" || txt == "−" || txt == "-" || txt == "×" || txt == "*" || txt == "÷" || txt == "/")
                return _theme.OpButton;
            if (txt == Strings.T("btn_clear") || txt == "CE" || txt == "⌫" || txt == "%" || txt == "+/-" ||
                txt == "1/x" || txt == "x²" || txt == "√x" || txt == "MC" || txt == "MR" || txt == "M+" || txt == "M-" || txt == "MS")
                return _theme.FuncButton;
            return _theme.DigitButton;
        }

        private Color ClassifyTextColor(RoundButton b)
        {
            object tag = b.Tag;
            string t = tag as string ?? "";
            if (t.StartsWith("sci:") || t.StartsWith("base:")) return _theme.FuncText;
            string txt = b.Text;
            if (txt == "+" || txt == "−" || txt == "-" || txt == "×" || txt == "*" || txt == "÷" || txt == "/")
                return _theme.OpText;
            if (txt == Strings.T("btn_clear") || txt == "CE" || txt == "⌫" || txt == "%" || txt == "+/-" ||
                txt == "1/x" || txt == "x²" || txt == "√x" || txt == "MC" || txt == "MR" || txt == "M+" || txt == "M-" || txt == "MS")
                return _theme.FuncText;
            return _theme.DigitText;
        }

        private void ApplyLanguage()
        {
            // перестроить подписи без перезапуска
            if (_modeButtons != null)
            {
                string[] keys = { "mode_standard", "mode_scientific", "mode_laboratory" };
                for (int i = 0; i < 3; i++) _modeButtons[i].Text = Strings.T(keys[i]);
            }
            RebuildLocalizedControls();
            UpdateDisplay();
            RefreshHistory();
        }

        private void RebuildLocalizedControls()
        {
            // пересобираем динамические части с новыми подписями
            BuildStandardKeypad();
            BuildScientificExtras();
            BuildLaboratory();
            ApplyTheme();
            SwitchMode(_mode);
        }

        // ============================================================
        // НАСТРОЙКИ
        // ============================================================

        private void OpenSettings()
        {
            using (var dlg = new SettingsForm(_settings, _theme, _dpiScale))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string newLang = dlg.ResultSettings.Language ?? Strings.DetectSystemLanguage();
                    bool langChanged = newLang != Strings.Current;
                    _settings = dlg.ResultSettings;
                    StorageService.SaveSettings(_settings);
                    Strings.Current = newLang;
                    ApplyTheme();
                    if (langChanged) ApplyLanguage();
                    UpdateDisplay();
                    RefreshHistory();
                }
            }
        }

        // ============================================================
        // ПЕРЕТАСКИВАНИЕ ОКНА
        // ============================================================

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 2;

        private void AttachDrag(Control c)
        {
            c.MouseDown += (a, e) =>
            {
                if (e.Button == MouseButtons.Left && e.Clicks == 1)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
        }

        // ============================================================
        // КЛАВИАТУРА
        // ============================================================

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // В лабораторном режиме поля ввода работают как обычные текстовые поля:
            // не перехватываем у них ввод (иначе нельзя напечатать «(», «x», «-» и т.п.).
            Control focused = ActiveControl;
            if (_mode == CalcMode.Laboratory &&
                (focused is TextBox || focused is ComboBox || focused is ListView || focused is DateTimePicker))
                return base.ProcessCmdKey(ref msg, keyData);

            switch (keyData)
            {
                case Keys.Escape:
                case Keys.Delete:
                    ClearAll(); return true;
                case Keys.Enter: // включает NumPad Enter (общий код 0x0D)
                    DoEquals(); return true;
                case Keys.Back:
                    Backspace(); return true;
                case Keys.F9:
                    Negate(); return true;
                case Keys.Control | Keys.C:
                    CopyResult(false); return true;
                case Keys.Control | Keys.Shift | Keys.C:
                    CopyResult(true); return true;
                case Keys.Control | Keys.V:
                    PasteFromClipboard(); return true;
                case Keys.Control | Keys.H:
                    ToggleHistory(); return true;
                case Keys.Control | Keys.Oemcomma:
                    OpenSettings(); return true;
                case Keys.Up:
                    NavigateHistory(-1); return true;
                case Keys.Down:
                    NavigateHistory(1); return true;
                case Keys.Add:
                case Keys.Oemplus:
                    if (_mode != CalcMode.Laboratory) { OperatorPressed('+'); return true; } break;
                case Keys.Subtract:
                case Keys.OemMinus:
                    if (_mode != CalcMode.Laboratory) { OperatorPressed('-'); return true; } break;
                case Keys.Multiply:
                    if (_mode != CalcMode.Laboratory) { OperatorPressed('*'); return true; } break;
                case Keys.Divide:
                    if (_mode != CalcMode.Laboratory) { OperatorPressed('/'); return true; } break;
                case Keys.OemPeriod:
                case Keys.Decimal:
                    if (_mode != CalcMode.Laboratory) { AppendDecimalSeparator(); return true; } break;
                case Keys.Oemcomma:
                    if (_mode == CalcMode.Scientific) { AppendExpr(","); UpdateDisplay(); return true; }
                    if (_mode == CalcMode.Standard) { AppendDecimalSeparator(); return true; }
                    break;
                case Keys.OemQuestion:
                case Keys.D1 | Keys.Shift: // '%' via Shift+5
                    break;
            }
            if (keyData == (Keys.D5 | Keys.Shift))
            {
                if (_mode != CalcMode.Laboratory) { OnPercentClick(null, EventArgs.Empty); return true; }
            }
            if (keyData == (Keys.D6 | Keys.Shift)) // '^'
            {
                if (_mode == CalcMode.Scientific) { SciInsert("^", "^"); return true; }
            }
            if (keyData >= Keys.D0 && keyData <= Keys.D9 && (keyData & Keys.Control) == 0 && (keyData & Keys.Shift) == 0)
            {
                if (_mode != CalcMode.Laboratory) { AppendChar(((char)('0' + (keyData - Keys.D0))).ToString()); return true; }
            }
            if (keyData >= Keys.NumPad0 && keyData <= Keys.NumPad9)
            {
                if (_mode != CalcMode.Laboratory) { AppendChar(((char)('0' + (keyData - Keys.NumPad0))).ToString()); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void CopyResult(bool expression)
        {
            try
            {
                string text = expression ? _expressionText + " " + _lblDisplay.Text : _lblDisplay.Text;
                if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
            }
            catch (Exception) { /* буфер занят — молча игнорируем, не падаем */ }
        }

        private void PasteFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;
                string text = Clipboard.GetText().Trim();
                decimal v;
                if (Strings.TryParseNumber(text, out v))
                {
                    InsertIntoCurrent(v);
                }
                else if (_mode == CalcMode.Scientific)
                {
                    _exprInput += text;
                    UpdateDisplay();
                }
            }
            catch (Exception) { }
        }

        private void NavigateHistory(int delta)
        {
            if (_history.Count == 0) return;
            if (!_historyPanel.Visible) { _historyPanel.Visible = true; RelayoutBody(); }
            _historyNavIndex = Math.Max(-1, Math.Min(_history.Count - 1, _historyNavIndex + (delta < 0 ? 1 : -1)));
            if (_historyNavIndex >= 0)
            {
                var it = _history[_history.Count - 1 - _historyNavIndex];
                _historyList.SelectedIndex = Math.Min(_historyList.Items.Count - 1, _historyNavIndex);
                decimal r;
                if (decimal.TryParse(it.Result, NumberStyles.Float, CultureInfo.InvariantCulture, out r))
                {
                    if (_mode == CalcMode.Scientific) _exprInput = it.Expression;
                    else _entryBuffer = it.Result;
                    ShowOnDisplay(r);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_modeSwitch != null && _modeButtons != null)
            {
                float s = _dpiScale;
                _modeSwitch.Size = new Size(ClientSize.Width - (int)(2 * Pad * s), _modeSwitch.Height);
                int bw = (ClientSize.Width - (int)((2 * Pad + 16) * s)) / 3;
                foreach (var b in _modeButtons) if (b != null) b.Width = Math.Max((int)(40 * s), bw);
                // дисплей тянется за шириной
                if (_displayPanel != null) _displayPanel.Width = ClientSize.Width - (int)(2 * Pad * s);
                RelayoutBody();
            }
        }
    }
}
