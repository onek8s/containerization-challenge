-- Demo customers, knowledge base articles and cases. Run in the CustomerCare database after 04-reference-data.sql.
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

INSERT care.Customer (AccountNumber, Name, Email, Phone, Street, PostalCode, City, Segment, IsPriorityCustomer) VALUES
    ('1000000123', N'Matti Virtanen', N'matti.virtanen@example.fi', '0401234567', N'Kauppakatu 12 A 3', '00100', N'Helsinki', 'RES', 0),
    ('1000000453', N'Anna Korhonen', N'anna.korhonen@example.fi', '0452345678', N'Hämeenkatu 5 B 12', '33100', N'Tampere', 'RES', 1),
    ('1000000784', N'Juha Mäkinen', N'juha.makinen@example.fi', '0503456789', N'Rantatie 8', '20100', N'Turku', 'RES', 0),
    ('1000001014', N'Laura Nieminen', NULL, '0414567890', N'Koulukatu 21', '90100', N'Oulu', 'RES', 0),
    ('1000001345', N'Pekka Mäkelä', N'pekka.makela@example.fi', '0405678901', N'Pellonpää 3', '33580', N'Tampere', 'RES', 0),
    ('1000001675', N'Sari Hämäläinen', N'sari.hamalainen@example.fi', '0446789012', N'Mäntytie 14', '40100', N'Jyväskylä', 'RES', 1),
    ('2000000139', N'Kahvila Aamukaste Oy', N'laskut@aamukaste.example.fi', '0207890123', N'Torikatu 2', '00170', N'Helsinki', 'SME', 0),
    ('2000000469', N'Rautakauppa Lehtonen Ky', N'info@lehtonen.example.fi', '0338901234', N'Teollisuuskatu 40', '33720', N'Tampere', 'SME', 0),
    ('2000000790', N'Pohjolan Leipomo Oy', N'talous@pohjolanleipomo.example.fi', '0209012345', N'Leipurintie 1', '90620', N'Oulu', 'SME', 0),
    ('3000000145', N'Satamakaupungin Sairaala', N'hankinta@sairaala.example.fi', '0201234500', N'Sairaalankatu 1', '20520', N'Turku', 'LCO', 1),
    ('3000000475', N'Metsä-Kuitu Tehtaat Oyj', N'energia@metsakuitu.example.fi', '0201234511', N'Tehtaantie 100', '35800', N'Mänttä', 'LCO', 0),
    ('3000000707', N'Kauppakeskus Kaari', N'kiinteisto@kaari.example.fi', '0201234522', N'Kaaren aukio 1', '00700', N'Helsinki', 'LCO', 0);

INSERT care.KbArticle (Slug, Title, Body, Tags, CategoryCode, IsPublished, ModifiedBy, ModifiedUtc) VALUES
    ('how-to-read-the-meter', N'How to read the meter',
     N'<p>Most meters are read remotely every hour. If the customer has an older meter, ask for the reading from the display labelled <b>1.8.0</b>.</p><p>Enter the reading in the billing system under <i>Meter readings</i> and tick <i>Customer reported</i>.</p>',
     N'meter, reading, billing', 'METER', 1, N'CORP\hlaine', SYSUTCDATETIME()),
    ('payment-plan-rules', N'Payment plan rules',
     N'<p>Residential customers can split an invoice over 2 to 6 months when the invoice is over 200 EUR. No interest is charged for plans up to 3 months.</p><p>Plans for SME customers need approval from Billing.</p>',
     N'payment, plan, invoice', 'PAYPLAN', 1, N'CORP\hlaine', SYSUTCDATETIME()),
    ('power-outage-checklist', N'Power outage checklist',
     N'<ol><li>Check the outage map for the postal code.</li><li>Ask the customer to check the main fuse.</li><li>If neighbours have power and the fuse is fine, create an Outage case with priority High.</li><li>Priority customers (medical equipment) are always Critical.</li></ol>',
     N'outage, fuse, map', 'OUTAGE', 1, N'CORP\tsalo', SYSUTCDATETIME()),
    ('moving-house', N'Moving house',
     N'<p>The contract moves with the customer. Ask for the moving date, the new address and the final meter reading of the old address.</p>',
     N'move, contract', 'MOVE', 1, N'CORP\hlaine', SYSUTCDATETIME()),
    ('solar-feed-in-contract', N'Solar feed-in contract',
     N'<p>Customers with solar panels need a feed-in contract. The installer sends the grid notification; Customer Care only creates the contract.</p>',
     N'solar, feed-in', 'SOLAR', 1, N'CORP\tsalo', SYSUTCDATETIME()),
    ('complaint-handling', N'Complaint handling (draft)',
     N'<p>Acknowledge within one working day. Escalate compensation claims over 500 EUR to the team lead.</p>',
     N'complaint', 'COMPLAINT', 0, N'CORP\tsalo', SYSUTCDATETIME());
GO

-- Demo cases relative to the current time.
DECLARE @now datetime2(0) = SYSUTCDATETIME();
DECLARE @year int = YEAR(@now);

INSERT care.[Case] (CaseNumber, CustomerId, QueueId, CategoryCode, Subject, Description, Priority, Status, Channel, AssignedTo,
                    CreatedUtc, CreatedBy, ResponseDueUtc, ResolveDueUtc, FirstResponseUtc, ResolvedUtc, ClosedUtc, ModifiedUtc)
SELECT 'CC-' + CAST(@year AS char(4)) + '-' + RIGHT('000000' + CAST(v.Seq AS varchar(6)), 6),
       cu.Id, q.Id, v.Category, v.Subject, v.Description, v.Priority, v.Status, v.Channel, v.AssignedTo,
       DATEADD(HOUR, -v.AgeHours, @now), N'CORP\hlaine',
       DATEADD(HOUR, v.ResponseHours - v.AgeHours, @now), DATEADD(HOUR, v.ResolveHours - v.AgeHours, @now),
       CASE WHEN v.Status > 0 THEN DATEADD(HOUR, 1 - v.AgeHours, @now) END,
       CASE WHEN v.Status >= 4 THEN DATEADD(HOUR, -2, @now) END,
       CASE WHEN v.Status = 5 THEN DATEADD(HOUR, -1, @now) END,
       @now
  FROM (VALUES
        (1,  '1000000123', 'BILLING',    'BILL',      N'Invoice higher than usual',         N'Customer says the September invoice is twice the normal amount.', 2, 1, 1, N'CORP\hlaine', 30, 9, 36),
        (2,  '1000000453', 'FIELD-WEST', 'OUTAGE',    N'No power since this morning',       N'Medical equipment at home. Neighbours also without power.',       4, 2, 1, N'CORP\tsalo', 3, 1, 8),
        (3,  '1000000784', 'MOVES',      'MOVE',      N'Moving to Helsinki on 1st',         NULL,                                                              2, 0, 3, NULL, 5, 9, 36),
        (4,  '1000001014', 'BILLING',    'PAYPLAN',   N'Payment plan for winter invoice',   N'Wants to split 640 EUR over 4 months.',                           2, 3, 2, N'CORP\hlaine', 50, 9, 36),
        (5,  '1000001345', 'FIELD-WEST', 'CONNECT',   N'New connection for sauna building', NULL,                                                              1, 0, 3, NULL, 80, 18, 90),
        (6,  '1000001675', 'GENERAL',    'SOLAR',     N'Feed-in contract for new panels',   NULL,                                                              2, 4, 2, N'CORP\tsalo', 120, 9, 36),
        (7,  '2000000139', 'BILLING',    'BILL',      N'VAT missing from invoice',          N'Accounting needs a corrected invoice.',                           2, 1, 2, N'CORP\hlaine', 40, 9, 36),
        (8,  '2000000469', 'COMPLAINTS', 'COMPLAINT', N'Technician did not arrive',         N'Booked visit on Tuesday, nobody came.',                           3, 0, 1, NULL, 20, 4, 18),
        (9,  '3000000145', 'KEYACC',     'CONTRACT',  N'Add two new metering points',       NULL,                                                              3, 2, 2, N'CORP\jniemi', 6, 4, 18),
        (10, '3000000475', 'KEYACC',     'BILL',      N'Monthly invoice split per site',    NULL,                                                              2, 5, 2, N'CORP\jniemi', 400, 9, 36),
        (11, '3000000707', 'KEYACC',     'OUTAGE',    N'Planned outage notice',             N'Needs 2 weeks notice for planned work.',                          3, 1, 4, N'CORP\jniemi', 2, 4, 18),
        (12, '2000000790', 'BILLING',    'METER',     N'Meter reading looks wrong',         NULL,                                                              2, 0, 3, NULL, 10, 9, 36)
       ) AS v (Seq, Account, QueueCode, Category, Subject, Description, Priority, Status, Channel, AssignedTo, AgeHours, ResponseHours, ResolveHours)
  JOIN care.Customer cu ON cu.AccountNumber = v.Account
  JOIN care.Queue q ON q.Code = v.QueueCode;

INSERT care.CaseNumberSequence ([Year], LastValue) VALUES (@year, 12);

INSERT care.CaseNote (CaseId, Author, Body, IsInternal, CreatedUtc)
SELECT c.Id, c.AssignedTo, N'Called the customer and explained the next steps.', 0, DATEADD(HOUR, 1, c.CreatedUtc)
  FROM care.[Case] c WHERE c.AssignedTo IS NOT NULL;
GO
