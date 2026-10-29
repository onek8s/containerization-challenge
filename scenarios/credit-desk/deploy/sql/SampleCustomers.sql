/* Sample customers for test and training environments. Production data comes from the ERP sync job. */
USE CreditDesk;
GO
MERGE dbo.Customer AS t
USING (VALUES
    ('C10442', N'Alpine Hardware GmbH',        N'Ms. Huber',     N'Industriestrasse 12', NULL,          N'6020',  N'Innsbruck',  'AT', N'FABRIKAM\jmorris',  20000,  12450.80,     0.00,  0, '2015-01-12', 0),
    ('C10518', N'Bayside Builders Supply',     N'Tom Alvarez',   N'2200 Harbor Blvd',    N'Suite 4',    N'94607', N'Oakland',    'US', N'FABRIKAM\jmorris',  45000,  41210.00,  3200.00, 34, '2011-06-01', 0),
    ('C10601', N'Northgate Plumbing Ltd',      N'Accounts',      N'14 Mill Lane',        NULL,          N'LS1 4AB', N'Leeds',    'GB', N'FABRIKAM\aokafor',  15000,   2210.15,     0.00,  0, '2019-09-23', 0),
    ('C10777', N'Lakeshore Electrical Inc.',   N'Priya Nair',    N'801 W Lake St',       NULL,          N'60661', N'Chicago',    'US', N'FABRIKAM\aokafor', 120000,  98200.40, 15400.00, 72, '2008-03-14', 0),
    ('C10802', N'Red Rock Contractors LLC',    NULL,             N'55 Canyon Rd',        NULL,          N'85001', N'Phoenix',    'US', N'FABRIKAM\jmorris',   5000,   6100.00,  6100.00, 95, '2023-02-01', 1),
    ('C10915', N'Summit Facility Services',    N'Greg Lund',     N'1 Summit Plaza',      N'Floor 9',    N'80202', N'Denver',     'US', N'FABRIKAM\lchen',   240000, 187300.00,     0.00,  0, '2005-11-30', 0)
) AS s (AccountNumber, Name, ContactName, AddressLine1, AddressLine2, PostalCode, City, CountryCode, SalesRep,
        CreditLimit, OpenBalance, OverdueBalance, OldestOverdueDays, CustomerSince, OnStop)
ON t.AccountNumber = s.AccountNumber
WHEN NOT MATCHED THEN INSERT (AccountNumber, Name, ContactName, AddressLine1, AddressLine2, PostalCode, City, CountryCode,
        SalesRep, CreditLimit, OpenBalance, OverdueBalance, OldestOverdueDays, CustomerSince, OnStop)
    VALUES (s.AccountNumber, s.Name, s.ContactName, s.AddressLine1, s.AddressLine2, s.PostalCode, s.City, s.CountryCode,
        s.SalesRep, s.CreditLimit, s.OpenBalance, s.OverdueBalance, s.OldestOverdueDays, s.CustomerSince, s.OnStop);
GO
