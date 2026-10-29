using System.Text.Json;
using Proseware.ControlTower.Booking.Models;

namespace Proseware.ControlTower.Booking.Services;

/// <summary>Keeps the booking wizard draft in the ASP.NET Core session.</summary>
public sealed class BookingDraftStore
{
    private const string Key = "booking-draft";
    private readonly IHttpContextAccessor _http;

    public BookingDraftStore(IHttpContextAccessor http) => _http = http;

    private ISession Session => _http.HttpContext?.Session ?? throw new InvalidOperationException("No session.");

    public BookingDraft Get()
    {
        var json = Session.GetString(Key);
        return json is null ? new BookingDraft() : JsonSerializer.Deserialize<BookingDraft>(json) ?? new BookingDraft();
    }

    public void Save(BookingDraft draft) => Session.SetString(Key, JsonSerializer.Serialize(draft));

    public void Clear() => Session.Remove(Key);
}
