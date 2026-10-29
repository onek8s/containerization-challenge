-- Reference data for a new installation (distributors, catalogue, first agreements).
:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE [$(DatabaseName)];
GO

MERGE rebate.Distributor AS t
USING (VALUES
    (N'KESPRO', N'Kespro Foodservice Oy',   N'300145', 'EUR', N'rebates@kespro.example',   N'KESPRO'),
    (N'HEINON', N'Heinon Tukku Oy',         N'300212', 'EUR', N'ostoreskontra@heinon.example', N'HEINON'),
    (N'METRO',  N'Metro Cash and Carry AB', N'410077', 'SEK', N'ap.nordic@metro.example',  N'METRO')
) AS s (Code, Name, ErpVendorNo, Currency, StatementEmail, StatementFolder)
ON t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Name, ErpVendorNo, Currency, StatementEmail, StatementFolder, ModifiedBy)
    VALUES (s.Code, s.Name, s.ErpVendorNo, s.Currency, s.StatementEmail, s.StatementFolder, N'setup');
GO

MERGE rebate.Product AS t
USING (VALUES
    ('06414000010015', N'COF-1001', N'Contoso Dark Roast 500 g', N'Contoso', N'Coffee'),
    ('06414000010084', N'COF-1002', N'Contoso Mild Roast 500 g', N'Contoso', N'Coffee'),
    ('06414000010152', N'COF-1010', N'Contoso Espresso Beans 1 kg', N'Contoso', N'Coffee'),
    ('06414000010220', N'TEA-2001', N'Fernhill Green Tea 20 bags', N'Fernhill', N'Tea'),
    ('06414000010299', N'TEA-2002', N'Fernhill Earl Grey 50 bags', N'Fernhill', N'Tea'),
    ('06414000010367', N'BIS-3001', N'Nordbake Oat Biscuits 300 g', N'Nordbake', N'Bakery'),
    ('06414000010435', N'BIS-3002', N'Nordbake Rye Crisp 250 g', N'Nordbake', N'Bakery'),
    ('06414000010503', N'CER-4001', N'Nordbake Muesli 750 g', N'Nordbake', N'Cereals'),
    ('06414000010572', N'CER-4002', N'Nordbake Porridge Oats 1 kg', N'Nordbake', N'Cereals'),
    ('06414000010640', N'JUI-5001', N'Sunvale Orange Juice 1 l', N'Sunvale', N'Juice'),
    ('06414000010718', N'JUI-5002', N'Sunvale Apple Juice 1 l', N'Sunvale', N'Juice'),
    ('06414000010787', N'JUI-5003', N'Sunvale Juice Multipack 6 x 0.2 l', N'Sunvale', N'Juice')
) AS s (Gtin, Sku, Description, Brand, Category)
ON t.Gtin = s.Gtin
WHEN MATCHED THEN UPDATE SET Sku = s.Sku, Description = s.Description, Brand = s.Brand, Category = s.Category
WHEN NOT MATCHED THEN INSERT (Gtin, Sku, Description, Brand, Category) VALUES (s.Gtin, s.Sku, s.Description, s.Brand, s.Category);
GO

DECLARE @year int = YEAR(GETDATE());
DECLARE @from date = DATEFROMPARTS(@year, 1, 1), @to date = DATEFROMPARTS(@year, 12, 31);
DECLARE @a TABLE (AgreementNo nvarchar(20), Code nvarchar(20), Description nvarchar(200), Basis char(1), Category nvarchar(40), Tiers nvarchar(400), CapAmount decimal(19,2) NULL);
INSERT @a VALUES
    (N'TR-' + CAST(@year AS nvarchar(4)) + N'-001', N'KESPRO', N'Coffee volume bonus',          'Q', N'Coffee', N'R;0:0;2000:1.5;5000:2.5;10000:3.5', NULL),
    (N'TR-' + CAST(@year AS nvarchar(4)) + N'-002', N'KESPRO', N'Total range growth rebate',    'V', NULL,      N'S;0:0;50000:1;150000:2',            25000),
    (N'TR-' + CAST(@year AS nvarchar(4)) + N'-003', N'HEINON', N'Bakery and cereals listing',   'V', N'Bakery', N'R;0:1;20000:2',                     NULL),
    (N'TR-' + CAST(@year AS nvarchar(4)) + N'-004', N'METRO',  N'Juice volume bonus',           'Q', N'Juice',  N'S;0:0.5;10000:1.25;30000:2',         NULL);

INSERT rebate.Agreement (AgreementNo, DistributorId, Description, Basis, Category, Tiers, ValidFrom, ValidTo, CapAmount, Status, ApprovedBy, ApprovedAt, ModifiedBy)
SELECT a.AgreementNo, d.DistributorId, a.Description, a.Basis, a.Category, CAST(a.Tiers AS rebate.TierSchedule), @from, @to, a.CapAmount,
       'A', N'setup', SYSDATETIME(), N'setup'
FROM @a AS a
JOIN rebate.Distributor AS d ON d.Code = a.Code
WHERE NOT EXISTS (SELECT 1 FROM rebate.Agreement AS x WHERE x.AgreementNo = a.AgreementNo);
GO
