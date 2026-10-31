using System;

namespace NovaCalc.Engine
{
    /// <summary>Базовое исключение математического движка. Показывается пользователю аккуратно.</summary>
    public class CalcException : Exception
    {
        /// <summary>Ключ локализации сообщения (см. Localization/Strings.cs).</summary>
        public string MessageKey { get; private set; }

        public CalcException(string messageKey, string message) : base(message)
        {
            MessageKey = messageKey;
        }
    }

    /// <summary>Деление на ноль.</summary>
    public class DivideByZeroCalcException : CalcException
    {
        public DivideByZeroCalcException()
            : base("err_divzero", "Attempted to divide by zero.") { }
    }

    /// <summary>Недопустимая операция (sqrt от отрицательного, log от неположительного и т.п.).</summary>
    public class InvalidOperationCalcException : CalcException
    {
        public InvalidOperationCalcException(string key = "err_invalidop")
            : base(key, "Invalid operation.") { }
    }

    /// <summary>Переполнение или уход значения в бесконечность.</summary>
    public class OverflowCalcException : CalcException
    {
        public OverflowCalcException()
            : base("err_overflow", "Overflow.") { }
    }

    /// <summary>Синтаксическая ошибка выражения.</summary>
    public class ParseException : CalcException
    {
        public ParseException(string detail)
            : base("err_syntax", "Syntax error: " + detail) { }
    }
}
