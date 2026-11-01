using System;
using System.Collections.Generic;
using System.Globalization;

namespace NovaCalc.Themes
{
    /// <summary>Палитра темы: цвета, градиенты, шрифты.</summary>
    public class ThemePalette
    {
        public string Name;
        public System.Drawing.Color BackgroundTop;      // градиент фона окна
        public System.Drawing.Color BackgroundBottom;
        public System.Drawing.Color Panel;              // панели/карточки (имитация акрила)
        public System.Drawing.Color TextPrimary;
        public System.Drawing.Color TextSecondary;
        public System.Drawing.Color Accent;             // цвет акцентной кнопки (=)
        public System.Drawing.Color AccentText;
        public System.Drawing.Color DigitButton;        // кнопки цифр
        public System.Drawing.Color DigitText;
        public System.Drawing.Color OpButton;           // кнопки операторов
        public System.Drawing.Color OpText;
        public System.Drawing.Color FuncButton;         // функции/память
        public System.Drawing.Color FuncText;
        public System.Drawing.Color HoverTint;          // подсветка при наведении
        public System.Drawing.Color PressTint;          // эффект нажатия
        public System.Drawing.Color Border;
        public System.Drawing.Color ErrorText;
        public System.Drawing.Color HistoryBg;
        public System.Drawing.Color CaptionText;        // текст кнопок заголовка (свернуть/закрыть/…)
        public System.Drawing.Color ControlChrome;      // фон стандартных контролов (ComboBox/ListView/DateTimePicker)
        public float CornerRadius = 10f;                // скругление (в DIP, масштабируется DPI)

        /// <summary>Тёмная тема по умолчанию.</summary>
        public static ThemePalette Dark()
        {
            return new ThemePalette
            {
                Name = "dark",
                BackgroundTop = System.Drawing.Color.FromArgb(24, 26, 38),
                BackgroundBottom = System.Drawing.Color.FromArgb(16, 17, 26),
                Panel = System.Drawing.Color.FromArgb(35, 38, 55),
                TextPrimary = System.Drawing.Color.FromArgb(235, 238, 248),
                TextSecondary = System.Drawing.Color.FromArgb(150, 156, 178),
                Accent = System.Drawing.Color.FromArgb(96, 132, 255),
                AccentText = System.Drawing.Color.White,
                DigitButton = System.Drawing.Color.FromArgb(48, 52, 72),
                DigitText = System.Drawing.Color.FromArgb(235, 238, 248),
                OpButton = System.Drawing.Color.FromArgb(58, 63, 88),
                OpText = System.Drawing.Color.FromArgb(200, 210, 255),
                FuncButton = System.Drawing.Color.FromArgb(40, 44, 62),
                FuncText = System.Drawing.Color.FromArgb(160, 168, 195),
                HoverTint = System.Drawing.Color.FromArgb(255, 255, 255),
                PressTint = System.Drawing.Color.FromArgb(0, 0, 0),
                Border = System.Drawing.Color.FromArgb(70, 76, 105),
                ErrorText = System.Drawing.Color.FromArgb(255, 120, 120),
                HistoryBg = System.Drawing.Color.FromArgb(30, 33, 48),
                CaptionText = System.Drawing.Color.FromArgb(205, 210, 228),
                ControlChrome = System.Drawing.Color.FromArgb(42, 46, 66),
            };
        }

        /// <summary>Светлая тема.</summary>
        public static ThemePalette Light()
        {
            return new ThemePalette
            {
                Name = "light",
                BackgroundTop = System.Drawing.Color.FromArgb(243, 245, 252),
                BackgroundBottom = System.Drawing.Color.FromArgb(226, 231, 244),
                Panel = System.Drawing.Color.FromArgb(255, 255, 255),
                TextPrimary = System.Drawing.Color.FromArgb(28, 30, 40),
                TextSecondary = System.Drawing.Color.FromArgb(105, 110, 130),
                Accent = System.Drawing.Color.FromArgb(64, 98, 228),
                AccentText = System.Drawing.Color.White,
                DigitButton = System.Drawing.Color.FromArgb(255, 255, 255),
                DigitText = System.Drawing.Color.FromArgb(28, 30, 40),
                OpButton = System.Drawing.Color.FromArgb(235, 239, 250),
                OpText = System.Drawing.Color.FromArgb(50, 60, 110),
                FuncButton = System.Drawing.Color.FromArgb(225, 230, 245),
                FuncText = System.Drawing.Color.FromArgb(80, 88, 120),
                HoverTint = System.Drawing.Color.FromArgb(0, 0, 0),
                PressTint = System.Drawing.Color.FromArgb(120, 120, 120),
                Border = System.Drawing.Color.FromArgb(200, 206, 224),
                ErrorText = System.Drawing.Color.FromArgb(200, 40, 40),
                HistoryBg = System.Drawing.Color.FromArgb(248, 249, 253),
                CaptionText = System.Drawing.Color.FromArgb(70, 76, 100),
                ControlChrome = System.Drawing.Color.FromArgb(255, 255, 255),
            };
        }

        public static ThemePalette For(string name)
        {
            return name == "light" ? Light() : Dark();
        }

        // ===== Смешение цветов для hover/press эффектов =====

        /// <summary>Смешивает два цвета; alpha в [0..1] — доля overlay.</summary>
        public static System.Drawing.Color Blend(System.Drawing.Color baseColor, System.Drawing.Color overlay, double alpha)
        {
            if (alpha < 0) alpha = 0; if (alpha > 1) alpha = 1;
            return System.Drawing.Color.FromArgb(
                (int)(baseColor.R * (1 - alpha) + overlay.R * alpha),
                (int)(baseColor.G * (1 - alpha) + overlay.G * alpha),
                (int)(baseColor.B * (1 - alpha) + overlay.B * alpha));
        }

        public static System.Drawing.Color HoverOf(ThemePalette t, System.Drawing.Color c)
        {
            return Blend(c, t.HoverTint, t.Name == "dark" ? 0.12 : 0.06);
        }

        public static System.Drawing.Color PressOf(ThemePalette t, System.Drawing.Color c)
        {
            return Blend(c, t.PressTint, t.Name == "dark" ? 0.22 : 0.14);
        }
    }
}
