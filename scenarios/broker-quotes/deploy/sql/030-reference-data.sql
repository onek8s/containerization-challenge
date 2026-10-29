-- Reference data for HumongousQuoting. Run once after 010-quoting-schema.sql.
-- Agencies are maintained afterwards in the underwriting workbench (Agencies page).
SET NOCOUNT ON;

MERGE Agencies AS t
USING (VALUES
    (N'ALP01', N'Alpine Insurance Brokers Ltd', N'305112', 0.2000, 1, N'documents@alpine-brokers.example'),
    (N'NWB02', N'Northbridge Commercial Insurance', N'412877', 0.1750, 1, N'newbusiness@northbridge.example'),
    (N'HRP03', N'Harrogate Risk Partners LLP', N'598001', 0.2250, 1, N'admin@harrogaterisk.example')
) AS s (Code, Name, FcaNumber, CommissionRate, Active, DocumentEmail)
ON t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Name, FcaNumber, CommissionRate, Active, DocumentEmail, CreatedUtc)
    VALUES (s.Code, s.Name, s.FcaNumber, s.CommissionRate, s.Active, s.DocumentEmail, SYSUTCDATETIME());

-- Number sequences continue from the previous broker extranet (last quote HQ 418,220, last policy 61,904).
IF NOT EXISTS (SELECT 1 FROM NumberSequences WHERE Name = N'Quote')
    INSERT INTO NumberSequences (Name, NextValue) VALUES (N'Quote', 418221);
IF NOT EXISTS (SELECT 1 FROM NumberSequences WHERE Name = N'Policy')
    INSERT INTO NumberSequences (Name, NextValue) VALUES (N'Policy', 61905);
