using System;
using System.Globalization;

namespace NovaCalc.Engine
{
    /// <summary>
    /// Числовое значение движка. Точный трек — decimal (до 28-29 значащих цифр).
    /// Для transcendental-функций (тригонометрия, логарифмы) используется double,
    /// результат которого нормализуется обратно в decimal с корректным округлением,
    /// чтобы не показывать ложную точность.
    /// </summary>
    public struct Value
    {
        public decimal Dec;

        public Value(decimal d) { Dec = d; }

        public static Value FromDouble(double x)
        {
            if (double.IsNaN(x)) throw new InvalidOperationCalcException("err_invalidop");
            if (double.IsInfinity(x)) throw new OverflowCalcException();
            // Приводим к 15 значащим цифрам — реальная точность double.
            double rounded = Math.Round(x, 15, MidpointRounding.AwayFromZero);
            try
            {
                return new Value((decimal)rounded);
            }
            catch (OverflowException)
            {
                throw new OverflowCalcException();
            }
        }

        public double ToDouble()
        {
            double d;
            try { d = (double)Dec; }
            catch (OverflowException) { throw new OverflowCalcException(); }
            return d;
        }

        public bool IsZero { get { return Dec == 0m; } }
        public bool IsNegative { get { return Dec < 0m; } }

        public Value Negate()
        {
            if (Dec == decimal.MinValue) throw new OverflowCalcException();
            return new Value(-Dec);
        }

        /// <summary>Инвариантное строковое представление для хранения/парсинга.</summary>
        public string ToInvariantString()
        {
            return Dec.ToString(CultureInfo.InvariantCulture);
        }

        public override string ToString() { return ToInvariantString(); }
    }
}
