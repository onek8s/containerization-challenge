using PolicyDesk.Lab.Forms;

namespace PolicyDesk.Lab;

internal static class Program
{
    /// <summary>HPDESK.EXE [/env:PROD|TEST] [/nosplash]</summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var env = args.FirstOrDefault(a => a.StartsWith("/env:", StringComparison.OrdinalIgnoreCase))?[5..].ToUpperInvariant() ?? "PROD";
        using var signIn = new SignInForm(env);
        if (signIn.ShowDialog() != DialogResult.OK) return;

        Application.Run(new MainForm(signIn.UserId, signIn.SelectedEnvironment));
    }
}
