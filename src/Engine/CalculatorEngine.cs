using System;
using System.Collections.Generic;
using System.Globalization;

namespace NovaCalc.Engine
{
    /// <summary>
    /// Математический движок: токенизация, парсер рекурсивным спуском, вычисление.
    /// Базовая арифметика — на decimal (0.1 + 0.2 == 0.3 ровно).
    /// Поддержка: скобки, приоритеты, унарные +/-, функции, константы, Ans,
    /// процентные операторы с семантикой калькулятора Windows, факториал,
    /// целочисленные битовые операции.
    /// </summary>
    public class CalculatorEngine
    {
        public AngleMode AngleMode = AngleMode.Degrees;

        /// <summary>Последний результат (Ans).</summary>
        public decimal Ans = 0m;

        /// <summary>Ячейка памяти.</summary>
        public decimal Memory = 0m;

        // ===== Публичный API =====

        /// <summary>Вычисляет выражение целиком (инженерный режим).</summary>
        public decimal Evaluate(string expression)
        {
            // Сброс внутреннего состояния на входе — парсер должен быть чистым
            // при каждом вызове (иначе «залипшие» флаги портят следующие вычисления).
            _inParenContext = false;
            _hasPercentBase = false;
            _percentBaseOp = '\0';
            _percentBaseLeft = 0m;
            if (string.IsNullOrWhiteSpace(expression))
                throw new ParseException("empty expression");
            var tokens = Lexer.Tokenize(expression);
            int pos = 0;
            decimal result = Normalize(ParseExpression(tokens, ref pos));
            if (tokens[pos].Type != TokenType.End)
                throw new ParseException("unexpected token '" + tokens[pos].Text + "'");
            Ans = result; // переменная Ans обновляется после успешного вычисления
            return result;
        }

        /// <summary>
        /// Нормализация результата: убираем «хвостовые нули» scale'а decimal
        /// (20.0 -> 20, 0.30 -> 0.3), иначе сравнения и вывод выглядят неаккуратно.
        /// </summary>
        public static decimal Normalize(decimal v)
        {
            try { return decimal.Parse(v.ToString("0.##############################", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture); }
            catch (Exception) { return v; }
        }

        /// <summary>
        /// Обычный (цепочечный) режим: применяет операцию над текущим значением.
        /// pendingOp — накопленный оператор ('+', '-', '*', '/'), lhs — левый операнд.
        /// Возвращает новый lhs после применения предыдущей операции к right.
        /// Реализует семантику процентов Windows-калькулятора.
        /// </summary>
        public decimal ChainApply(char pendingOp, decimal lhs, decimal rhs, out bool usedPercentBase)
        {
            usedPercentBase = false;
            switch (pendingOp)
            {
                case '\0': return Normalize(rhs);
                case '+': return Normalize(SafeAdd(lhs, rhs));
                case '-': return Normalize(SafeSub(lhs, rhs));
                case '*': return Normalize(SafeMul(lhs, rhs));
                case '/': return Normalize(SafeDiv(lhs, rhs));
                default: throw new ParseException("unknown operator " + pendingOp);
            }
        }

        /// <summary>Безопасные операции с обработкой переполнения decimal.</summary>
        public static decimal SafeAdd(decimal a, decimal b)
        {
            try { return a + b; } catch (OverflowException) { throw new OverflowCalcException(); }
        }
        public static decimal SafeSub(decimal a, decimal b)
        {
            try { return a - b; } catch (OverflowException) { throw new OverflowCalcException(); }
        }
        public static decimal SafeMul(decimal a, decimal b)
        {
            try { return a * b; } catch (OverflowException) { throw new OverflowCalcException(); }
        }
        public static decimal SafeDiv(decimal a, decimal b)
        {
            if (b == 0m) throw new DivideByZeroCalcException();
            try { return a / b; } catch (OverflowException) { throw new OverflowCalcException(); }
        }

        /// <summary>
        /// Процент в контексте цепочки: base op percent%.
        /// 200 + 10% -> 200 + 20 = 220; 200 * 10% -> 200 * 0.1 = 20; 200 / 10% -> 200 / 0.1 = 2000.
        /// </summary>
        public static decimal PercentInContext(decimal baseValue, char op, decimal percentValue)
        {
            switch (op)
            {
                case '+':
                case '-':
                    return SafeDiv(SafeMul(baseValue, percentValue), 100m);
                case '*':
                case '/':
                    return SafeDiv(percentValue, 100m);
                default:
                    return SafeDiv(percentValue, 100m);
            }
        }

        /// <summary>
        /// Алиас: выражения с процентами вычисляются сразу через Evaluate
        /// (процентная семантика Windows встроена в основной парсер).
        /// </summary>
        public decimal EvaluatePercent(string expression)
        {
            return Evaluate(expression);
        }

        // ===== Парсер (рекурсивный спуск) =====
        //   expr      := toplevelterm (('+'|'-') term-with-pct)*
        //   toplevelterm / term: unary (('*'|'/') ...)*; на верхнем уровне * и /
        //                тоже применяют процентную семантику к правому операнду
        //   unary     := ('+'|'-') unary | postfix
        //   postfix   := power ('%' | '!')*          -- унарный % и факториал
        //   power     := primary ('^' unary)?        -- правоассоциативно
        //   primary   := number | const | func '(' args ')' | '(' expr ')'

        private decimal ParseExpression(List<Token> t, ref int pos)
        {
            bool top = !_inParenContext;
            if (top) _inParenContext = true;
            try
            {
                decimal left = top ? ParseTermTopLevel(t, ref pos) : ParseTerm(t, ref pos);
                bool savedBase = _hasPercentBase;
                char savedBaseOp = _percentBaseOp;
                decimal savedBaseVal = _percentBaseLeft;
                while (true)
                {
                    Token tk = t[pos];
                    if (tk.Type == TokenType.Operator && (tk.Text == "+" || tk.Text == "-"))
                    {
                        pos++;
                        _hasPercentBase = true; _percentBaseOp = tk.Text[0]; _percentBaseLeft = left;
                        decimal right = ParseTermWithPercent(t, ref pos, left, tk.Text[0]);
                        _hasPercentBase = savedBase; _percentBaseOp = savedBaseOp; _percentBaseLeft = savedBaseVal;
                        left = tk.Text == "+" ? SafeAdd(left, right) : SafeSub(left, right);
                    }
                    else break;
                }
                return left;
            }
            finally { if (top) _inParenContext = false; }
        }

        /// <summary>Флаг «мы внутри скобок/аргумента» — там проценты автономны.</summary>
        private bool _inParenContext;

        /// <summary>Уровень term без контекстного процента (используется внутри скобок/аргументов).</summary>
        private decimal ParseTerm(List<Token> t, ref int pos)
        {
            decimal left = ParseUnary(t, ref pos);
            while (true)
            {
                Token tk = t[pos];
                if (tk.Type == TokenType.Operator && (tk.Text == "*" || tk.Text == "/"))
                {
                    pos++;
                    decimal right = ParseUnary(t, ref pos);
                    if (tk.Text == "*") left = SafeMul(left, right);
                    else left = SafeDiv(left, right); // SafeDiv проверяет ноль
                }
                else break;
            }
            return left;
        }

        /// <summary>
        /// Уровень term для левого операнда '+'/'-': обрабатывает * и / с процентной
        /// семантикой Windows (200 * 10% = 20, 200 / 10% = 2000). Используется только
        /// на верхнем уровне выражения; внутри скобок — обычный ParseTerm.
        /// </summary>
        private decimal ParseTermTopLevel(List<Token> t, ref int pos)
        {
            decimal left = ParseUnary(t, ref pos);
            while (true)
            {
                Token tk = t[pos];
                if (tk.Type == TokenType.Operator && (tk.Text == "*" || tk.Text == "/"))
                {
                    pos++;
                    decimal right = ParseFactorWithPercent(t, ref pos, left, tk.Text[0]);
                    if (tk.Text == "*") left = SafeMul(left, right);
                    else left = SafeDiv(left, right);
                }
                else break;
            }
            return left;
        }

        /// <summary>
        /// Разбирает правую часть '+'/'-' и применяет Windows-семантику процента:
        /// если терм завершается постфиксным '%', он трактуется относительно левого
        /// значения (200 + 10% = 220, 200 - 10% = 180). Иначе — обычное значение.
        /// Внутри скобок процент автономный: (200 + 10%) = 200.1.
        /// </summary>
        private decimal ParseTermWithPercent(List<Token> t, ref int pos, decimal left, char op)
        {
            // сразу за оператором идёт '(' — скобочная группа, контекстного процента нет
            if (t[pos].Type == TokenType.LParen) return ParseTerm(t, ref pos);
            int start = pos;
            decimal v = _inParenContext ? ParseTerm(t, ref pos) : ParseTermTopLevel(t, ref pos);
            if (SegmentEndsWithPercent(t, start, pos))
                return PercentInContext(left, op, StripTrailingPctValue(t, start, pos));
            return v;
        }

        /// <summary>
        /// Разбирает правую часть '*'/'/' с той же процентной семантикой:
        /// 200 * 10% = 20, 200 / 10% = 2000. Процент связывается «плотнее» деления,
        /// поэтому сегмент для проверки хвостового '%' берётся до следующего * или /.
        /// </summary>
        private decimal ParseFactorWithPercent(List<Token> t, ref int pos, decimal left, char op)
        {
            if (t[pos].Type == TokenType.LParen) return ParseUnary(t, ref pos);
            int start = pos;
            decimal v = ParsePower(t, ref pos);
            // собираем постфиксный сегмент (% и !) вручную, чтобы отличить хвостовой %
            int p = pos;
            var ops = new System.Collections.Generic.List<string>();
            while (t[p].Type == TokenType.Operator && (t[p].Text == "%" || t[p].Text == "!"))
            { ops.Add(t[p].Text); p++; }
            bool trailingPct = ops.Count > 0 && ops[ops.Count - 1] == "%";
            if (!trailingPct)
            {
                // применить все постфиксы обычным порядком
                foreach (var o in ops)
                    v = o == "%" ? SafeDiv(v, 100m) : Factorial(v);
                pos = p;
                return v;
            }
            // есть хвостовой %: вычисляем все кроме последнего, затем контекстный процент
            foreach (var o in ops)
            {
                if (ReferenceEquals(o, ops[ops.Count - 1])) continue;
                v = o == "%" ? SafeDiv(v, 100m) : Factorial(v);
            }
            pos = p;
            return PercentInContext(left, op, v);
        }

        /// <summary>Ищет хвостовой постфиксный '%' в сегменте [from..to), учитывая скобки.</summary>
        private static bool SegmentEndsWithPercent(List<Token> t, int from, int to)
        {
            for (int i = to - 1; i >= from; i--)
            {
                if (t[i].Type == TokenType.RParen) return false;
                if (t[i].Type == TokenType.Operator && t[i].Text == "%") return true;
            }
            return false;
        }

        /// <summary>Индекс за цепочкой постфиксов %/!, начиная с i.</summary>
        private static int PostfixEnd(List<Token> t, int i)
        {
            int p = i;
            while (t[p].Type == TokenType.Operator && (t[p].Text == "%" || t[p].Text == "!")) p++;
            return p;
        }

        /// <summary>Значение сегмента [from..to) без одного хвостового % (10% -> 10).</summary>
        private decimal StripTrailingPctValue(List<Token> t, int from, int to)
        {
            var sub = new List<Token>();
            for (int i = from; i < to; i++) sub.Add(t[i]);
            // убираем один хвостовой %
            for (int i = sub.Count - 1; i >= 0; i--)
            {
                if (sub[i].Type == TokenType.RParen) break;
                if (sub[i].Type == TokenType.Operator && sub[i].Text == "%") { sub.RemoveAt(i); break; }
            }
            sub.Add(new Token(TokenType.End, ""));
            var saved = _percentBaseLeft; bool savedF = _hasPercentBase; char savedOp = _percentBaseOp;
            _hasPercentBase = false;
            try
            {
                int p2 = 0;
                return ParsePostfix(sub, ref p2);
            }
            finally { _percentBaseLeft = saved; _hasPercentBase = savedF; _percentBaseOp = savedOp; }
        }

        private decimal _percentBaseLeft;
        private char _percentBaseOp;
        private bool _hasPercentBase;

        private decimal ParseUnary(List<Token> t, ref int pos)
        {
            Token tk = t[pos];
            // «200 - 10%» без скобок: минус относится к числу, а процент — к контексту слева.
            // Проверяем цепочку унарных +/- за которыми идёт число/функция с хвостовым %.
            if ((tk.Type == TokenType.Operator && (tk.Text == "-" || tk.Text == "+")) && !_inParenContext)
            {
                int save = pos;
                int q = pos;
                while (t[q].Type == TokenType.Operator && (t[q].Text == "-" || t[q].Text == "+")) q++;
                if (q > pos && t[q].Type == TokenType.Number && SegmentEndsWithPercent(t, q, PostfixEnd(t, q)))
                {
                    pos = q;
                    ParseTerm(t, ref pos); // продвигаемся через постфиксы; значение пересчитаем ниже
                    int pe = PostfixEnd(t, q);
                    decimal raw = StripTrailingPctValue(t, q, pe);
                    decimal part = PercentInContext(_percentBaseLeft, _percentBaseOp, raw);
                    for (int k = save; k < q; k++)
                        if (t[k].Text == "-") part = -part;
                    return part;
                }
                pos = save;
            }
            if (tk.Type == TokenType.Operator && (tk.Text == "-" || tk.Text == "+"))
            {
                pos++;
                decimal v = ParseUnary(t, ref pos);
                return tk.Text == "-" ? NegateChecked(v) : v;
            }
            return ParsePostfix(t, ref pos);
        }

        private decimal ParsePostfix(List<Token> t, ref int pos)
        {
            decimal v = ParsePower(t, ref pos);
            bool any = false; // был ли хотя бы один постфикс — чтобы не нормализовать «2^10» ошибочно
            while (true)
            {
                Token tk = t[pos];
                if (tk.Type == TokenType.Operator && tk.Text == "%")
                {
                    // Автономный постфиксный % (без бинарного оператора слева):
                    // 10% = 0.1. В контексте "200 + 10%" процент перехватывается
                    // выше на уровне expression до захода сюда.
                    pos++;
                    v = SafeDiv(v, 100m);
                    any = true;
                }
                else if (tk.Type == TokenType.Operator && tk.Text == "!")
                {
                    pos++;
                    v = Factorial(v);
                    any = true;
                }
                else break;
            }
            return any ? Normalize(v) : v;
        }

        private decimal ParsePower(List<Token> t, ref int pos)
        {
            decimal baseV = ParsePrimary(t, ref pos);
            if (t[pos].Type == TokenType.Operator && t[pos].Text == "^")
            {
                pos++;
                decimal expV = ParseUnary(t, ref pos); // правоассоциативность, поддерживает -2^-3
                return Normalize(Pow(baseV, expV));
            }
            return baseV;
        }

        private decimal ParsePrimary(List<Token> t, ref int pos)
        {
            Token tk = t[pos];
            if (tk.Type == TokenType.Number)
            {
                pos++;
                return tk.Number;
            }
            if (tk.Type == TokenType.LParen)
            {
                pos++;
                decimal v = ParseExpression(t, ref pos);
                if (t[pos].Type != TokenType.RParen) throw new ParseException("missing ')'");
                pos++;
                return v;
            }
            if (tk.Type == TokenType.Identifier)
            {
                string name = tk.Text.ToLowerInvariant();
                pos++;
                // Константы
                switch (name)
                {
                    case "pi":
                        // разрешаем и «pi», и «pi(...)»
                        if (t[pos].Type != TokenType.LParen) return MathRound(Math.PI);
                        break;
                    case "e":
                        if (t[pos].Type != TokenType.LParen) return MathRound(Math.E);
                        break;
                    case "ans": return Ans;
                }
                // Функции: ожидаем аргументы в скобках
                if (t[pos].Type != TokenType.LParen)
                    throw new ParseException("expected '(' after function '" + tk.Text + "'");
                pos++;
                var args = new List<decimal>();
                if (t[pos].Type != TokenType.RParen)
                {
                    args.Add(ParseExpression(t, ref pos));
                    while (t[pos].Type == TokenType.Comma)
                    {
                        pos++;
                        args.Add(ParseExpression(t, ref pos));
                    }
                }
                if (t[pos].Type != TokenType.RParen) throw new ParseException("missing ')' in call '" + tk.Text + "'");
                pos++;
                // функции pi/e допускают пустые скобки: pi() == pi
                if (args.Count == 0 && (name == "pi" || name == "e")) return ApplyFunction(name, args);
                return ApplyFunction(name, args);
            }
            throw new ParseException("unexpected token '" + (tk.Type == TokenType.End ? "end of input" : tk.Text) + "'");
        }

        private decimal NegateChecked(decimal v)
        {
            if (v == decimal.MinValue) throw new OverflowCalcException();
            return -v;
        }

        // ===== Встроенные функции =====

        private decimal ApplyFunction(string name, List<decimal> args)
        {
            switch (name)
            {
                // нульарные константы-функции: pi(), e() — допустимы и как «pi(» из UI
                case "pi": return MathRound(Math.PI);
                case "e": return MathRound(Math.E);
                case "sqrt": return Sqrt(One(args));
                case "cbrt": return MathRound(Math.Pow(ToD(One(args)), 1.0 / 3.0));
                case "root": return Root(args[1], args[0]);           // root(x, n) — корень n-й степени из x
                case "pow": return Pow(args[0], args[1]);             // pow(x, y)
                case "pow10": return Pow(10m, One(args));
                case "pow2": return Pow(2m, One(args));
                case "abs": return Math.Abs(One(args));
                case "mod": return SafeModulo(args[0], args[1]);
                case "exp": return MathRound(Math.Exp(ToD(One(args))));
                case "ln": { var a0 = One(args); if (a0 == MathRound(Math.E)) return 1m; return Ln(a0); }
                case "log": return Log(One(args));                    // log10
                case "log2": return LogBase(One(args), 2m);
                case "logb": return LogBase(args[0], args[1]);        // logb(x, base)
                case "fact": return Factorial(One(args));
                case "inv": return SafeDiv(1m, One(args));
                case "percent": return SafeDiv(One(args), 100m);
                case "sin": return Trig(One(args), true, "sin");
                case "cos": return Trig(One(args), true, "cos");
                case "tan": return Trig(One(args), true, "tan");
                case "asin": return InvTrig(One(args), "asin");
                case "acos": return InvTrig(One(args), "acos");
                case "atan": return InvTrig(One(args), "atan");
                case "sinh": return MathRound(Math.Sinh(ToD(One(args))));
                case "cosh": return MathRound(Math.Cosh(ToD(One(args))));
                case "tanh": return MathRound(Math.Tanh(ToD(One(args))));
                case "round": return DecimalRound(One(args), args.Count > 1 ? (int)args[1] : 0);
                case "floor": return Math.Floor(One(args));
                case "ceil": return Math.Ceiling(One(args));
                case "sign": return Math.Sign(One(args));
                case "and": return BitwiseOp(args, "and");
                case "or": return BitwiseOp(args, "or");
                case "xor": return BitwiseOp(args, "xor");
                case "not": return BitwiseNot(One(args));
                case "shl": return BitwiseShift(args[0], args[1], true);
                case "shr": return BitwiseShift(args[0], args[1], false);
                case "min": return MinMax(args, true);
                case "max": return MinMax(args, false);
                case "avg":
                {
                    decimal sum = 0m;
                    foreach (var a in args) sum = SafeAdd(sum, a);
                    return SafeDiv(sum, args.Count);
                }
                default:
                    throw new ParseException("unknown function '" + name + "'");
            }
        }

        private static decimal One(List<decimal> args)
        {
            if (args.Count != 1) throw new ParseException("function expects exactly 1 argument");
            return args[0];
        }


        private decimal Sqrt(decimal x)
        {
            if (x < 0m) throw new InvalidOperationCalcException("err_invalidop");
            if (x == 0m) return 0m;
            double d = Math.Sqrt(ToD(x));
            decimal r = MathRound(d);
            // Для полных квадратов достаточно double-приближения
            if (SafeMul(r, r) == x || IsMulOverflow(r, r)) return r;
            // Иначе — Ньютон на decimal: повышаем точность до ~28 знаков
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    decimal next = (r + SafeDiv(x, r)) / 2m;
                    if (next == r) break;
                    r = next;
                }
                catch (OverflowCalcException) { break; }
            }
            return r;
        }

        /// <summary>Проверяет, приводит ли умножение к переполнению decimal.</summary>
        private static bool IsMulOverflow(decimal a, decimal b)
        {
            try
            {
                // checked-контекст через локальную функцию совместим и со старыми компиляторами C#
                Func<decimal> mul = delegate { checked { return a * b; } };
                mul();
                return false;
            }
            catch (OverflowException) { return true; }
        }

        private decimal Root(decimal n, decimal x)
        {
            if (n == 0m) throw new InvalidOperationCalcException("err_invalidop");
            if (x < 0m && n != Math.Truncate(n) && (Math.Abs(n) % 2m) != 1m)
                throw new InvalidOperationCalcException("err_invalidop");
            double d = Math.Pow(ToD(x), 1.0 / ToD(n));
            return MathRound(d);
        }

        private decimal Pow(decimal x, decimal y)
        {
            // Целые степени считаем точно на decimal
            if (y == Math.Truncate(y) && Math.Abs(y) <= 1000m)
            {
                int iy = (int)y;
                if (iy >= 0)
                {
                    decimal result = 1m;
                    decimal b = x;
                    int e = iy;
                    while (e > 0)
                    {
                        if ((e & 1) == 1) result = SafeMul(result, b);
                        b = SafeMul(b, b);
                        e >>= 1;
                    }
                    return result;
                }
                else
                {
                    if (x == 0m) throw new DivideByZeroCalcException();
                    return SafeDiv(1m, Pow(x, -y));
                }
            }
            if (x < 0m) throw new InvalidOperationCalcException("err_invalidop");
            double d = Math.Pow((double)x, (double)y);
            return MathRound(d);
        }

        private decimal Ln(decimal x)
        {
            if (x <= 0m) throw new InvalidOperationCalcException("err_logdomain");
            return MathRound(Math.Log(ToD(x)));
        }

        private decimal Log(decimal x)
        {
            if (x <= 0m) throw new InvalidOperationCalcException("err_logdomain");
            return MathRound(Math.Log10(ToD(x)));
        }

        private decimal LogBase(decimal x, decimal b)
        {
            if (x <= 0m || b <= 0m || b == 1m) throw new InvalidOperationCalcException("err_logdomain");
            return MathRound(Math.Log(ToD(x)) / Math.Log(ToD(b)));
        }

        private decimal Trig(decimal x, bool toRadians, string fn)
        {
            double arg = toRadians ? AngleHelper.ToRadians(ToD(x), AngleMode) : ToD(x);
            double d;
            switch (fn)
            {
                case "sin": d = Math.Sin(arg); break;
                case "cos": d = Math.Cos(arg); break;
                case "tan":
                    d = Math.Tan(arg);
                    if (double.IsNaN(d) || Math.Abs(d) > 1e300) throw new InvalidOperationCalcException("err_tanundef");
                    break;
                default: throw new ParseException(fn);
            }
            return MathRound(d);
        }

        private decimal InvTrig(decimal x, string fn)
        {
            double d = ToD(x);
            switch (fn)
            {
                case "asin":
                    if (d < -1.0 || d > 1.0) throw new InvalidOperationCalcException("err_asindomain");
                    return MathRound(AngleHelper.FromRadians(Math.Asin(d), AngleMode));
                case "acos":
                    if (d < -1.0 || d > 1.0) throw new InvalidOperationCalcException("err_acosdomain");
                    return MathRound(AngleHelper.FromRadians(Math.Acos(d), AngleMode));
                case "atan":
                    return MathRound(AngleHelper.FromRadians(Math.Atan(d), AngleMode));
                default: throw new ParseException(fn);
            }
        }

        private decimal Factorial(decimal x)
        {
            if (x < 0m || x != Math.Truncate(x)) throw new InvalidOperationCalcException("err_factdomain");
            if (x > 66m)
            {
                // gamma через double, переполнение ловим
                double d = GammaApprox((double)x + 1.0);
                return Value.FromDouble(d).Dec;
            }
            decimal result = 1m;
            for (decimal i = 2m; i <= x; i += 1m) result = SafeMul(result, i);
            return result;
        }

        /// <summary>Приближение факториала/гаммы по Ланцошу для больших аргументов.</summary>
        private static double GammaApprox(double z)
        {
            double[] g = { 676.5203681218851, -1259.1350143222895, 771.32342877765313,
                           -176.61502916214059, 12.507343278686905, -0.13857109526572012,
                           9.984669071530857e-6, 1.5051791169315516e-7 };
            if (z < 0.5)
                return Math.PI / (Math.Sin(Math.PI * z) * GammaApprox(1 - z));
            z -= 1;
            double x = 0.99999999999980993;
            for (int i = 0; i < g.Length; i++) x += g[i] / (z + i + 1);
            double t = z + g.Length - 0.5;
            double res = Math.Sqrt(2 * Math.PI) * Math.Pow(t, z + 0.5) * Math.Exp(-t) * x;
            if (double.IsInfinity(res) || double.IsNaN(res)) throw new OverflowCalcException();
            return res;
        }

        private decimal SafeModulo(decimal a, decimal b)
        {
            if (b == 0m) throw new DivideByZeroCalcException();
            try { return a % b; } catch (OverflowException) { throw new OverflowCalcException(); }
        }

        private decimal MinMax(List<decimal> args, bool min)
        {
            if (args.Count == 0) throw new ParseException("min/max require arguments");
            decimal best = args[0];
            foreach (var a in args)
                if (min ? a < best : a > best) best = a;
            return best;
        }

        // ===== Битовые операции (целочисленные, long) =====

        private static long ToLongExact(decimal v)
        {
            if (v != Math.Truncate(v)) throw new InvalidOperationCalcException("err_intrequired");
            try { return (long)v; }
            catch (OverflowException) { throw new OverflowCalcException(); }
        }

        private decimal BitwiseOp(List<decimal> args, string op)
        {
            if (args.Count < 2) throw new ParseException(op + " requires 2 arguments");
            long a = ToLongExact(args[0]), b = ToLongExact(args[1]);
            long r;
            switch (op)
            {
                case "and": r = a & b; break;
                case "or": r = a | b; break;
                case "xor": r = a ^ b; break;
                default: throw new ParseException(op);
            }
            return (decimal)r;
        }

        private decimal BitwiseNot(decimal v)
        {
            return (decimal)(~ToLongExact(v));
        }

        private decimal BitwiseShift(decimal v, decimal count, bool left)
        {
            long a = ToLongExact(v);
            int c = (int)ToLongExact(count);
            if (c < 0 || c > 63) throw new InvalidOperationCalcException("err_shiftcount");
            long r = left ? (a << c) : (a >> c);
            return (decimal)r;
        }

        // ===== Вспомогательное =====

        private static double ToD(decimal x)
        {
            try { return (double)x; }
            catch (OverflowException) { throw new OverflowCalcException(); }
        }

        /// <summary>Округление результата double до 15 значащих цифр и перевод в decimal.</summary>
        private static decimal MathRound(double x)
        {
            if (x == 0.0) return 0m;
            // «непоказываем ложную точность»: 15 значащих цифр, без хвостовых нулей scale'а
            try
            {
                string s = x.ToString("G15", CultureInfo.InvariantCulture);
                decimal d;
                if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            }
            catch (Exception) { }
            return Value.FromDouble(x).Dec;
        }

        private static decimal DecimalRound(decimal x, int digits)
        {
            try { return Math.Round(x, digits, MidpointRounding.AwayFromZero); }
            catch (ArgumentOutOfRangeException) { throw new ParseException("bad digits"); }
        }
    }
}
