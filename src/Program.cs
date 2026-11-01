using System;
using System.Windows.Forms;

namespace NovaCalc
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Скрытый режим самотестирования: NovaCalc.exe --selftest
            if (args != null && Array.IndexOf(args, "--selftest") >= 0)
            {
                try
                {
                    return Services.SelfTest.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Selftest crashed: " + ex.Message);
                    return 2;
                }
            }

            // Глобальная защита от необработанных исключений — приложение не падает молча.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                try
                {
                    MessageBox.Show(
                        Localization.Strings.T("err_invalidop") + "\n" + e.Exception.Message,
                        "NovaCalc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception) { }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                // Финальный лог в файл рядом с данными; падения при обычном использовании не должно быть.
                try
                {
                    var ex = e.ExceptionObject as Exception;
                    if (ex != null)
                        System.IO.File.AppendAllText(
                            System.IO.Path.Combine(Services.StorageService.DataDir, "crash.log"),
                            DateTime.Now.ToString("s") + " " + ex + Environment.NewLine);
                }
                catch (Exception) { }
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UI.MainForm());
            return 0;
        }
    }
}
