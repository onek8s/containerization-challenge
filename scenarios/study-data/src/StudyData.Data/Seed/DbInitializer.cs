using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StudyData.Core.Subjects;
using StudyData.Data.Services;

namespace StudyData.Data.Seed;

public static class DbInitializer
{
    public const string DevelopmentPassword = "Fab201!site";

    /// <summary>Creates the database if needed. Production databases are created by the SQL scripts in deploy\sql.</summary>
    public static async Task EnsureCreatedAsync(IServiceProvider services, bool seedSampleData, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDataContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StudyData.Seed");
        await db.Database.EnsureCreatedAsync(ct);

        if (!seedSampleData || await db.Studies.AnyAsync(ct))
        {
            return;
        }

        log.LogInformation("Creating sample study {Study}", SampleStudy.Code);
        await CreateSampleStudyAsync(db, ct);
        await SeedUsersAsync(scope.ServiceProvider, db);
        await SeedSubjectsAsync(scope.ServiceProvider, db, ct);
    }

    /// <summary>Study design, sites and sponsor clients of FAB-ONC-201, without users or subjects.</summary>
    public static async Task CreateSampleStudyAsync(StudyDataContext db, CancellationToken ct = default)
    {
        if (await db.Studies.AnyAsync(s => s.Code == SampleStudy.Code, ct))
        {
            throw new InvalidOperationException($"Study {SampleStudy.Code} already exists.");
        }

        db.Studies.Add(SampleStudy.Create());
        db.SponsorClients.Add(new SponsorClient { ClientId = "tailspin-cdms", Name = "Tailspin Therapeutics CDMS", Studies = SampleStudy.Code });
        db.SponsorClients.Add(new SponsorClient { ClientId = "tailspin-dsmb", Name = "Tailspin DSMB statistician", Studies = SampleStudy.Code, MayUnblind = true });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Creates a site staff account delegated to one site. Returns the generated initial password.</summary>
    public static async Task<string> AddSiteUserAsync(IServiceProvider services, string email, string fullName, string studyCode, string siteNumber,
        string siteRole, bool canSign)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDataContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SiteUser>>();
        var site = await db.Sites.Include(s => s.Study).FirstOrDefaultAsync(s => s.Study!.Code == studyCode && s.Number == siteNumber)
            ?? throw new InvalidOperationException($"Site {siteNumber} of {studyCode} does not exist.");

        var user = await users.FindByEmailAsync(email);
        string password = "";
        if (user is null)
        {
            password = $"Fcr-{Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(9)).Replace('/', 'x').Replace('+', 'y')}!";
            user = new SiteUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, SiteRole = siteRole, TrainingCompletedAt = DateTime.UtcNow };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!await db.SiteAssignments.AnyAsync(a => a.UserId == user.Id && a.SiteId == site.Id))
        {
            db.SiteAssignments.Add(new SiteAssignment { UserId = user.Id, SiteId = site.Id, CanSign = canSign });
            await db.SaveChangesAsync();
        }

        return password;
    }

    private static async Task SeedUsersAsync(IServiceProvider sp, StudyDataContext db)
    {
        var users = sp.GetService<UserManager<SiteUser>>();
        if (users is null)
        {
            return;
        }

        var sites = await db.Sites.ToDictionaryAsync(s => s.Number);
        var accounts = new (string Email, string Name, string Role, string[] Sites, bool Sign)[]
        {
            ("aino.virtanen@hus.example", "Aino Virtanen", Roles.Investigator, new[] { "101" }, true),
            ("liisa.makela@hus.example", "Liisa Mäkelä", "Coordinator", new[] { "101" }, false),
            ("mikko.laine@tays.example", "Mikko Laine", Roles.Investigator, new[] { "102" }, true),
            ("kadri.tamm@regionaalhaigla.example", "Kadri Tamm", Roles.Investigator, new[] { "201" }, true),
            ("erik.holm@karolinska.example", "Erik Holm", "Coordinator", new[] { "301" }, false)
        };

        foreach (var a in accounts)
        {
            var user = new SiteUser
            {
                UserName = a.Email, Email = a.Email, EmailConfirmed = true, FullName = a.Name,
                SiteRole = a.Role, TrainingCompletedAt = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc)
            };
            var result = await users.CreateAsync(user, DevelopmentPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            foreach (var s in a.Sites)
            {
                db.SiteAssignments.Add(new SiteAssignment { UserId = user.Id, SiteId = sites[s].Id, CanSign = a.Sign });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSubjectsAsync(IServiceProvider sp, StudyDataContext db, CancellationToken ct)
    {
        var subjects = sp.GetRequiredService<SubjectService>();
        var forms = sp.GetRequiredService<FormEntryService>();
        var queries = sp.GetRequiredService<QueryService>();
        var sites = await db.Sites.ToDictionaryAsync(s => s.Number, ct);

        async Task Enter(Subject s, string visit, string form, int row, DateOnly? date, string user, params (string, string?)[] values)
        {
            var v = await db.Visits.Include(x => x.Definition).Include(x => x.Forms).ThenInclude(f => f.Definition)
                .FirstAsync(x => x.SubjectId == s.Id && x.Definition!.Code == visit, ct);
            if (date is not null && v.Date is null)
            {
                await subjects.SetVisitDateAsync(v.Id, date.Value, user, null, ct);
            }

            var instance = v.Forms.FirstOrDefault(f => f.Definition!.Code == form && f.Row == row)
                ?? await subjects.AddRowAsync(v.Id, form, user, ct);
            var result = await forms.SaveAsync(new SaveFormRequest(instance.Id, values.ToDictionary(x => x.Item1, x => x.Item2), user, null, null), ct);
            if (!result.Saved)
            {
                throw new InvalidOperationException($"Sample data for {s.Number} {visit} {form}: {string.Join("; ", result.Errors.Select(e => e.Message))}");
            }
        }

        // 101-001: randomised, two cycles given, nausea and neutropenia.
        const string c101 = "liisa.makela@hus.example";
        var s1 = await subjects.ScreenAsync(new ScreenRequest(sites["101"].Id, "AK", 1967, "F", new DateOnly(2026, 6, 2), c101), ct);
        var scr = new DateOnly(2026, 6, 4);
        await Enter(s1, "SCR", "IC", 1, scr, c101, ("ICDAT", "2026-06-02"), ("ICVER", "v4.0 FI"), ("ICPGX", "1"));
        await Enter(s1, "SCR", "DM", 1, null, c101, ("BRTHDAT", "1967-09-14"), ("SEX", "2"), ("RACE", "1"), ("ETHNIC", "2"), ("ECOG", "0"));
        await Enter(s1, "SCR", "MH", 1, null, c101, ("DIAGDAT", "2022-11-03"), ("FIGO", "III"), ("HIST", "1"), ("PRIORLN", "1"), ("PFI", "14"), ("BRCA", "2"));
        await Enter(s1, "SCR", "VS", 1, null, c101, ("VSDAT", "2026-06-04"), ("VSHEIGHT", "165"), ("VSWEIGHT", "68.2"), ("VSSYSBP", "128"), ("VSDIABP", "79"), ("VSPULSE", "72"), ("VSTEMP", "36.6"));
        await Enter(s1, "SCR", "LB", 1, null, c101, ("LBDAT", "2026-06-04"), ("LBCREAT", "62"), ("LBANC", "3.4"), ("LBPLAT", "262"), ("LBHGB", "128"), ("LBALT", "22"));
        await Enter(s1, "SCR", "RS", 1, null, c101, ("RSDAT", "2026-05-29"), ("RSMETH", "1"), ("RSTLSUM", "64"), ("RSNEWL", "0"), ("RSOVR", "NE"), ("CA125", "412"));
        await Enter(s1, "SCR", "IE", 1, null, "aino.virtanen@hus.example", ("INC01", "1"), ("INC02", "1"), ("INC03", "1"), ("INC04", "1"), ("EXC01", "0"), ("EXC02", "0"), ("IEYN", "1"));
        await subjects.RandomiseAsync(s1.Id, "BRCAWT", c101, ct);
        await Enter(s1, "C1D1", "VS", 1, new DateOnly(2026, 6, 16), c101, ("VSDAT", "2026-06-16"), ("VSWEIGHT", "67.9"), ("VSSYSBP", "124"), ("VSDIABP", "76"), ("VSPULSE", "70"), ("VSTEMP", "36.4"));
        await Enter(s1, "C1D1", "LB", 1, null, c101, ("LBDAT", "2026-06-16"), ("LBCREAT", "64"), ("LBANC", "3.1"), ("LBPLAT", "248"), ("LBHGB", "126"), ("LBALT", "25"));
        await Enter(s1, "C1D1", "EX", 1, null, c101, ("EXDAT", "2026-06-16"), ("EXAUC", "5"), ("EXCARBO", "575"), ("EXFABKG", "8"), ("EXFAB", "540"), ("EXADJ", "0"));
        await Enter(s1, "C1D8", "LB", 1, new DateOnly(2026, 6, 23), c101, ("LBDAT", "2026-06-23"), ("LBCREAT", "66"), ("LBANC", "1.2"), ("LBPLAT", "131"), ("LBHGB", "118"), ("LBALT", "31"));
        await Enter(s1, "C2D1", "VS", 1, new DateOnly(2026, 7, 7), c101, ("VSDAT", "2026-07-07"), ("VSWEIGHT", "66.5"), ("VSSYSBP", "131"), ("VSDIABP", "82"), ("VSPULSE", "77"), ("VSTEMP", "36.8"));
        await Enter(s1, "C2D1", "LB", 1, null, c101, ("LBDAT", "2026-07-07"), ("LBCREAT", "63"), ("LBANC", "1.9"), ("LBPLAT", "176"), ("LBHGB", "117"), ("LBALT", "28"));
        await Enter(s1, "C2D1", "EX", 1, null, c101, ("EXDAT", "2026-07-07"), ("EXAUC", "5"), ("EXCARBO", "585"), ("EXFABKG", "8"), ("EXFAB", "530"), ("EXADJ", "0"));
        await Enter(s1, "LOG", "AE", 1, null, c101, ("AETERM", "Nausea"), ("AESTDAT", "2026-06-17"), ("AEONGO", "0"), ("AEENDAT", "2026-06-20"), ("AETOXGR", "2"), ("AESER", "0"), ("AEREL", "1"), ("AERELC", "1"), ("AEACN", "1"), ("AEOUT", "1"));
        await Enter(s1, "LOG", "AE", 2, null, c101, ("AETERM", "neutrophil count decreased"), ("AESTDAT", "2026-06-23"), ("AEONGO", "0"), ("AEENDAT", "2026-07-03"), ("AETOXGR", "3"), ("AESER", "0"), ("AEREL", "1"), ("AERELC", "1"), ("AEACN", "1"), ("AEOUT", "1"));
        await Enter(s1, "LOG", "CM", 1, null, c101, ("CMTRT", "Ondansetron"), ("CMINDC", "Nausea prophylaxis"), ("CMDOSE", "8"), ("CMDOSU", "mg"), ("CMROUTE", "PO"), ("CMSTDAT", "2026-06-16"), ("CMONGO", "1"));
        await Enter(s1, "LOG", "CM", 2, null, c101, ("CMTRT", "panadol"), ("CMINDC", "Headache"), ("CMDOSE", "1"), ("CMDOSU", "g"), ("CMROUTE", "PO"), ("CMSTDAT", "2026-06-25"), ("CMONGO", "0"), ("CMENDAT", "2026-06-26"));

        // 102-001: randomised, carboplatin given above the Calvert dose (automatic query).
        const string c102 = "mikko.laine@tays.example";
        var s2 = await subjects.ScreenAsync(new ScreenRequest(sites["102"].Id, "MR", 1958, "F", new DateOnly(2026, 7, 1), c102), ct);
        await Enter(s2, "SCR", "IC", 1, new DateOnly(2026, 7, 3), c102, ("ICDAT", "2026-07-01"), ("ICVER", "v4.0 FI"), ("ICPGX", "0"));
        await Enter(s2, "SCR", "DM", 1, null, c102, ("BRTHDAT", "1958-02-27"), ("SEX", "2"), ("RACE", "1"), ("ETHNIC", "2"), ("ECOG", "1"));
        await Enter(s2, "SCR", "MH", 1, null, c102, ("DIAGDAT", "2021-04-12"), ("FIGO", "IV"), ("HIST", "1"), ("PRIORLN", "2"), ("PFI", "9"), ("BRCA", "1"));
        await Enter(s2, "SCR", "VS", 1, null, c102, ("VSDAT", "2026-07-03"), ("VSHEIGHT", "158"), ("VSWEIGHT", "52.4"), ("VSSYSBP", "142"), ("VSDIABP", "88"), ("VSPULSE", "81"), ("VSTEMP", "36.9"));
        await Enter(s2, "SCR", "LB", 1, null, c102, ("LBDAT", "2026-07-03"), ("LBCREAT", "88"), ("LBANC", "2.6"), ("LBPLAT", "201"), ("LBHGB", "112"), ("LBALT", "18"));
        await Enter(s2, "SCR", "RS", 1, null, c102, ("RSDAT", "2026-06-30"), ("RSMETH", "1"), ("RSTLSUM", "41"), ("RSNEWL", "0"), ("RSOVR", "NE"), ("CA125", "188"));
        await Enter(s2, "SCR", "IE", 1, null, c102, ("INC01", "1"), ("INC02", "1"), ("INC03", "1"), ("INC04", "1"), ("EXC01", "0"), ("EXC02", "0"), ("IEYN", "1"));
        await subjects.RandomiseAsync(s2.Id, "BRCAMUT", c102, ct);
        await Enter(s2, "C1D1", "VS", 1, new DateOnly(2026, 7, 15), c102, ("VSDAT", "2026-07-15"), ("VSWEIGHT", "52.0"), ("VSSYSBP", "138"), ("VSDIABP", "86"), ("VSPULSE", "79"), ("VSTEMP", "36.7"));
        await Enter(s2, "C1D1", "LB", 1, null, c102, ("LBDAT", "2026-07-15"), ("LBCREAT", "91"), ("LBANC", "2.4"), ("LBPLAT", "189"), ("LBHGB", "110"), ("LBALT", "20"));
        await Enter(s2, "C1D1", "EX", 1, null, c102, ("EXDAT", "2026-07-15"), ("EXAUC", "5"), ("EXCARBO", "450"), ("EXFABKG", "8"), ("EXFAB", "420"), ("EXADJ", "0"));
        await Enter(s2, "LOG", "AE", 1, null, c102, ("AETERM", "Fatigue"), ("AESTDAT", "2026-07-17"), ("AEONGO", "1"), ("AETOXGR", "1"), ("AESER", "0"), ("AEREL", "1"), ("AERELC", "0"), ("AEACN", "1"));
        await Enter(s2, "LOG", "AE", 2, null, c102, ("AETERM", "tingling in fingers"), ("AESTDAT", "2026-07-20"), ("AEONGO", "1"), ("AETOXGR", "1"), ("AESER", "0"), ("AEREL", "0"), ("AERELC", "1"), ("AEACN", "1"));

        // 201-001: in screening, platinum-free interval too short (automatic query, not yet answered).
        const string c201 = "kadri.tamm@regionaalhaigla.example";
        var s3 = await subjects.ScreenAsync(new ScreenRequest(sites["201"].Id, null, 1972, "F", new DateOnly(2026, 9, 21), c201), ct);
        await Enter(s3, "SCR", "IC", 1, new DateOnly(2026, 9, 22), c201, ("ICDAT", "2026-09-21"), ("ICVER", "v4.0 EE"), ("ICPGX", "1"));
        await Enter(s3, "SCR", "DM", 1, null, c201, ("BRTHDAT", "1972-05-08"), ("SEX", "2"), ("RACE", "1"), ("ETHNIC", "2"), ("ECOG", "1"));
        await Enter(s3, "SCR", "MH", 1, null, c201, ("DIAGDAT", "2024-01-15"), ("FIGO", "III"), ("HIST", "2"), ("PRIORLN", "1"), ("PFI", "5"), ("BRCA", "9"));
        await Enter(s3, "SCR", "VS", 1, null, c201, ("VSDAT", "2026-09-22"), ("VSHEIGHT", "171"), ("VSWEIGHT", "95"), ("VSSYSBP", "151"), ("VSDIABP", "93"), ("VSPULSE", "88"), ("VSTEMP", "36.5"));

        // A monitor query on 101-001 answered by the site.
        var vsForm = await db.Forms.Include(f => f.Visit).ThenInclude(v => v!.Definition).Include(f => f.Definition)
            .FirstAsync(f => f.Visit!.SubjectId == s1.Id && f.Visit.Definition!.Code == "C2D1" && f.Definition!.Code == "VS", ct);
        var q = await queries.OpenAsync(vsForm.Id, "VSWEIGHT", "Weight in the source notes is 66.0 kg. Please check.", Core.Queries.QueryOrigin.Monitor, "CORP\\monitor1", ct);
        await queries.AnswerAsync(q.Id, "Source re-checked: 66.5 kg measured on the ward scale, clinic note had a typo.", c101, ct);
    }
}
