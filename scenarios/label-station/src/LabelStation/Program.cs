namespace LabelStation;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var settings = StationSettings.Load();
        var journal = new PrintJournal();

        Application.Run(new MainForm(settings, journal));
    }
}
