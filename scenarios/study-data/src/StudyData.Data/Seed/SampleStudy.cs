using StudyData.Core.Design;

namespace StudyData.Data.Seed;

/// <summary>
/// FAB-ONC-201: randomised, double-blind phase II study of FAB-1157 or placebo with carboplatin in
/// platinum-sensitive recurrent ovarian cancer. Used as the sample study in development and as the
/// template study for new set-ups.
/// </summary>
public static class SampleStudy
{
    public const string Code = "FAB-ONC-201";
    public const string WorkbookFile = "FAB-ONC-201_derivations_v3.xlsx";
    public const string WorkbookSha256 = "e52e1977456c649dc469a7931d95a3085914df5723b9f33061525a68725e5f48";

    public static Study Create()
    {
        var study = new Study
        {
            Code = Code,
            Title = "A randomised, double-blind, placebo-controlled phase II study of FAB-1157 with carboplatin in platinum-sensitive recurrent ovarian cancer",
            Sponsor = "Tailspin Therapeutics",
            SponsorClientId = "tailspin-cdms",
            Phase = "II",
            Status = StudyStatus.Enrolling,
            DesignVersion = 3,
            SubjectNumberFormat = "{site}-{seq:000}",
            TargetEnrolment = 120,
            QueryReminderDays = 7,
            Blinded = true
        };

        study.Sites.AddRange(new[]
        {
            new Site { Number = "101", Name = "Helsinki University Hospital, Gynaecological Oncology", Investigator = "Dr. Aino Virtanen", Country = "FI", TimeZone = "Europe/Helsinki", ContactEmail = "fab201.site101@hus.example" },
            new Site { Number = "102", Name = "Tampere University Hospital, Cancer Centre", Investigator = "Dr. Mikko Laine", Country = "FI", TimeZone = "Europe/Helsinki", ContactEmail = "fab201.site102@tays.example" },
            new Site { Number = "201", Name = "North Estonia Medical Centre", Investigator = "Dr. Kadri Tamm", Country = "EE", TimeZone = "Europe/Tallinn", ContactEmail = "fab201.site201@regionaalhaigla.example" },
            new Site { Number = "301", Name = "Karolinska University Hospital, Tema Cancer", Investigator = "Dr. Sara Lindqvist", Country = "SE", TimeZone = "Europe/Stockholm", ContactEmail = "fab201.site301@karolinska.example" }
        });

        study.Arms.AddRange(new[]
        {
            new Arm { Code = "A", Name = "FAB-1157 + carboplatin", Ratio = 1 },
            new Arm { Code = "B", Name = "Placebo + carboplatin", Ratio = 1 }
        });

        const string cycle = "VS,LB,EX,RS";
        study.Visits.AddRange(new[]
        {
            new VisitDefinition { Code = "SCR", Name = "Screening", Order = 1, PlannedDay = -14, WindowBefore = 14, WindowAfter = 13, FormCodes = "IC,DM,MH,IE,VS,LB,RS" },
            new VisitDefinition { Code = "C1D1", Name = "Cycle 1 Day 1", Order = 2, PlannedDay = 1, IsAnchor = true, FormCodes = "VS,LB,EX" },
            new VisitDefinition { Code = "C1D8", Name = "Cycle 1 Day 8", Order = 3, PlannedDay = 8, WindowBefore = 1, WindowAfter = 1, FormCodes = "VS,LB" },
            new VisitDefinition { Code = "C2D1", Name = "Cycle 2 Day 1", Order = 4, PlannedDay = 22, WindowBefore = 3, WindowAfter = 3, FormCodes = "VS,LB,EX" },
            new VisitDefinition { Code = "C3D1", Name = "Cycle 3 Day 1", Order = 5, PlannedDay = 43, WindowBefore = 3, WindowAfter = 3, FormCodes = cycle },
            new VisitDefinition { Code = "C4D1", Name = "Cycle 4 Day 1", Order = 6, PlannedDay = 64, WindowBefore = 3, WindowAfter = 3, FormCodes = "VS,LB,EX" },
            new VisitDefinition { Code = "C5D1", Name = "Cycle 5 Day 1", Order = 7, PlannedDay = 85, WindowBefore = 3, WindowAfter = 3, FormCodes = cycle },
            new VisitDefinition { Code = "C6D1", Name = "Cycle 6 Day 1", Order = 8, PlannedDay = 106, WindowBefore = 3, WindowAfter = 3, FormCodes = "VS,LB,EX" },
            new VisitDefinition { Code = "EOT", Name = "End of Treatment", Order = 9, PlannedDay = 127, WindowBefore = 7, WindowAfter = 7, FormCodes = "VS,LB,RS,EOT" },
            new VisitDefinition { Code = "FU30", Name = "30-day Safety Follow-up", Order = 10, PlannedDay = 157, WindowBefore = 7, WindowAfter = 7, FormCodes = "VS" },
            new VisitDefinition { Code = "LOG", Name = "Adverse Events and Medications", Order = 99, Unscheduled = true, FormCodes = "AE,CM" }
        });

        study.Forms.AddRange(new[]
        {
            Form("IC", "Informed Consent", 1, null,
                F("ICDAT", "Date of informed consent", FieldType.Date, required: true),
                F("ICVER", "Informed consent form version", FieldType.Text, required: true, maxLength: 20),
                F("ICPGX", "Optional pharmacogenomics consent", FieldType.Choice, choices: "1=Yes|0=No")),
            Form("DM", "Demographics", 2, null,
                F("BRTHDAT", "Date of birth", FieldType.Date, required: true),
                F("SEX", "Sex", FieldType.Choice, required: true, choices: "1=Male|2=Female"),
                F("RACE", "Race", FieldType.Choice, choices: "1=White|2=Black or African American|3=Asian|4=Other|9=Not reported"),
                F("ETHNIC", "Ethnicity", FieldType.Choice, choices: "1=Hispanic or Latino|2=Not Hispanic or Latino|9=Not reported"),
                F("ECOG", "ECOG performance status", FieldType.Choice, required: true, choices: "0=0|1=1|2=2|3=3|4=4")),
            Form("MH", "Medical History and Prior Therapy", 3, null,
                F("DIAGDAT", "Date of initial diagnosis", FieldType.Date, required: true),
                F("FIGO", "FIGO stage at diagnosis", FieldType.Choice, required: true, choices: "I=I|II=II|III=III|IV=IV"),
                F("HIST", "Histology", FieldType.Choice, required: true, choices: "1=High-grade serous|2=Endometrioid|3=Clear cell|4=Other"),
                F("PRIORLN", "Prior lines of platinum therapy", FieldType.Integer, required: true, min: 1, max: 6),
                F("PFI", "Platinum-free interval", FieldType.Integer, required: true, unit: "months", min: 0, max: 120),
                F("BRCA", "BRCA status", FieldType.Choice, choices: "1=BRCA1/2 mutated|2=Wild type|9=Unknown")),
            Form("IE", "Eligibility", 4, null,
                F("INC01", "Age 18 years or older", FieldType.Boolean, required: true),
                F("INC02", "Platinum-free interval of 6 months or more", FieldType.Boolean, required: true),
                F("INC03", "Measurable disease per RECIST 1.1", FieldType.Boolean, required: true),
                F("INC04", "Adequate organ function", FieldType.Boolean, required: true),
                F("EXC01", "Prior FAB-1157 or other investigational PARP inhibitor", FieldType.Boolean, required: true),
                F("EXC02", "Grade 2 or higher peripheral neuropathy", FieldType.Boolean, required: true),
                F("IEYN", "Subject is eligible", FieldType.Choice, required: true, choices: "1=Yes|0=No")),
            Form("VS", "Vital Signs and Body Measurements", 5, "DERIV",
                F("VSDAT", "Date of assessment", FieldType.Date, required: true),
                F("VSHEIGHT", "Height", FieldType.Decimal, unit: "cm", min: 120, max: 210, decimals: 1),
                F("VSWEIGHT", "Weight", FieldType.Decimal, required: true, unit: "kg", min: 30, max: 200, decimals: 1),
                F("VSSYSBP", "Systolic blood pressure", FieldType.Integer, required: true, unit: "mmHg", min: 70, max: 220),
                F("VSDIABP", "Diastolic blood pressure", FieldType.Integer, required: true, unit: "mmHg", min: 40, max: 130),
                F("VSPULSE", "Pulse", FieldType.Integer, unit: "beats/min", min: 40, max: 160),
                F("VSTEMP", "Temperature", FieldType.Decimal, unit: "°C", min: 34, max: 42, decimals: 1),
                D("VSBMI", "Body mass index", "BMI", "kg/m²", 1),
                D("VSBSA", "Body surface area (Mosteller)", "BSA", "m²", 2)),
            Form("LB", "Local Laboratory", 6, "DERIV",
                F("LBDAT", "Date of sample", FieldType.Date, required: true),
                F("LBCREAT", "Serum creatinine", FieldType.Decimal, required: true, unit: "µmol/L", min: 20, max: 600, decimals: 0),
                F("LBANC", "Absolute neutrophil count", FieldType.Decimal, required: true, unit: "10^9/L", min: 0, max: 30, decimals: 2),
                F("LBPLAT", "Platelets", FieldType.Integer, required: true, unit: "10^9/L", min: 5, max: 1000),
                F("LBHGB", "Haemoglobin", FieldType.Integer, required: true, unit: "g/L", min: 40, max: 200),
                F("LBALT", "ALT", FieldType.Integer, unit: "U/L", min: 1, max: 2000),
                D("LBCRCL", "Creatinine clearance (Cockcroft-Gault)", "CRCL", "mL/min", 0),
                D("LBEGFR", "eGFR (CKD-EPI 2021)", "EGFR", "mL/min/1.73m²", 0)),
            Form("EX", "Study Treatment Administration", 7, "DERIV",
                F("EXDAT", "Date of administration", FieldType.Date, required: true),
                F("EXAUC", "Carboplatin target AUC", FieldType.Choice, required: true, choices: "5=AUC 5|4=AUC 4 (reduced)|6=AUC 6"),
                F("EXCARBO", "Carboplatin dose given", FieldType.Integer, required: true, unit: "mg", min: 100, max: 1200),
                F("EXFABKG", "FAB-1157 / placebo dose level", FieldType.Choice, required: true, choices: "8=8 mg/kg|6=6 mg/kg (reduced)|4=4 mg/kg (reduced)"),
                F("EXFAB", "FAB-1157 / placebo dose given", FieldType.Integer, required: true, unit: "mg", min: 0, max: 1500),
                F("EXADJ", "Dose adjusted", FieldType.Choice, required: true, choices: "0=No|1=Reduced for toxicity|2=Delayed|3=Interrupted"),
                F("EXADJR", "Reason for adjustment", FieldType.Text, maxLength: 200),
                D("EXCARBOP", "Carboplatin planned dose (Calvert)", "CARBO_DOSE", "mg", 0),
                D("EXFABP", "FAB-1157 planned dose", "FAB_DOSE", "mg", 0),
                D("EXCARBDV", "Carboplatin deviation from planned", "CARBO_DEV_PCT", "%", 1)),
            Form("RS", "Tumour Assessment (RECIST 1.1)", 8, null,
                F("RSDAT", "Date of scan", FieldType.Date, required: true),
                F("RSMETH", "Method", FieldType.Choice, required: true, choices: "1=CT|2=MRI"),
                F("RSTLSUM", "Sum of target lesion diameters", FieldType.Integer, required: true, unit: "mm", min: 0, max: 1000),
                F("RSNEWL", "New lesions", FieldType.Choice, required: true, choices: "0=No|1=Yes"),
                F("RSOVR", "Overall response", FieldType.Choice, required: true, choices: "CR=Complete response|PR=Partial response|SD=Stable disease|PD=Progressive disease|NE=Not evaluable"),
                F("CA125", "CA-125", FieldType.Integer, unit: "kU/L", min: 0, max: 100000)),
            Form("EOT", "End of Treatment", 9, null,
                F("EOTDAT", "Date of last dose", FieldType.Date, required: true),
                F("EOTREAS", "Primary reason", FieldType.Choice, required: true, choices: "1=Completed 6 cycles|2=Disease progression|3=Adverse event|4=Withdrawal of consent|5=Investigator decision|6=Death|7=Other"),
                F("EOTSPEC", "Specify", FieldType.Text, maxLength: 200)),
            Form("AE", "Adverse Events", 20, null, true,
                F("AETERM", "Adverse event (verbatim)", FieldType.Text, required: true, maxLength: 200, coding: "MEDDRA"),
                F("AESTDAT", "Start date", FieldType.Date, required: true),
                F("AEONGO", "Ongoing", FieldType.Choice, required: true, choices: "1=Yes|0=No"),
                F("AEENDAT", "End date", FieldType.Date),
                F("AETOXGR", "CTCAE grade", FieldType.Choice, required: true, choices: "1=1 Mild|2=2 Moderate|3=3 Severe|4=4 Life-threatening|5=5 Death"),
                F("AESER", "Serious", FieldType.Choice, required: true, choices: "1=Yes|0=No"),
                F("AEREL", "Related to FAB-1157 / placebo", FieldType.Choice, required: true, choices: "1=Related|0=Not related"),
                F("AERELC", "Related to carboplatin", FieldType.Choice, required: true, choices: "1=Related|0=Not related"),
                F("AEACN", "Action taken with study treatment", FieldType.Choice, required: true, choices: "1=None|2=Dose reduced|3=Interrupted|4=Withdrawn|9=Not applicable"),
                F("AEOUT", "Outcome", FieldType.Choice, choices: "1=Recovered|2=Recovering|3=Not recovered|4=Recovered with sequelae|5=Fatal")),
            Form("CM", "Concomitant Medications", 21, null, true,
                F("CMTRT", "Medication (trade or generic name)", FieldType.Text, required: true, maxLength: 200, coding: "WHODRUG"),
                F("CMINDC", "Indication", FieldType.Text, required: true, maxLength: 200),
                F("CMDOSE", "Dose", FieldType.Decimal, min: 0, max: 100000),
                F("CMDOSU", "Unit", FieldType.Choice, choices: "mg=mg|g=g|ug=µg|mL=mL|IU=IU|tab=tablet"),
                F("CMROUTE", "Route", FieldType.Choice, choices: "PO=Oral|IV=Intravenous|SC=Subcutaneous|TOP=Topical|INH=Inhaled"),
                F("CMSTDAT", "Start date", FieldType.Date),
                F("CMONGO", "Ongoing", FieldType.Choice, required: true, choices: "1=Yes|0=No"),
                F("CMENDAT", "End date", FieldType.Date))
        });

        Check("IC", "IC01", "ICDAT <= TODAY()", "ICDAT", "Date of consent is in the future.", EditCheckSeverity.Hard);
        Check("DM", "DM01", "YEARS(BRTHDAT, IC.ICDAT) >= 18", "BRTHDAT", "Subject is younger than 18 at informed consent. Please verify the date of birth.");
        Check("DM", "DM02", "SEX = 2", "SEX", "The protocol enrols female subjects only. Please verify.");
        Check("DM", "DM03", "ECOG <= 1", "ECOG", "ECOG performance status above 1 does not meet inclusion criterion 5.");
        Check("MH", "MH01", "DIAGDAT <= IC.ICDAT", "DIAGDAT", "Date of diagnosis is after informed consent.");
        Check("MH", "MH02", "PFI >= 6", "PFI", "Platinum-free interval below 6 months: subject is not platinum-sensitive.");
        Check("IE", "IE01", "NOT (IEYN = 1 AND (INC01 = FALSE OR INC02 = FALSE OR INC03 = FALSE OR INC04 = FALSE OR EXC01 = TRUE OR EXC02 = TRUE))", "IEYN",
            "Subject is marked eligible but an inclusion criterion is not met or an exclusion criterion applies.", EditCheckSeverity.Hard);
        Check("IE", "IE02", "MH.PFI >= 6 OR INC02 = FALSE", "INC02", "Inclusion 2 is ticked but the platinum-free interval in medical history is below 6 months.");
        Check("VS", "VS01", "VSSYSBP > VSDIABP", "VSDIABP", "Diastolic blood pressure is not lower than systolic.", EditCheckSeverity.Hard);
        Check("VS", "VS02", "VSDAT >= IC.ICDAT", "VSDAT", "Assessment date is before informed consent.");
        Check("VS", "VS03", "NOTBLANK(VSHEIGHT) OR NOTBLANK(SCR.VSHEIGHT)", "VSHEIGHT", "Height is required at screening.");
        Check("VS", "VS04", "VSTEMP < 38.5", "VSTEMP", "Temperature 38.5 °C or above: report a febrile adverse event if applicable.", EditCheckSeverity.Warning);
        Check("LB", "LB01", "LBANC >= 1.5 OR NOT IN(LB.LBDAT, EX.EXDAT)", "LBANC", "ANC below 1.5 on a dosing day: treatment should be delayed per protocol section 6.4.");
        Check("LB", "LB02", "LBPLAT >= 100 OR ISBLANK(EX.EXDAT)", "LBPLAT", "Platelets below 100 with treatment given at this visit: confirm the dose decision.");
        Check("LB", "LB03", "LBCRCL >= 30 OR ISBLANK(LBCRCL)", "LBCREAT", "Creatinine clearance below 30 mL/min. Confirm the creatinine value and units.");
        Check("EX", "EX01", "ABS(EXCARBDV) <= 10 OR ISBLANK(EXCARBDV)", "EXCARBO", "Carboplatin dose deviates more than 10% from the Calvert dose. Please explain.");
        Check("EX", "EX02", "ABS(EXFAB - EXFABP) <= EXFABP * 0.1 OR ISBLANK(EXFABP)", "EXFAB", "FAB-1157 / placebo dose deviates more than 10% from the planned dose.");
        Check("EX", "EX03", "NOTBLANK(EXADJR)", "EXADJR", "Reason for adjustment is required when the dose was adjusted.", when: "EXADJ <> 0");
        Check("EX", "EX04", "EXDAT >= C1D1.EXDAT OR ISBLANK(C1D1.EXDAT)", "EXDAT", "Administration date is before cycle 1 day 1.");
        Check("RS", "RS01", "NOT (RSNEWL = 1 AND RSOVR <> 'PD')", "RSOVR", "New lesions require an overall response of progressive disease.", EditCheckSeverity.Hard);
        Check("EOT", "EOT01", "EOTDAT >= IC.ICDAT", "EOTDAT", "Date of last dose is before informed consent.");
        Check("EOT", "EOT02", "NOTBLANK(EOTSPEC)", "EOTSPEC", "Specify the reason.", when: "EOTREAS = 7");
        Check("AE", "AE01", "AEENDAT >= AESTDAT", "AEENDAT", "End date is before start date.", EditCheckSeverity.Hard);
        Check("AE", "AE02", "NOTBLANK(AEENDAT)", "AEENDAT", "End date is required for an event that is not ongoing.", when: "AEONGO = 0");
        Check("AE", "AE03", "ISBLANK(AEENDAT)", "AEENDAT", "An ongoing event must not have an end date.", when: "AEONGO = 1");
        Check("AE", "AE04", "AESER = 1", "AESER", "Grade 5 event must be reported as serious.", when: "AETOXGR = 5");
        Check("AE", "AE05", "AESTDAT >= IC.ICDAT", "AESTDAT", "Event started before informed consent: record it in medical history instead.");
        Check("AE", "AE06", "AEOUT = 5", "AEOUT", "Outcome must be fatal for a grade 5 event.", when: "AETOXGR = 5");
        Check("CM", "CM01", "CMENDAT >= CMSTDAT OR ISBLANK(CMSTDAT)", "CMENDAT", "End date is before start date.", EditCheckSeverity.Hard);
        Check("CM", "CM02", "NOTBLANK(CMENDAT)", "CMENDAT", "End date is required for a medication that is not ongoing.", when: "CMONGO = 0");
        Check("CM", "CM03", "NOTBLANK(CMDOSU)", "CMDOSU", "Dose unit is required when a dose is entered.", when: "NOTBLANK(CMDOSE)");

        var workbook = new CalculationWorkbook
        {
            Code = "DERIV",
            Version = 3,
            FileName = WorkbookFile,
            Sha256 = WorkbookSha256,
            ValidatedBy = "J. Korhonen (Biostatistics), validation report VR-201-DERIV-003",
            ValidatedAt = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
            Active = true
        };
        workbook.Inputs.AddRange(new[]
        {
            new CalculationInput { Name = "HEIGHT_CM", Source = "IF(NOTBLANK(VS.VSHEIGHT), VS.VSHEIGHT, SCR.VSHEIGHT)" },
            new CalculationInput { Name = "WEIGHT_KG", Source = "VS.VSWEIGHT" },
            new CalculationInput { Name = "SEX", Source = "IF(DM.SEX = 2, 'F', 'M')" },
            new CalculationInput { Name = "AGE_Y", Source = "YEARS(DM.BRTHDAT, IC.ICDAT)" },
            new CalculationInput { Name = "SCR_UMOL", Source = "LB.LBCREAT" },
            new CalculationInput { Name = "AUC_TARGET", Source = "EX.EXAUC" },
            new CalculationInput { Name = "DOSE_MG_KG", Source = "EX.EXFABKG" },
            new CalculationInput { Name = "CARBO_ACTUAL", Source = "EX.EXCARBO" }
        });
        study.Workbooks.Add(workbook);
        return study;

        void Check(string form, string code, string expression, string field, string message,
            EditCheckSeverity severity = EditCheckSeverity.Query, string? when = null) =>
            study.Forms.First(f => f.Code == form).EditChecks.Add(new EditCheckDefinition
            {
                Code = code, Expression = expression, FieldCode = field, Message = message, Severity = severity, When = when
            });
    }

    private static FormDefinition Form(string code, string name, int order, string? workbook, params FieldDefinition[] fields) =>
        Form(code, name, order, workbook, false, fields);

    private static FormDefinition Form(string code, string name, int order, string? workbook, bool repeating, params FieldDefinition[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            fields[i].Order = i + 1;
        }

        return new FormDefinition { Code = code, Name = name, Order = order, WorkbookCode = workbook, Repeating = repeating, Fields = fields.ToList() };
    }

    private static FieldDefinition F(string code, string label, FieldType type, bool required = false, string? unit = null,
        decimal? min = null, decimal? max = null, int? decimals = null, int? maxLength = null, string? choices = null, string? coding = null) =>
        new()
        {
            Code = code, Label = label, Type = type, Required = required, Unit = unit, Min = min, Max = max,
            Decimals = decimals, MaxLength = maxLength, Choices = choices, CodingDictionary = coding,
            Sdv = type != FieldType.Text || coding is not null
        };

    private static FieldDefinition D(string code, string label, string output, string unit, int decimals) =>
        new() { Code = code, Label = label, Type = FieldType.Derived, WorkbookOutput = output, Unit = unit, Decimals = decimals, Sdv = false };
}
