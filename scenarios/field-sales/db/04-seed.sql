-- Demo data: holidays, price rules, products, customers, promotions and users.
-- Demo users all have the password FieldSales2017 and should change it in the sales office site.
SET NOCOUNT ON;
GO

INSERT dbo.Holiday (Day, Name) VALUES
    ('2026-01-01', N'New Year''s Day'),
    ('2026-01-06', N'Epiphany'),
    ('2026-04-03', N'Good Friday'),
    ('2026-04-06', N'Easter Monday'),
    ('2026-05-01', N'May Day'),
    ('2026-05-14', N'Ascension Day'),
    ('2026-06-19', N'Midsummer Eve'),
    ('2026-12-24', N'Christmas Eve'),
    ('2026-12-25', N'Christmas Day'),
    ('2026-12-26', N'Boxing Day'),
    ('2026-12-06', N'Independence Day'),
    ('2027-01-01', N'New Year''s Day'),
    ('2027-01-06', N'Epiphany'),
    ('2027-03-26', N'Good Friday'),
    ('2027-03-29', N'Easter Monday'),
    ('2027-05-06', N'Ascension Day'),
    ('2027-06-25', N'Midsummer Eve'),
    ('2027-12-06', N'Independence Day'),
    ('2027-12-24', N'Christmas Eve'),
    ('2027-12-27', N'Boxing Day (observed)');
GO

INSERT dbo.PriceRule (PriceGroup, Category, DiscountPercent) VALUES
    (N'A', NULL, 8),
    (N'A', N'DAIRY', 3),
    (N'A', N'FROZEN', 5),
    (N'A', N'BEV', 10),
    (N'B', NULL, 4),
    (N'B', N'BEV', 6),
    (N'B', N'DAIRY', 2),
    (N'C', NULL, 0),
    (N'D', NULL, 12),
    (N'D', N'DAIRY', 4);
GO

INSERT dbo.Product (ItemNo, Name, Category, Unit, CaseSize, ListPrice, VatRate, MinOrderQty, Active) VALUES
    (N'10010', N'Coffee dark roast 500 g', N'DRY', N'CS', 12, 48.90, 14.00, 1, 1),
    (N'10017', N'Coffee medium roast 500 g', N'DRY', N'CS', 12, 46.50, 14.00, 1, 1),
    (N'10024', N'Tea black 100 bags', N'DRY', N'CS', 10, 32.00, 14.00, 1, 1),
    (N'10031', N'Oat flakes 1 kg', N'DRY', N'CS', 8, 14.40, 14.00, 1, 1),
    (N'10038', N'Wheat flour 2 kg', N'DRY', N'CS', 6, 11.10, 14.00, 1, 1),
    (N'10045', N'Rice jasmine 1 kg', N'DRY', N'CS', 10, 21.50, 14.00, 1, 1),
    (N'10052', N'Pasta penne 500 g', N'DRY', N'CS', 20, 19.80, 14.00, 1, 1),
    (N'10059', N'Pasta spaghetti 500 g', N'DRY', N'CS', 20, 19.20, 14.00, 1, 1),
    (N'10066', N'Tomato crushed 400 g', N'DRY', N'CS', 24, 22.80, 14.00, 1, 1),
    (N'10073', N'Sugar 1 kg', N'DRY', N'CS', 10, 13.90, 14.00, 1, 1),
    (N'10080', N'Muesli berry 750 g', N'DRY', N'CS', 8, 27.60, 14.00, 1, 1),
    (N'10087', N'Rye crispbread 250 g', N'DRY', N'CS', 16, 25.60, 14.00, 1, 1),
    (N'10094', N'Sunflower oil 1 l', N'DRY', N'CS', 12, 34.80, 14.00, 1, 1),
    (N'10101', N'Ketchup 1 kg', N'DRY', N'CS', 10, 28.00, 14.00, 1, 1),
    (N'10108', N'Mustard 450 g', N'DRY', N'CS', 12, 19.10, 14.00, 1, 1),
    (N'20010', N'Milk 1 l', N'DAIRY', N'CS', 10, 9.80, 14.00, 2, 1),
    (N'20017', N'Milk lactose free 1 l', N'DAIRY', N'CS', 10, 13.60, 14.00, 2, 1),
    (N'20024', N'Butter 500 g', N'DAIRY', N'CS', 12, 46.80, 14.00, 2, 1),
    (N'20031', N'Yoghurt natural 1 kg', N'DAIRY', N'CS', 6, 13.20, 14.00, 2, 1),
    (N'20038', N'Cheese Edam 1 kg', N'DAIRY', N'CS', 6, 61.20, 14.00, 2, 1),
    (N'20045', N'Cream 2 dl', N'DAIRY', N'CS', 20, 23.00, 14.00, 2, 1),
    (N'20052', N'Quark 250 g', N'DAIRY', N'CS', 16, 18.40, 14.00, 2, 1),
    (N'20059', N'Oat drink 1 l', N'DAIRY', N'CS', 8, 15.20, 14.00, 2, 1),
    (N'20066', N'Sour milk 1 l', N'DAIRY', N'CS', 10, 11.50, 14.00, 2, 1),
    (N'30010', N'Peas frozen 1 kg', N'FROZEN', N'CS', 8, 22.40, 14.00, 1, 1),
    (N'30017', N'Mixed vegetables 2.5 kg', N'FROZEN', N'CS', 4, 24.80, 14.00, 1, 1),
    (N'30024', N'Fish fingers 1 kg', N'FROZEN', N'CS', 6, 43.20, 14.00, 1, 1),
    (N'30031', N'Blueberries frozen 1 kg', N'FROZEN', N'CS', 8, 52.00, 14.00, 1, 1),
    (N'30038', N'Meatballs 1 kg', N'FROZEN', N'CS', 6, 49.80, 14.00, 1, 1),
    (N'30045', N'Pizza margherita 6 pcs', N'FROZEN', N'CS', 4, 30.40, 14.00, 1, 1),
    (N'30052', N'Ice cream vanilla 2 l', N'FROZEN', N'CS', 6, 38.40, 14.00, 1, 1),
    (N'40010', N'Mineral water 0.5 l', N'BEV', N'CS', 24, 14.40, 14.00, 1, 1),
    (N'40017', N'Orange juice 1 l', N'BEV', N'CS', 12, 22.80, 14.00, 1, 1),
    (N'40024', N'Apple juice 1 l', N'BEV', N'CS', 12, 21.60, 14.00, 1, 1),
    (N'40031', N'Cola 1.5 l', N'BEV', N'CS', 8, 15.20, 14.00, 1, 1),
    (N'40038', N'Energy drink 0.33 l', N'BEV', N'CS', 24, 31.20, 14.00, 1, 1),
    (N'40045', N'Lemonade 0.33 l', N'BEV', N'CS', 24, 18.00, 14.00, 1, 1),
    (N'40052', N'Coffee drink 0.25 l', N'BEV', N'CS', 12, 19.80, 14.00, 1, 1),
    (N'50010', N'Potato crisps 275 g', N'SNACK', N'CS', 12, 30.00, 14.00, 1, 1),
    (N'50017', N'Milk chocolate 200 g', N'SNACK', N'CS', 16, 35.20, 14.00, 1, 1),
    (N'50024', N'Salted peanuts 300 g', N'SNACK', N'CS', 12, 28.80, 14.00, 1, 1),
    (N'50031', N'Liquorice 350 g', N'SNACK', N'CS', 14, 30.80, 14.00, 1, 1),
    (N'50038', N'Cookies oat 300 g', N'SNACK', N'CS', 12, 22.80, 14.00, 1, 1),
    (N'50045', N'Popcorn 90 g', N'SNACK', N'CS', 20, 19.00, 14.00, 1, 1),
    (N'50052', N'Fruit gums 280 g', N'SNACK', N'CS', 14, 25.20, 14.00, 1, 1),
    (N'60010', N'Dish soap 500 ml', N'CLEAN', N'CS', 12, 22.80, 24.00, 1, 1),
    (N'60017', N'Laundry detergent 3 kg', N'CLEAN', N'CS', 3, 29.70, 24.00, 1, 1),
    (N'60024', N'Paper towel 4 rolls', N'CLEAN', N'CS', 6, 27.00, 24.00, 1, 1),
    (N'60031', N'Toilet paper 8 rolls', N'CLEAN', N'CS', 6, 33.60, 24.00, 1, 1),
    (N'60038', N'Surface cleaner 1 l', N'CLEAN', N'CS', 12, 30.00, 24.00, 1, 1),
    (N'60045', N'Bin bags 30 l 40 pcs', N'CLEAN', N'CS', 20, 29.00, 24.00, 1, 1),
    (N'69999', N'Shopping bag paper (discontinued)', N'CLEAN', N'EA', 1, 0.30, 24.00, 50, 0),
    (N'70010', N'Display stand rental', N'SERV', N'EA', 1, 15.00, 24.00, 1, 1);
GO

INSERT dbo.Customer (CustomerNo, Name, Street, PostalCode, City, Email, PriceGroup, RepCode, DeliveryDays, CreditBlocked) VALUES
    (N'C1001', N'Tähti Food Store', N'Kirkkotie 30', N'02100', N'Espoo', N'orders.c1001@example.com', N'C', N'RJ', 10, 1),
    (N'C1002', N'Lehto Lähikauppa', N'Kauppakatu 50', N'05800', N'Hyvinkää', NULL, N'C', N'RJ', 16, 0),
    (N'C1003', N'Vuori Lähikauppa', N'Rantatie 46', N'00100', N'Helsinki', N'orders.c1003@example.com', N'B', N'RJ', 1, 0),
    (N'C1004', N'Lumi Mini Market', N'Rantatie 51', N'05800', N'Hyvinkää', NULL, N'A', N'RJ', 1, 0),
    (N'C1005', N'Lumi Food Store', N'Koulukatu 4', N'04400', N'Järvenpää', N'orders.c1005@example.com', N'B', N'RJ', 9, 0),
    (N'C1006', N'Salo Service Station', N'Kirkkotie 13', N'05800', N'Hyvinkää', N'orders.c1006@example.com', N'A', N'RJ', 1, 0),
    (N'C1007', N'Saari Food Store', N'Asemantie 40', N'01300', N'Vantaa', N'orders.c1007@example.com', N'B', N'RJ', 9, 0),
    (N'C1008', N'Harju Lähikauppa', N'Satamakatu 38', N'05800', N'Hyvinkää', N'orders.c1008@example.com', N'C', N'RJ', 18, 0),
    (N'C1009', N'Berg Mini Market', N'Mäkitie 60', N'02100', N'Espoo', N'orders.c1009@example.com', N'B', N'RJ', 4, 0),
    (N'C1010', N'Laakso Lähikauppa', N'Kirkkotie 10', N'37600', N'Valkeakoski', N'orders.c1010@example.com', N'B', N'AN', 9, 0),
    (N'C1011', N'Tähti Mini Market', N'Puistotie 26', N'33500', N'Tampere', N'orders.c1011@example.com', N'C', N'AN', 21, 0),
    (N'C1012', N'Ranta Cafe', N'Kirkkotie 19', N'33100', N'Tampere', N'orders.c1012@example.com', N'D', N'AN', 1, 0),
    (N'C1013', N'Koivu Kiosk', N'Puistotie 48', N'33100', N'Tampere', N'orders.c1013@example.com', N'A', N'AN', 4, 0),
    (N'C1014', N'Vuori Cafe', N'Koulukatu 30', N'37600', N'Valkeakoski', N'orders.c1014@example.com', N'B', N'AN', 18, 0),
    (N'C1015', N'Metsä Food Store', N'Mäkitie 11', N'33100', N'Tampere', N'orders.c1015@example.com', N'B', N'AN', 21, 0),
    (N'C1016', N'Sora Kiosk', N'Koulukatu 56', N'33500', N'Tampere', N'orders.c1016@example.com', N'A', N'AN', 10, 0),
    (N'C1017', N'Niemi Lähikauppa', N'Puistotie 27', N'33100', N'Tampere', NULL, N'B', N'AN', 16, 0),
    (N'C1018', N'Berg Service Station', N'Asemantie 47', N'33500', N'Tampere', N'orders.c1018@example.com', N'D', N'AN', 16, 0),
    (N'C1019', N'Vuori Mini Market', N'Kirkkotie 4', N'15100', N'Lahti', N'orders.c1019@example.com', N'C', N'EL', 16, 1),
    (N'C1020', N'Koivu Cafe', N'Kauppakatu 39', N'15100', N'Lahti', N'orders.c1020@example.com', N'D', N'EL', 1, 0),
    (N'C1021', N'Kivi Service Station', N'Satamakatu 33', N'13100', N'Hämeenlinna', N'orders.c1021@example.com', N'A', N'EL', 18, 0),
    (N'C1022', N'Ranta Food Store', N'Rantatie 2', N'15100', N'Lahti', N'orders.c1022@example.com', N'B', N'EL', 21, 0),
    (N'C1023', N'Sora Kiosk', N'Rantatie 20', N'13100', N'Hämeenlinna', NULL, N'D', N'EL', 16, 1),
    (N'C1024', N'Nurmi Deli', N'Rantatie 34', N'13100', N'Hämeenlinna', N'orders.c1024@example.com', N'B', N'EL', 1, 0),
    (N'C1025', N'Mäki Cafe', N'Kirkkotie 60', N'15100', N'Lahti', N'orders.c1025@example.com', N'C', N'EL', 9, 0),
    (N'C1026', N'Laakso Kiosk', N'Kauppakatu 53', N'13100', N'Hämeenlinna', N'orders.c1026@example.com', N'B', N'EL', 10, 0),
    (N'C1027', N'Harju Service Station', N'Puistotie 52', N'13100', N'Hämeenlinna', N'orders.c1027@example.com', N'D', N'EL', 9, 0),
    (N'C1028', N'Koivu Mini Market', N'Keskuskatu 60', N'21200', N'Raisio', N'orders.c1028@example.com', N'A', N'PK', 18, 0),
    (N'C1029', N'Pelto Deli', N'Satamakatu 51', N'20100', N'Turku', N'orders.c1029@example.com', N'D', N'PK', 18, 0),
    (N'C1030', N'Kivi Market', N'Mäkitie 52', N'20100', N'Turku', N'orders.c1030@example.com', N'C', N'PK', 1, 0),
    (N'C1031', N'Kallio Deli', N'Keskuskatu 10', N'21200', N'Raisio', N'orders.c1031@example.com', N'B', N'PK', 21, 0),
    (N'C1032', N'Ranta Food Store', N'Mäkitie 2', N'21200', N'Raisio', N'orders.c1032@example.com', N'D', N'PK', 21, 1),
    (N'C1033', N'Berg Market', N'Puistotie 15', N'21200', N'Raisio', N'orders.c1033@example.com', N'B', N'PK', 16, 0),
    (N'C1034', N'Laakso Lähikauppa', N'Puistotie 9', N'05800', N'Hyvinkää', NULL, N'C', NULL, 2, 0),
    (N'C1035', N'Ranta Food Store', N'Keskuskatu 35', N'00100', N'Helsinki', NULL, N'C', NULL, 2, 0),
    (N'C1036', N'Metsä Market', N'Kirkkotie 33', N'05800', N'Hyvinkää', NULL, N'C', NULL, 2, 0);
GO

-- Promotions relative to the installation date so that the demo always has current campaigns.
INSERT dbo.Promotion (ItemNo, PromoPrice, ValidFrom, ValidTo, CreatedBy) VALUES
    (N'10010', 42.00, DATEADD(day, -3, CAST(GETDATE() AS date)), DATEADD(day, 10, CAST(GETDATE() AS date)), N'mgr.virtanen'),
    (N'40010', 11.90, DATEADD(day, 0, CAST(GETDATE() AS date)), DATEADD(day, 14, CAST(GETDATE() AS date)), N'mgr.virtanen'),
    (N'50017', 29.90, DATEADD(day, 7, CAST(GETDATE() AS date)), DATEADD(day, 21, CAST(GETDATE() AS date)), N'mgr.virtanen'),
    (N'20017', 39.90, DATEADD(day, -10, CAST(GETDATE() AS date)), DATEADD(day, -1, CAST(GETDATE() AS date)), N'mgr.virtanen');
GO

INSERT dbo.AspNetRoles (Id, Name) VALUES
    (N'53545ade-286a-5988-f1af-9c65f5bc7901', N'SalesRep'),
    (N'b6125f09-008d-deb6-db95-ce7fd4c0f9e4', N'SalesManager'),
    (N'247aebfb-efdb-3c38-4c0a-ba81852414a4', N'Administrator');
GO

INSERT dbo.AspNetUsers (Id, DisplayName, RepCode, DiscountLimitPercent, Email, EmailConfirmed, PasswordHash, SecurityStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, UserName) VALUES
    (N'4d9f5d8e-6be0-9107-d1cb-30052a162b83', N'Field Sales Admin', NULL, 0, N'it-support@wingtipfoods.com', 1, N'AM03J+r55W1PjCDNXhDNYDDAt/OSbvjF3ua1W58/pE5YqjJrgKZPw0CfAVetD5fOxQ==', N'9738388b-6584-adda-bda3-e7d0a0d5a150', 0, 0, 1, 0, N'admin'),
    (N'16e62b08-4c87-6d20-0fa0-e2b3c505ccbd', N'Kaisa Virtanen', NULL, 15, N'kaisa.virtanen@wingtipfoods.com', 1, N'ADpoH+pEyhkOOFcR27UHQe4LkuIBui/sxKZShHvU4mRQdWe+XXH35cO2ESDICfvgyA==', N'aaff7cc0-2698-9ddf-9259-fcd3fac90651', 0, 0, 1, 0, N'mgr.virtanen'),
    (N'24de8b2e-dab4-cbc5-61e9-cbaf72fb5792', N'Peter Holm', NULL, 15, N'peter.holm@wingtipfoods.com', 1, N'AKru7jGykBRWmUyJXsT+NAiKVkDWZ32y4mgh+k9FaccIPGii9Ja4xBksgd0jc+jHgg==', N'3525e85d-d855-d431-8aaa-ce22b1e7e48b', 0, 0, 1, 0, N'mgr.holm'),
    (N'd7fd294c-bd11-7afa-34ea-c6d607e69972', N'Rob Jones', N'RJ', 5, N'rob.jones@wingtipfoods.com', 1, N'AH7ls8bTsJtF60zud1PCNss/X+v8lNJiZreERXX2gSWacFFYae1ORUt73vjcROy68g==', N'27957a64-6488-7dde-bc09-de4c784d1144', 0, 0, 1, 0, N'rep.jones'),
    (N'2485eea8-ddcf-1bcb-1f00-fc4bde22fce1', N'Anna Nieminen', N'AN', 5, N'anna.nieminen@wingtipfoods.com', 1, N'AEmKpf62G/eUp/9MzCBq4kvvyqAvcqvTS7EpgNZvx9HyvrYBn6vRksF5DKkCCAb7ZA==', N'9a6ff36f-99c4-044b-2874-fa58b980d142', 0, 0, 1, 0, N'rep.nieminen'),
    (N'5ad211d1-a481-c15a-fbda-1ff30b7cd456', N'Erik Lindqvist', N'EL', 7, N'erik.lindqvist@wingtipfoods.com', 1, N'AJv1wJyuyvzKUFobSa5rTvtjn2jlmUfXRDU+XZGUW/FxR7iIrbX6P9x8/LXqc+wOPA==', N'220c5444-2f3f-282e-0a80-7470f169837f', 0, 0, 1, 0, N'rep.lindqvist'),
    (N'9a87eeb4-453d-0ba2-f744-0a46ce86de6e', N'Pekka Korhonen', N'PK', 5, N'pekka.korhonen@wingtipfoods.com', 1, N'AADsVPfQ6w6EoC6vxJOIAs6L5xbUXMgUE6c1+qpf3hVfzYIVdziNjzPoPMRUJLUXNg==', N'e03aea25-6dc4-3cb6-a85e-84495843328e', 0, 0, 1, 0, N'rep.korhonen');
GO

INSERT dbo.AspNetUserRoles (UserId, RoleId) VALUES
    (N'4d9f5d8e-6be0-9107-d1cb-30052a162b83', N'247aebfb-efdb-3c38-4c0a-ba81852414a4'),
    (N'16e62b08-4c87-6d20-0fa0-e2b3c505ccbd', N'b6125f09-008d-deb6-db95-ce7fd4c0f9e4'),
    (N'16e62b08-4c87-6d20-0fa0-e2b3c505ccbd', N'247aebfb-efdb-3c38-4c0a-ba81852414a4'),
    (N'24de8b2e-dab4-cbc5-61e9-cbaf72fb5792', N'b6125f09-008d-deb6-db95-ce7fd4c0f9e4'),
    (N'd7fd294c-bd11-7afa-34ea-c6d607e69972', N'53545ade-286a-5988-f1af-9c65f5bc7901'),
    (N'2485eea8-ddcf-1bcb-1f00-fc4bde22fce1', N'53545ade-286a-5988-f1af-9c65f5bc7901'),
    (N'5ad211d1-a481-c15a-fbda-1ff30b7cd456', N'53545ade-286a-5988-f1af-9c65f5bc7901'),
    (N'9a87eeb4-453d-0ba2-f744-0a46ce86de6e', N'53545ade-286a-5988-f1af-9c65f5bc7901'),
    (N'9a87eeb4-453d-0ba2-f744-0a46ce86de6e', N'b6125f09-008d-deb6-db95-ce7fd4c0f9e4');
GO

