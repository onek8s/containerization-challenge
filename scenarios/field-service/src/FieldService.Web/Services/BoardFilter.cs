using System.Text.Json;
using FieldService.Data.Entities;

namespace FieldService.Web.Services;

/// <summary>
/// The dispatcher's current work order board filter, remembered for the session.
/// </summary>
public class BoardFilter
{
    private const string SessionKey = "BoardFilter";

    public WorkOrderStatus? Status { get; set; }

    public int? TechnicianId { get; set; }

    public string? Customer { get; set; }

    public bool OpenOnly { get; set; } = true;

    public bool OverdueOnly { get; set; }

    public static BoardFilter Load(ISession session)
    {
        var json = session.GetString(SessionKey);
        return json == null ? new BoardFilter() : JsonSerializer.Deserialize<BoardFilter>(json) ?? new BoardFilter();
    }

    public void Save(ISession session) => session.SetString(SessionKey, JsonSerializer.Serialize(this));
}
