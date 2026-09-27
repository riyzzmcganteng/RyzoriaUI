using System;
using System.Windows.Forms;
using RyzoriaUI.UI;

namespace RyzoriaUI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MessageBox.Show($"RyzoriaUI error:\n{e.Exception.Message}", "RyzoriaUI", MessageBoxButtons.OK, MessageBoxIcon.Error);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { };
        Application.Run(new MainForm());
    }
}
