using System;
using NovaCalc.Engine;
class TestEntry
{
    static int fails = 0;
    static void Check(string name, object got, object want)
    {
        bool ok = Convert.ToString(got) == Convert.ToString(want);
        if (!ok) { fails++; Console.WriteLine("FAIL " + name + ": got [" + got + "] want [" + want + "]"); }
    }
    [STAThread]
    static int Main(string[] args)
    {
        var e = new CalculatorEngine();
        Check("6+6", e.Evaluate("6+6"), "12");
        Check("0.1+0.2", e.Evaluate("0.1+0.2"), "0.3");
        Check("2+2*2", e.Evaluate("2+2*2"), "6");
        Check("(2+2)*2", e.Evaluate("(2+2)*2"), "8");
        Check("-5+3", e.Evaluate("-5+3"), "-2");
        Check("10%", e.Evaluate("10%"), "0.1");
        Check("200+10%", e.Evaluate("200+10%"), "220");
        Check("200-10%", e.Evaluate("200-10%"), "180");
        Check("200*10%", e.Evaluate("200*10%"), "20");
        Check("200/10%", e.Evaluate("200/10%"), "2000");
        Check("50%+25%", e.Evaluate("50%+25%"), "0.75");
        try { e.Evaluate("5/0"); Check("div0", "no", "throw"); } catch (DivideByZeroCalcException) { }
        Check("sqrt(9)", e.Evaluate("sqrt(9)"), "3");
        Check("sqrt(2)", e.Evaluate("sqrt(2)"), "1.414213562373095");
        Check("2^10", e.Evaluate("2^10"), "1024");
        Check("root(27,3)", e.Evaluate("root(27,3)"), "3");
        Check("mod(10,3)", e.Evaluate("mod(10,3)"), "1");
        Check("and(12,10)", e.Evaluate("and(12,10)"), "8");
        Check("pi()", e.Evaluate("pi()"), "3.14159265358979");
        Check("pi", e.Evaluate("pi"), "3.14159265358979");
        Check("e", e.Evaluate("e"), "2.71828182845905");
        Check("sin(30)", e.Evaluate("sin(30)"), "0.5");
        Check("cos(60)", e.Evaluate("cos(60)"), "0.5");
        Check("ln(e)", e.Evaluate("ln(e)"), "1");
        Check("log(100)", e.Evaluate("log(100)"), "2");
        Check("fact(5)", e.Evaluate("fact(5)"), "120");
        Check("5!", e.Evaluate("5!"), "120");
        Check("abs(-7)", e.Evaluate("abs(-7)"), "7");
        Check("0x1F", e.Evaluate("0x1F"), "31");
        Check("0b1010", e.Evaluate("0b1010"), "10");
        Check("1/3*3", e.Evaluate("1/3*3"), "1");
        Check("pow10(3)", e.Evaluate("pow10(3)"), "1000");
        Check("2^(3^2)", e.Evaluate("2^(3^2)"), "512");
        e.Evaluate("5*5");
        Check("Ans", e.Evaluate("Ans+1"), "26");
        // ошибки не должны ронать
        foreach (var bad in new[]{"", "+", "((1", "1+", "foo(2)", "sqrt(-4)", "log(0)", "asin(2)", "1,,2"})
            try { e.Evaluate(bad); Check("bad:"+bad, "no throw", "throw"); } catch (CalcException) { } catch (Exception ex) { Check("bad:"+bad+" type", "CalcException", ex.GetType().Name); }
        // форматирование
        Check("fmt 12", NovaCalc.Localization.Strings.FormatNumber(12m, 16, true, true), "12");
        Check("fmt 20.0", NovaCalc.Localization.Strings.FormatNumber(20.0m, 16, true, true), "20");
        Check("fmt 0.3", NovaCalc.Localization.Strings.FormatNumber(0.3m, 16, true, true), "0.3");
        Check("fmt big", NovaCalc.Localization.Strings.FormatNumber(1234567.891m, 16, true, true), "1,234,567.891");
        string sci = NovaCalc.Localization.Strings.FormatNumber(1.23e21m, 16, true, true);
        Check("fmt sci", sci.Contains("E+21") && !sci.Contains("E+021"), true);
        Console.WriteLine(fails == 0 ? "ALL ENGINE TESTS PASSED" : fails + " FAILURES");
        return fails == 0 ? 0 : 1;
    }
}
