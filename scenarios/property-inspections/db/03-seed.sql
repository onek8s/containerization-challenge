-- Demo accounts. Adjuster password: Inspect!2024, partner password: Partner!2024.
-- Create other hashes with: Inspections.Jobs.exe hash-password
SET NOCOUNT ON;

INSERT INTO dbo.Adjusters (Login, DisplayName, Email, Company, PasswordHash, IsSupervisor)
VALUES (N'akelly',  N'Aoife Kelly',   N'aoife.kelly@northwind-insurance.example', N'Northwind Insurance', 'PBKDF2$10000$qGktQzExAUAHNf90Wdn31w==$VkVx0K3zIM9tnTH62bOjlbxkfKgfaqhJ/+4uKb0mkvc=', 1),
       (N'pbrennan', N'Paul Brennan', N'paul.brennan@lossadjusters.example',     N'Brennan Loss Adjusters', 'PBKDF2$10000$qGktQzExAUAHNf90Wdn31w==$VkVx0K3zIM9tnTH62bOjlbxkfKgfaqhJ/+4uKb0mkvc=', 0),
       (N'smurphy', N'Siobhan Murphy', N'smurphy@westcoastclaims.example',        N'West Coast Claims Ltd', 'PBKDF2$10000$qGktQzExAUAHNf90Wdn31w==$VkVx0K3zIM9tnTH62bOjlbxkfKgfaqhJ/+4uKb0mkvc=', 0);

INSERT INTO dbo.Partners (UserName, Name, PasswordHash)
VALUES (N'claims-core', N'Claims system integration', 'PBKDF2$10000$+xVZF3SHKGLlkoZD8qRPRg==$Olou08kYpeiesvaySeRsGSEhAzcfl93gV4hZNjQj9uE=');

INSERT INTO dbo.Inspections (ClaimNumber, PolicyHolder, PropertyAddress, PerilCode, InspectedOn, AdjusterId, HandlerEmail, Status, EstimatedLoss, Notes, CreatedUtc)
VALUES ('HO-2024-004512', N'Mary O''Brien', N'12 Harbour Road, Cobh, Co. Cork', 'WAT', '2024-05-17', 2, N'claims.cork@northwind-insurance.example', 1, 18250, N'Ceiling collapse in kitchen after burst pipe in bathroom above.', SYSUTCDATETIME()),
       ('CO-2024-000871', N'Quayside Bakery Ltd', N'4 Merchant''s Quay, Limerick', 'FIR', '2024-06-02', 3, N'commercial.claims@northwind-insurance.example', 0, NULL, NULL, SYSUTCDATETIME()),
       ('HO-2024-005120', N'Declan Walsh', N'Rose Cottage, Kinvara, Co. Galway', 'STO', '2024-06-11', 2, N'claims.galway@northwind-insurance.example', 0, 4300, N'Slates missing on north roof slope.', SYSUTCDATETIME());
