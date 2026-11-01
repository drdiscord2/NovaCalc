using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NovaCalc.Engine
{
    /// <summary>Типы токенов выражения.</summary>
    public enum TokenType
    {
        Number,          // числовой литерал (decimal)
        Identifier,      // имя функции или константы
        Operator,        // бинарный оператор: + - * / ^ % ! и т.д.
        Prefix,          // унарный префиксный оператор: - +
        LParen,
        RParen,
        Comma,
        End
    }

    /// <summary>Токен лексера.</summary>
    public struct Token
    {
        public TokenType Type;
        public string Text;
        public decimal Number;

        public Token(TokenType type, string text)
        {
            Type = type; Text = text; Number = 0m;
        }

        public Token(decimal number)
        {
            Type = TokenType.Number; Text = number.ToString(CultureInfo.InvariantCulture); Number = number;
        }
    }

    /// <summary>
    /// Лексер математических выражений. Принимает строку с точкой или запятой как
    /// десятичным разделителем, поддерживает экспоненциальную запись (1.5e3),
    /// HEX/BIN/OCT литералы (0x.., 0b.., 0o..) и суффиксы.
    /// </summary>
    public static class Lexer
    {
        public static List<Token> Tokenize(string input)
        {
            var tokens = new List<Token>();
            int i = 0;
            int n = input.Length;

            while (i < n)
            {
                char c = input[i];

                if (c == ' ' || c == '\t' || c == '\u00A0') { i++; continue; }

                // Число: только если начинается с цифры или точки, за которой идёт цифра.
                // Запятая здесь — разделитель аргументов функций, а не начало числа.
                if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(input[i + 1])))
                {
                    int start = i;
                    bool dotSeen = false;
                    // спец-префиксы систем счисления
                    if (c == '0' && i + 1 < n && (input[i + 1] == 'x' || input[i + 1] == 'X'))
                    {
                        i += 2;
                        var hex = new StringBuilder();
                        while (i < n && IsHexDigit(input[i])) { hex.Append(input[i]); i++; }
                        if (hex.Length == 0) throw new ParseException("bad hex literal");
                        long hv = long.Parse(hex.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        tokens.Add(new Token((decimal)hv));
                        continue;
                    }
                    if (c == '0' && i + 1 < n && (input[i + 1] == 'b' || input[i + 1] == 'B'))
                    {
                        i += 2;
                        var bin = new StringBuilder();
                        while (i < n && (input[i] == '0' || input[i] == '1')) { bin.Append(input[i]); i++; }
                        if (bin.Length == 0) throw new ParseException("bad binary literal");
                        long bv = Convert.ToInt64(bin.ToString(), 2);
                        tokens.Add(new Token((decimal)bv));
                        continue;
                    }
                    if (c == '0' && i + 1 < n && (input[i + 1] == 'o' || input[i + 1] == 'O'))
                    {
                        i += 2;
                        var oct = new StringBuilder();
                        while (i < n && input[i] >= '0' && input[i] <= '7') { oct.Append(input[i]); i++; }
                        if (oct.Length == 0) throw new ParseException("bad octal literal");
                        long ov = Convert.ToInt64(oct.ToString(), 8);
                        tokens.Add(new Token((decimal)ov));
                        continue;
                    }

                    while (i < n)
                    {
                        char ch = input[i];
                        if (char.IsDigit(ch)) { i++; }
                        else if (ch == '.' && !dotSeen)
                        {
                            // десятичная точка; запятая внутри числа не допускается —
                            // она является разделителем аргументов функций (см. парсер)
                            dotSeen = true; i++;
                        }
                        else if ((ch == 'e' || ch == 'E') && i > start && char.IsLetterOrDigit(input[i - 1]) &&
                                 i + 1 < n &&
                                 (char.IsDigit(input[i + 1]) || ((input[i + 1] == '+' || input[i + 1] == '-') && i + 2 < n && char.IsDigit(input[i + 2]))))
                        {
                            i++; // e
                            if (input[i] == '+' || input[i] == '-') i++;
                            while (i < n && char.IsDigit(input[i])) i++;
                        }
                        else break;
                    }
                    string numText = input.Substring(start, i - start);
                    if (numText.EndsWith(".")) numText = numText.Substring(0, numText.Length - 1);
                    decimal value;
                    if (!decimal.TryParse(numText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign,
                                          CultureInfo.InvariantCulture, out value))
                        throw new ParseException("bad number '" + numText + "'");
                    tokens.Add(new Token(value));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < n && (char.IsLetterOrDigit(input[i]) || input[i] == '_')) i++;
                    tokens.Add(new Token(TokenType.Identifier, input.Substring(start, i - start)));
                    continue;
                }

                switch (c)
                {
                    case '+': case '-': case '*': case '/': case '^':
                    case '!': case '%':
                        tokens.Add(new Token(TokenType.Operator, c.ToString())); i++; break;
                    case '(': case '[':
                        tokens.Add(new Token(TokenType.LParen, "(")); i++; break;
                    case ')': case ']':
                        tokens.Add(new Token(TokenType.RParen, ")")); i++; break;
                    case ',': case ';':
                        tokens.Add(new Token(TokenType.Comma, ",")); i++; break;
                    default:
                        throw new ParseException("unexpected character '" + c + "'");
                }
            }
            tokens.Add(new Token(TokenType.End, ""));
            return tokens;
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }
    }
}
