using System;
using NovaCalc.Engine;
class D3 {
  static void Main(){
    var e=new CalculatorEngine();
    Console.WriteLine("50%+25% => "+e.Evaluate("50%+25%"));
    Console.WriteLine("10%+5% => "+e.Evaluate("10%+5%"));
    Console.WriteLine("sqrt(2) => "+e.Evaluate("sqrt(2)"));
    Console.WriteLine("1/3*3 => "+e.Evaluate("1/3*3"));
    Console.WriteLine("200+10% => "+e.Evaluate("200+10%"));
    Console.WriteLine("(200+10%) => "+e.Evaluate("(200+10%)"));
  }
}
