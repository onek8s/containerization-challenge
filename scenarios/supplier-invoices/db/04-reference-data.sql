-- Suppliers and delivery channels connected to the hub.
--   sqlcmd -S sql01.corp.local -E -d SupplierInvoices -i 04-reference-data.sql
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

MERGE inv.Supplier AS t
USING (VALUES
    ('NF',  N'Nordic Fasteners Oy',          'FI12345671',  'V10021', 28591, NULL, 0, 'WH01', N'billing@nordicfasteners.example'),
    ('BP',  N'Baltic Packaging AS',          'EE100234567', 'V10045', 65001, NULL, 0, 'WH01', N'invoices@balticpack.example'),
    ('LOS', N'Lakeside Office Supply Ltd',   'FI23456782',  'V10107', 1252,  NULL, 1, 'ADM1', N'laskutus@lakeside.example'),
    ('KTL', N'Kuljetus Tuominen Oy',         'FI34567893',  'V10230', 1252,  5.00, 1, 'LOG1', N'kirjanpito@ktuominen.example'),
    ('SEC', N'Securitas Guarding Finland',   'FI45678904',  'V10388', 65001, NULL, 1, 'ADM1', NULL)
) AS s (Code, Name, VatNumber, VendorNumber, CodePage, PriceTolerancePercent, TwoWayMatch, CostCenter, ContactEmail)
ON t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Name, VatNumber, VendorNumber, CodePage, PriceTolerancePercent, TwoWayMatch, CostCenter, ContactEmail, ModifiedBy)
    VALUES (s.Code, s.Name, s.VatNumber, s.VendorNumber, s.CodePage, s.PriceTolerancePercent, s.TwoWayMatch, s.CostCenter, s.ContactEmail, N'setup');
GO

DECLARE @channels TABLE (Code varchar(10), ChannelType varchar(5), Location nvarchar(260), FilePattern nvarchar(100));
INSERT @channels VALUES
    ('NF',  'FTP',   N'suppliers/NF/out',  N'*.edi;*.inv'),
    ('BP',  'SHARE', N'BP',                N'*.xml'),
    ('LOS', 'SHARE', N'LOS',               N'*.csv'),
    ('KTL', 'FTP',   N'suppliers/KTL/out', N'*.csv;*.txt'),
    ('SEC', 'SHARE', N'Scanning\SEC',      N'*.xml');

INSERT inv.Channel (SupplierId, ChannelType, Location, FilePattern)
SELECT s.SupplierId, c.ChannelType, c.Location, c.FilePattern
FROM @channels c
JOIN inv.Supplier s ON s.Code = c.Code
WHERE NOT EXISTS (SELECT 1 FROM inv.Channel x WHERE x.SupplierId = s.SupplierId AND x.Location = c.Location);
GO
