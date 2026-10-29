using System.Data;
using Lucerne.Lending.Domain;

namespace Lucerne.Lending.Data;

public sealed class ApplicationRepository
{
    private const string ApplicantColumns = @"ApplicationId, IsMain, PersonalIdentityCode, FirstName, LastName, Email, Phone, StreetAddress, PostalCode, City,
        Citizenship, ResidentInFinland, MaritalStatus, Housing, Adults, Children, Employment, Employer, EmployedSince, OnProbation,
        GrossMonthlyIncome, NetMonthlyIncome, HousingCostMonthly, OtherLoanPaymentsMonthly, OtherLoanBalance, PoliticallyExposed, CoreCustomerNumber";

    private readonly Db _db;

    public ApplicationRepository(Db db) => _db = db;

    public async Task<ApplicationRow> CreateAsync(LoanApplication app, string actor, CancellationToken ct = default)
    {
        return await _db.InTransactionAsync(async tx =>
        {
            var now = DateTime.UtcNow;
            var seq = await _db.NextSequenceAsync("application." + now.Year, tx);
            app.Reference = ReferenceGenerator.Application(now.Year, seq);
            app.CreatedUtc = app.UpdatedUtc = now;
            app.SubmittedUtc = app.Status == ApplicationStatus.Submitted ? now : null;
            var id = await _db.ScalarAsync<long>(@"
                INSERT INTO lending.Application(Reference, ProductCode, Purpose, Amount, TermMonths, DueDay, Channel, Status, CollateralDescription,
                    CollateralValue, DisbursementIban, ConsentBureau, ConsentMarketing, CustomerUserId, CreatedUtc, SubmittedUtc, UpdatedUtc, Version)
                VALUES (@Reference, @ProductCode, @Purpose, @Amount, @TermMonths, @DueDay, @Channel, @Status, @CollateralDescription,
                    @CollateralValue, @DisbursementIban, @ConsentBureau, @ConsentMarketing, @CustomerUserId, @CreatedUtc, @SubmittedUtc, @UpdatedUtc, 1);
                SELECT CAST(SCOPE_IDENTITY() AS bigint);",
                new
                {
                    app.Reference, app.ProductCode, Purpose = (int)app.Purpose, app.Amount, app.TermMonths, app.DueDay, Channel = (int)app.Channel,
                    Status = (int)app.Status, app.CollateralDescription, app.CollateralValue, app.DisbursementIban, app.ConsentBureau,
                    app.ConsentMarketing, app.CustomerUserId, app.CreatedUtc, app.SubmittedUtc, app.UpdatedUtc
                }, tx);
            app.Id = id;
            foreach (var a in app.Applicants)
            {
                a.Id = await _db.ScalarAsync<long>($@"
                    INSERT INTO lending.Applicant({ApplicantColumns})
                    VALUES (@ApplicationId, @IsMain, @PersonalIdentityCode, @FirstName, @LastName, @Email, @Phone, @StreetAddress, @PostalCode, @City,
                        @Citizenship, @ResidentInFinland, @MaritalStatus, @Housing, @Adults, @Children, @Employment, @Employer, @EmployedSince, @OnProbation,
                        @GrossMonthlyIncome, @NetMonthlyIncome, @HousingCostMonthly, @OtherLoanPaymentsMonthly, @OtherLoanBalance, @PoliticallyExposed, @CoreCustomerNumber);
                    SELECT CAST(SCOPE_IDENTITY() AS bigint);",
                    new
                    {
                        ApplicationId = id, a.IsMain, PersonalIdentityCode = a.PersonalIdentityCode.ToUpperInvariant(), a.FirstName, a.LastName, a.Email, a.Phone,
                        a.StreetAddress, a.PostalCode, a.City, a.Citizenship, a.ResidentInFinland, MaritalStatus = (int)a.MaritalStatus,
                        Housing = (int)a.Housing, a.Adults, a.Children, Employment = (int)a.Employment, a.Employer, a.EmployedSince, a.OnProbation,
                        a.GrossMonthlyIncome, a.NetMonthlyIncome, a.HousingCostMonthly, a.OtherLoanPaymentsMonthly, a.OtherLoanBalance,
                        a.PoliticallyExposed, a.CoreCustomerNumber
                    }, tx);
            }
            await AuditAsync(id, "Created", actor, $"{app.ProductCode} {app.Amount:0.00} EUR / {app.TermMonths} mo, status {app.Status}", tx);
            return (await _db.FirstOrDefaultAsync<ApplicationRow>("SELECT * FROM lending.Application WHERE Id = @id", new { id }, tx))!;
        }, ct);
    }

    public Task<ApplicationRow?> GetRowAsync(string reference, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<ApplicationRow>("SELECT * FROM lending.Application WHERE Reference = @reference", new { reference }, ct: ct);

    public Task<ApplicationRow?> GetRowAsync(long id, CancellationToken ct = default) =>
        _db.FirstOrDefaultAsync<ApplicationRow>("SELECT * FROM lending.Application WHERE Id = @id", new { id }, ct: ct);

    public async Task<ApplicationRecord?> GetAsync(string reference, CancellationToken ct = default)
    {
        var row = await GetRowAsync(reference, ct);
        return row == null ? null : new ApplicationRecord(row, await ApplicantsAsync(row.Id, ct));
    }

    public async Task<ApplicationRecord?> GetAsync(long id, CancellationToken ct = default)
    {
        var row = await GetRowAsync(id, ct);
        return row == null ? null : new ApplicationRecord(row, await ApplicantsAsync(row.Id, ct));
    }

    public async Task<List<Applicant>> ApplicantsAsync(long applicationId, CancellationToken ct = default) =>
        (await _db.QueryAsync<Applicant>("SELECT * FROM lending.Applicant WHERE ApplicationId = @applicationId ORDER BY IsMain DESC, Id",
            new { applicationId }, ct: ct)).ToList();

    public Task<IReadOnlyList<ApplicationRow>> ListForCustomerAsync(string customerUserId, CancellationToken ct = default) =>
        _db.QueryAsync<ApplicationRow>("SELECT * FROM lending.Application WHERE CustomerUserId = @customerUserId ORDER BY CreatedUtc DESC",
            new { customerUserId }, ct: ct);

    public Task<IReadOnlyList<ApplicationRow>> ListForPersonAsync(string personalIdentityCode, CancellationToken ct = default) =>
        _db.QueryAsync<ApplicationRow>(@"SELECT a.* FROM lending.Application a
            WHERE EXISTS (SELECT 1 FROM lending.Applicant p WHERE p.ApplicationId = a.Id AND p.PersonalIdentityCode = @pic)
            ORDER BY a.CreatedUtc DESC", new { pic = personalIdentityCode.ToUpperInvariant() }, ct: ct);

    public Task<IReadOnlyList<ApplicationRow>> ListByStatusAsync(ApplicationStatus status, int take, CancellationToken ct = default) =>
        _db.QueryAsync<ApplicationRow>("SELECT TOP (@take) * FROM lending.Application WHERE Status = @status ORDER BY UpdatedUtc",
            new { take, status = (int)status }, ct: ct);

    public Task<IReadOnlyList<QueueItem>> SearchAsync(ApplicationStatus[]? statuses, string? assignedTo, string? text, int skip, int take,
        CancellationToken ct = default)
    {
        var sql = @"SELECT a.Id, a.Reference, a.ProductCode, a.Amount, a.TermMonths, a.Status, a.AssignedTo, a.CreatedUtc, a.UpdatedUtc, a.Channel,
                p.FirstName + ' ' + p.LastName AS ApplicantName, p.PersonalIdentityCode,
                (SELECT TOP (1) d.Outcome FROM lending.Decision d WHERE d.ApplicationId = a.Id ORDER BY d.Id DESC) AS Outcome,
                (SELECT TOP (1) d.RiskGrade FROM lending.Decision d WHERE d.ApplicationId = a.Id ORDER BY d.Id DESC) AS RiskGrade
            FROM lending.Application a JOIN lending.Applicant p ON p.ApplicationId = a.Id AND p.IsMain = 1
            WHERE 1 = 1";
        if (statuses is { Length: > 0 }) sql += " AND a.Status IN @statuses";
        if (!string.IsNullOrEmpty(assignedTo)) sql += " AND a.AssignedTo = @assignedTo";
        if (!string.IsNullOrWhiteSpace(text)) sql += " AND (a.Reference LIKE @like OR p.LastName LIKE @like OR p.PersonalIdentityCode = @text)";
        sql += " ORDER BY a.UpdatedUtc DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
        if (_db.IsSqlite)
            sql = sql.Replace("p.FirstName + ' ' + p.LastName", "p.FirstName || ' ' || p.LastName")
                .Replace("(SELECT TOP (1) d.Outcome FROM lending.Decision d WHERE d.ApplicationId = a.Id ORDER BY d.Id DESC)",
                    "(SELECT d.Outcome FROM Decision d WHERE d.ApplicationId = a.Id ORDER BY d.Id DESC LIMIT 1)")
                .Replace("(SELECT TOP (1) d.RiskGrade FROM lending.Decision d WHERE d.ApplicationId = a.Id ORDER BY d.Id DESC)",
                    "(SELECT d.RiskGrade FROM Decision d WHERE d.ApplicationId = a.Id ORDER BY d.Id DESC LIMIT 1)");
        return _db.QueryAsync<QueueItem>(sql, new
        {
            statuses = statuses?.Select(s => (int)s).ToArray(), assignedTo, text = text?.Trim().ToUpperInvariant(),
            like = "%" + text?.Trim() + "%", skip, take
        }, ct: ct);
    }

    public async Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusAsync(CancellationToken ct = default)
    {
        var rows = await _db.QueryAsync<(int Status, int Count)>("SELECT Status, COUNT(*) AS Count FROM lending.Application GROUP BY Status", ct: ct);
        return rows.ToDictionary(r => (ApplicationStatus)r.Status, r => r.Count);
    }

    /// <summary>Moves the application to a new status if it still has the expected status and version.</summary>
    public async Task<bool> TransitionAsync(long id, ApplicationStatus from, ApplicationStatus to, int version, string actor, string? detail,
        IDbTransaction? tx = null, CancellationToken ct = default)
    {
        ApplicationStatusRules.EnsureCanMove(from, to);
        var now = DateTime.UtcNow;
        var rows = await _db.ExecuteAsync(@"
            UPDATE lending.Application SET Status = @to, Version = Version + 1, UpdatedUtc = @now,
                DecidedUtc = CASE WHEN @decided = 1 THEN @now ELSE DecidedUtc END,
                SubmittedUtc = CASE WHEN @to = 10 THEN @now ELSE SubmittedUtc END
            WHERE Id = @id AND Status = @from AND Version = @version",
            new
            {
                id, from = (int)from, to = (int)to, version, now,
                decided = to is ApplicationStatus.Approved or ApplicationStatus.Declined or ApplicationStatus.Referred ? 1 : 0
            }, tx, ct);
        if (rows == 0) return false;
        await AuditAsync(id, "Status." + to, actor, detail, tx);
        return true;
    }

    public Task AssignAsync(long id, string? user, CancellationToken ct = default) =>
        _db.ExecuteAsync("UPDATE lending.Application SET AssignedTo = @user, UpdatedUtc = @now WHERE Id = @id",
            new { id, user, now = DateTime.UtcNow }, ct: ct);

    public Task SetApplicantCustomerNumberAsync(long applicantId, string customerNumber, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Applicant SET CoreCustomerNumber = @customerNumber WHERE Id = @applicantId", new { applicantId, customerNumber }, tx);

    public Task SetCoreAccountAsync(long id, string accountNumber, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Application SET CoreAccountNumber = @accountNumber, UpdatedUtc = @now WHERE Id = @id",
            new { id, accountNumber, now = DateTime.UtcNow }, tx);

    public Task SetDisbursementIbanAsync(long id, string iban, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Application SET DisbursementIban = @iban, UpdatedUtc = @now WHERE Id = @id",
            new { id, iban, now = DateTime.UtcNow }, tx);

    public Task RecordErrorAsync(long id, string error, CancellationToken ct = default) =>
        _db.ExecuteAsync("UPDATE lending.Application SET Attempts = Attempts + 1, LastError = @error, UpdatedUtc = @now WHERE Id = @id",
            new { id, error = error.Length > 1000 ? error[..1000] : error, now = DateTime.UtcNow }, ct: ct);

    public Task ClearErrorAsync(long id, IDbTransaction? tx = null) =>
        _db.ExecuteAsync("UPDATE lending.Application SET Attempts = 0, LastError = NULL WHERE Id = @id", new { id }, tx);

    public Task AuditAsync(long? applicationId, string eventType, string actor, string? detail, IDbTransaction? tx = null) =>
        _db.ExecuteAsync(@"INSERT INTO lending.AuditEvent(ApplicationId, EventType, Actor, Detail, CreatedUtc)
                           VALUES (@applicationId, @eventType, @actor, @detail, @now)",
            new { applicationId, eventType, actor, detail, now = DateTime.UtcNow }, tx);

    public Task<IReadOnlyList<AuditRow>> AuditTrailAsync(long applicationId, CancellationToken ct = default) =>
        _db.QueryAsync<AuditRow>("SELECT * FROM lending.AuditEvent WHERE ApplicationId = @applicationId ORDER BY Id", new { applicationId }, ct: ct);

    public Task AddNoteAsync(long applicationId, string author, string text, CancellationToken ct = default) =>
        _db.ExecuteAsync("INSERT INTO lending.Note(ApplicationId, Author, Text, CreatedUtc) VALUES (@applicationId, @author, @text, @now)",
            new { applicationId, author, text, now = DateTime.UtcNow }, ct: ct);

    public Task<IReadOnlyList<NoteRow>> NotesAsync(long applicationId, CancellationToken ct = default) =>
        _db.QueryAsync<NoteRow>("SELECT * FROM lending.Note WHERE ApplicationId = @applicationId ORDER BY Id DESC", new { applicationId }, ct: ct);
}
