using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Wingtip.Invoices.Admin.Services;
using Wingtip.Invoices.Core;

namespace Wingtip.Invoices.Admin.Pages.Suppliers;

public class EditModel : PageModel
{
    private readonly HubData data;
    private readonly CurrentUser user;

    public EditModel(HubData data, CurrentUser user, IConfiguration configuration)
    {
        this.data = data;
        this.user = user;
        FtpHost = configuration["Channels:FtpHost"] ?? "ftp.corp.local";
        DropRoot = configuration["Channels:DropRoot"] ?? @"\\files01.corp.local\InvoiceDrop$";
    }

    [BindProperty]
    public SupplierRow Supplier { get; set; }

    public IList<ChannelRow> Channels { get; private set; } = new List<ChannelRow>();
    public string FtpHost { get; }
    public string DropRoot { get; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            Supplier = new SupplierRow { CodePage = 1252, Active = true };
            return Page();
        }

        Supplier = await data.SupplierAsync(id.Value);
        if (Supplier == null) return NotFound();
        Channels = await data.ChannelsAsync(id.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Supplier.Code = Supplier.Code?.Trim().ToUpperInvariant();
        Supplier.VatNumber = VatNumber.Normalize(Supplier.VatNumber);
        if (string.IsNullOrEmpty(Supplier.Code) || string.IsNullOrEmpty(Supplier.VatNumber))
            ModelState.AddModelError(string.Empty, "Code and VAT number are required.");
        if (!ModelState.IsValid) return Page();

        try
        {
            var saved = await data.SaveSupplierAsync(Supplier, user.Account);
            TempData["Message"] = "Saved.";
            return RedirectToPage(new { id = saved });
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            ModelState.AddModelError(string.Empty, "A supplier with the code " + Supplier.Code + " already exists.");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostChannelAsync(int id, ChannelRow channel)
    {
        if (channel.ChannelType is not ("FTP" or "SHARE") || string.IsNullOrWhiteSpace(channel.Location))
        {
            TempData["Error"] = "Channel type and location are required.";
            return RedirectToPage(new { id });
        }
        channel.SupplierId = id;
        channel.Location = channel.Location.Trim();
        channel.FilePattern = string.IsNullOrWhiteSpace(channel.FilePattern) ? "*.*" : channel.FilePattern.Trim();
        await data.SaveChannelAsync(channel);
        TempData["Message"] = "Channel saved. The gateway picks up the change on its next poll.";
        return RedirectToPage(new { id });
    }
}
