using System.ComponentModel.DataAnnotations;
using StudyData.Core.Design;
using StudyData.Core.EditChecks;
using StudyData.Core.Queries;
using StudyData.Core.Subjects;
using StudyData.Data.Services;

namespace StudyData.Edc.Web.Models;

public class LoginModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class TwoFactorModel
{
    [Required, StringLength(7, MinimumLength = 6)]
    public string Code { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class AuthenticatorModel
{
    public string SharedKey { get; set; } = "";
    public string Uri { get; set; } = "";
    public bool Enabled { get; set; }
    public string? Code { get; set; }
}

public record SiteSummary(int SiteId, string StudyCode, string StudyTitle, string SiteNumber, string SiteName, int Subjects, int OpenQueries, int AnsweredQueries);

public class ScreenModel
{
    public int SiteId { get; set; }
    public string SiteName { get; set; } = "";

    [StringLength(3)]
    public string? Initials { get; set; }

    [Range(1900, 2100)]
    public int BirthYear { get; set; }

    [Required]
    public string Sex { get; set; } = "F";

    [Required]
    public DateOnly ConsentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}

public record VisitRow(Visit Visit, DateOnly? Target, DateOnly? Earliest, DateOnly? Latest, IReadOnlyList<(FormInstance Form, FormDefinition Definition, int OpenQueries)> Forms);

public class SubjectDetailsModel
{
    public Subject Subject { get; set; } = null!;
    public Study Study { get; set; } = null!;
    public List<VisitRow> Visits { get; set; } = new();
    public int OpenQueries { get; set; }
    public bool CanSign { get; set; }
    public List<Core.Documents.SourceDocument> Documents { get; set; } = new();
}

public class FormEditModel
{
    public Subject Subject { get; set; } = null!;
    public Visit Visit { get; set; } = null!;
    public FormDefinition Definition { get; set; } = null!;
    public FormInstance Form { get; set; } = null!;
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Query> Queries { get; set; } = new();
    public string? Reason { get; set; }
    public string RowVersion { get; set; } = "";
    public SaveFormResult? Result { get; set; }

    public IEnumerable<CheckResult> IssuesFor(string field) =>
        Result is null ? Enumerable.Empty<CheckResult>() : Result.Errors.Concat(Result.Queries).Concat(Result.Warnings).Where(r => r.FieldCode == field);
}

public class SignModel
{
    public int SubjectId { get; set; }
    public string SubjectNumber { get; set; } = "";
    public int Forms { get; set; }

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm the statement.")]
    public bool Confirm { get; set; }
}
