namespace OPods;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // 跟随 Windows 系统颜色模式（浅色 / 深色自动切换，.NET 10 原生支持）。
        Application.SetColorMode(SystemColorMode.System);
        Preferences.Load();
        Application.Run(new UI.MainForm());
    }
}
