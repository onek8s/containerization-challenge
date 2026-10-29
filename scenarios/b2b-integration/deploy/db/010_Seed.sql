/*
    VanArsdelIntegration database: trading partners, cross references, SLA definitions, notification groups and
    holidays. Generated from samples/seed/*.json (the same data the lab tool loads); re-running updates rows in
    place and leaves partners and cross references added through the operations portal alone.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

MERGE dbo.TradingPartner AS t
USING (VALUES
    (N'C1001', N'Hartwell Electrical Supply', 1, 1, 1, N'ZZ', N'HARTWELL', N'00401', NULL, N'104711', N'HARTWELL-AS2', N'USD', N'US', N'UPSGND', 1, 240, 25000, 1, 0, 1),
    (N'C1002', N'Brightline Facilities', 1, 1, 2, N'01', N'078123456', N'00401', NULL, N'104822', NULL, N'USD', N'US', N'FDXG', 1, 480, 10000, 1, 0, 0),
    (N'C2001', N'Bauwerk Haustechnik GmbH', 1, 2, 1, N'14', N'4012345000009', N'D96A', N'4012345000009', N'200310', N'BAUWERK-EDI', N'EUR', N'DE', N'DHLFR', 1, 240, 20000, 1, 0, 1),
    (N'C2002', N'Nordvik Byggvarer AS', 1, 2, 3, N'14', N'7023456000007', N'D96A', N'7023456000007', N'200455', NULL, N'NOK', N'NO', N'DSV', 0, 0, 150000, 1, 0, 0),
    (N'C3001', N'Ridgeway Mechanical', 1, 3, 4, NULL, NULL, NULL, NULL, N'104930', NULL, N'USD', N'US', N'BRANCH', 0, 0, 5000, 1, 0, 0),
    (N'C3002', N'Lakeshore Electric LLC', 1, 3, 4, NULL, NULL, NULL, NULL, N'104955', NULL, N'USD', N'US', N'UPSGND', 0, 0, 5000, 1, 0, 0),
    (N'S5001', N'Kestrel Cable Co Ltd', 2, 2, 2, N'14', N'5034567000000', N'D96A', N'5034567000000', N'300120', NULL, N'EUR', N'GB', NULL, 1, 1440, 0, 1, 0, 0),
    (N'S5002', N'Pellham Valves Inc', 2, 1, 1, N'ZZ', N'PELLHAMVLV', N'00401', NULL, N'300245', N'PELLHAM', N'USD', N'US', NULL, 1, 1440, 0, 1, 0, 1)
) AS s (PartnerId, Name, RoleId, StandardId, TransportId, InterchangeQualifier, InterchangeId, StandardVersion, Gln, ErpNumber, As2Identifier, CurrencyCode, CountryCode, DefaultShipVia, AckRequired, AckTimeoutMinutes, CreditCheckThreshold, Active, TestIndicator, ExpectsDailyTraffic)
ON t.PartnerId = s.PartnerId
WHEN MATCHED THEN UPDATE SET
    Name = s.Name,
    RoleId = s.RoleId,
    StandardId = s.StandardId,
    TransportId = s.TransportId,
    InterchangeQualifier = s.InterchangeQualifier,
    InterchangeId = s.InterchangeId,
    StandardVersion = s.StandardVersion,
    Gln = s.Gln,
    ErpNumber = s.ErpNumber,
    As2Identifier = s.As2Identifier,
    CurrencyCode = s.CurrencyCode,
    CountryCode = s.CountryCode,
    DefaultShipVia = s.DefaultShipVia,
    AckRequired = s.AckRequired,
    AckTimeoutMinutes = s.AckTimeoutMinutes,
    CreditCheckThreshold = s.CreditCheckThreshold,
    TestIndicator = s.TestIndicator,
    ExpectsDailyTraffic = s.ExpectsDailyTraffic,
    ChangedBy = N'010_Seed.sql', ChangedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (PartnerId, Name, RoleId, StandardId, TransportId, InterchangeQualifier, InterchangeId, StandardVersion, Gln, ErpNumber, As2Identifier, CurrencyCode, CountryCode, DefaultShipVia, AckRequired, AckTimeoutMinutes, CreditCheckThreshold, Active, TestIndicator, ExpectsDailyTraffic, ChangedBy)
    VALUES (s.PartnerId, s.Name, s.RoleId, s.StandardId, s.TransportId, s.InterchangeQualifier, s.InterchangeId, s.StandardVersion, s.Gln, s.ErpNumber, s.As2Identifier, s.CurrencyCode, s.CountryCode, s.DefaultShipVia, s.AckRequired, s.AckTimeoutMinutes, s.CreditCheckThreshold, s.Active, s.TestIndicator, s.ExpectsDailyTraffic, N'010_Seed.sql');

MERGE dbo.PartnerXref AS t
USING (VALUES
    (N'*', 1, N'00614141100200', N'EL-10020', NULL, NULL),
    (N'*', 1, N'00614141100323', N'EL-10032', NULL, NULL),
    (N'*', 1, N'00614141112401', N'EL-11240', NULL, NULL),
    (N'*', 1, N'00614141204106', N'EL-20410', NULL, NULL),
    (N'*', 1, N'00614141301157', N'EL-30115', NULL, NULL),
    (N'*', 1, N'00614141302208', N'EL-30220', NULL, NULL),
    (N'*', 1, N'00614141501250', N'CB-50125', NULL, NULL),
    (N'*', 1, N'00614141503254', N'CB-50325', NULL, NULL),
    (N'*', 1, N'00614141516100', N'CB-51610', NULL, NULL),
    (N'*', 1, N'00614141600151', N'PL-60015', NULL, NULL),
    (N'*', 1, N'00614141600229', N'PL-60022', NULL, NULL),
    (N'*', 1, N'00614141610501', N'PL-61050', NULL, NULL),
    (N'*', 1, N'00614141620166', N'PL-62016', NULL, NULL),
    (N'*', 1, N'00614141701209', N'HV-70120', NULL, NULL),
    (N'*', 1, N'00614141715008', N'HV-71500', NULL, NULL),
    (N'*', 1, N'00614141720101', N'HV-72010', NULL, NULL),
    (N'*', 1, N'00614141800100', N'HZ-80010', NULL, NULL),
    (N'*', 1, N'00614141802203', N'HZ-80220', NULL, NULL),
    (N'*', 1, N'00614141900015', N'PK-90001', NULL, NULL),
    (N'*', 1, N'00614141900121', N'PK-90012', NULL, NULL),
    (N'C1001', 1, N'HW-88231', N'EL-10020', NULL, NULL),
    (N'C1001', 1, N'HW-88240', N'EL-10032', NULL, NULL),
    (N'C1001', 1, N'HW-90115', N'EL-30115', NULL, NULL),
    (N'C1001', 1, N'HW-77125', N'CB-50125', NULL, NULL),
    (N'C1001', 1, N'HW-77325', N'CB-50325', NULL, NULL),
    (N'C1001', 1, N'HW-66801', N'HZ-80220', NULL, NULL),
    (N'C1001', 1, N'HW-77999', N'CB-51610', N'2015-01-01', N'2017-12-31'),
    (N'C1001', 3, N'0042', N'604201', NULL, NULL),
    (N'C1001', 3, N'0057', N'604202', NULL, NULL),
    (N'C1001', 3, N'0101', N'604203', NULL, NULL),
    (N'C1001', 5, N'UPSGND', N'UPSN', NULL, NULL),
    (N'C1001', 5, N'FDXG', N'FDEG', NULL, NULL),
    (N'C1002', 1, N'BL-1020', N'EL-10020', NULL, NULL),
    (N'C1002', 1, N'BL-3115', N'EL-30115', NULL, NULL),
    (N'C1002', 1, N'BL-6015', N'PL-60015', NULL, NULL),
    (N'C1002', 1, N'BL-7012', N'HV-70120', NULL, NULL),
    (N'C1002', 3, N'DAYTON-01', N'604301', NULL, NULL),
    (N'C1002', 3, N'AKRON-02', N'604302', NULL, NULL),
    (N'C1002', 5, N'FDXG', N'FDEG', NULL, NULL),
    (N'C1002', 2, N'EA', N'ST', NULL, NULL),
    (N'C2001', 1, N'4711020', N'EL-10020', NULL, NULL),
    (N'C2001', 1, N'4711240', N'EL-11240', NULL, NULL),
    (N'C2001', 1, N'5500150', N'PL-60015', NULL, NULL),
    (N'C2001', 1, N'5500220', N'PL-60022', NULL, NULL),
    (N'C2001', 1, N'6100500', N'HV-71500', NULL, NULL),
    (N'C2001', 3, N'4012345000108', N'704101', NULL, NULL),
    (N'C2001', 3, N'4012345000115', N'704102', NULL, NULL),
    (N'C2001', 5, N'DHLFR', N'DHL', NULL, NULL),
    (N'C2002', 1, N'NB-220410', N'EL-20410', NULL, NULL),
    (N'C2002', 1, N'NB-350325', N'CB-50325', NULL, NULL),
    (N'C2002', 1, N'NB-610500', N'PL-61050', NULL, NULL),
    (N'C2002', 3, N'7023456000014', N'704201', NULL, NULL),
    (N'C2002', 5, N'DSV', N'DSV', NULL, NULL),
    (N'C3001', 2, N'coil', N'ROL', NULL, NULL),
    (N'S5001', 1, N'KC-NYM315', N'CB-50125', NULL, NULL),
    (N'S5001', 1, N'KC-NYM525', N'CB-50325', NULL, NULL),
    (N'S5001', 1, N'KC-NYY516', N'CB-51610', NULL, NULL),
    (N'S5002', 1, N'PV-2050-BR', N'PL-61050', NULL, NULL),
    (N'S5002', 1, N'PV-TRV15', N'HV-70120', NULL, NULL)
) AS s (PartnerId, KindId, PartnerValue, InternalValue, ValidFrom, ValidTo)
ON t.PartnerId = s.PartnerId AND t.KindId = s.KindId AND t.PartnerValue = s.PartnerValue
WHEN MATCHED AND (t.InternalValue <> s.InternalValue OR ISNULL(t.ValidFrom, '19000101') <> ISNULL(s.ValidFrom, '19000101') OR ISNULL(t.ValidTo, '99991231') <> ISNULL(s.ValidTo, '99991231')) THEN
    UPDATE SET InternalValue = s.InternalValue, ValidFrom = s.ValidFrom, ValidTo = s.ValidTo, ChangedBy = N'010_Seed.sql', ChangedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (PartnerId, KindId, PartnerValue, InternalValue, ValidFrom, ValidTo, ChangedBy)
    VALUES (s.PartnerId, s.KindId, s.PartnerValue, s.InternalValue, s.ValidFrom, s.ValidTo, N'010_Seed.sql');

MERGE dbo.SlaDefinition AS t
USING (VALUES
    (N'ACK-OVERDUE', 1, N'No 997/CONTRL within the partner''s ack window', NULL, 0, 0, 2, 0, N'Integration', 1),
    (N'ORDER-ERP-60', 2, N'Customer order not in the ERP after 60 business minutes', NULL, 60, 0, 3, 1, N'CustomerService', 1),
    (N'CREDIT-HOLD-4H', 3, N'Order on credit hold for more than 4 business hours', NULL, 240, 0, 2, 1, N'Credit', 1),
    (N'SUSPENDED-5', 4, N'Five or more suspended instances of one service', NULL, 0, 5, 2, 0, N'Integration', 1),
    (N'HOST-DOWN', 5, N'A host instance is not running', NULL, 0, 0, 3, 0, N'Integration', 1),
    (N'SILENT-C1001', 6, N'Hartwell sends orders every working day', N'C1001', 480, 0, 1, 1, N'CustomerService', 1),
    (N'ASN-30', 7, N'Despatch confirmed by the ERP but no ASN after 30 minutes', NULL, 30, 0, 2, 0, N'CustomerService', 1)
) AS s (Code, KindId, Description, PartnerId, ThresholdMinutes, ThresholdCount, SeverityId, BusinessHoursOnly, NotifyGroup, Enabled)
ON t.Code = s.Code
WHEN MATCHED THEN UPDATE SET KindId = s.KindId, Description = s.Description, PartnerId = s.PartnerId, ThresholdMinutes = s.ThresholdMinutes,
    ThresholdCount = s.ThresholdCount, SeverityId = s.SeverityId, BusinessHoursOnly = s.BusinessHoursOnly, NotifyGroup = s.NotifyGroup
WHEN NOT MATCHED THEN INSERT (Code, KindId, Description, PartnerId, ThresholdMinutes, ThresholdCount, SeverityId, BusinessHoursOnly, NotifyGroup, Enabled)
    VALUES (s.Code, s.KindId, s.Description, s.PartnerId, s.ThresholdMinutes, s.ThresholdCount, s.SeverityId, s.BusinessHoursOnly, s.NotifyGroup, s.Enabled);

MERGE dbo.NotifyGroup AS t
USING (VALUES
    (N'CustomerService', N'customer.service@vanarsdel.com'),
    (N'Credit', N'credit.control@vanarsdel.com'),
    (N'Integration', N'integration.support@vanarsdel.com'),
    (N'Purchasing', N'purchasing@vanarsdel.com')
) AS s (GroupName, MailAddresses)
ON t.GroupName = s.GroupName
WHEN NOT MATCHED THEN INSERT (GroupName, MailAddresses) VALUES (s.GroupName, s.MailAddresses);

-- distribution centre calendar (order cut-off and business-hours SLAs); maintained by customer service each autumn
MERGE dbo.Holiday AS t
USING (VALUES
    ('20260101', N'New Year''s Day'),
    ('20260525', N'Memorial Day'),
    ('20260703', N'Independence Day (observed)'),
    ('20260907', N'Labor Day'),
    ('20261126', N'Thanksgiving Day'),
    ('20261127', N'Day after Thanksgiving'),
    ('20261225', N'Christmas Day'),
    ('20270101', N'New Year''s Day'),
    ('20270531', N'Memorial Day'),
    ('20270705', N'Independence Day (observed)'),
    ('20270906', N'Labor Day'),
    ('20271125', N'Thanksgiving Day'),
    ('20271126', N'Day after Thanksgiving'),
    ('20271224', N'Christmas Day (observed)')
) AS s (HolidayDate, Name)
ON t.HolidayDate = s.HolidayDate
WHEN NOT MATCHED THEN INSERT (HolidayDate, Name) VALUES (s.HolidayDate, s.Name);

COMMIT TRANSACTION;
GO
