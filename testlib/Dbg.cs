using System;
using NovaCalc.Engine;
class Dbg {
  static void T(string expr){
    var e=new CalculatorEngine{AngleMode=AngleMode.Degrees};
    try{ Console.WriteLine(expr+" => "+e.Evaluate(expr)); }catch(Exception ex){ Console.WriteLine(expr+" => ERR "+ex.Message); }
  }
  static void Main(){
    T("200 + 10%"); T("200 - 10%"); T("200 * 10%"); T("200 / 10%"); T("10%");
    var e=new CalculatorEngine(); e.Evaluate("40+2"); Console.WriteLine("Ans="+e.Ans);
    T("ln(e)"); T("ln(2.718281828459045)");
    Console.WriteLine("gb->mib check: 1GB bytes="+(1000000000m)+" MiB="+(1000000000m/1048576m));
  }
}
