-- Reference data maintained by sales management: tier discounts per discount group, freight per country and family,
-- portal roles. Re-runnable (MERGE).
USE [DealerOrders];
GO

-- Tier: 0 = A, 1 = B, 2 = C, 3 = sub-dealer. Discount groups match the AX item discount groups (InventTable.PriceGroup).
MERGE [dlr].[DiscountAgreement] AS t
USING (VALUES
    (0, 'MACHINE', 18),
    (1, 'MACHINE', 15),
    (2, 'MACHINE', 12),
    (3, 'MACHINE', 8),
    (0, 'OPTION', 15),
    (1, 'OPTION', 12),
    (2, 'OPTION', 10),
    (3, 'OPTION', 6),
    (0, 'ATTACHMENT', 20),
    (1, 'ATTACHMENT', 18),
    (2, 'ATTACHMENT', 15),
    (3, 'ATTACHMENT', 10),
    (0, 'SERVICE', 0),
    (1, 'SERVICE', 0),
    (2, 'SERVICE', 0),
    (3, 'SERVICE', 0),
    (0, 'PARTS', 25),
    (1, 'PARTS', 22),
    (2, 'PARTS', 20),
    (3, 'PARTS', 12)
) AS s ([Tier], [DiscountGroup], [Percent])
ON t.[Tier] = s.[Tier] AND t.[DiscountGroup] = s.[DiscountGroup]
WHEN MATCHED THEN UPDATE SET [Percent] = s.[Percent]
WHEN NOT MATCHED THEN INSERT ([Tier], [DiscountGroup], [Percent]) VALUES (s.[Tier], s.[DiscountGroup], s.[Percent]);
GO

-- Family: 0 = harvester, 1 = forwarder, 2 = compact loader. Delivery from Kontiolahti, CPT and DAP terms only.
MERGE [dlr].[FreightRate] AS t
USING (VALUES
    ('FI', '1', 1, 1200),
    ('FI', '1', 0, 1500),
    ('FI', '1', 2, 360),
    ('SE', '1', 1, 2400),
    ('SE', '1', 0, 3000),
    ('SE', '1', 2, 720),
    ('NO', '1', 1, 3100),
    ('NO', '1', 0, 3875),
    ('NO', '1', 2, 930),
    ('DK', '1', 1, 3300),
    ('DK', '1', 0, 4125),
    ('DK', '1', 2, 990),
    ('EE', '1', 1, 2600),
    ('EE', '1', 0, 3250),
    ('EE', '1', 2, 780),
    ('DE', '1', 1, 4800),
    ('DE', '1', 0, 6000),
    ('DE', '1', 2, 1440),
    ('AT', '1', 1, 5600),
    ('AT', '1', 0, 7000),
    ('AT', '1', 2, 1680),
    ('PL', '1', 1, 4200),
    ('PL', '1', 0, 5250),
    ('PL', '1', 2, 1260),
    ('GB', '1', 1, 6900),
    ('GB', '1', 0, 8625),
    ('GB', '1', 2, 2070),
    ('FR', '1', 1, 6200),
    ('FR', '1', 0, 7750),
    ('FR', '1', 2, 1860),
    ('US', '1', 1, 14500),
    ('US', '1', 0, 18125),
    ('US', '1', 2, 4350),
    ('CA', '1', 1, 16800),
    ('CA', '1', 0, 21000),
    ('CA', '1', 2, 5040)
) AS s ([CountryCode], [Zone], [Family], [AmountEur])
ON t.[CountryCode] = s.[CountryCode] AND t.[Zone] = s.[Zone] AND t.[Family] = s.[Family]
WHEN MATCHED THEN UPDATE SET [AmountEur] = s.[AmountEur]
WHEN NOT MATCHED THEN INSERT ([CountryCode], [Zone], [Family], [AmountEur]) VALUES (s.[CountryCode], s.[Zone], s.[Family], s.[AmountEur]);
GO

MERGE [dlr].[Campaign] AS t
USING (VALUES
    ('AUT19-TF14', N'Autumn 2019 TF14 campaign', 'TF14', NULL, NULL, 3, '2019-09-01', '2019-12-31'),
    ('AUT19-LINK', N'TailspinLink free year', NULL, 'TELEMATICS', NULL, 33.33, '2019-09-01', '2019-12-31')
) AS s ([Code], [Name], [ModelCode], [OptionCode], [CountryCode], [Percent], [FromDate], [ToDate])
ON t.[Code] = s.[Code]
WHEN MATCHED THEN UPDATE SET [Name] = s.[Name], [Percent] = s.[Percent], [FromDate] = s.[FromDate], [ToDate] = s.[ToDate]
WHEN NOT MATCHED THEN INSERT ([Code], [Name], [ModelCode], [OptionCode], [CountryCode], [Percent], [FromDate], [ToDate])
    VALUES (s.[Code], s.[Name], s.[ModelCode], s.[OptionCode], s.[CountryCode], s.[Percent], s.[FromDate], s.[ToDate]);
GO

MERGE [dbo].[AspNetRoles] AS t
USING (VALUES ('5f1c6a43-6d2a-4c55-9a43-0c2f8f9f1a01', 'DealerAdmin'), ('5f1c6a43-6d2a-4c55-9a43-0c2f8f9f1a02', 'DealerSales'),
              ('5f1c6a43-6d2a-4c55-9a43-0c2f8f9f1a03', 'DealerParts')) AS s ([Id], [Name])
ON t.[Name] = s.[Name]
WHEN NOT MATCHED THEN INSERT ([Id], [Name]) VALUES (s.[Id], s.[Name]);
GO
