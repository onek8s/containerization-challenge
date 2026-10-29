using FurnaceRecords.Core;
using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Portal.Pages.Furnaces;

public class ChannelModel : PageModel
{
    private readonly FurnaceRecordsContext _db;

    public ChannelModel(FurnaceRecordsContext db) => _db = db;

    public sealed class CorrectionInput
    {
        public double? AtCelsius { get; set; }
        public double? CorrectionCelsius { get; set; }
    }

    public sealed class ChannelInput
    {
        public string Name { get; set; } = "";
        public ChannelRole Role { get; set; }
        public ThermocoupleType Type { get; set; }
        public string? SensorSerial { get; set; }
        public DateOnly? CalibratedOn { get; set; }
        public DateOnly? CalibrationDue { get; set; }
        public bool Enabled { get; set; }
        public List<CorrectionInput> Corrections { get; set; } = new();
    }

    [BindProperty] public ChannelInput Input { get; set; } = new();
    public string FurnaceCode { get; private set; } = "";
    public int ChannelNumber { get; private set; }
    public List<string> Errors { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var channel = await FindAsync(id, ct);
        if (channel is null) return NotFound();
        Input = new ChannelInput
        {
            Name = channel.Name,
            Role = channel.Role,
            Type = channel.Type,
            SensorSerial = channel.SensorSerial,
            CalibratedOn = channel.CalibratedOn,
            CalibrationDue = channel.CalibrationDue,
            Enabled = channel.Enabled,
            Corrections = channel.Corrections.OrderBy(c => c.AtCelsius)
                .Select(c => new CorrectionInput { AtCelsius = c.AtCelsius, CorrectionCelsius = c.CorrectionCelsius }).ToList(),
        };
        Pad();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        var channel = await FindAsync(id, ct);
        if (channel is null) return NotFound();

        var points = Input.Corrections
            .Where(c => c.AtCelsius is not null && c.CorrectionCelsius is not null)
            .Select(c => new CorrectionPoint(c.AtCelsius!.Value, c.CorrectionCelsius!.Value))
            .ToList();
        Errors = SensorCorrection.Validate(points).ToList();
        if (string.IsNullOrWhiteSpace(Input.Name)) Errors.Add("Name is required.");
        if (Input.CalibratedOn > Input.CalibrationDue) Errors.Add("Calibration due date is before the calibration date.");
        if (Errors.Count > 0)
        {
            Pad();
            return Page();
        }

        channel.Name = Input.Name.Trim();
        channel.Role = Input.Role;
        channel.Type = Input.Type;
        channel.SensorSerial = Input.SensorSerial?.Trim();
        channel.CalibratedOn = Input.CalibratedOn;
        channel.CalibrationDue = Input.CalibrationDue;
        channel.Enabled = Input.Enabled;
        _db.Corrections.RemoveRange(channel.Corrections);
        channel.Corrections = points.Select(p => new ChannelCorrection { AtCelsius = p.AtCelsius, CorrectionCelsius = p.CorrectionCelsius }).ToList();
        await _db.SaveChangesAsync(ct);
        return RedirectToPage("Index");
    }

    private async Task<FurnaceChannel?> FindAsync(int id, CancellationToken ct)
    {
        var channel = await _db.Channels.Include(c => c.Corrections).Include(c => c.Furnace).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (channel is not null)
        {
            FurnaceCode = channel.Furnace!.Code;
            ChannelNumber = channel.Channel;
        }
        return channel;
    }

    private void Pad()
    {
        while (Input.Corrections.Count < 5)
        {
            Input.Corrections.Add(new CorrectionInput());
        }
    }
}
