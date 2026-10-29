using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StudyData.Core.Services;
using StudyData.Data;
using StudyData.Data.Services;
using StudyData.Documents;

namespace StudyData.Monitor.Api.Services;

/// <summary>
/// Rendered casebook pages per subject, kept in memory until the subject's data changes. Rendering a
/// casebook takes a few seconds, and monitors page through it many times during a visit.
/// </summary>
public class CasebookPageCache
{
    private readonly IMemoryCache _cache;
    private readonly ICasebookRenderer _renderer;
    private readonly MonitorOptions _options;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _render = new(1, 1);

    public CasebookPageCache(IMemoryCache cache, ICasebookRenderer renderer, IOptions<MonitorOptions> options, IClock clock)
    {
        _cache = cache;
        _renderer = renderer;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<IReadOnlyList<RenderedPage>> GetAsync(StudyDataContext db, StudyReadService read, int subjectId, string user, CancellationToken ct)
    {
        var lastChange = await db.Audit.Where(a => a.SubjectId == subjectId).MaxAsync(a => (long?)a.Id, ct) ?? 0;
        var key = $"casebook:{subjectId}:{lastChange}";
        if (_cache.TryGetValue(key, out IReadOnlyList<RenderedPage>? pages))
        {
            return pages!;
        }

        await _render.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(key, out pages))
            {
                return pages!;
            }

            var subject = await read.GetCasebookAsync(subjectId, ct) ?? throw new KeyNotFoundException();
            var study = (await read.GetDesignAsync(subject.StudyId, ct))!;
            var audit = await db.Audit.Where(a => a.SubjectId == subjectId).OrderBy(a => a.Id).ToListAsync(ct);
            var queries = await db.Queries.Where(q => q.SubjectId == subjectId).ToListAsync(ct);
            var model = CasebookModel.Build(study, subject, audit, queries, user, _clock.UtcNow);
            pages = await _renderer.RenderPagesAsync(model, _options.PageImageDpi, ct);
            _cache.Set(key, pages, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(20), Size = null });
            return pages;
        }
        finally
        {
            _render.Release();
        }
    }
}
