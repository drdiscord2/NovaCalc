using System;
using NovaCalc.Services;
class TestEntry
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args != null && Array.IndexOf(args, "--selftest") >= 0)
            return SelfTest.Run();
        Console.WriteLine("NovaCalc test host: use --selftest");
        return 0;
    }
}
