-- Reference data. Cost center managers are Windows accounts in the given domain (or computer name).
-- sqlcmd -S sql01.corp.local -E -d Purchasing -i 03-seed.sql -v AccountDomain="CORP"
SET NOCOUNT ON;
GO

MERGE dbo.CostCenters AS t
USING (VALUES
    (N'1100', N'Management',              N'$(AccountDomain)\ahalme', N'anna.halme@contoso.com'),
    (N'2100', N'Finance and Accounting',  N'$(AccountDomain)\pvirta', N'pekka.virta@contoso.com'),
    (N'3100', N'IT Services',             N'$(AccountDomain)\mlind',  N'maria.lind@contoso.com'),
    (N'4100', N'Production Line 1',       N'$(AccountDomain)\mlind',  N'maria.lind@contoso.com'),
    (N'4200', N'Production Line 2',       N'$(AccountDomain)\ksalo',  N'kalle.salo@contoso.com'),
    (N'5100', N'Maintenance',             N'$(AccountDomain)\ksalo',  N'kalle.salo@contoso.com')
) AS s (Code, Name, ManagerAccount, ManagerEmail)
ON t.Code = s.Code
WHEN MATCHED THEN UPDATE SET Name = s.Name, ManagerAccount = s.ManagerAccount, ManagerEmail = s.ManagerEmail
WHEN NOT MATCHED THEN INSERT (Code, Name, ManagerAccount, ManagerEmail) VALUES (s.Code, s.Name, s.ManagerAccount, s.ManagerEmail);
GO

MERGE dbo.Suppliers AS t
USING (VALUES
    (N'NORDTOOL',  N'Nordic Tools Oy',             N'FI12345678',     N'orders@nordictools.example', N'V10011'),
    (N'OFFICEPRO', N'OfficePro Supplies Ltd',      N'FI23456789',     N'sales@officepro.example',    N'V10027'),
    (N'SAFEWEAR',  N'SafeWear Industrial',         N'SE556677889901', N'order@safewear.example',     N'V10044'),
    (N'TECHDIST',  N'TechDist Computers',          N'FI34567890',     N'b2b@techdist.example',       N'V10052'),
    (N'HYDROSERV', N'Hydraulic Service Partners',  N'FI45678901',     N'service@hydroserv.example',  N'V10063'),
    (N'CLEANCO',   N'CleanCo Facility Services',   N'FI56789012',     N'contracts@cleanco.example',  N'V10078')
) AS s (Code, Name, VatNumber, Email, ErpVendorNo)
ON t.Code = s.Code
WHEN MATCHED THEN UPDATE SET Name = s.Name, VatNumber = s.VatNumber, Email = s.Email, ErpVendorNo = s.ErpVendorNo
WHEN NOT MATCHED THEN INSERT (Code, Name, VatNumber, Email, ErpVendorNo) VALUES (s.Code, s.Name, s.VatNumber, s.Email, s.ErpVendorNo);
GO
