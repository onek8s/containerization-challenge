using Lucerne.Lending.BackOffice.Infrastructure;
using Lucerne.Lending.BackOffice.Models;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.BackOffice.Controllers;

public sealed class HomeController : Controller
{
    private readonly ApplicationRepository _applications;
    private readonly CollectionRepository _collections;
    private readonly JobRunRepository _jobs;
    private readonly OutboxRepository _outbox;

    public HomeController(ApplicationRepository applications, CollectionRepository collections, JobRunRepository jobs, OutboxRepository outbox)
    {
        _applications = applications;
        _collections = collections;
        _jobs = jobs;
        _outbox = outbox;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new DashboardModel
        {
            ByStatus = await _applications.CountByStatusAsync(ct),
            Mine = await _applications.SearchAsync(null, User.UserName(), null, 0, 10, ct),
            OpenCollectionCases = (await _collections.ListAsync(null, null, ct)).Count,
            Jobs = await _jobs.RecentAsync(8),
            PendingDocuments = await _outbox.PendingCountAsync(Topics.DocumentRequested)
        };
        return View(model);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
