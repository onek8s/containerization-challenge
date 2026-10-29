using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Options;
using ShiftClock.Shared;

namespace ShiftClock.Service;

/// <summary>
/// Named pipe used by the supervisor console. Access is limited by the pipe ACL and every
/// request is checked against the caller's Windows group membership.
/// </summary>
public sealed class SupervisorPipeServer : BackgroundService
{
    private const int MaxInstances = 4;

    private readonly ShiftClockOptions _options;
    private readonly PunchStore _store;
    private readonly ReaderState _state;
    private readonly PayrollExporter _exporter;
    private readonly EventLogWriter _eventLog;
    private readonly ILogger<SupervisorPipeServer> _logger;
    private readonly WindowsIdentity _serviceIdentity = WindowsIdentity.GetCurrent();

    public SupervisorPipeServer(
        IOptions<ShiftClockOptions> options,
        PunchStore store,
        ReaderState state,
        PayrollExporter exporter,
        EventLogWriter eventLog,
        ILogger<SupervisorPipeServer> logger)
    {
        _options = options.Value;
        _store = store;
        _state = state;
        _exporter = exporter;
        _eventLog = eventLog;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var security = BuildPipeSecurity();
        var sessions = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var pipe = NamedPipeServerStreamAcl.Create(
                PipeProtocol.PipeName,
                PipeDirection.InOut,
                MaxInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                security);

            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync();
                break;
            }

            sessions.Add(ServeAsync(pipe, stoppingToken));
            sessions.RemoveAll(t => t.IsCompleted);
        }

        await Task.WhenAll(sessions);
    }

    private PipeSecurity BuildPipeSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));

        foreach (var group in new[] { _options.SupervisorGroup, _options.ViewerGroup })
        {
            try
            {
                var sid = (SecurityIdentifier)new NTAccount(group).Translate(typeof(SecurityIdentifier));
                security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            }
            catch (IdentityNotMappedException)
            {
                _logger.LogError("Group {Group} could not be resolved; its members cannot use the console", group);
            }
        }
        return security;
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        await using var _ = pipe;
        string caller = "unknown";
        try
        {
            WindowsIdentity? client = null;
            pipe.RunAsClient(() => client = WindowsIdentity.GetCurrent());
            using var clientIdentity = client!;
            caller = clientIdentity.Name;
            var isSupervisor = new WindowsPrincipal(clientIdentity).IsInRole(_options.SupervisorGroup);

            while (pipe.IsConnected && !ct.IsCancellationRequested)
            {
                var request = await PipeProtocol.ReadAsync<SupervisorRequest>(pipe, ct);
                if (request is null)
                    break;

                // Continuations of the pipe reads can resume on a thread that still carries the
                // client's token, so requests run explicitly under the service account.
                var response = WindowsIdentity.RunImpersonated(_serviceIdentity.AccessToken,
                    () => Handle(clientIdentity, request, caller, isSupervisor)) with
                {
                    CallerName = caller,
                    CallerIsSupervisor = isSupervisor
                };
                await PipeProtocol.WriteAsync(pipe, response, ct);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            // Console closed or service stopping.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supervisor session for {Caller} failed", caller);
        }
    }

    private SupervisorResponse Handle(WindowsIdentity client, SupervisorRequest request, string caller, bool isSupervisor)
    {
        switch (request.Command)
        {
            case SupervisorCommand.GetStatus:
                var today = DateOnly.FromDateTime(DateTime.Now);
                return new SupervisorResponse
                {
                    Ok = true,
                    Status = new ReaderStatus(
                        _options.Terminal,
                        _options.PortName,
                        _state.PortOpen,
                        _state.LastBadgeAt,
                        _state.LastBadgeName,
                        _store.Read(today, today).Count,
                        _state.RejectedFrames,
                        _state.StartedAt)
                };

            case SupervisorCommand.ListPunches:
                var (from, to) = Period(request);
                return new SupervisorResponse { Ok = true, Punches = _store.Read(from, to) };
        }

        if (!isSupervisor)
        {
            _eventLog.Write(EventLogWriter.AccessDenied,
                $"{caller} attempted {request.Command} without membership in {_options.SupervisorGroup}.",
                EventLogEntryType.FailureAudit);
            return SupervisorResponse.Fail($"Only members of {_options.SupervisorGroup} can do this.");
        }

        switch (request.Command)
        {
            case SupervisorCommand.AddCorrection when request.Correction is { } c:
                if (string.IsNullOrWhiteSpace(c.Reason))
                    return SupervisorResponse.Fail("A reason is required for corrections.");
                var punch = c with
                {
                    Id = Guid.NewGuid(),
                    Source = PunchSource.Correction,
                    Terminal = _options.Terminal,
                    CorrectedBy = caller
                };
                _store.Append(punch);
                _eventLog.Write(EventLogWriter.PunchCorrected,
                    $"{caller} added {punch.Direction} punch for {punch.EmployeeNumber} at {punch.Timestamp:g}: {punch.Reason}");
                return new SupervisorResponse { Ok = true };

            case SupervisorCommand.DeletePunch when request.PunchId is { } id:
                var (dFrom, dTo) = Period(request);
                var existing = _store.Find(id, dFrom, dTo);
                if (existing is null)
                    return SupervisorResponse.Fail("Punch not found.");
                _store.Delete(id, existing.Timestamp, caller, "Deleted from console");
                _eventLog.Write(EventLogWriter.PunchDeleted,
                    $"{caller} deleted punch {existing.Direction} {existing.Timestamp:g} of {existing.EmployeeNumber}.");
                return new SupervisorResponse { Ok = true };

            case SupervisorCommand.ExportPayroll:
                var (eFrom, eTo) = Period(request);
                var punches = _store.Read(eFrom, eTo);
                // The payroll share only accepts files written by supervisors, so the export
                // is written under the caller's own Windows account.
                string path;
                try
                {
                    path = WindowsIdentity.RunImpersonated(client.AccessToken,
                        () => _exporter.Export(punches, eFrom, eTo, caller));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(ex, "Payroll export by {Caller} to {Path} failed", caller, _options.PayrollExportPath);
                    return SupervisorResponse.Fail($"Payroll export to {_options.PayrollExportPath} failed: {ex.Message}");
                }
                _eventLog.Write(EventLogWriter.PayrollExported, $"{caller} exported payroll {eFrom:d}-{eTo:d} to {path}.");
                return new SupervisorResponse { Ok = true, ExportPath = path };

            default:
                return SupervisorResponse.Fail($"Unsupported request {request.Command}.");
        }
    }

    private static (DateOnly From, DateOnly To) Period(SupervisorRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return (request.From ?? today, request.To ?? request.From ?? today);
    }
}
