SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO
USE MeterBilling;
GO

-- Tariffs: domestic (DOM) and commercial (COM); prices change on 1 March 2026.
IF NOT EXISTS (SELECT 1 FROM bill.Tariff)
BEGIN
    INSERT bill.Tariff (Code, Name, ValidFrom, StandingChargePerDay, WastewaterPrice, WastewaterShare, VatRate) VALUES
        ('DOM', N'Domestic', '2025-01-01', 0.21370, 2.41000, 1.000, 25.50),
        ('DOM', N'Domestic', '2026-03-01', 0.22800, 2.56000, 1.000, 25.50),
        ('DOMNS', N'Domestic, no sewer connection', '2025-01-01', 0.21370, 0, 0, 25.50),
        ('COM', N'Commercial', '2025-01-01', 0.68500, 2.41000, 0.950, 25.50),
        ('COM', N'Commercial', '2026-03-01', 0.72900, 2.56000, 0.950, 25.50);

    INSERT bill.TariffBlock (TariffId, BlockNo, UpTo, Price)
    SELECT t.TariffId, b.BlockNo, b.UpTo, b.Price
    FROM bill.Tariff t
    CROSS APPLY (VALUES (1, CAST(10 AS decimal(9,3)), 1.38000), (2, 25, 1.71000), (3, NULL, 2.05000)) b (BlockNo, UpTo, Price)
    WHERE t.Code IN ('DOM', 'DOMNS') AND t.ValidFrom = '2025-01-01'
    UNION ALL
    SELECT t.TariffId, b.BlockNo, b.UpTo, b.Price
    FROM bill.Tariff t
    CROSS APPLY (VALUES (1, CAST(10 AS decimal(9,3)), 1.46000), (2, 25, 1.83000), (3, NULL, 2.19000)) b (BlockNo, UpTo, Price)
    WHERE t.Code = 'DOM' AND t.ValidFrom = '2026-03-01'
    UNION ALL
    SELECT t.TariffId, 1, NULL, CASE WHEN t.ValidFrom = '2025-01-01' THEN 1.52000 ELSE 1.61000 END
    FROM bill.Tariff t WHERE t.Code = 'COM';
END
GO

IF NOT EXISTS (SELECT 1 FROM bill.BillCycle)
    INSERT bill.BillCycle (CycleCode, Name, IntervalMonths, NextCutOff, ReadWindowDays, PaymentDays) VALUES
        ('N1', N'North, quarterly', 3, '2026-04-30', 21, 14),
        ('S1', N'South, quarterly', 3, '2026-05-31', 21, 14),
        ('COMM', N'Commercial, monthly', 1, '2026-04-30', 10, 21);
GO

-- Lab and test accounts (match the files in samples\).
IF NOT EXISTS (SELECT 1 FROM bill.Meter)
BEGIN
    INSERT bill.Meter (Serial, Dials, Multiplier, SizeCode, InstalledOn) VALUES
        ('W19A004417', 5, 1, 'DN20', '2019-05-14'),
        ('W19A004581', 5, 1, 'DN20', '2019-05-14'),
        ('W21B110230', 5, 1, 'DN20', '2021-09-02'),
        ('W17C000912', 4, 1, 'DN15', '2017-03-28'),
        ('W22B330871', 5, 1, 'DN20', '2022-06-20'),
        ('W20K700045', 6, 1, 'DN40', '2020-11-11'),
        ('W23K700310', 6, 10, 'DN80', '2023-02-01');

    INSERT bill.MeterRead (MeterSerial, ReadDate, Reading, ReadType, Source, ReaderCode, Status) VALUES
        ('W19A004417', '2026-01-28', 1832.4, 0, 0, '00', 1),
        ('W19A004581', '2026-01-28', 977.0, 0, 0, '00', 1),
        ('W21B110230', '2026-01-29', 452.8, 0, 0, '00', 1),
        ('W17C000912', '2026-01-29', 9985.0, 0, 0, '00', 1),
        ('W22B330871', '2026-01-30', 315.6, 2, 3, 'EST', 1),
        ('W20K700045', '2026-03-31', 48211.7, 0, 1, '00', 1),
        ('W23K700310', '2026-03-31', 7710.2, 0, 1, '00', 1);

    INSERT bill.Account (AccountNumber, CustomerName, AddressLine1, AddressLine2, PostalCode, City, TariffCode, CycleCode, MeterSerial,
                         PaymentMethod, Iban, MandateId, MandateSignedOn, FirstCollection, AverageDailyUse, LastBilledReadId)
    SELECT v.AccountNumber, v.CustomerName, v.AddressLine1, v.AddressLine2, v.PostalCode, v.City, v.TariffCode, v.CycleCode, v.MeterSerial,
           v.PaymentMethod, v.Iban, v.MandateId, v.MandateSignedOn, v.FirstCollection, v.AverageDailyUse, r.ReadId
    FROM (VALUES
        ('10042117', N'Anna Lindqvist', N'Harbour Road 14', NULL, '20100', N'Northport', 'DOM', 'N1', 'W19A004417', 1, 'FI2112345600000785', 'CW-10042117', CAST('2019-06-01' AS date), 0, 0.3400),
        ('10042581', N'Mikko and Sari Ojala', N'Harbour Road 18', NULL, '20100', N'Northport', 'DOM', 'N1', 'W19A004581', 0, NULL, NULL, NULL, 1, 0.2100),
        ('10051023', N'Jonas Bergström', N'Mill Lane 3 B 7', NULL, '20140', N'Northport', 'DOM', 'N1', 'W21B110230', 1, 'FI5542345670000081', 'CW-10051023', '2025-11-12', 1, 0.1600),
        ('10050912', N'Eeva Mäkelä', N'Old Quarry Road 51', N'c/o Pekka Mäkelä', '20160', N'Northport', 'DOMNS', 'N1', 'W17C000912', 0, NULL, NULL, NULL, 1, 0.2800),
        ('10063087', N'Leena Koski', N'Birch Avenue 9', NULL, '20200', N'Northport', 'DOM', 'N1', 'W22B330871', 1, 'FI4250001510000023', 'CW-10063087', '2022-07-01', 0, 0.4500),
        ('20000045', N'Northport Laundry Oy', N'Dock Street 2', NULL, '20100', N'Northport', 'COM', 'COMM', 'W20K700045', 0, NULL, NULL, NULL, 1, 38.0000),
        ('20000310', N'Coho Brewing Company', N'Brewery Yard 1', N'Accounts payable', '20180', N'Northport', 'COM', 'COMM', 'W23K700310', 1, 'FI1410093000123458', 'CW-20000310', '2023-02-15', 0, 61.5000)
    ) v (AccountNumber, CustomerName, AddressLine1, AddressLine2, PostalCode, City, TariffCode, CycleCode, MeterSerial,
         PaymentMethod, Iban, MandateId, MandateSignedOn, FirstCollection, AverageDailyUse)
    JOIN bill.MeterRead r ON r.MeterSerial = v.MeterSerial;
END
GO
