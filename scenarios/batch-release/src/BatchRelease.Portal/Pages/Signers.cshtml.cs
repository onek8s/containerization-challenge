using BatchRelease.Contracts;
using BatchRelease.Core.Model;
using BatchRelease.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BatchRelease.Portal.Pages;

public class SignersModel : PageModel
{
    private readonly BatchQueries _queries;
    private readonly ISigningHostClient _signing;

    public SignersModel(BatchQueries queries, ISigningHostClient signing)
    {
        _queries = queries;
        _signing = signing;
    }

    public List<Signer> Signers { get; private set; } = new();
    public SigningHostStatus? Status { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Signers = await _queries.SignersAsync(ct);
        try
        {
            Status = await _signing.GetStatusAsync(ct);
        }
        catch (Exception ex) when (ex is SigningHostException or HttpRequestException or TaskCanceledException)
        {
            Status = null;
        }
    }
}
