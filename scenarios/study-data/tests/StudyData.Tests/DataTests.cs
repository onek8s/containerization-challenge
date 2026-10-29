using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyData.Coding;
using StudyData.Core.Coding;
using StudyData.Core.Queries;
using StudyData.Core.Subjects;
using StudyData.Data;
using StudyData.Data.Services;

namespace StudyData.Tests;

public class DataTests
{
    [Fact]
    public async Task SampleDataIsDerivedAndQueried()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var ctx = scope.ServiceProvider.GetRequiredService<StudyDataContext>();
        Assert.Equal(3, await ctx.Subjects.CountAsync());
        Assert.Contains(await ctx.Values.Where(v => v.FieldCode == "VSBMI").Select(v => v.Value).ToListAsync(), v => v == "25.1");
        var auto = await ctx.Queries.Where(q => q.Origin == QueryOrigin.EditCheck).Select(q => q.EditCheckCode).ToListAsync();
        Assert.Contains("EX01", auto);
        Assert.Contains("MH02", auto);
        Assert.True(await ctx.Audit.CountAsync() > 100);
    }

    private static async Task<(FormInstance Form, Dictionary<string, string?> Values)> VitalsAsync(StudyDataContext ctx, string subject, string visit)
    {
        var form = await ctx.Forms.Include(f => f.Values).Include(f => f.Visit).ThenInclude(v => v!.Subject).Include(f => f.Visit).ThenInclude(v => v!.Definition).Include(f => f.Definition)
            .FirstAsync(f => f.Visit!.Subject!.Number == subject && f.Visit.Definition!.Code == visit && f.Definition!.Code == "VS");
        return (form, form.Values.Where(v => !v.FieldCode.StartsWith("VSB")).ToDictionary(v => v.FieldCode, v => v.Value));
    }

    [Fact]
    public async Task ChangingSavedDataNeedsReasonAndResetsSdv()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var sp = scope.ServiceProvider;
        var ctx = sp.GetRequiredService<StudyDataContext>();
        var (form, values) = await VitalsAsync(ctx, "101-001", "SCR");

        var verified = await sp.GetRequiredService<ReviewService>().VerifyAsync(form.Id, null, "CORP\\monitor1");
        Assert.True(verified > 0);

        values["VSWEIGHT"] = "70,0";
        var entry = sp.GetRequiredService<FormEntryService>();
        var refused = await entry.SaveAsync(new SaveFormRequest(form.Id, values, "liisa", null, null));
        Assert.False(refused.Saved);
        Assert.Contains(refused.Errors, e => e.Code == "SYS.REASON");

        var saved = await entry.SaveAsync(new SaveFormRequest(form.Id, values, "liisa", "Transcription error", null));
        Assert.True(saved.Saved);
        Assert.Equal("25.7", saved.Derived["VSBMI"]);

        var weight = await ctx.Values.AsNoTracking().FirstAsync(v => v.FormInstanceId == form.Id && v.FieldCode == "VSWEIGHT");
        Assert.Equal("70.0", weight.Value);
        Assert.False(weight.Verified);
        Assert.True(await ctx.Audit.AnyAsync(a => a.Action == "Changed" && a.OldValue == "68.2" && a.NewValue == "70.0" && a.Reason == "Transcription error"));
        Assert.True(await ctx.Audit.AnyAsync(a => a.Action == "SdvReset"));
    }

    [Fact]
    public async Task HardChecksRefuseTheSave()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var ctx = scope.ServiceProvider.GetRequiredService<StudyDataContext>();
        var (form, values) = await VitalsAsync(ctx, "201-001", "SCR");
        values["VSDIABP"] = "160";
        var result = await scope.ServiceProvider.GetRequiredService<FormEntryService>().SaveAsync(new SaveFormRequest(form.Id, values, "kadri", "Data clarified with source", null));
        Assert.False(result.Saved);
        Assert.Contains(result.Errors, e => e.Code == "VS01");
    }

    [Fact]
    public async Task CorrectedDataClosesTheAutomaticQuery()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var ctx = scope.ServiceProvider.GetRequiredService<StudyDataContext>();
        var ex = await ctx.Forms.Include(f => f.Values).Include(f => f.Visit).ThenInclude(v => v!.Subject).Include(f => f.Definition)
            .FirstAsync(f => f.Visit!.Subject!.Number == "102-001" && f.Definition!.Code == "EX");
        var values = ex.Values.Where(v => !v.FieldCode.EndsWith('P') && v.FieldCode != "EXCARBDV").ToDictionary(v => v.FieldCode, v => v.Value);
        values["EXCARBO"] = "340";
        var result = await scope.ServiceProvider.GetRequiredService<FormEntryService>().SaveAsync(new SaveFormRequest(ex.Id, values, "mikko", "Transcription error", null));
        Assert.True(result.Saved);
        var q = await ctx.Queries.AsNoTracking().SingleAsync(q => q.FormInstanceId == ex.Id && q.EditCheckCode == "EX01");
        Assert.Equal(QueryState.Closed, q.State);
    }

    [Fact]
    public async Task SignatureNeedsQueriesClosedAndIsVoidedByChanges()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var sp = scope.ServiceProvider;
        var ctx = sp.GetRequiredService<StudyDataContext>();
        var review = sp.GetRequiredService<ReviewService>();
        var subject = await ctx.Subjects.FirstAsync(s => s.Number == "101-001");

        await Assert.ThrowsAsync<SubjectRuleException>(() => review.SignCasebookAsync(subject.Id, "aino", "approve"));
        var queries = sp.GetRequiredService<QueryService>();
        foreach (var q in await ctx.Queries.Where(q => q.SubjectId == subject.Id && (q.State == QueryState.Open || q.State == QueryState.Answered)).ToListAsync())
        {
            await queries.CloseAsync(q.Id, "Accepted", "CORP\\monitor1");
        }

        var signed = await review.SignCasebookAsync(subject.Id, "aino", "approve");
        Assert.True(signed > 10);

        var (form, values) = await VitalsAsync(ctx, "101-001", "C1D1");
        Assert.Equal(FormStatus.Signed, form.Status);
        values["VSPULSE"] = "74";
        var result = await sp.GetRequiredService<FormEntryService>().SaveAsync(new SaveFormRequest(form.Id, values, "liisa", "Late data from source", null));
        Assert.True(result.Saved);
        var reloaded = await ctx.Forms.AsNoTracking().FirstAsync(f => f.Id == form.Id);
        Assert.Null(reloaded.SignedAt);
        Assert.Equal(FormStatus.Complete, reloaded.Status);
    }

    [Fact]
    public async Task RandomisationNeedsEligibility()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var sp = scope.ServiceProvider;
        var ctx = sp.GetRequiredService<StudyDataContext>();
        var subject = await ctx.Subjects.FirstAsync(s => s.Number == "201-001");
        var ex = await Assert.ThrowsAsync<SubjectRuleException>(() => sp.GetRequiredService<SubjectService>().RandomiseAsync(subject.Id, "BRCAWT", "kadri"));
        Assert.Contains("Eligibility", ex.Message);
    }

    [Fact]
    public async Task StaleFormIsAConflict()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var ctx = scope.ServiceProvider.GetRequiredService<StudyDataContext>();
        var (form, values) = await VitalsAsync(ctx, "201-001", "SCR");
        await Assert.ThrowsAsync<FormConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<FormEntryService>().SaveAsync(new SaveFormRequest(form.Id, values, "kadri", null, new byte[16])));
    }

    [Fact]
    public async Task AutocoderCodesExactTermsAndSynonyms()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var coding = scope.ServiceProvider.GetRequiredService<CodingService>();
        var (auto, review) = await coding.AutoCodeAsync();
        Assert.True(auto >= 4);
        var ctx = scope.ServiceProvider.GetRequiredService<StudyDataContext>();
        var items = await ctx.Coding.ToListAsync();
        Assert.Equal("Paracetamol", items.Single(i => i.Verbatim == "panadol").Term);
        Assert.Equal("Paraesthesia", items.Single(i => i.Verbatim == "tingling in fingers").Term);
        Assert.All(items.Where(i => i.State == CodingState.AutoCoded), i => Assert.NotNull(i.Hierarchy));
    }
}
