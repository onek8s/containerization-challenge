using System.Diagnostics;
using Microsoft.Extensions.Options;
using PolicyChanges.Core.Flows;
using PolicyChanges.DeskRunner.Desktop;

namespace PolicyChanges.DeskRunner;

/// <summary>Starts PolicyDesk in the desk session, signs in and keeps it on the main window.</summary>
internal sealed class PolicyDeskClient : IDisposable
{
    private readonly PolicyDeskOptions _options;
    private readonly ILogger<PolicyDeskClient> _logger;
    private Process? _process;
    private int _changesSinceStart;

    public PolicyDeskClient(IOptions<PolicyDeskOptions> options, ILogger<PolicyDeskClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        Desktop = new Win32Desktop(() => (uint)(_process?.Id ?? 0));
    }

    public Win32Desktop Desktop { get; }

    public string? Version
    {
        get
        {
            try
            {
                return File.Exists(_options.ExecutablePath) ? FileVersionInfo.GetVersionInfo(_options.ExecutablePath).FileVersion : null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    /// <summary>Makes sure PolicyDesk runs, is signed in and shows only its main window.</summary>
    public void EnsureReady()
    {
        if (_process is { HasExited: false } && _changesSinceStart >= _options.RestartAfterChanges)
        {
            _logger.LogInformation("Restarting PolicyDesk after {Count} changes", _changesSinceStart);
            Stop();
        }

        if (_process is null || _process.HasExited)
        {
            _process = FindRunning() ?? Start();
            _changesSinceStart = 0;
        }

        var main = Desktop.FindWindow(_options.MainWindowTitle);
        if (main == IntPtr.Zero)
        {
            SignIn();
            main = Desktop.FindWindow(_options.MainWindowTitle);
        }
        if (main == IntPtr.Zero) throw new DesktopException("PolicyDesk main window is not open.");

        var closed = Desktop.CloseOthers(main);
        if (closed > 0) _logger.LogInformation("Closed {Count} leftover PolicyDesk windows", closed);
        if (Win32.TopLevelWindows((uint)_process.Id).Count > 1)
        {
            Stop();
            throw new DesktopException("PolicyDesk windows could not be closed; client restarted.");
        }
    }

    public void ChangeEntered() => _changesSinceStart++;

    public void Stop()
    {
        if (_process is null) return;
        try
        {
            if (!_process.HasExited)
            {
                _process.CloseMainWindow();
                if (!_process.WaitForExit(10_000)) _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        _process.Dispose();
        _process = null;
    }

    private Process? FindRunning()
    {
        var name = Path.GetFileNameWithoutExtension(_options.ExecutablePath);
        var session = InteractiveSession.CurrentSessionId;
        var running = Process.GetProcessesByName(name).FirstOrDefault(p => p.SessionId == session);
        if (running is not null) _logger.LogInformation("Using running PolicyDesk process {Pid}", running.Id);
        return running;
    }

    private Process Start()
    {
        if (!File.Exists(_options.ExecutablePath))
            throw new DesktopException($"PolicyDesk is not installed at {_options.ExecutablePath}.");

        var info = new ProcessStartInfo(_options.ExecutablePath, _options.Arguments.Replace("{Environment}", _options.Environment))
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(_options.ExecutablePath)!,
        };
        var process = Process.Start(info) ?? throw new DesktopException("PolicyDesk did not start.");
        _logger.LogInformation("Started PolicyDesk {Version} as process {Pid}", Version, process.Id);
        process.WaitForInputIdle(_options.StartupTimeoutSeconds * 1000);
        return process;
    }

    private void SignIn()
    {
        var timeout = TimeSpan.FromSeconds(_options.StartupTimeoutSeconds);
        var credential = CredentialStore.Read(_options.CredentialTarget)
            ?? throw new DesktopException($"No PolicyDesk sign-in stored as '{_options.CredentialTarget}'. Run 'PolicyChanges.DeskRunner store-credential'.");

        var signIn = Desktop.WaitForWindow(_options.SignInTitle, timeout);
        signIn.SetText("User ID", credential.UserName);
        signIn.SetText("Password", credential.Password);
        signIn.Select("Environment", _options.Environment);
        signIn.Click("Sign in");

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Desktop.FindWindow(_options.MainWindowTitle) != IntPtr.Zero)
            {
                _logger.LogInformation("Signed in to PolicyDesk {Environment} as {User}", _options.Environment, credential.UserName);
                return;
            }
            var dialog = Desktop.FindDialog();
            if (dialog != IntPtr.Zero)
            {
                var message = new Win32Dialog(dialog);
                var text = message.Text;
                message.Click("OK");
                throw new DesktopException($"PolicyDesk sign-in failed: {text}");
            }
            Thread.Sleep(250);
        }
        throw new DesktopException("PolicyDesk main window did not open after sign-in.");
    }

    public void Dispose() => _process?.Dispose();
}
