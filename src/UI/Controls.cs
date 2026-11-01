using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NovaCalc.UI
{
    /// <summary>Скруглённая кнопка с hover/press-эффектами, без мигания.</summary>
    public class RoundButton : Button
    {
        public float Radius = 10f;
        public Color NormalColor = Color.FromArgb(48, 52, 72);
        public Color HoverColor = Color.FromArgb(64, 70, 96);
        public Color PressColor = Color.FromArgb(36, 40, 58);
        public Color TextColor = Color.White;
        public bool IsAccent = false;
        /// <summary>Кнопки заголовка: плоские, подложка только на hover; close — красная.</summary>
        public bool CaptionStyle = false;

        private bool _hover;
        private bool _press;

        public RoundButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _press = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.2f, Height - 1.2f);
            bool closeHover = CaptionStyle && AccessibleName == "close" && _hover;
            using (GraphicsPath path = RoundedPath(r, Radius))
            {
                if (CaptionStyle)
                {
                    if (closeHover)
                        using (var b = new SolidBrush(Color.FromArgb(198, 48, 62)))
                            g.FillPath(b, path);
                    else if (_hover || _press)
                        using (var b = new SolidBrush(_press ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(36, 255, 255, 255)))
                            g.FillPath(b, path);
                }
                else
                {
                    Color fill = IsAccent ? AccentShade() : BaseShade();
                    using (var b = new SolidBrush(fill))
                        g.FillPath(b, path);
                    // лёгкий верхний блик для глубины
                    using (var gloss = new LinearGradientBrush(Rectangle.Round(r), Color.FromArgb(26, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                        g.FillPath(gloss, path);
                    // тонкая рамка для акрила
                    using (var p = new Pen(Color.FromArgb(IsAccent ? 0 : 40, 255, 255, 255), 1f))
                        g.DrawPath(p, path);
                }
            }

            Color textColor = closeHover ? Color.White : (Enabled ? TextColor : Color.Gray);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        private Color BaseShade()
        {
            if (_press) return PressColor;
            if (_hover) return HoverColor;
            return NormalColor;
        }

        private Color AccentShade()
        {
            if (_press) return ControlPaint.Dark(NormalColor, 0.15f);
            if (_hover) return ControlPaint.Light(NormalColor, 0.12f);
            return NormalColor;
        }

        public static GraphicsPath RoundedPath(RectangleF rect, float radius)
        {
            float d = radius * 2f;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>Карточка-панель со скруглением и полупрозрачной подложкой (безопасная имитация акрила).</summary>
    public class GlassPanel : Panel
    {
        public float Radius = 12f;
        public Color FillColor = Color.FromArgb(35, 38, 55);
        public Color BorderColor = Color.FromArgb(70, 76, 105);
        public bool DrawBorder = true;

        public GlassPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.2f, Height - 1.2f);
            using (GraphicsPath path = RoundButton.RoundedPath(rect, Radius))
            {
                using (var b = new SolidBrush(FillColor))
                    g.FillPath(b, path);
                if (DrawBorder)
                    using (var p = new Pen(BorderColor, 1f))
                        g.DrawPath(p, path);
            }
        }
    }

    /// <summary>Фоновая панель с вертикальным градиентом (градиентный фон окна).</summary>
    public class GradientPanel : Panel
    {
        public Color ColorTop = Color.FromArgb(24, 26, 38);
        public Color ColorBottom = Color.FromArgb(16, 17, 26);

        public GradientPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, Width, Math.Max(1, Height)), ColorTop, ColorBottom, LinearGradientMode.Vertical))
                g.FillRectangle(br, ClientRectangle);
        }
    }

    /// <summary>Метка с фиксированным цветом текста (перекрашивается при смене темы).</summary>
    public class ThemedLabel : Label
    {
        public Color FixedColor = Color.White;
        public ThemedLabel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = FixedColor;
        }
    }

    /// <summary>Вспомогательные P/Invoke для тени окна и скругления углов.</summary>
    internal static class NativeMethods
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS { public int Left, Right, Top, Bottom; }

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);
    }
}
