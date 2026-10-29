-- Reference data: queues, categories, routing rules, SLA targets and holidays. Run in the CustomerCare database after 03-procedures.sql.
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

INSERT care.Queue (Code, Name, Email, DefaultPriority, IsActive, SortOrder) VALUES
    ('GENERAL',    N'General enquiries',       N'care-general@tailspin-energy.example', 2, 1, 10),
    ('BILLING',    N'Billing and payments',    N'care-billing@tailspin-energy.example', 2, 1, 20),
    ('MOVES',      N'Moves and contracts',     N'care-moves@tailspin-energy.example',   2, 1, 30),
    ('FIELD',      N'Network and outages',     N'care-field@tailspin-energy.example',   3, 1, 40),
    ('FIELD-WEST', N'Network and outages, West', N'care-field-west@tailspin-energy.example', 3, 1, 41),
    ('KEYACC',     N'Key accounts',            N'keyaccounts@tailspin-energy.example',  3, 1, 50),
    ('COMPLAINTS', N'Complaints',              N'care-complaints@tailspin-energy.example', 2, 1, 60);

INSERT care.Category (Code, Name, QueueCode) VALUES
    ('BILL',      N'Invoice question',          'BILLING'),
    ('PAYPLAN',   N'Payment plan',              'BILLING'),
    ('METER',     N'Meter reading',             'BILLING'),
    ('MOVE',      N'Moving house',              'MOVES'),
    ('CONTRACT',  N'Contract change',           'MOVES'),
    ('OUTAGE',    N'Power outage',              'FIELD'),
    ('CONNECT',   N'New connection',            'FIELD'),
    ('SOLAR',     N'Solar panels and feed-in',  'GENERAL'),
    ('COMPLAINT', N'Complaint',                 'COMPLAINTS'),
    ('OTHER',     N'Other',                     NULL);

INSERT care.RoutingRule (Sequence, CategoryCode, PostalCodePrefix, Segment, QueueCode) VALUES
    (10, NULL,     NULL, 'LCO', 'KEYACC'),
    (20, 'OUTAGE', '20', NULL,  'FIELD-WEST'),
    (21, 'OUTAGE', '33', NULL,  'FIELD-WEST'),
    (22, 'CONNECT','20', NULL,  'FIELD-WEST'),
    (23, 'CONNECT','33', NULL,  'FIELD-WEST');

INSERT care.SlaTarget (Priority, ResponseHours, ResolveHours) VALUES
    (1, 18, 90),
    (2, 9, 36),
    (3, 4, 18),
    (4, 1, 8);

INSERT care.Holiday ([Date], Name) VALUES
    ('2026-01-01', N'New Year''s Day'),
    ('2026-01-06', N'Epiphany'),
    ('2026-04-03', N'Good Friday'),
    ('2026-04-06', N'Easter Monday'),
    ('2026-05-01', N'May Day'),
    ('2026-05-14', N'Ascension Day'),
    ('2026-06-19', N'Midsummer Eve'),
    ('2026-12-24', N'Christmas Eve'),
    ('2026-12-25', N'Christmas Day'),
    ('2026-12-31', N'New Year''s Eve'),
    ('2027-01-01', N'New Year''s Day'),
    ('2027-01-06', N'Epiphany'),
    ('2027-03-26', N'Good Friday'),
    ('2027-03-29', N'Easter Monday'),
    ('2027-05-06', N'Ascension Day'),
    ('2027-06-25', N'Midsummer Eve'),
    ('2027-12-06', N'Independence Day'),
    ('2027-12-24', N'Christmas Eve');
GO
