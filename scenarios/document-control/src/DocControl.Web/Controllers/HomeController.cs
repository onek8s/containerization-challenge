using System.Diagnostics;
using Litware.DocControl.Data;
using Litware.DocControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Litware.DocControl.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly DocumentRepository _documents;
    private readonly RevisionRepository _revisions;
    private readonly TransmittalRepository _transmittals;

    public HomeController(DocumentRepository documents, RevisionRepository revisions, TransmittalRepository transmittals)
    {
        _documents = documents;
        _revisions = revisions;
        _transmittals = transmittals;
    }

    public async Task<IActionResult> Index()
    {
        var recent = await _transmittals.ListAsync(null, 10);
        return View(new DashboardViewModel
        {
            Projects = await _documents.ProjectsAsync(),
            PendingReview = await _revisions.PendingReviewAsync(),
            RecentTransmittals = recent,
        });
    }

    [AllowAnonymous]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
