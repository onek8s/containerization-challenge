using System.Security.Claims;
using Proseware.ControlTower.Documents.Labels;
using Proseware.ControlTower.Scanning.Labels;
using Proseware.ControlTower.Scanning.Scans;

namespace Proseware.ControlTower.Scanning;

public static class ScanEndpoints
{
    public const int MaxBatchSize = 500;

    public static void MapScanEndpoints(this WebApplication app)
    {
        app.MapPost("/api/scans", async (ScanBatch batch, ScanService scans, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(batch.DeviceId) || batch.Scans is null || batch.Scans.Count == 0)
                return Results.BadRequest(new { error = "DeviceId and at least one scan are required." });
            if (batch.Scans.Count > MaxBatchSize)
                return Results.BadRequest(new { error = $"At most {MaxBatchSize} scans per request." });
            if (batch.Scans.Any(s => s.ClientScanId == Guid.Empty))
                return Results.BadRequest(new { error = "Every scan needs a ClientScanId." });
            var results = await scans.RecordAsync(batch, user.Identity?.Name ?? user.FindFirstValue("client_id") ?? "device", ct);
            return Results.Ok(results);
        })
        .RequireAuthorization("Scanner").WithName("PostScans");

        app.MapGet("/api/packages/{sscc}", async (string sscc, ScanService scans, CancellationToken ct) =>
            await scans.GetPackageAsync(sscc, ct) is { } info ? Results.Ok(info) : Results.NotFound())
        .RequireAuthorization("Scanner").WithName("GetPackage");

        app.MapGet("/api/terminals/{code}/expected", async (string code, ScanService scans, CancellationToken ct) =>
            await scans.GetExpectedAsync(code, ct) is { } list ? Results.Ok(list) : Results.NotFound())
        .RequireAuthorization("Scanner").WithName("GetExpected");

        app.MapGet("/api/printers", (ZplLabelPrinter printers) => Results.Ok(printers.PrinterNames.OrderBy(n => n)))
        .RequireAuthorization("Scanner").WithName("GetPrinters");

        app.MapPost("/api/labels/print", async (PrintRequest request, LabelService labels, ZplLabelPrinter printers, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!printers.PrinterNames.Contains(request.Printer, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = $"Unknown printer '{request.Printer}'." });
            var (printed, error) = await labels.PrintAsync(request.ShipmentNumber, request.Sscc, request.Printer, user.Identity?.Name ?? "device", ct);
            return error is null ? Results.Ok(new { printed }) : Results.NotFound(new { error });
        })
        .RequireAuthorization("Scanner").WithName("PrintLabels");
    }
}
