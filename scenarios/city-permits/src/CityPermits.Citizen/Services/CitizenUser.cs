using System.Security.Claims;
using CityPermits.Data;
using CityPermits.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Citizen.Services;

/// <summary>Maps the signed-in eID identity to an applicant record (created on first sign-in).</summary>
public sealed class CitizenUser
{
    // Attributes released by the national eID broker (eIDAS-style SAML profile).
    public const string SubjectClaim = ClaimTypes.NameIdentifier;
    public const string NameClaim = "urn:oid:2.5.4.3";
    public const string PersonalIdClaim = "urn:oid:1.2.246.21";
    public const string OrganisationClaim = "urn:eid:organisation:number";
    public const string OrganisationNameClaim = "urn:eid:organisation:name";

    private readonly PermitsDbContext _db;

    public CitizenUser(PermitsDbContext db) => _db = db;

    public async Task<Applicant> CurrentAsync(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(SubjectClaim) ?? throw new InvalidOperationException("No subject.");
        var org = user.FindFirstValue(OrganisationClaim);
        var key = org is null ? subject : $"{subject}|{org}";
        var a = await _db.Applicants.FirstOrDefaultAsync(x => x.EidSubject == key);
        if (a is not null) return a;
        a = new Applicant
        {
            EidSubject = key,
            Type = org is null ? ApplicantType.Person : ApplicantType.Organisation,
            CompanyNumber = org,
            DisplayName = user.FindFirstValue(OrganisationNameClaim) ?? user.FindFirstValue(NameClaim) ?? "Applicant",
            PersonalIdHash = user.FindFirstValue(PersonalIdClaim) is { } pid
                ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pid))) : null,
            CreatedUtc = DateTime.UtcNow
        };
        _db.Applicants.Add(a);
        await _db.SaveChangesAsync();
        return a;
    }
}
