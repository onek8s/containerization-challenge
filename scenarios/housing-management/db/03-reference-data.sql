/*
    Fabrikam Housing - reference data: patches, estates are loaded from the asset register import,
    Schedule of Rates 2021/22, framework contractors and bank holidays.
    Contractor API secrets are issued from the back office (Admin > Contractors > New API secret).
    sqlcmd -S sql01.corp.local -E -d FabrikamHousing -i 03-reference-data.sql
*/
SET NOCOUNT ON;
GO

MERGE housing.Patches AS t USING (VALUES
    (N'NORTH', N'North patch', N'CORP\j.okafor', N'Joy Okafor', N'j.okafor@fabrikamhomes.co.uk'),
    (N'SOUTH', N'South patch', N'CORP\d.price', N'Dafydd Price', N'd.price@fabrikamhomes.co.uk'),
    (N'EAST', N'East patch', N'CORP\s.kowalska', N'Sylwia Kowalska', N's.kowalska@fabrikamhomes.co.uk')
) AS s (Code, Name, OfficerLogin, OfficerName, OfficerEmail) ON t.Code = s.Code
WHEN MATCHED THEN UPDATE SET Name = s.Name, OfficerLogin = s.OfficerLogin, OfficerName = s.OfficerName, OfficerEmail = s.OfficerEmail
WHEN NOT MATCHED THEN INSERT (Code, Name, OfficerLogin, OfficerName, OfficerEmail) VALUES (s.Code, s.Name, s.OfficerLogin, s.OfficerName, s.OfficerEmail);
GO

MERGE housing.ScheduleOfRates AS t USING (VALUES
    (N'PL0101', N'Renew tap washer or cartridge', 1, 3, 38.50, N'IT'),
    (N'PL0105', N'Renew kitchen mixer tap', 1, 3, 96.20, N'IT'),
    (N'PL0210', N'Repair WC cistern flush mechanism', 1, 2, 54.75, N'IT'),
    (N'PL0215', N'Renew WC pan and seat', 1, 3, 182.40, N'IT'),
    (N'PL0305', N'Reseal bath or shower tray', 1, 3, 46.10, N'LM'),
    (N'PL0901', N'Attend uncontrollable leak, make safe', 1, 1, 88.00, N'IT'),
    (N'DR0101', N'Clear blocked WC (only WC)', 8, 1, 72.30, N'IT'),
    (N'DR0201', N'Clear blocked sink or basin waste', 8, 2, 48.90, N'IT'),
    (N'DR0301', N'Clear external gully or drain, jetting', 8, 2, 118.00, N'IT'),
    (N'HT0101', N'Attend no heating or hot water', 3, 1, 95.00, N'IT'),
    (N'HT0110', N'Repressurise boiler and check expansion vessel', 3, 2, 64.20, N'IT'),
    (N'HT0204', N'Bleed or balance radiator, renew valve', 3, 3, 57.80, N'IT'),
    (N'HT0301', N'Annual gas safety check and service (CP12)', 3, 4, 62.00, N'IT'),
    (N'HT0310', N'Renew combination boiler', 3, 4, 1985.00, N'IT'),
    (N'EL0101', N'Attend loss of power, make safe', 2, 1, 92.00, N'IT'),
    (N'EL0205', N'Renew damaged socket or switch', 2, 2, 44.60, N'IT'),
    (N'EL0301', N'Repair or renew light fitting', 2, 3, 51.30, N'IT'),
    (N'EL0410', N'Repair or renew electric shower', 2, 2, 245.00, N'IT'),
    (N'EL0601', N'Renew mains smoke or heat alarm', 2, 2, 68.40, N'IT'),
    (N'EL0901', N'Electrical installation condition report (EICR)', 2, 4, 145.00, N'IT'),
    (N'CA0101', N'Repair or renew front door lock', 4, 1, 86.50, N'IT'),
    (N'CA0301', N'Repair kitchen unit door or drawer', 4, 3, 42.00, N'IT'),
    (N'CA0305', N'Renew kitchen worktop', 4, 3, 64.00, N'LM'),
    (N'CA0601', N'Renew fence panel', 4, 3, 79.90, N'IT'),
    (N'CA0701', N'Ease and adjust internal door', 4, 3, 36.00, N'IT'),
    (N'GL0101', N'Board up and renew double glazed unit', 7, 2, 212.00, N'IT'),
    (N'GL0201', N'Renew window handle or friction hinge', 7, 3, 49.50, N'IT'),
    (N'RF0101', N'Renew slipped or missing roof tiles', 5, 2, 155.00, N'IT'),
    (N'RF0301', N'Clear and realign gutter', 5, 3, 98.00, N'IT'),
    (N'PS0101', N'Hack off and replaster wall', 6, 3, 41.00, N'M2'),
    (N'DM0101', N'Mould wash and treat, fungicidal paint', 9, 2, 124.00, N'IT'),
    (N'DM0201', N'Damp survey and report', 9, 2, 165.00, N'IT'),
    (N'DM0301', N'Install humidistat extractor fan', 9, 3, 210.00, N'IT'),
    (N'GN0101', N'General handyperson, per hour', 10, 3, 32.00, N'HR'),
    (N'GN0201', N'Remove bulky items from communal area', 10, 3, 85.00, N'IT')
) AS s (Code, Description, Trade, DefaultPriority, UnitPrice, Unit) ON t.Code = s.Code
WHEN MATCHED THEN UPDATE SET Description = s.Description, Trade = s.Trade, DefaultPriority = s.DefaultPriority, UnitPrice = s.UnitPrice, Unit = s.Unit
WHEN NOT MATCHED THEN INSERT (Code, Description, Trade, DefaultPriority, UnitPrice, Unit, Active) VALUES (s.Code, s.Description, s.Trade, s.DefaultPriority, s.UnitPrice, s.Unit, 1);
GO

MERGE housing.BankHolidays AS t USING (VALUES
    ('2019-01-01'),
    ('2019-04-19'),
    ('2019-04-22'),
    ('2019-05-06'),
    ('2019-05-27'),
    ('2019-08-26'),
    ('2019-12-25'),
    ('2019-12-26'),
    ('2020-01-01'),
    ('2020-04-10'),
    ('2020-04-13'),
    ('2020-05-08'),
    ('2020-05-25'),
    ('2020-08-31'),
    ('2020-12-25'),
    ('2020-12-28'),
    ('2021-01-01'),
    ('2021-04-02'),
    ('2021-04-05'),
    ('2021-05-03'),
    ('2021-05-31'),
    ('2021-08-30'),
    ('2021-12-27'),
    ('2021-12-28'),
    ('2022-01-03'),
    ('2022-04-15'),
    ('2022-04-18'),
    ('2022-05-02'),
    ('2022-06-02'),
    ('2022-06-03'),
    ('2022-08-29'),
    ('2022-09-19'),
    ('2022-12-26'),
    ('2022-12-27')
) AS s ([Date]) ON t.[Date] = s.[Date]
WHEN NOT MATCHED THEN INSERT ([Date], [Name]) VALUES (s.[Date], N'Bank holiday');
GO

MERGE housing.Contractors AS t USING (VALUES
    (N'AVONPH', N'Avon Plumbing & Heating Ltd', N'Plumbing,Heating,Drainage', N'*', 1, 1, N'531926', 60, N'jobs@avonph.example.co.uk', N'0117 496 0160', N'accounts@avonph.example.co.uk', 1, N'GB319 2681 21', N'40-11-62', N'31926819', N'Avon Plumbing & Heating Ltd', N'contractor-avonph'),
    (N'BRISEL', N'Brislington Electrical Services', N'Electrical', N'*', 1, 0, NULL, 45, N'jobs@brisel.example.co.uk', N'0117 496 0145', N'accounts@brisel.example.co.uk', 1, N'GB557 7991 21', N'20-00-00', N'55779911', N'Brislington Electrical Services', N'contractor-brisel'),
    (N'CLIFTJ', N'Clifton Joinery & Glazing', N'Carpentry,Glazing', N'NORTH,EAST', 0, 0, NULL, 30, N'jobs@cliftj.example.co.uk', N'0117 496 0130', N'accounts@cliftj.example.co.uk', 1, N'GB102 9384 21', N'30-94-12', N'10293847', N'Clifton Joinery & Glazing', N'contractor-cliftj'),
    (N'DUNDRY', N'Dundry Roofing Co', N'Roofing,Plastering', N'*', 0, 0, NULL, 15, N'jobs@dundry.example.co.uk', N'0117 496 0115', N'accounts@dundry.example.co.uk', 0, NULL, N'60-83-71', N'77001234', N'Dundry Roofing Co', N'contractor-dundry'),
    (N'EASTON', N'Easton Property Maintenance', N'Carpentry,General,Plastering,Glazing', N'SOUTH,EAST', 0, 0, NULL, 40, N'jobs@easton.example.co.uk', N'0117 496 0140', N'accounts@easton.example.co.uk', 1, N'GB445 5667 21', N'09-01-28', N'44556677', N'Easton Property Maintenance', N'contractor-easton'),
    (N'FROMEV', N'Frome Valley Damp Solutions', N'DampAndMould,Plastering', N'*', 0, 0, NULL, 20, N'jobs@fromev.example.co.uk', N'0117 496 0120', N'accounts@fromev.example.co.uk', 1, N'GB123 4567 21', N'11-00-01', N'12345678', N'Frome Valley Damp Solutions', N'contractor-fromev'),
    (N'GASCAR', N'GasCare South West', N'Heating', N'*', 1, 1, N'566332', 80, N'jobs@gascar.example.co.uk', N'0117 496 0180', N'accounts@gascar.example.co.uk', 1, N'GB663 3221 21', N'40-46-15', N'66332211', N'GasCare South West', N'contractor-gascar'),
    (N'HENLEA', N'Henleaze Drainage', N'Drainage,Plumbing', N'NORTH', 1, 0, NULL, 25, N'jobs@henlea.example.co.uk', N'0117 496 0125', N'accounts@henlea.example.co.uk', 0, NULL, N'77-91-19', N'90807060', N'Henleaze Drainage', N'contractor-henlea')
) AS s (Code, Name, Trades, Patches, OutOfHours, GasSafeRegistered, GasSafeNumber, WeeklyCapacity, ContactEmail, ContactPhone, RemittanceEmail, VatRegistered, VatNumber, BankSortCode, BankAccountNumber, BankAccountName, ApiClientId) ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code, Name, Trades, Patches, OutOfHours, GasSafeRegistered, GasSafeNumber, WeeklyCapacity, ContactEmail, ContactPhone, RemittanceEmail, VatRegistered, VatNumber, BankSortCode, BankAccountNumber, BankAccountName, ApiClientId, Active, CreatedAt, CreatedBy)
    VALUES (s.Code, s.Name, s.Trades, s.Patches, s.OutOfHours, s.GasSafeRegistered, s.GasSafeNumber, s.WeeklyCapacity, s.ContactEmail, s.ContactPhone, s.RemittanceEmail, s.VatRegistered, s.VatNumber, s.BankSortCode, s.BankAccountNumber, s.BankAccountName, s.ApiClientId, 1, SYSDATETIME(), N'setup');
GO

IF NOT EXISTS (SELECT 1 FROM housing.Counters WHERE Name = N'BacsFile')
    INSERT INTO housing.Counters (Name, Value) VALUES (N'BacsFile', 0);
GO
