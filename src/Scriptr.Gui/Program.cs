namespace Scriptr.Gui;

internal static class Program
{
    // Application.SetHighDpiMode owns the SetProcessDpiAwarenessContext call for WinForms;
    // calling Platform.InitDpiAwareness() in addition would silently fail on the second call.
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(new AppState(), LoadAppIcon()));
    }

    private static Icon LoadAppIcon()
    {
        using var stream = typeof(Program).Assembly
            .GetManifestResourceStream("Scriptr.Assets.scriptr_icon.ico");
        if (stream is not null)
            return new Icon(stream, 32, 32);
        return SystemIcons.Application;
    }
}
