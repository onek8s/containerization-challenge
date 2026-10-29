using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Proseware.ControlTower.Identity.Pages;

public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;

    public IndexModel(IConfiguration configuration) => _configuration = configuration;

    public (string Booking, string Tower, string Portal) Links { get; private set; }

    public void OnGet()
    {
        Links = (_configuration["Links:Booking"] ?? "https://booking.corp.local/",
                 _configuration["Links:Tower"] ?? "https://tower.corp.local/app/",
                 _configuration["Links:Portal"] ?? "https://track.corp.local/");
    }
}
