namespace OPods;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Preferences.Load();
        Application.Run(new UI.MainForm());
    }
}
