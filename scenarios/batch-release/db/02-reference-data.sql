-- Product families and products released at the Turku site.
-- Release policies are set afterwards with deploy\set-release-policy.ps1; until then only
-- local administrators can read records and nobody can sign.
-- sqlcmd -S release-sql.corp.local -E -d BatchRelease -i 02-reference-data.sql
:on error exit
SET NOCOUNT ON;

MERGE br.ProductFamily AS t
USING (VALUES
    (N'OSD', N'Oral solid dose'),
    (N'STI', N'Sterile injectables'),
    (N'TOP', N'Topicals')
) AS s (Code, Name)
ON t.Code = s.Code
WHEN MATCHED THEN UPDATE SET Name = s.Name
WHEN NOT MATCHED THEN INSERT (Code, Name, PolicySddl, PolicyChangedUtc, PolicyChangedBy)
    VALUES (s.Code, s.Name, N'O:BAG:SYD:P(A;;0x1;;;BA)', SYSUTCDATETIME(), SUSER_SNAME());

MERGE br.Product AS t
USING (VALUES
    (N'PRX-0450', N'Proxanol 450 mg film-coated tablets', N'OSD', N'450 mg', N'Film-coated tablet', 36, 18),
    (N'PRX-0900', N'Proxanol 900 mg film-coated tablets', N'OSD', N'900 mg', N'Film-coated tablet', 36, 18),
    (N'VLD-0010', N'Velidex 10 mg/ml solution for injection', N'STI', N'10 mg/ml', N'Solution for injection', 24, 12),
    (N'VLD-0040', N'Velidex 40 mg/ml solution for injection', N'STI', N'40 mg/ml', N'Solution for injection', 24, 12),
    (N'CMR-0005', N'Calmera 5 % cream', N'TOP', N'5 %', N'Cream', 30, 12)
) AS s (Code, Name, FamilyCode, Strength, DosageForm, ShelfLifeMonths, MinimumRemainingMonths)
ON t.Code = s.Code
WHEN MATCHED THEN UPDATE SET Name = s.Name, FamilyCode = s.FamilyCode, Strength = s.Strength, DosageForm = s.DosageForm,
    ShelfLifeMonths = s.ShelfLifeMonths, MinimumRemainingMonths = s.MinimumRemainingMonths
WHEN NOT MATCHED THEN INSERT (Code, Name, FamilyCode, Strength, DosageForm, ShelfLifeMonths, MinimumRemainingMonths, Active)
    VALUES (s.Code, s.Name, s.FamilyCode, s.Strength, s.DosageForm, s.ShelfLifeMonths, s.MinimumRemainingMonths, 1);
GO
