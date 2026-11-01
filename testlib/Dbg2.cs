using System;
using NovaCalc.Engine;
class Dbg2 {
  static void Main(){
    var e = new CalculatorEngine();
    Console.WriteLine("6*6 => " + e.Evaluate("6*6"));
    Console.WriteLine("Normalize(36) => " + CalculatorEngine.Normalize(36m));
    Console.WriteLine("Normalize(6*6) => " + CalculatorEngine.Normalize(e.Evaluate("6*6")));
    // имитация FormatNumber с MaxDigits=16 (как в настройках по умолчанию):
    decimal v = e.Evaluate("6*6");
    string fmt = "#,##0.###############################";
    Console.WriteLine("raw G29-ish: " + v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture));
    // теперь с Math.Round как в Strings.FormatNumber:
    decimal v2 = Math.Round(v, 16, MidpointRounding.AwayFromZero);
    Console.WriteLine("after Round16 scale=" + System.BitControl.GetType());
  }
}
