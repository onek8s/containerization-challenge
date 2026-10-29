using Microsoft.Extensions.Configuration;
using PolicyChanges.DeskRunner.Desktop;

namespace PolicyChanges.DeskRunner;

internal static class Commands
{
    public static int Run(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var desk = config.GetSection(PolicyDeskOptions.Section).Get<PolicyDeskOptions>() ?? new PolicyDeskOptions();

        return args[0] switch
        {
            "store-credential" => StoreCredential(args.Length > 1 ? args[1] : desk.CredentialTarget),
            "check" => Check(config, desk),
            _ => 2,
        };
    }

    /// <summary>Run as the desk user: the credential is stored in that user's Credential Manager.</summary>
    private static int StoreCredential(string target)
    {
        Console.Write($"PolicyDesk user ID for {target}: ");
        var user = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Password: ");
        var password = ReadHidden();
        if (user.Length == 0 || password.Length == 0)
        {
            Console.Error.WriteLine("User ID and password are required.");
            return 1;
        }
        CredentialStore.Write(target, user, password);
        Console.WriteLine($"Stored for {Environment.UserDomainName}\\{Environment.UserName} as '{target}'.");
        return 0;
    }

    private static int Check(IConfiguration config, PolicyDeskOptions desk)
    {
        var ok = true;
        void Line(bool good, string text)
        {
            ok &= good;
            Console.WriteLine($"[{(good ? " OK " : "FAIL")}] {text}");
        }

        var session = InteractiveSession.Check();
        Line(session.Usable, $"Session {session.SessionId}: {session.Reason} (desktop {session.InputDesktop ?? "-"}, locked {session.Locked?.ToString() ?? "unknown"})");
        Line(File.Exists(desk.ExecutablePath), $"PolicyDesk client {desk.ExecutablePath}");

        var credential = CredentialStore.Read(desk.CredentialTarget);
        Line(credential is not null, $"Credential '{desk.CredentialTarget}'{(credential is null ? " missing" : " for " + credential.UserName)}");

        var flowsPath = Path.Combine(AppContext.BaseDirectory, config[$"{DeskOptions.Section}:FlowsPath"] ?? "flows");
        try
        {
            var flows = new FlowLibrary(flowsPath);
            Line(flows.All.Count > 0, $"{flows.All.Count} flows in {flowsPath}: {string.Join(", ", flows.All.Select(f => f.ChangeType))}");
        }
        catch (Exception ex) when (ex is IOException or FormatException or System.Text.Json.JsonException)
        {
            Line(false, $"Flows in {flowsPath}: {ex.Message}");
        }

        var evidence = Environment.ExpandEnvironmentVariables(config[$"{DeskOptions.Section}:EvidenceRoot"] ?? "");
        Line(Directory.Exists(evidence), $"Evidence share {evidence}");

        return ok ? 0 : 1;
    }

    private static string ReadHidden()
    {
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0) chars.RemoveAt(chars.Count - 1);
                continue;
            }
            chars.Add(key.KeyChar);
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }
}
