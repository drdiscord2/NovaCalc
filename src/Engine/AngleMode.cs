using System;

namespace NovaCalc.Engine
{
    /// <summary>Режим измерения углов для тригонометрических функций.</summary>
    public enum AngleMode
    {
        Degrees,
        Radians,
        Gradians
    }

    /// <summary>Преобразование углов между режимами.</summary>
    public static class AngleHelper
    {
        /// <summary>Переводит значение из выбранного режима в радианы.</summary>
        public static double ToRadians(double value, AngleMode mode)
        {
            switch (mode)
            {
                case AngleMode.Degrees: return value * Math.PI / 180.0;
                case AngleMode.Gradians: return value * Math.PI / 200.0;
                default: return value;
            }
        }

        /// <summary>Переводит радианы в выбранный режим.</summary>
        public static double FromRadians(double radians, AngleMode mode)
        {
            switch (mode)
            {
                case AngleMode.Degrees: return radians * 180.0 / Math.PI;
                case AngleMode.Gradians: return radians * 200.0 / Math.PI;
                default: return radians;
            }
        }
    }
}
