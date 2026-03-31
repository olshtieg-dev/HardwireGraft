using PhysicalKBLayoutSwitcher.App.Services;

namespace PhysicalKBLayoutSwitcher.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => AppLog.Error("Unhandled UI exception.", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled non-UI exception.", args.ExceptionObject as Exception);

        AppLog.Info("Application starting.");
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        AppLog.Info("Application exited.");
    }
}
