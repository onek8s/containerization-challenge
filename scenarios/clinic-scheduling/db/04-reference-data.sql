-- Southridge Clinic Systems: reference data (sites, providers, appointment types, payers, codes, fee schedule,
-- schedule templates, settings, API clients). Safe to run once on an empty database.
USE SouthridgeClinic;
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

INSERT dbo.NumberSequence (Name, NextValue) VALUES ('MRN', 1045230), ('Claim', 200000), ('Interchange', 1001), ('LabOrder', 500000), ('Referral', 13000);

SET IDENTITY_INSERT dbo.Site ON;
INSERT dbo.Site (SiteId, Code, Name, Address1, City, State, Zip, Phone, Fax, PlaceOfService, TaxId, GroupNpi, OpensAt, ClosesAt, OnlineBooking) VALUES
 (1, 'NTH', N'Northgate Family Medicine',   N'1200 Northgate Way',      N'Tacoma',      'WA', '98403', '2535550110', '2535550111', '11', '911234567', '1122334455', '07:30', '18:00', 1),
 (2, 'LKW', N'Lakewood Pediatrics',         N'5810 Lakewood Towne Ctr', N'Lakewood',    'WA', '98499', '2535550120', '2535550121', '11', '911234567', '1122334455', '08:00', '17:00', 1),
 (3, 'TAC', N'Southridge Heart Center',     N'915 Pacific Ave Ste 300', N'Tacoma',      'WA', '98402', '2535550130', '2535550131', '11', '911234567', '1122334455', '08:00', '17:00', 0),
 (4, 'PUY', N'Puyallup Orthopedics',        N'402 S Meridian',          N'Puyallup',    'WA', '98371', '2535550140', '2535550141', '11', '911234567', '1122334455', '08:00', '17:00', 0),
 (5, 'FED', N'Federal Way Family Medicine', N'31620 23rd Ave S',        N'Federal Way', 'WA', '98003', '2535550150', '2535550151', '11', '911234567', '1122334455', '08:00', '17:30', 1),
 (6, 'IMG', N'Southridge Imaging',          N'925 Pacific Ave',         N'Tacoma',      'WA', '98402', '2535550160', '2535550161', '22', '911234567', '1122334455', '07:00', '19:00', 0);
SET IDENTITY_INSERT dbo.Site OFF;

INSERT dbo.Specialty (SpecialtyCode, Name, TaxonomyCode, RequiresReferral) VALUES
 ('FM',   N'Family Medicine',     '207Q00000X', 0),
 ('PED',  N'Pediatrics',          '208000000X', 0),
 ('CARD', N'Cardiology',          '207RC0000X', 1),
 ('ORTH', N'Orthopaedic Surgery', '207X00000X', 1),
 ('RAD',  N'Diagnostic Radiology','2085R0202X', 1);

SET IDENTITY_INSERT dbo.Provider ON;
INSERT dbo.Provider (ProviderId, Npi, FirstName, LastName, Credentials, SpecialtyCode, DefaultSiteId, UserName, AcceptsNewPatients, OnlineBooking) VALUES
 (1,  '1234567893', N'Maria',    N'Alvarez',   'MD', 'FM',   1, 'malvarez',  1, 1),
 (2,  '1246789022', N'David',    N'Chen',      'MD', 'FM',   1, 'dchen',     0, 1),
 (3,  '1335792465', N'Rachel',   N'Okafor',    'DO', 'FM',   5, 'rokafor',   1, 1),
 (4,  '1452369873', N'Thomas',   N'Lindqvist', 'PA-C','FM',  5, 'tlindqvist',1, 1),
 (5,  '1567890128', N'Priya',    N'Raman',     'MD', 'PED',  2, 'praman',    1, 1),
 (6,  '1678901236', N'Kevin',    N'Brooks',    'MD', 'PED',  2, 'kbrooks',   1, 1),
 (7,  '1789012345', N'Hannah',   N'Weiss',     'ARNP','PED', 2, 'hweiss',    1, 1),
 (8,  '1890123452', N'Samuel',   N'Adeyemi',   'MD', 'CARD', 3, 'sadeyemi',  1, 0),
 (9,  '1901234561', N'Laura',    N'Kowalski',  'MD', 'CARD', 3, 'lkowalski', 1, 0),
 (10, '1223344551', N'Michael',  N'Tran',      'MD', 'ORTH', 4, 'mtran',     1, 0),
 (11, '1324354657', N'Erin',     N'Gallagher', 'PA-C','ORTH',4, 'egallagher',1, 0),
 (12, '1425364753', N'Robert',   N'Haines',    'MD', 'RAD',  6, 'rhaines',   0, 0);
SET IDENTITY_INSERT dbo.Provider OFF;

SET IDENTITY_INSERT dbo.AppointmentType ON;
INSERT dbo.AppointmentType (AppointmentTypeId, Code, Name, DurationMinutes, Color, NewPatient, AllowOnline, DefaultCptCode, SendToHospital) VALUES
 (1,  'NP',     N'New patient visit',          40, '#2e7d32', 1, 1, '99203', 0),
 (2,  'EST',    N'Office visit',               20, '#1565c0', 0, 1, '99213', 0),
 (3,  'PHY',    N'Annual physical',            30, '#6a1b9a', 0, 1, '99396', 0),
 (4,  'WCC',    N'Well child check',           30, '#00838f', 0, 1, '99392', 0),
 (5,  'SICK',   N'Same day sick visit',        15, '#c62828', 0, 1, '99213', 0),
 (6,  'FU',     N'Follow-up',                  15, '#1565c0', 0, 0, '99212', 0),
 (7,  'VAX',    N'Vaccination',                10, '#ef6c00', 0, 1, '90471', 0),
 (8,  'CARDNP', N'Cardiology consultation',    45, '#ad1457', 1, 0, '99204', 0),
 (9,  'ECHO',   N'Echocardiogram',             60, '#ad1457', 0, 0, '93306', 1),
 (10, 'ORTHNP', N'Orthopedic consultation',    30, '#4e342e', 1, 0, '99204', 0),
 (11, 'XRAY',   N'X-ray',                      15, '#455a64', 0, 0, '73560', 1),
 (12, 'MRI',    N'MRI',                        45, '#455a64', 0, 0, '73721', 1);
SET IDENTITY_INSERT dbo.AppointmentType OFF;

SET IDENTITY_INSERT dbo.Payer ON;
INSERT dbo.Payer (PayerId, Code, Name, PayerType, ClaimPayerId, Address1, City, State, Zip, FilingIndicator, TimelyFilingDays) VALUES
 (1, 'SELF', N'Self pay',                         0, 'SELF',  NULL, NULL, NULL, NULL, 'ZZ', 0),
 (2, 'MCARE',N'Medicare Part B (Jurisdiction F)', 2, '02202', N'PO Box 6704', N'Fargo', 'ND', '58108', 'MB', 365),
 (3, 'MCAID',N'Washington Apple Health',          3, 'WAMCD', N'PO Box 9248', N'Olympia', 'WA', '98507', 'MC', 365),
 (4, 'EVG',  N'Evergreen Health Plan',            1, 'EVG01', N'PO Box 34500', N'Seattle', 'WA', '98124', 'CI', 90),
 (5, 'NWH',  N'Northwind Health',                 1, 'NWH77', N'PO Box 2210', N'Spokane', 'WA', '99210', 'CI', 180),
 (6, 'CAS',  N'Cascade Mutual',                   1, 'CSM12', N'PO Box 91059', N'Portland', 'OR', '97291', 'BL', 365),
 (7, 'LNI',  N'Labor and Industries',             4, 'WALNI', N'PO Box 44291', N'Olympia', 'WA', '98504', 'WC', 365);
SET IDENTITY_INSERT dbo.Payer OFF;

INSERT dbo.ProcedureCode (CptCode, Description, StandardFee, Category) VALUES
 ('99201', N'Office visit, new patient, level 1',          75.00,  'E/M'),
 ('99202', N'Office visit, new patient, level 2',          115.00, 'E/M'),
 ('99203', N'Office visit, new patient, level 3',          165.00, 'E/M'),
 ('99204', N'Office visit, new patient, level 4',          250.00, 'E/M'),
 ('99205', N'Office visit, new patient, level 5',          320.00, 'E/M'),
 ('99211', N'Office visit, established patient, level 1',  40.00,  'E/M'),
 ('99212', N'Office visit, established patient, level 2',  80.00,  'E/M'),
 ('99213', N'Office visit, established patient, level 3',  125.00, 'E/M'),
 ('99214', N'Office visit, established patient, level 4',  185.00, 'E/M'),
 ('99215', N'Office visit, established patient, level 5',  250.00, 'E/M'),
 ('99391', N'Preventive visit, established, infant',       175.00, 'Preventive'),
 ('99392', N'Preventive visit, established, age 1-4',      185.00, 'Preventive'),
 ('99393', N'Preventive visit, established, age 5-11',     185.00, 'Preventive'),
 ('99395', N'Preventive visit, established, age 18-39',    210.00, 'Preventive'),
 ('99396', N'Preventive visit, established, age 40-64',    225.00, 'Preventive'),
 ('99397', N'Preventive visit, established, age 65+',      240.00, 'Preventive'),
 ('90471', N'Immunization administration',                 28.00,  'Immunization'),
 ('90686', N'Influenza vaccine, quadrivalent, IM',         32.00,  'Immunization'),
 ('90707', N'MMR vaccine',                                 85.00,  'Immunization'),
 ('36415', N'Routine venipuncture',                        15.00,  'Lab'),
 ('81002', N'Urinalysis, non-automated, without microscopy',12.00, 'Lab'),
 ('85025', N'Complete blood count with differential',      28.00,  'Lab'),
 ('80053', N'Comprehensive metabolic panel',               42.00,  'Lab'),
 ('80061', N'Lipid panel',                                 38.00,  'Lab'),
 ('83036', N'Hemoglobin A1c',                              30.00,  'Lab'),
 ('84443', N'Thyroid stimulating hormone',                 45.00,  'Lab'),
 ('93000', N'Electrocardiogram, complete',                 55.00,  'Cardiology'),
 ('93306', N'Echocardiography, complete with Doppler',     780.00, 'Cardiology'),
 ('93224', N'Holter monitor, 48 hours',                    260.00, 'Cardiology'),
 ('71046', N'Chest x-ray, 2 views',                        95.00,  'Imaging'),
 ('73560', N'Knee x-ray, 1-2 views',                       85.00,  'Imaging'),
 ('73721', N'MRI knee without contrast',                   1150.00,'Imaging'),
 ('20610', N'Arthrocentesis/injection, major joint',       145.00, 'Procedure'),
 ('J1030', N'Methylprednisolone acetate 40 mg',            18.00,  'Drug'),
 ('29125', N'Short arm splint application',                110.00, 'Procedure');

INSERT dbo.DiagnosisCode (Code, CodeSet, Description) VALUES
 ('401.9',  9, N'Essential hypertension, unspecified'),         ('I10',     10, N'Essential (primary) hypertension'),
 ('250.00', 9, N'Diabetes mellitus type II without complication'),('E11.9',  10, N'Type 2 diabetes mellitus without complications'),
 ('465.9',  9, N'Acute upper respiratory infection'),           ('J06.9',   10, N'Acute upper respiratory infection, unspecified'),
 ('V70.0',  9, N'Routine general medical examination'),         ('Z00.00',  10, N'Encounter for general adult medical examination without abnormal findings'),
 ('V20.2',  9, N'Routine infant or child health check'),        ('Z00.129', 10, N'Encounter for routine child health examination without abnormal findings'),
 ('719.46', 9, N'Pain in joint, lower leg'),                    ('M25.561', 10, N'Pain in right knee'),
 ('786.50', 9, N'Chest pain, unspecified'),                     ('R07.9',   10, N'Chest pain, unspecified'),
 ('427.31', 9, N'Atrial fibrillation'),                         ('I48.91',  10, N'Unspecified atrial fibrillation'),
 ('V04.81', 9, N'Need for vaccination against influenza'),      ('Z23',     10, N'Encounter for immunization'),
 ('493.90', 9, N'Asthma, unspecified'),                         ('J45.909', 10, N'Unspecified asthma, uncomplicated'),
 ('272.4',  9, N'Hyperlipidemia'),                              ('E78.5',   10, N'Hyperlipidemia, unspecified'),
 ('715.16', 9, N'Osteoarthrosis, localized, primary, lower leg'),('M17.11', 10, N'Unilateral primary osteoarthritis, right knee'),
 ('784.0',  9, N'Headache'),                                    ('R51.9',   10, N'Headache, unspecified'),
 ('428.0',  9, N'Congestive heart failure, unspecified'),       ('I50.9',   10, N'Heart failure, unspecified'),
 ('813.42', 9, N'Fracture of distal radius, closed'),           ('S52.501A',10, N'Unspecified fracture of the lower end of right radius, initial encounter');

-- Payer specific allowed amounts; codes without a row use the standard fee.
INSERT dbo.FeeSchedule (PayerId, CptCode, AllowedAmount, EffectiveFrom)
SELECT 2, CptCode, ROUND(StandardFee * 0.62, 2), '2014-01-01' FROM dbo.ProcedureCode
UNION ALL SELECT 3, CptCode, ROUND(StandardFee * 0.48, 2), '2014-01-01' FROM dbo.ProcedureCode
UNION ALL SELECT 4, CptCode, ROUND(StandardFee * 0.80, 2), '2015-01-01' FROM dbo.ProcedureCode
UNION ALL SELECT 5, CptCode, ROUND(StandardFee * 0.76, 2), '2015-07-01' FROM dbo.ProcedureCode
UNION ALL SELECT 6, CptCode, ROUND(StandardFee * 0.84, 2), '2014-01-01' FROM dbo.ProcedureCode;

INSERT dbo.LabTest (TestCode, Name, Specimen, CptCode) VALUES
 ('CBC',   N'Complete blood count with differential', 'Blood', '85025'),
 ('CMP',   N'Comprehensive metabolic panel',          'Serum', '80053'),
 ('LIPID', N'Lipid panel',                            'Serum', '80061'),
 ('A1C',   N'Hemoglobin A1c',                         'Blood', '83036'),
 ('TSH',   N'Thyroid stimulating hormone',            'Serum', '84443'),
 ('UA',    N'Urinalysis',                             'Urine', '81002');

-- Weekly templates. DayOfWeek: 0 = Sunday ... 6 = Saturday.
DECLARE @d int = 1;
WHILE @d <= 5
BEGIN
    INSERT dbo.ScheduleTemplate (ProviderId, SiteId, DayOfWeek, StartTime, EndTime, SlotMinutes, AppointmentTypes, MaxOverbook, EffectiveFrom) VALUES
     (1, 1, @d, '08:00', '12:00', 20, 'NP,EST,PHY,SICK,FU,VAX', 2, '2014-01-01'),
     (1, 1, @d, '13:00', '17:00', 20, 'NP,EST,PHY,SICK,FU,VAX', 2, '2014-01-01'),
     (2, 1, @d, '07:30', '12:30', 20, 'EST,PHY,SICK,FU', 1, '2014-01-01'),
     (3, 5, @d, '08:00', '12:00', 20, 'NP,EST,PHY,SICK,FU,VAX', 2, '2014-01-01'),
     (3, 5, @d, '13:00', '17:00', 20, 'NP,EST,PHY,SICK,FU,VAX', 2, '2014-01-01'),
     (5, 2, @d, '08:00', '12:00', 30, 'NP,WCC,SICK,FU,VAX', 1, '2014-01-01'),
     (5, 2, @d, '13:00', '16:30', 30, 'NP,WCC,SICK,FU,VAX', 1, '2014-01-01'),
     (7, 2, @d, '09:00', '16:00', 15, 'SICK,FU,VAX', 2, '2014-01-01');
    IF @d IN (1, 3, 5)
        INSERT dbo.ScheduleTemplate (ProviderId, SiteId, DayOfWeek, StartTime, EndTime, SlotMinutes, AppointmentTypes, MaxOverbook, EffectiveFrom) VALUES
         (4, 5, @d, '09:00', '17:00', 15, 'EST,SICK,FU,VAX', 3, '2014-01-01'),
         (6, 2, @d, '08:00', '17:00', 30, 'NP,WCC,SICK,FU', 0, '2014-01-01'),
         (8, 3, @d, '08:00', '12:00', 15, 'CARDNP,FU', 0, '2014-01-01'),
         (9, 3, @d, '13:00', '17:00', 15, 'CARDNP,FU', 0, '2014-01-01'),
         (10, 4, @d, '08:00', '16:00', 15, 'ORTHNP,FU', 1, '2014-01-01');
    IF @d IN (2, 4)
        INSERT dbo.ScheduleTemplate (ProviderId, SiteId, DayOfWeek, StartTime, EndTime, SlotMinutes, AppointmentTypes, MaxOverbook, EffectiveFrom) VALUES
         (6, 2, @d, '08:00', '12:00', 30, 'WCC,SICK,FU', 0, '2014-01-01'),
         (8, 6, @d, '08:00', '12:00', 60, 'ECHO', 0, '2014-01-01'),
         (9, 3, @d, '08:00', '16:00', 15, 'CARDNP,FU', 0, '2014-01-01'),
         (11, 4, @d, '08:00', '16:00', 15, 'ORTHNP,FU', 1, '2014-01-01'),
         (12, 6, @d, '07:00', '19:00', 15, 'XRAY,MRI', 0, '2014-01-01');
    SET @d = @d + 1;
END
INSERT dbo.ScheduleTemplate (ProviderId, SiteId, DayOfWeek, StartTime, EndTime, SlotMinutes, AppointmentTypes, MaxOverbook, EffectiveFrom) VALUES
 (4, 5, 6, '09:00', '13:00', 15, 'SICK,VAX', 4, '2014-01-01'),
 (7, 2, 6, '09:00', '12:00', 15, 'SICK,VAX', 2, '2014-01-01');

INSERT dbo.AppSetting (Name, Value, Description) VALUES
 ('Billing.ClaimLagDays',          '1',     N'Days after charge entry before an encounter is billed'),
 ('Billing.StatementMinimum',      '5.00',  N'Smallest balance that gets a patient statement'),
 ('Billing.StatementDueDays',      '21',    N'Days from statement date to due date'),
 ('Billing.CollectionsDunningLevel','3',    N'Dunning level after which the account is flagged for collections'),
 ('Scheduling.OnlineDaysAhead',    '60',    N'How far ahead the booking site shows times'),
 ('Scheduling.SameDayHoldMinutes', '120',   N'Same day sick slots are held for phone booking until this many minutes before'),
 ('Reminders.HoursAhead',          '48',    N'Reminders go out this many hours before the appointment'),
 ('Reminders.SmsEnabled',          'true',  N'Send SMS reminders to patients who prefer text'),
 ('Referrals.ReviewDays',          '3',     N'Business days to decide on a routine referral'),
 ('Referrals.AuthorizationValidDays','90',  N'How long an approved referral can be scheduled'),
 ('Interface.HospitalEnabled',     'true',  N'Send scheduling and ADT messages to the hospital'),
 ('Interface.LabEnabled',          'true',  N'Send lab orders and patient registrations to the lab');

-- API keys are stored as SHA-256 hashes. The lab keys below are for the test environment only.
INSERT dbo.ApiClient (Name, ClientType, ApiKeyHash, SiteId) VALUES
 (N'Northgate lobby kiosk',      'Kiosk',   '2341986A8BE1DE27FAB323A04619E8EF82F9B6B5C8A3F24D58B3D82E71640655', 1),
 (N'Lakewood lobby kiosk',       'Kiosk',   'C9FFD249E8D34B9B04C16CCC07CAB945DE333CE5E2B8C17C9CC127A81C61A360', 2),
 (N'Heart Center kiosk',         'Kiosk',   '4C0A95B0D9EB373C2F1A125D389CD0DC18FAE19996A65F18B63731F6D27EA216', 3),
 (N'Patient booking site',       'Portal',  '11C6A7A71B1B2B7DC02300C29C1FC4B8746BF02638E2AA58FBDCB0AAFCEB89D3', NULL),
 (N'St. Catherine Hospital',     'Partner', '8351B48502F27FB9896E287BF00AC8FCD81EFABEF3FB41C6063C2FB25A75612E', NULL),
 (N'Referring practice network', 'Partner', 'A131090F418053F0FCE21A2CCF6F63EF52892CF3D1A71627755723FDF1322B34', NULL);
GO
