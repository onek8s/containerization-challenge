-- Product master extract from the ERP (normally refreshed nightly by the ERP export job).
SET QUOTED_IDENTIFIER ON;
GO
USE [$(DatabaseName)];
GO
MERGE hub.Products AS t
USING (VALUES
    ('LW-TNT-2P-GRN', N'Fjell 2P tent, green',             '6430071230011', 1),
    ('LW-TNT-3P-GRN', N'Fjell 3P tent, green',             '6430071230028', 1),
    ('LW-SB-M5-REG',  N'Polar -5 sleeping bag, regular',   '6430071230103', 1),
    ('LW-SB-M5-LNG',  N'Polar -5 sleeping bag, long',      '6430071230110', 1),
    ('LW-MAT-AIR-R',  N'Airmat regular',                   '6430071230202', 1),
    ('LW-PK-45-BLK',  N'Trek 45 backpack, black',          '6430071230301', 1),
    ('LW-PK-65-BLK',  N'Trek 65 backpack, black',          '6430071230318', 1),
    ('LW-STV-GAS',    N'Gas stove compact',                '6430071230400', 1),
    ('LW-HDL-300',    N'Headlamp 300 lm',                  '6430071230509', 1),
    ('LW-PK-40-RED',  N'Trek 40 backpack, red (2017)',     '6430071230295', 0)
) AS s (Sku, Description, Ean, Active)
ON t.Sku = s.Sku
WHEN MATCHED THEN UPDATE SET Description = s.Description, Ean = s.Ean, Active = s.Active
WHEN NOT MATCHED THEN INSERT (Sku, Description, Ean, Active) VALUES (s.Sku, s.Description, s.Ean, s.Active);
GO
