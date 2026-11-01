using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using NovaCalc.Converters;
using NovaCalc.Localization;
using NovaCalc.Services;
using NovaCalc.Themes;

namespace NovaCalc.UI
{
    /// <summary>Диалог настроек: тема, язык, режим запуска, знаки, topmost, формат чисел.</summary>
    public class SettingsForm : Form
    {
        public AppSettings ResultSettings;

        private readonly AppSettings _orig;
        private readonly ThemePalette _theme;
        private readonly float _s;

        private ComboBox _cmbTheme, _cmbLang, _cmbMode;
        private NumericUpDown _numDigits;
        private CheckBox _chkTopMost, _chkGroup, _chkSci, _chkRemember;

        public SettingsForm(AppSettings current, ThemePalette theme, float dpiScale)
        {
            _orig = current;
            ResultSettings = current;
            _theme = theme;
            _s = dpiScale;

            SuspendLayout();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size((int)(380 * _s), (int)(360 * _s));
            BackColor = _theme.BackgroundBottom;
            ForeColor = _theme.TextPrimary;
            Font = new Font("Segoe UI", 9.5f * _s);
            Text = Strings.T("settings") + " — NovaCalc";

            int y = (int)(16 * _s), lh = (int)(40 * _s);
            int labelW = (int)(170 * _s);

            AddLabel(Strings.T("settings_theme"), ref y, lh, labelW);
            _cmbTheme = MakeCombo(ref y, lh);
            _cmbTheme.Items.Add(Strings.T("theme_dark"));
            _cmbTheme.Items.Add(Strings.T("theme_light"));
            _cmbTheme.SelectedIndex = _orig.Theme == "light" ? 1 : 0;

            AddLabel(Strings.T("settings_language"), ref y, lh, labelW);
            _cmbLang = MakeCombo(ref y, lh);
            foreach (var code in Strings.SupportedLanguages)
                _cmbLang.Items.Add(Strings.LanguageNativeName(code));
            string cur = _orig.Language ?? Strings.Current;
            int li = Strings.SupportedLanguages.IndexOf(cur);
            _cmbLang.SelectedIndex = li >= 0 ? li : 0;

            AddLabel(Strings.T("settings_startup_mode"), ref y, lh, labelW);
            _cmbMode = MakeCombo(ref y, lh);
            _cmbMode.Items.Add(Strings.T("mode_standard"));
            _cmbMode.Items.Add(Strings.T("mode_scientific"));
            _cmbMode.Items.Add(Strings.T("mode_laboratory"));
            _cmbMode.SelectedIndex = Math.Max(0, Math.Min(2, _orig.StartupMode));

            AddLabel(Strings.T("settings_max_digits"), ref y, lh, labelW);
            _numDigits = new NumericUpDown
            {
                Location = new Point(labelW + (int)(6 * _s), y - (int)(4 * _s)),
                Width = (int)(80 * _s),
                Minimum = 0, Maximum = 28,
                Value = _orig.MaxDigits
            };
            Controls.Add(_numDigits);
            y += lh;

            _chkTopMost = MakeCheck(Strings.T("settings_topmost"), ref y, lh, _orig.TopMost);
            _chkGroup = MakeCheck(Strings.T("settings_group_sep"), ref y, lh, _orig.GroupSeparators);
            _chkSci = MakeCheck(Strings.T("settings_scientific"), ref y, lh, _orig.ScientificNotation);
            _chkRemember = MakeCheck(Strings.T("settings_keep_mode"), ref y, lh, _orig.RememberLastMode);

            var ok = new Button { Text = Strings.T("ok"), DialogResult = DialogResult.OK, Width = (int)(110 * _s), Height = (int)(32 * _s) };
            ok.Location = new Point(ClientSize.Width - (int)(240 * _s), ClientSize.Height - (int)(44 * _s));
            ok.BackColor = _theme.Accent; ok.ForeColor = _theme.AccentText;
            var cancel = new Button { Text = Strings.T("cancel"), DialogResult = DialogResult.Cancel, Width = (int)(110 * _s), Height = (int)(32 * _s) };
            cancel.Location = new Point(ClientSize.Width - (int)(120 * _s), ClientSize.Height - (int)(44 * _s));
            cancel.BackColor = _theme.Panel; cancel.ForeColor = _theme.TextPrimary;
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;

            FormClosing += (a, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                ResultSettings = new AppSettings
                {
                    Theme = _cmbTheme.SelectedIndex == 1 ? "light" : "dark",
                    Language = Strings.SupportedLanguages[_cmbLang.SelectedIndex],
                    StartupMode = _cmbMode.SelectedIndex,
                    MaxDigits = (int)_numDigits.Value,
                    TopMost = _chkTopMost.Checked,
                    GroupSeparators = _chkGroup.Checked,
                    ScientificNotation = _chkSci.Checked,
                    RememberLastMode = _chkRemember.Checked
                };
            };
            ResumeLayout(false);
        }

        private void AddLabel(string text, ref int y, int lh, int labelW)
        {
            var l = new Label { Text = text, AutoSize = false, Size = new Size(labelW, lh), ForeColor = _theme.TextPrimary };
            l.Location = new Point((int)(14 * _s), y + (int)(8 * _s));
            Controls.Add(l);
        }

        private ComboBox MakeCombo(ref int y, int lh)
        {
            var c = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point((int)(180 * _s), y + (int)(4 * _s)),
                Width = (int)(180 * _s),
                BackColor = _theme.Panel,
                ForeColor = _theme.TextPrimary
            };
            Controls.Add(c);
            y += lh;
            return c;
        }

        private CheckBox MakeCheck(string text, ref int y, int lh, bool val)
        {
            var c = new CheckBox
            {
                Text = text,
                Checked = val,
                AutoSize = true,
                ForeColor = _theme.TextPrimary,
                Location = new Point((int)(18 * _s), y)
            };
            Controls.Add(c);
            y += lh;
            return c;
        }
    }

    /// <summary>Редактор пользовательских курсов валют с датой последней редакции.</summary>
    public class RatesEditorForm : Form
    {
        public List<Currency> ResultRates;

        private readonly ListView _list;
        private readonly TextBox[] _editors = new TextBox[3];
        private readonly ThemePalette _theme;
        private readonly float _s;
        private List<Currency> _rates;
        private int _selected = -1;

        public RatesEditorForm(List<Currency> rates, ThemePalette theme, float dpiScale)
        {
            _rates = new List<Currency>();
            foreach (var c in rates) _rates.Add(new Currency { Code = c.Code, Name = c.Name, RateToBase = c.RateToBase });
            _theme = theme; _s = dpiScale;

            SuspendLayout();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size((int)(440 * _s), (int)(420 * _s));
            BackColor = _theme.BackgroundBottom;
            ForeColor = _theme.TextPrimary;
            Font = new Font("Segoe UI", 9.5f * _s);
            Text = Strings.T("lab_edit_rates") + " (1 USD = X)";

            _list = new ListView
            {
                View = View.Details, FullRowSelect = true, HideSelection = false,
                Location = new Point((int)(12 * _s), (int)(12 * _s)),
                Size = new Size((int)(200 * _s), (int)(300 * _s)),
                BackColor = _theme.HistoryBg, ForeColor = _theme.TextPrimary
            };
            _list.Columns.Add("Code", (int)(60 * _s));
            _list.Columns.Add("Rate", (int)(120 * _s));
            _list.SelectedIndexChanged += (a, e) =>
            {
                _selected = _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] : -1;
                FillEditors();
            };
            Controls.Add(_list);

            var lblCode = NewLbl(Strings.T("lbl_input") + ":", (int)(224 * _s), (int)(14 * _s));
            _editors[0] = NewEdit((int)(224 * _s), (int)(34 * _s));
            var lblName = NewLbl("Name:", (int)(224 * _s), (int)(64 * _s));
            _editors[1] = NewEdit((int)(224 * _s), (int)(84 * _s));
            var lblRate = NewLbl("1 USD =", (int)(224 * _s), (int)(114 * _s));
            _editors[2] = NewEdit((int)(224 * _s), (int)(134 * _s));

            var btnApply = new Button { Text = Strings.T("settings_save"), Location = new Point((int)(224 * _s), (int)(170 * _s)), Size = new Size((int)(100 * _s), (int)(30 * _s)) };
            btnApply.Click += (a, e) => ApplyEdits();
            var btnAdd = new Button { Text = "+", Location = new Point((int)(330 * _s), (int)(170 * _s)), Size = new Size((int)(40 * _s), (int)(30 * _s)) };
            btnAdd.Click += (a, e) => { _selected = -1; ClearEditors(); };
            var btnDel = new Button { Text = "−", Location = new Point((int)(376 * _s), (int)(170 * _s)), Size = new Size((int)(40 * _s), (int)(30 * _s)) };
            btnDel.Click += (a, e) => DeleteSelected();

            var dateLbl = new Label
            {
                Text = Strings.T("rates_last_edit") + ": " + CurrencyService.GetLastEditedDate(),
                Location = new Point((int)(12 * _s), (int)(320 * _s)),
                AutoSize = true, ForeColor = _theme.TextSecondary
            };

            var ok = new Button { Text = Strings.T("ok"), DialogResult = DialogResult.OK, Location = new Point(ClientSize.Width - (int)(240 * _s), ClientSize.Height - (int)(44 * _s)), Size = new Size((int)(110 * _s), (int)(32 * _s)) };
            ok.BackColor = _theme.Accent; ok.ForeColor = _theme.AccentText;
            ok.Click += (a, e) =>
            {
                ApplyEditsSilently();
                string tmp;
                CurrencyService.Save(_rates, out tmp);
                ResultRates = _rates;
            };
            var cancel = new Button { Text = Strings.T("cancel"), DialogResult = DialogResult.Cancel, Location = new Point(ClientSize.Width - (int)(120 * _s), ClientSize.Height - (int)(44 * _s)), Size = new Size((int)(110 * _s), (int)(32 * _s)) };
            cancel.BackColor = _theme.Panel; cancel.ForeColor = _theme.TextPrimary;

            Controls.AddRange(new Control[] { lblCode, _editors[0], lblName, _editors[1], lblRate, _editors[2], btnApply, btnAdd, btnDel, dateLbl, ok, cancel });
            AcceptButton = ok;
            RefreshList();
            ResumeLayout(false);
        }

        private Label NewLbl(string t, int x, int y)
        {
            return new Label { Text = t, AutoSize = true, ForeColor = _theme.TextPrimary, Location = new Point(x, y) };
        }

        private TextBox NewEdit(int x, int y)
        {
            return new TextBox { Location = new Point(x, y), Width = (int)(190 * _s), BackColor = _theme.Panel, ForeColor = _theme.TextPrimary };
        }

        private void RefreshList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var c in _rates)
            {
                var item = new ListViewItem(c.Code);
                item.SubItems.Add(c.RateToBase.ToString(CultureInfo.InvariantCulture));
                _list.Items.Add(item);
            }
            _list.EndUpdate();
        }

        private void FillEditors()
        {
            if (_selected >= 0 && _selected < _rates.Count)
            {
                var c = _rates[_selected];
                _editors[0].Text = c.Code;
                _editors[1].Text = c.Name;
                _editors[2].Text = c.RateToBase.ToString(CultureInfo.InvariantCulture);
            }
            else ClearEditors();
        }

        private void ClearEditors()
        {
            _editors[0].Text = ""; _editors[1].Text = "New currency"; _editors[2].Text = "1";
        }

        private bool ApplyEditsSilently()
        {
            string code = _editors[0].Text.Trim().ToUpperInvariant();
            decimal rate;
            if (code.Length != 3 || !decimal.TryParse(_editors[2].Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out rate) || rate <= 0m)
                return false;
            string name = _editors[1].Text.Trim();
            if (_selected >= 0 && _selected < _rates.Count)
            {
                // если код меняем — убеждаемся, что он уникален
                var existing = _rates.Find(c => c.Code == code);
                if (existing != null && existing != _rates[_selected]) return false;
                var c0 = _rates[_selected];
                c0.Code = code; c0.Name = name; c0.RateToBase = rate;
            }
            else
            {
                if (_rates.Exists(c => c.Code == code)) return false;
                _rates.Add(new Currency { Code = code, Name = name, RateToBase = rate });
            }
            return true;
        }

        private void ApplyEdits()
        {
            if (ApplyEditsSilently())
            {
                RefreshList();
                _selected = -1;
            }
            else
            {
                MessageBox.Show(this, Strings.T("err_syntax"), "NovaCalc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeleteSelected()
        {
            if (_selected >= 0 && _selected < _rates.Count)
            {
                var c = _rates[_selected];
                if (c.Code == CurrencyService.BaseCode)
                {
                    MessageBox.Show(this, Strings.T("err_invalidop"), "NovaCalc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _rates.RemoveAt(_selected);
                _selected = -1;
                RefreshList();
                ClearEditors();
            }
        }
    }
}
