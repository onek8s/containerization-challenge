INSERT INTO Projects (Code, Name, Client, Active) VALUES
 ('P1042', 'Kemira Oulu bleach plant upgrade', 'Pohjola Pulp Oy', 1),
 ('P1057', 'Harbour terminal conveyor line 3', 'Northwind Ports', 1),
 ('P0988', 'Riverside water treatment', 'City of Riverside', 0);

INSERT INTO Documents (Number, ProjectCode, Discipline, DocumentType, Title, CreatedUtc, CreatedBy) VALUES
 ('P1042-LIT-ME-DRG-0012', 'P1042', 'ME', 'DRG', 'Bleach tower 2 general arrangement', '2024-03-04 08:12:00', 'CORP\mvirtanen'),
 ('P1042-LIT-PI-PID-0003', 'P1042', 'PI', 'PID', 'Chlorine dioxide dosing P&ID', '2024-03-05 09:40:00', 'CORP\mvirtanen'),
 ('P1042-LIT-EL-LST-0001', 'P1042', 'EL', 'LST', 'Motor list', '2024-03-11 13:05:00', 'CORP\jkorhonen'),
 ('P1057-LIT-ST-CAL-0004', 'P1057', 'ST', 'CAL', 'Conveyor gallery support steel calculation', '2024-05-21 07:55:00', 'CORP\asalo');

INSERT INTO Revisions (DocumentId, Code, Status, FileName, VaultPath, Size, Sha256, Source, SubmittedBy, SubmittedUtc, ReviewedBy, ReviewedUtc, ReviewComment) VALUES
 (1, 'P01', 3, 'P1042-LIT-ME-DRG-0012_P01.pdf', 'P1042/P1042-LIT-ME-DRG-0012/P01/P1042-LIT-ME-DRG-0012_P01.pdf', 0, '', 'Portal', 'CORP\mvirtanen', '2024-03-04 08:12:00', 'CORP\lniemi', '2024-03-06 10:00:00', NULL),
 (1, 'P02', 1, 'P1042-LIT-ME-DRG-0012_P02.pdf', 'P1042/P1042-LIT-ME-DRG-0012/P02/P1042-LIT-ME-DRG-0012_P02.pdf', 0, '', 'Drop', 'CORP\svc-dc-indexer', '2024-04-02 14:30:00', 'CORP\lniemi', '2024-04-03 09:15:00', 'Nozzle N4 moved per client comment'),
 (2, 'P01', 0, 'P1042-LIT-PI-PID-0003_P01.pdf', 'P1042/P1042-LIT-PI-PID-0003/P01/P1042-LIT-PI-PID-0003_P01.pdf', 0, '', 'Portal', 'CORP\mvirtanen', '2024-03-05 09:40:00', NULL, NULL, NULL),
 (3, 'P01', 1, 'P1042-LIT-EL-LST-0001_P01.xlsx', 'P1042/P1042-LIT-EL-LST-0001/P01/P1042-LIT-EL-LST-0001_P01.xlsx', 0, '', 'Portal', 'CORP\jkorhonen', '2024-03-11 13:05:00', 'CORP\lniemi', '2024-03-12 08:00:00', NULL),
 (4, 'C01', 1, 'P1057-LIT-ST-CAL-0004_C01.pdf', 'P1057/P1057-LIT-ST-CAL-0004/C01/P1057-LIT-ST-CAL-0004_C01.pdf', 0, '', 'Portal', 'CORP\asalo', '2024-05-21 07:55:00', 'CORP\lniemi', '2024-05-22 11:20:00', NULL);
