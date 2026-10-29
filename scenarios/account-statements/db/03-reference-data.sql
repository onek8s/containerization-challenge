-- Reference and test data: company profile, holidays, customers, contracts, a generated AR ledger with invoices
-- for January to October 2026 (ledger date 27.10.2026) and the standard subscriptions.
-- sqlcmd -S sql01.corp.local -E -d AccountStatements -i 03-reference-data.sql -v StatementShare="\\files01.corp.local\Statements$" FinanceShare="\\files01.corp.local\Finance$"
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM stm.CompanyProfile)
INSERT stm.CompanyProfile (ProfileId, CompanyName, AddressLine1, PostalCode, City, BusinessId, VatNumber, Phone, Email, Web, BankName, Iban, Bic)
VALUES (1, N'Woodgrove Leasing Oy', N'Porkkalankatu 22', N'00180', N'Helsinki', N'2913456-7', N'FI29134567', N'+358 9 4241 7700',
        N'receivables@woodgrove-leasing.fi', N'www.woodgrove-leasing.fi', N'Nordea', N'FI2112345600000785', N'NDEAFIHH');
GO

MERGE stm.Holiday AS t
USING (VALUES
    ('2026-01-01', N'New Year''s Day'), ('2026-01-06', N'Epiphany'), ('2026-04-03', N'Good Friday'), ('2026-04-06', N'Easter Monday'),
    ('2026-05-01', N'May Day'), ('2026-05-14', N'Ascension Day'), ('2026-06-19', N'Midsummer Eve'), ('2026-12-24', N'Christmas Eve'),
    ('2026-12-25', N'Christmas Day'), ('2026-12-26', N'Boxing Day'), ('2026-12-06', N'Independence Day'),
    ('2027-01-01', N'New Year''s Day'), ('2027-01-06', N'Epiphany'), ('2027-03-26', N'Good Friday'), ('2027-03-29', N'Easter Monday'))
    AS s (HolidayDate, Name)
ON t.HolidayDate = s.HolidayDate
WHEN NOT MATCHED THEN INSERT (HolidayDate, Name) VALUES (s.HolidayDate, s.Name);
GO

IF NOT EXISTS (SELECT 1 FROM stm.Customer)
BEGIN
    INSERT stm.Customer (CustomerNo, Name, AddressLine1, AddressLine2, PostalCode, City, CountryCode, Email, Language, Delivery, DropFolder, CreditController, CreditLimit, OnHold)
    VALUES
    ('C10001', N'Kaleva Rakennus Oy',            N'Hermiankatu 6',        NULL,                 N'33720', N'Tampere',   'FI', N'reskontra@kalevarakennus.fi',     'fi-FI', 1, NULL,                         N'Anna Virtanen', 90000, 0),
    ('C10002', N'Nordic Freight Partners AB',    N'Industrivägen 14',     NULL,                 N'41505', N'Göteborg',  'SE', N'ap@nordicfreight.se',             'sv-SE', 1, NULL,                         N'Erik Lindqvist', 60000, 0),
    ('C10003', N'Helsingin Hammasklinikat Oy',   N'Mannerheimintie 12 B', N'4. krs',            N'00100', N'Helsinki',  'FI', NULL,                               'fi-FI', 3, NULL,                         N'Anna Virtanen', 40000, 0),
    ('C10004', N'Tampereen Painotalo Oy',        N'Lempääläntie 21',      NULL,                 N'33820', N'Tampere',   'FI', N'talous@painotalo.fi',             'fi-FI', 2, N'C10004 Tampereen Painotalo', N'Mikko Laine',   50000, 0),
    ('C10005', N'Oulu Arena Events Oy',          N'Kiikelinkatu 3',       NULL,                 N'90100', N'Oulu',      'FI', N'laskut@oulu-arena.fi',            'en-GB', 1, NULL,                         N'Mikko Laine',   25000, 0),
    ('C10006', N'Baltic Cold Chain AS',          N'Peterburi tee 46',     NULL,                 N'11415', N'Tallinn',   'EE', N'finance@balticcold.ee',           'en-GB', 1, NULL,                         N'Erik Lindqvist', 45000, 0),
    ('C10007', N'Lahden Pakkaus Oy',             N'Ahjokatu 9',           NULL,                 N'15800', N'Lahti',     'FI', NULL,                               'fi-FI', 3, NULL,                         N'Anna Virtanen', 30000, 0),
    ('C10008', N'Espoon Kuvantamiskeskus Oy',    N'Keilaranta 1',         NULL,                 N'02150', N'Espoo',     'FI', N'ostolaskut@kuvantaminen.fi',      'fi-FI', 1, NULL,                         N'Mikko Laine',  120000, 0),
    ('C10009', N'Åbo Marinservice Ab',           N'Linnankatu 72',        NULL,                 N'20100', N'Turku',     'FI', N'ekonomi@abomarin.fi',             'sv-SE', 2, N'C10009 Abo Marinservice',   N'Erik Lindqvist', 35000, 0),
    ('C10010', N'Göteborgs Kontorslösningar AB', N'Lindholmspiren 7',     NULL,                 N'41756', N'Göteborg',  'SE', NULL,                               'sv-SE', 3, NULL,                         N'Erik Lindqvist', 20000, 0),
    ('C10011', N'Jyväskylän Agritekniikka Oy',   N'Ohjelmakaari 10',      NULL,                 N'40500', N'Jyväskylä', 'FI', N'kirjanpito@agritek.fi',           'fi-FI', 1, NULL,                         N'Anna Virtanen', 15000, 1),
    ('C10012', N'Vantaan Logistiikkakeskus Oy',  N'Tikkurilantie 140',    NULL,                 N'01530', N'Vantaa',    'FI', N'talous@vlk.fi',                   'fi-FI', 9, NULL,                         NULL,            80000, 0);
END
GO

IF NOT EXISTS (SELECT 1 FROM stm.LeaseContract)
BEGIN
    INSERT stm.LeaseContract (ContractNo, CustomerId, AssetCategory, AssetDescription, StartDate, EndDate, MonthlyRent, AssetValue, ResidualValue, Status)
    SELECT v.ContractNo, c.CustomerId, v.AssetCategory, v.AssetDescription, v.StartDate, v.EndDate, v.MonthlyRent, v.AssetValue, v.ResidualValue, v.Status
      FROM (VALUES
        ('L-2023-0412', 'C10001', N'Construction machinery', N'Tracked excavator 22 t, s/n EX22-77812',        '2023-04-01', '2028-03-31', 3150.00, 168000.00, 25000.00, 'ACTIVE'),
        ('L-2024-0107', 'C10001', N'Construction machinery', N'Wheel loader 14 t, s/n WL14-30219',             '2024-01-15', '2029-01-14', 2240.00, 121000.00, 18000.00, 'ACTIVE'),
        ('L-2025-0618', 'C10001', N'Vehicles',               N'Van 3.5 t, 4 units, reg. KRL-881..884',         '2025-06-01', '2029-05-31', 2680.00, 132000.00, 30000.00, 'ACTIVE'),
        ('L-2024-0330', 'C10002', N'Vehicles',               N'Tractor unit 6x2, reg. NFP 412',                '2024-03-01', '2029-02-28', 2390.00, 128000.00, 22000.00, 'ACTIVE'),
        ('L-2025-0201', 'C10002', N'Forklifts and warehouse',N'Reach truck 2.0 t, 3 units',                    '2025-02-01', '2030-01-31', 1470.00,  79000.00,  9000.00, 'ACTIVE'),
        ('L-2022-1103', 'C10003', N'Medical equipment',      N'Dental units, 6 chairs incl. imaging',          '2022-11-01', '2027-10-31', 2920.00, 155000.00,  5000.00, 'ACTIVE'),
        ('L-2024-0901', 'C10004', N'Printing equipment',     N'Sheet-fed offset press B2, 5 colours',          '2024-09-01', '2031-08-31', 4870.00, 340000.00, 40000.00, 'ACTIVE'),
        ('L-2026-0301', 'C10004', N'IT equipment',           N'Prepress workstations and RIP servers',         '2026-03-01', '2029-02-28',  690.00,  23500.00,     0.00, 'ACTIVE'),
        ('L-2025-0815', 'C10005', N'IT equipment',           N'LED video wall 24 m2 with controllers',         '2025-08-15', '2029-08-14', 1860.00,  82000.00,  6000.00, 'ACTIVE'),
        ('L-2025-0410', 'C10006', N'Refrigeration',          N'Refrigerated semi-trailer, 2 units',            '2025-04-01', '2030-03-31', 2150.00, 118000.00, 20000.00, 'ACTIVE'),
        ('L-2023-0705', 'C10007', N'Forklifts and warehouse',N'Counterbalance forklift 3.0 t, 2 units',        '2023-07-01', '2027-06-30',  980.00,  52000.00,  6000.00, 'ACTIVE'),
        ('L-2024-1201', 'C10007', N'Printing equipment',     N'Flexo label line',                               '2024-12-01', '2030-11-30', 1720.00, 105000.00, 10000.00, 'ACTIVE'),
        ('L-2023-0201', 'C10008', N'Medical equipment',      N'MRI scanner 1.5 T incl. service contract',      '2023-02-01', '2030-01-31', 9840.00, 690000.00, 60000.00, 'ACTIVE'),
        ('L-2025-1001', 'C10008', N'Medical equipment',      N'Ultrasound systems, 4 units',                    '2025-10-01', '2030-09-30', 1630.00,  88000.00,  4000.00, 'ACTIVE'),
        ('L-2024-0515', 'C10009', N'Vehicles',               N'Workboat 9 m with outboard engines',            '2024-05-15', '2031-05-14', 1290.00,  86000.00, 15000.00, 'ACTIVE'),
        ('L-2025-0301', 'C10010', N'IT equipment',           N'Multifunction printers, 18 units',              '2025-03-01', '2029-02-28',  760.00,  31000.00,     0.00, 'ACTIVE'),
        ('L-2024-0601', 'C10011', N'Construction machinery', N'Compact tractor with front loader',             '2024-06-01', '2029-05-31',  890.00,  47000.00,  7000.00, 'ACTIVE'),
        ('L-2025-0701', 'C10011', N'IT equipment',           N'Greenhouse climate control system',             '2025-07-01', '2030-06-30',  540.00,  27000.00,     0.00, 'ACTIVE'),
        ('L-2023-0901', 'C10012', N'Forklifts and warehouse',N'Automated pallet racking shuttles',             '2023-09-01', '2030-08-31', 5320.00, 380000.00, 35000.00, 'ACTIVE'),
        ('L-2021-0301', 'C10012', N'Vehicles',               N'Terminal tractors, 2 units',                     '2021-03-01', '2026-02-28', 1450.00,  76000.00,  8000.00, 'ENDED')
      ) AS v (ContractNo, CustomerNo, AssetCategory, AssetDescription, StartDate, EndDate, MonthlyRent, AssetValue, ResidualValue, Status)
      JOIN stm.Customer c ON c.CustomerNo = v.CustomerNo;
END
GO

-- Invoices: one rent invoice per contract and month (1st of the month, due on the 14th), vehicles with an insurance line.
IF NOT EXISTS (SELECT 1 FROM stm.Invoice)
BEGIN
    DECLARE @month date = '2026-01-01';
    WHILE @month <= '2026-10-01'
    BEGIN
        INSERT stm.Invoice (InvoiceNo, CustomerId, ContractNo, InvoiceDate, DueDate, PeriodFrom, PeriodTo, NetAmount, VatAmount, TotalAmount, PaymentReference, CustomerReference)
        SELECT 'T' + lc.ContractNo + FORMAT(@month, 'yyyyMM'), lc.CustomerId, lc.ContractNo, @month, DATEADD(DAY, 13, @month), @month, EOMONTH(@month),
               lc.MonthlyRent + CASE WHEN lc.AssetCategory = N'Vehicles' THEN 45.00 ELSE 0 END,
               CASE WHEN c.CountryCode = 'FI' THEN ROUND(lc.MonthlyRent * 0.255, 2) ELSE 0 END,
               lc.MonthlyRent + CASE WHEN lc.AssetCategory = N'Vehicles' THEN 45.00 ELSE 0 END
                              + CASE WHEN c.CountryCode = 'FI' THEN ROUND(lc.MonthlyRent * 0.255, 2) ELSE 0 END,
               '', CASE WHEN c.CustomerNo = 'C10008' THEN N'PO 4500-' + RIGHT(lc.ContractNo, 4) END
          FROM stm.LeaseContract lc
          JOIN stm.Customer c ON c.CustomerId = lc.CustomerId
         WHERE lc.StartDate <= EOMONTH(@month) AND lc.EndDate >= @month
         ORDER BY lc.ContractNo;
        SET @month = DATEADD(MONTH, 1, @month);
    END

    -- Invoice numbers 70100001.. and Finnish creditor references (number + 7-3-1 check digit).
    ;WITH n AS (SELECT InvoiceId, 70100000 + ROW_NUMBER() OVER (ORDER BY InvoiceDate, ContractNo) AS Num FROM stm.Invoice)
    UPDATE i SET InvoiceNo = CAST(n.Num AS varchar(20)),
                 PaymentReference = CAST(n.Num AS varchar(20)) + CAST((10 - (
                       7 * CAST(SUBSTRING(CAST(n.Num AS char(8)), 8, 1) AS int) + 3 * CAST(SUBSTRING(CAST(n.Num AS char(8)), 7, 1) AS int)
                     + 1 * CAST(SUBSTRING(CAST(n.Num AS char(8)), 6, 1) AS int) + 7 * CAST(SUBSTRING(CAST(n.Num AS char(8)), 5, 1) AS int)
                     + 3 * CAST(SUBSTRING(CAST(n.Num AS char(8)), 4, 1) AS int) + 1 * CAST(SUBSTRING(CAST(n.Num AS char(8)), 3, 1) AS int)
                     + 7 * CAST(SUBSTRING(CAST(n.Num AS char(8)), 2, 1) AS int) + 3 * CAST(SUBSTRING(CAST(n.Num AS char(8)), 1, 1) AS int)
                   ) % 10) % 10 AS varchar(1))
      FROM stm.Invoice i JOIN n ON n.InvoiceId = i.InvoiceId;

    INSERT stm.InvoiceLine (InvoiceId, LineNumber, Description, Quantity, UnitPrice, VatRate, NetAmount, VatAmount)
    SELECT i.InvoiceId, 1, N'Lease rent ' + FORMAT(i.PeriodFrom, 'dd.MM.yyyy') + N'-' + FORMAT(i.PeriodTo, 'dd.MM.yyyy') + N', ' + lc.AssetDescription,
           1, lc.MonthlyRent, CASE WHEN c.CountryCode = 'FI' THEN 25.5 ELSE 0 END, lc.MonthlyRent, i.VatAmount
      FROM stm.Invoice i
      JOIN stm.LeaseContract lc ON lc.ContractNo = i.ContractNo
      JOIN stm.Customer c ON c.CustomerId = i.CustomerId;

    INSERT stm.InvoiceLine (InvoiceId, LineNumber, Description, Quantity, UnitPrice, VatRate, NetAmount, VatAmount)
    SELECT i.InvoiceId, 2, N'Equipment insurance (VAT exempt)', 1, 45.00, 0, 45.00, 0
      FROM stm.Invoice i
      JOIN stm.LeaseContract lc ON lc.ContractNo = i.ContractNo
     WHERE lc.AssetCategory = N'Vehicles';
END
GO

-- Ledger: invoices, payments by customer payment behaviour, plus a credit note, interest, a partial payment and an unallocated payment.
IF NOT EXISTS (SELECT 1 FROM stm.LedgerEntry)
BEGIN
    DECLARE @ledgerDate date = '2026-10-27';
    DECLARE @behaviour TABLE (CustomerNo varchar(12) PRIMARY KEY, DelayDays int, StopAfter date NULL);
    INSERT @behaviour VALUES
        ('C10001', -2, NULL), ('C10002', 3, NULL), ('C10003', 0, NULL), ('C10004', 9, NULL), ('C10005', 38, NULL), ('C10006', 1, NULL),
        ('C10007', 4, '2026-06-30'), ('C10008', 0, NULL), ('C10009', 21, NULL), ('C10010', 12, '2026-08-31'), ('C10011', 25, '2026-04-30'), ('C10012', 0, NULL);

    DECLARE @pay TABLE (InvoiceId int PRIMARY KEY, PayDate date);
    INSERT @pay (InvoiceId, PayDate)
    SELECT i.InvoiceId, DATEADD(DAY, b.DelayDays, i.DueDate)
      FROM stm.Invoice i
      JOIN stm.Customer c ON c.CustomerId = i.CustomerId
      JOIN @behaviour b ON b.CustomerNo = c.CustomerNo
     WHERE DATEADD(DAY, b.DelayDays, i.DueDate) <= @ledgerDate
       AND (b.StopAfter IS NULL OR DATEADD(DAY, b.DelayDays, i.DueDate) <= b.StopAfter);

    INSERT stm.LedgerEntry (CustomerId, EntryType, DocumentNo, ContractNo, PostingDate, DueDate, Description, Amount, OpenAmount, ClosedDate, PaymentReference)
    SELECT i.CustomerId, 'INV', i.InvoiceNo, i.ContractNo, i.InvoiceDate, i.DueDate,
           N'Lease rent ' + FORMAT(i.PeriodFrom, 'MM/yyyy'), i.TotalAmount,
           CASE WHEN p.InvoiceId IS NULL THEN i.TotalAmount ELSE 0 END, p.PayDate, i.PaymentReference
      FROM stm.Invoice i
      LEFT JOIN @pay p ON p.InvoiceId = i.InvoiceId;

    INSERT stm.LedgerEntry (CustomerId, EntryType, DocumentNo, ContractNo, PostingDate, DueDate, Description, Amount, OpenAmount, ClosedDate, PaymentReference)
    SELECT i.CustomerId, 'PAY', 'P' + i.InvoiceNo, i.ContractNo, p.PayDate, NULL, N'Payment ' + i.PaymentReference, -i.TotalAmount, 0, p.PayDate, i.PaymentReference
      FROM stm.Invoice i
      JOIN @pay p ON p.InvoiceId = i.InvoiceId;

    -- Credit note for a damaged reach truck month (Nordic Freight), still open as a credit.
    INSERT stm.LedgerEntry (CustomerId, EntryType, DocumentNo, ContractNo, PostingDate, DueDate, Description, Amount, OpenAmount, ClosedDate, PaymentReference)
    SELECT CustomerId, 'CRN', '75100014', 'L-2025-0201', '2026-09-18', '2026-09-18', N'Credit note: rent reduction 08/2026, unit 2 out of service', -735.00, -735.00, NULL, NULL
      FROM stm.Customer WHERE CustomerNo = 'C10002';

    -- Late payment interest (Oulu Arena), open.
    INSERT stm.LedgerEntry (CustomerId, EntryType, DocumentNo, ContractNo, PostingDate, DueDate, Description, Amount, OpenAmount, ClosedDate, PaymentReference)
    SELECT CustomerId, 'INT', '78100003', NULL, '2026-09-30', '2026-10-14', N'Late payment interest Q3/2026', 112.60, 112.60, NULL, '781000031'
      FROM stm.Customer WHERE CustomerNo = 'C10005';

    -- Payment received without a reference (Baltic Cold Chain), unallocated.
    INSERT stm.LedgerEntry (CustomerId, EntryType, DocumentNo, ContractNo, PostingDate, DueDate, Description, Amount, OpenAmount, ClosedDate, PaymentReference)
    SELECT CustomerId, 'PAY', 'P20261020-07', NULL, '2026-10-20', NULL, N'Payment without reference, SEB 2026-10-20', -1200.00, -1200.00, NULL, NULL
      FROM stm.Customer WHERE CustomerNo = 'C10006';

    -- Partial payment of the last open invoice of Lahden Pakkaus.
    DECLARE @lastOpen varchar(20) = (SELECT TOP (1) e.DocumentNo FROM stm.LedgerEntry e JOIN stm.Customer c ON c.CustomerId = e.CustomerId
                                      WHERE c.CustomerNo = 'C10007' AND e.EntryType = 'INV' AND e.OpenAmount > 0 ORDER BY e.PostingDate DESC, e.DocumentNo DESC);
    UPDATE stm.LedgerEntry SET OpenAmount = OpenAmount - 1000.00 WHERE EntryType = 'INV' AND DocumentNo = @lastOpen;
    INSERT stm.LedgerEntry (CustomerId, EntryType, DocumentNo, ContractNo, PostingDate, DueDate, Description, Amount, OpenAmount, ClosedDate, PaymentReference)
    SELECT c.CustomerId, 'PAY', 'P' + @lastOpen + '-1', NULL, '2026-10-22', NULL, N'Partial payment', -1000.00, 0, '2026-10-22', NULL
      FROM stm.Customer c WHERE c.CustomerNo = 'C10007';

    INSERT stm.LedgerLoad (LoadedAt, LedgerDate, Entries, Source) SELECT '2026-10-28T02:15:00', @ledgerDate, COUNT(*), N'ERP AR export' FROM stm.LedgerEntry;
END
GO

IF NOT EXISTS (SELECT 1 FROM stm.Subscription)
BEGIN
    INSERT stm.Subscription (Name, ReportCode, Schedule, Parameters, OutputFormat, DeliveryMethod, Recipients, SharePath, FileNameTemplate, EmailSubject, Owner, Enabled, ModifiedBy)
    VALUES
    (N'Monthly customer statements', 'STATEMENT-RUN', 'MONTHLY WD3 06:00', N'PeriodStart={PrevMonthStart};StatementDate={PrevMonthEnd};MinimumBalance=0.01',
     'PDF', 'CUSTOMER', N'receivables@woodgrove-leasing.fi', N'$(StatementShare)', N'{CustomerNo}_{StatementDate:yyyyMMdd}.pdf', N'Account statement {StatementDate:dd.MM.yyyy}',
     N'CORP\anna.virtanen', 1, N'CORP\anna.virtanen'),
    (N'Aged debt for credit control', 'AGED-DEBT', 'WEEKLY MON 07:00', N'AsOf={Yesterday}',
     'EXCEL', 'EMAIL', N'credit.control@woodgrove-leasing.fi', NULL, N'AgedDebt_{RunDate:yyyyMMdd}.xlsx', N'Aged debt {RunDate:dd.MM.yyyy}',
     N'CORP\anna.virtanen', 1, N'CORP\anna.virtanen'),
    (N'Aged debt, Mikko Laine customers', 'AGED-DEBT', 'WORKDAYS 07:15', N'AsOf={Yesterday};CreditController=Mikko Laine',
     'PDF', 'EMAIL', N'mikko.laine@woodgrove-leasing.fi', NULL, N'AgedDebt_MLaine_{RunDate:yyyyMMdd}.pdf', N'Your overdue customers {RunDate:dd.MM.yyyy}',
     N'CORP\mikko.laine', 1, N'CORP\mikko.laine'),
    (N'Lease portfolio for management', 'PORTFOLIO', 'MONTHLY 1 07:30', N'AsOf={PrevMonthEnd}',
     'PDF', 'EMAIL', N'management@woodgrove-leasing.fi; controller@woodgrove-leasing.fi', NULL, N'Portfolio_{RunDate:yyyyMM}.pdf', N'Lease portfolio {RunDate:MM/yyyy}',
     N'CORP\cfo', 1, N'CORP\cfo'),
    (N'Daily ledger transactions to finance', 'TRANSACTIONS', 'WORKDAYS 05:30', N'From={Yesterday};To={Yesterday}',
     'EXCEL', 'SHARE', NULL, N'$(FinanceShare)\AR\Daily', N'AR_Transactions_{RunDate:yyyyMMdd}.xlsx', NULL,
     N'CORP\anna.virtanen', 1, N'CORP\anna.virtanen');
END
GO
