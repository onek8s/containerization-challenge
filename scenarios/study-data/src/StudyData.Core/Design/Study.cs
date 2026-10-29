namespace StudyData.Core.Design;

public enum StudyStatus
{
    Setup,
    Enrolling,
    Treatment,
    FollowUp,
    Locked
}

public class Study
{
    public int Id { get; set; }

    /// <summary>Protocol number, for example FAB-ONC-201.</summary>
    public string Code { get; set; } = "";

    public string Title { get; set; } = "";
    public string Sponsor { get; set; } = "";
    public string SponsorClientId { get; set; } = "";
    public string Phase { get; set; } = "";
    public StudyStatus Status { get; set; }
    public int DesignVersion { get; set; } = 1;

    /// <summary>Subject number pattern, {site} and {seq} are replaced.</summary>
    public string SubjectNumberFormat { get; set; } = "{site}-{seq:000}";

    public int TargetEnrolment { get; set; }
    public int QueryReminderDays { get; set; } = 7;
    public bool Blinded { get; set; }

    public List<Site> Sites { get; set; } = new();
    public List<Arm> Arms { get; set; } = new();
    public List<VisitDefinition> Visits { get; set; } = new();
    public List<FormDefinition> Forms { get; set; } = new();
    public List<CalculationWorkbook> Workbooks { get; set; } = new();
}

public class Site
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public Study? Study { get; set; }

    /// <summary>Site number within the study, for example 101.</summary>
    public string Number { get; set; } = "";

    public string Name { get; set; } = "";
    public string Investigator { get; set; } = "";
    public string Country { get; set; } = "";

    /// <summary>IANA or Windows time zone id of the site; visit dates are entered in local time.</summary>
    public string TimeZone { get; set; } = "Europe/Helsinki";

    public bool Active { get; set; } = true;
    public string ContactEmail { get; set; } = "";
}

public class Arm
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Ratio { get; set; } = 1;
}

public class VisitDefinition
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Order { get; set; }

    /// <summary>Planned study day relative to the anchor visit (day 1 = first dose).</summary>
    public int PlannedDay { get; set; }

    public int WindowBefore { get; set; }
    public int WindowAfter { get; set; }

    /// <summary>The visit whose date defines day 1. Exactly one per study.</summary>
    public bool IsAnchor { get; set; }

    /// <summary>Unscheduled or event-driven visit (adverse events, end of treatment).</summary>
    public bool Unscheduled { get; set; }

    /// <summary>Comma separated form codes collected at this visit.</summary>
    public string FormCodes { get; set; } = "";

    public IEnumerable<string> FormCodeList =>
        FormCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class FormDefinition
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Order { get; set; }

    /// <summary>Form with repeating rows (adverse events, concomitant medications).</summary>
    public bool Repeating { get; set; }

    /// <summary>Workbook that derives the calculated fields of this form, if any.</summary>
    public string? WorkbookCode { get; set; }

    /// <summary>Casebook template used for printing; defaults to the generic form layout.</summary>
    public string? PrintTemplate { get; set; }

    public List<FieldDefinition> Fields { get; set; } = new();
    public List<EditCheckDefinition> EditChecks { get; set; } = new();
}

public enum FieldType
{
    Text,
    Integer,
    Decimal,
    Date,
    Time,
    Choice,
    Boolean,
    Derived
}

public class FieldDefinition
{
    public int Id { get; set; }
    public int FormDefinitionId { get; set; }

    /// <summary>CDISC-style variable name, for example VSWEIGHT.</summary>
    public string Code { get; set; } = "";

    public string Label { get; set; } = "";
    public FieldType Type { get; set; }
    public int Order { get; set; }
    public string? Unit { get; set; }
    public bool Required { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public int? Decimals { get; set; }
    public int? MaxLength { get; set; }

    /// <summary>Choice list as code=label pairs separated by '|', for example 1=Mild|2=Moderate.</summary>
    public string? Choices { get; set; }

    /// <summary>For derived fields: the named output range in the form's workbook.</summary>
    public string? WorkbookOutput { get; set; }

    /// <summary>Requires source data verification by the monitor.</summary>
    public bool Sdv { get; set; } = true;

    /// <summary>Medical coding dictionary for verbatim terms (MEDDRA, WHODRUG).</summary>
    public string? CodingDictionary { get; set; }

    public IReadOnlyList<KeyValuePair<string, string>> ChoiceList()
    {
        if (string.IsNullOrWhiteSpace(Choices))
        {
            return Array.Empty<KeyValuePair<string, string>>();
        }

        return Choices.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(c =>
            {
                var i = c.IndexOf('=');
                return i < 0
                    ? new KeyValuePair<string, string>(c.Trim(), c.Trim())
                    : new KeyValuePair<string, string>(c[..i].Trim(), c[(i + 1)..].Trim());
            })
            .ToList();
    }
}

public enum EditCheckSeverity
{
    /// <summary>Saving is refused until the value is corrected.</summary>
    Hard,

    /// <summary>The form is saved and an automatic query is opened.</summary>
    Query,

    /// <summary>Shown to the user only.</summary>
    Warning
}

public class EditCheckDefinition
{
    public int Id { get; set; }
    public int FormDefinitionId { get; set; }
    public string Code { get; set; } = "";

    /// <summary>Condition that must be true for valid data, for example VSSYSBP > VSDIABP.</summary>
    public string Expression { get; set; } = "";

    /// <summary>Optional condition; the check runs only when it is true.</summary>
    public string? When { get; set; }

    public string Message { get; set; } = "";

    /// <summary>Field the query is attached to.</summary>
    public string FieldCode { get; set; } = "";

    public EditCheckSeverity Severity { get; set; } = EditCheckSeverity.Query;
    public bool Active { get; set; } = true;
}

public class CalculationWorkbook
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public string Code { get; set; } = "";
    public int Version { get; set; }

    /// <summary>File name in the study's calculation folder.</summary>
    public string FileName { get; set; } = "";

    /// <summary>SHA-256 of the validated workbook; a file with another hash is not used.</summary>
    public string Sha256 { get; set; } = "";

    public string ValidatedBy { get; set; } = "";
    public DateTime ValidatedAt { get; set; }
    public bool Active { get; set; } = true;

    public List<CalculationInput> Inputs { get; set; } = new();
}

/// <summary>
/// A named input range of a calculation workbook and where its value comes from. The source is an
/// edit check expression, so it can be a field of the form (VSWEIGHT), a field of another form
/// (DM.SEX) or a calculation (YEARS(DM.BRTHDAT, LB.LBDAT)).
/// </summary>
public class CalculationInput
{
    public int Id { get; set; }
    public int CalculationWorkbookId { get; set; }
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
}
