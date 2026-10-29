:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE [$(DatabaseName)];
GO

----------------------------------------------------------------------------------------------
-- Portal
----------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE rebate.Dashboard
AS
SET NOCOUNT ON;

SELECT
    (SELECT COUNT(*) FROM rebate.Claim WHERE Status = 'V') AS ClaimsToReview,
    (SELECT COUNT(*) FROM rebate.Claim WHERE Status = 'V' AND WarningCount > 0) AS ClaimsWithWarnings,
    (SELECT COUNT(*) FROM rebate.Agreement WHERE Status = 'A' AND CAST(GETDATE() AS date) BETWEEN ValidFrom AND ValidTo) AS ActiveAgreements,
    (SELECT ISNULL(SUM(AccruedAmount), 0) FROM rebate.Accrual WHERE Period / 100 = YEAR(GETDATE())) AS AccruedThisYear,
    (SELECT ISNULL(SUM(AccruedAmount), 0) FROM rebate.Accrual WHERE StatementId IS NULL) AS OpenAccruals,
    (SELECT COUNT(*) FROM rebate.Statement WHERE Status IN ('Q', 'R')) AS StatementsInProgress,
    (SELECT COUNT(*) FROM rebate.Statement WHERE Status = 'F') AS StatementsFailed;

-- Agreements closest to their next tier (year to date).
SELECT TOP (10) a.AgreementId, a.AgreementNo, d.Name AS DistributorName, a.Basis, x.Period, x.YtdQuantity, x.YtdNetAmount,
       x.RatePercent, x.ToNextTier, a.Tiers.RateFor(CASE a.Basis WHEN 'Q' THEN x.YtdQuantity ELSE x.YtdNetAmount END + x.ToNextTier) AS NextRate
FROM rebate.Accrual AS x
JOIN rebate.Agreement AS a ON a.AgreementId = x.AgreementId
JOIN rebate.Distributor AS d ON d.DistributorId = a.DistributorId
WHERE x.ToNextTier IS NOT NULL
  AND x.Period = (SELECT MAX(Period) FROM rebate.Accrual AS y WHERE y.AgreementId = x.AgreementId)
ORDER BY CASE a.Basis WHEN 'Q' THEN x.ToNextTier / NULLIF(x.YtdQuantity, 0) ELSE x.ToNextTier / NULLIF(x.YtdNetAmount, 0) END;

-- Effective rate per distributor this year.
SELECT d.Code, d.Name, SUM(x.NetAmount) AS NetAmount, SUM(x.AccruedAmount) AS Accrued,
       rebate.WeightedRate(x.RatePercent, x.NetAmount) AS AverageRate
FROM rebate.Accrual AS x
JOIN rebate.Agreement AS a ON a.AgreementId = x.AgreementId
JOIN rebate.Distributor AS d ON d.DistributorId = a.DistributorId
WHERE x.Period / 100 = YEAR(GETDATE())
GROUP BY d.Code, d.Name
ORDER BY d.Code;
GO

CREATE OR ALTER PROCEDURE rebate.ListDistributors
AS
SET NOCOUNT ON;
SELECT DistributorId, Code, Name, ErpVendorNo, Currency, StatementEmail, StatementFolder, BankAccountMasked, IsActive, ModifiedBy, ModifiedAt
FROM rebate.Distributor
ORDER BY Name;
GO

CREATE OR ALTER PROCEDURE rebate.SaveDistributorBankAccount
    @DistributorId int, @Iban nvarchar(42), @Actor nvarchar(128)
AS
SET NOCOUNT, XACT_ABORT ON;

DECLARE @normalized nvarchar(42) = UPPER(REPLACE(REPLACE(@Iban, N' ', N''), N'-', N''));
IF ISNULL(rebate.IbanIsValid(@normalized), 0) = 0
    THROW 50050, N'The IBAN is not valid.', 1;

UPDATE rebate.Distributor
SET BankAccountProtected = rebate.clr_ProtectBankAccount(@normalized),
    BankAccountMasked = rebate.IbanMask(@normalized),
    ModifiedBy = @Actor, ModifiedAt = SYSDATETIME()
WHERE DistributorId = @DistributorId;

IF @@ROWCOUNT = 0
    THROW 50051, N'Distributor not found.', 1;

DECLARE @id nvarchar(40) = CAST(@DistributorId AS nvarchar(40));
DECLARE @masked nvarchar(42) = rebate.IbanMask(@normalized);
EXEC rebate.WriteAudit @Actor, 'BankAccountChanged', 'Distributor', @id, @masked;
GO

-- Finance only (portal role Admin); every read is audited.
CREATE OR ALTER PROCEDURE rebate.RevealDistributorBankAccount @DistributorId int, @Actor nvarchar(128)
AS
SET NOCOUNT ON;
DECLARE @id nvarchar(40) = CAST(@DistributorId AS nvarchar(40));
EXEC rebate.WriteAudit @Actor, 'BankAccountRevealed', 'Distributor', @id, NULL;
SELECT rebate.clr_UnprotectBankAccount(BankAccountProtected) AS Iban
FROM rebate.Distributor WHERE DistributorId = @DistributorId;
GO

CREATE OR ALTER PROCEDURE rebate.ListAgreements @DistributorId int = NULL, @Status char(1) = NULL
AS
SET NOCOUNT ON;
SELECT a.AgreementId, a.AgreementNo, a.Description, d.Code AS DistributorCode, d.Name AS DistributorName, a.Basis, a.Category,
       a.TiersText, a.Tiers.Mode AS TierMode, a.Tiers.TierCount AS TierCount, a.ValidFrom, a.ValidTo, a.CapAmount, a.Status,
       (SELECT SUM(x.AccruedAmount) FROM rebate.Accrual AS x WHERE x.AgreementId = a.AgreementId) AS Accrued
FROM rebate.Agreement AS a
JOIN rebate.Distributor AS d ON d.DistributorId = a.DistributorId
WHERE (@DistributorId IS NULL OR a.DistributorId = @DistributorId)
  AND (@Status IS NULL OR a.Status = @Status)
ORDER BY a.ValidFrom DESC, a.AgreementNo;
GO

CREATE OR ALTER PROCEDURE rebate.GetAgreement @AgreementId int
AS
SET NOCOUNT ON;
SELECT a.AgreementId, a.AgreementNo, a.DistributorId, d.Name AS DistributorName, a.Description, a.Basis, a.Category, a.TiersText,
       a.ValidFrom, a.ValidTo, a.CapAmount, a.Status, a.ApprovedBy, a.ApprovedAt, a.ModifiedBy, a.ModifiedAt, a.RowVer
FROM rebate.Agreement AS a
JOIN rebate.Distributor AS d ON d.DistributorId = a.DistributorId
WHERE a.AgreementId = @AgreementId;

SELECT Period, Quantity, NetAmount, YtdQuantity, YtdNetAmount, RatePercent, YtdRebate, AccruedAmount, ToNextTier, StatementId
FROM rebate.Accrual WHERE AgreementId = @AgreementId ORDER BY Period;
GO

CREATE OR ALTER PROCEDURE rebate.SaveAgreement
    @AgreementId int = NULL, @AgreementNo nvarchar(20), @DistributorId int, @Description nvarchar(200), @Basis char(1),
    @Category nvarchar(40), @Tiers nvarchar(400), @ValidFrom date, @ValidTo date, @CapAmount decimal(19,2),
    @RowVer binary(8) = NULL, @Actor nvarchar(128), @NewAgreementId int = NULL OUTPUT
AS
SET NOCOUNT, XACT_ABORT ON;

DECLARE @error nvarchar(400) = rebate.TierScheduleError(@Tiers);
IF @error IS NOT NULL
    THROW 50060, @error, 1;
IF @ValidTo < @ValidFrom
    THROW 50061, N'Valid to must be on or after valid from.', 1;
IF @Basis NOT IN ('Q', 'V')
    THROW 50062, N'Basis must be Q (quantity) or V (net value).', 1;
IF EXISTS (SELECT 1 FROM rebate.Agreement WHERE AgreementNo = @AgreementNo AND (@AgreementId IS NULL OR AgreementId <> @AgreementId))
    THROW 50065, N'The agreement number is already in use.', 1;

DECLARE @schedule rebate.TierSchedule = CAST(@Tiers AS rebate.TierSchedule);

IF @AgreementId IS NULL
BEGIN
    INSERT rebate.Agreement (AgreementNo, DistributorId, Description, Basis, Category, Tiers, ValidFrom, ValidTo, CapAmount, ModifiedBy)
    VALUES (@AgreementNo, @DistributorId, @Description, @Basis, NULLIF(@Category, N''), @schedule, @ValidFrom, @ValidTo, @CapAmount, @Actor);
    SET @NewAgreementId = CAST(SCOPE_IDENTITY() AS int);
END
ELSE
BEGIN
    UPDATE rebate.Agreement
    SET AgreementNo = @AgreementNo, DistributorId = @DistributorId, Description = @Description, Basis = @Basis,
        Category = NULLIF(@Category, N''), Tiers = @schedule, ValidFrom = @ValidFrom, ValidTo = @ValidTo, CapAmount = @CapAmount,
        ModifiedBy = @Actor, ModifiedAt = SYSDATETIME()
    WHERE AgreementId = @AgreementId AND RowVer = @RowVer AND Status = 'D';

    IF @@ROWCOUNT = 0
        THROW 50063, N'Only draft agreements can be changed, or the agreement was changed by someone else.', 1;
    SET @NewAgreementId = @AgreementId;
END

DECLARE @id nvarchar(40) = CAST(@NewAgreementId AS nvarchar(40));
EXEC rebate.WriteAudit @Actor, 'AgreementSaved', 'Agreement', @id, @Tiers;
GO

CREATE OR ALTER PROCEDURE rebate.SetAgreementStatus @AgreementId int, @Status char(1), @Actor nvarchar(128)
AS
SET NOCOUNT, XACT_ABORT ON;
UPDATE rebate.Agreement
SET Status = @Status,
    ApprovedBy = CASE WHEN @Status = 'A' THEN @Actor ELSE ApprovedBy END,
    ApprovedAt = CASE WHEN @Status = 'A' THEN SYSDATETIME() ELSE ApprovedAt END,
    ModifiedBy = @Actor, ModifiedAt = SYSDATETIME()
WHERE AgreementId = @AgreementId
  AND ((@Status = 'A' AND Status = 'D') OR (@Status = 'C' AND Status = 'A'));
IF @@ROWCOUNT = 0
    THROW 50064, N'Agreement status cannot be changed that way.', 1;
DECLARE @id nvarchar(40) = CAST(@AgreementId AS nvarchar(40));
DECLARE @action varchar(40) = CASE @Status WHEN 'A' THEN 'AgreementActivated' ELSE 'AgreementClosed' END;
EXEC rebate.WriteAudit @Actor, @action, 'Agreement', @id, NULL;
GO

CREATE OR ALTER PROCEDURE rebate.ListClaims @Status char(1) = NULL, @DistributorId int = NULL, @Period int = NULL, @Top int = 200
AS
SET NOCOUNT ON;
SELECT TOP (@Top) c.ClaimId, d.Code AS DistributorCode, d.Name AS DistributorName, c.Period, c.Source, c.SourceRef, c.Status,
       c.LineCount, c.TotalQuantity, c.TotalAmount, c.WarningCount, c.RejectedCount, c.ReceivedAt, c.ReceivedBy, c.DecidedBy, c.DecidedAt
FROM rebate.Claim AS c
JOIN rebate.Distributor AS d ON d.DistributorId = c.DistributorId
WHERE (@Status IS NULL OR c.Status = @Status)
  AND (@DistributorId IS NULL OR c.DistributorId = @DistributorId)
  AND (@Period IS NULL OR c.Period = @Period)
ORDER BY c.ClaimId DESC;
GO

CREATE OR ALTER PROCEDURE rebate.GetClaim @ClaimId int
AS
SET NOCOUNT ON;
SELECT c.ClaimId, c.DistributorId, d.Code AS DistributorCode, d.Name AS DistributorName, c.Period, c.Source, c.SourceRef, c.Status,
       c.LineCount, c.TotalQuantity, c.TotalAmount, c.WarningCount, c.RejectedCount, c.ReceivedAt, c.ReceivedBy, c.ValidatedAt,
       c.DecidedBy, c.DecidedAt, c.DecisionNote, c.RowVer
FROM rebate.Claim AS c
JOIN rebate.Distributor AS d ON d.DistributorId = c.DistributorId
WHERE c.ClaimId = @ClaimId;

SELECT l.LineNumber, l.InvoiceNo, l.InvoiceDate, l.CustomerNo, l.Gtin, p.Description AS ProductDescription, l.Quantity, l.NetAmount,
       l.ListNetPrice, l.LineStatus, l.Issue
FROM rebate.ClaimLine AS l
LEFT JOIN rebate.Product AS p ON p.Gtin = l.Gtin
WHERE l.ClaimId = @ClaimId
ORDER BY l.LineNumber;

SELECT LineNumber, Message FROM rebate.ClaimIssue WHERE ClaimId = @ClaimId ORDER BY LineNumber, ClaimIssueId;
GO

CREATE OR ALTER PROCEDURE rebate.ListStatements @Quarter char(6) = NULL
AS
SET NOCOUNT ON;
SELECT s.StatementId, s.Quarter, d.Code AS DistributorCode, d.Name AS DistributorName, s.Amount, s.Status, s.RequestedBy, s.RequestedAt,
       s.FilePath, s.ErpDocumentNo, s.CompletedAt, s.ErrorMessage
FROM rebate.Statement AS s
JOIN rebate.Distributor AS d ON d.DistributorId = s.DistributorId
WHERE @Quarter IS NULL OR s.Quarter = @Quarter
ORDER BY s.Quarter DESC, d.Code;
GO

CREATE OR ALTER PROCEDURE rebate.ListAudit @Entity varchar(40) = NULL, @EntityId nvarchar(40) = NULL, @Top int = 200
AS
SET NOCOUNT ON;
SELECT TOP (@Top) AuditId, At, Actor, Action, Entity, EntityId, Detail
FROM rebate.AuditLog
WHERE (@Entity IS NULL OR Entity = @Entity) AND (@EntityId IS NULL OR EntityId = @EntityId)
ORDER BY AuditId DESC;
GO

----------------------------------------------------------------------------------------------
-- Distributor API
----------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE rebate.ResolveApiClient @KeyHash binary(32)
AS
SET NOCOUNT ON;
UPDATE c SET LastUsedAt = SYSDATETIME()
OUTPUT inserted.ApiClientId, inserted.DistributorId, d.Code AS DistributorCode, d.Name AS DistributorName
FROM rebate.ApiClient AS c
JOIN rebate.Distributor AS d ON d.DistributorId = c.DistributorId
WHERE c.KeyHash = @KeyHash AND c.IsActive = 1 AND d.IsActive = 1;
GO

CREATE OR ALTER PROCEDURE rebate.CreateApiClient
    @DistributorCode nvarchar(20), @Name nvarchar(100), @KeyPrefix char(8), @KeyHash binary(32)
AS
SET NOCOUNT, XACT_ABORT ON;
DECLARE @dist int = (SELECT DistributorId FROM rebate.Distributor WHERE Code = @DistributorCode);
IF @dist IS NULL
    THROW 50070, N'Unknown distributor code.', 1;
INSERT rebate.ApiClient (DistributorId, Name, KeyPrefix, KeyHash) VALUES (@dist, @Name, @KeyPrefix, @KeyHash);
DECLARE @id nvarchar(40) = CAST(SCOPE_IDENTITY() AS nvarchar(40));
EXEC rebate.WriteAudit NULL, 'ApiClientCreated', 'ApiClient', @id, @KeyPrefix;
SELECT CAST(@id AS int) AS ApiClientId;
GO

CREATE OR ALTER PROCEDURE rebate.ListDistributorClaims @DistributorId int, @Period int = NULL
AS
SET NOCOUNT ON;
SELECT ClaimId, Period, Source, SourceRef, Status, LineCount, TotalQuantity, TotalAmount, WarningCount, RejectedCount, ReceivedAt, DecidedAt
FROM rebate.Claim
WHERE DistributorId = @DistributorId AND (@Period IS NULL OR Period = @Period)
ORDER BY ClaimId DESC;
GO

CREATE OR ALTER PROCEDURE rebate.ListDistributorAgreements @DistributorId int
AS
SET NOCOUNT ON;
SELECT a.AgreementNo, a.Description, a.Basis, a.Category, a.TiersText, a.ValidFrom, a.ValidTo,
       x.Period AS LastPeriod, x.YtdQuantity, x.YtdNetAmount, x.RatePercent, x.YtdRebate, x.ToNextTier
FROM rebate.Agreement AS a
OUTER APPLY (SELECT TOP (1) * FROM rebate.Accrual AS y WHERE y.AgreementId = a.AgreementId ORDER BY y.Period DESC) AS x
WHERE a.DistributorId = @DistributorId AND a.Status = 'A'
ORDER BY a.ValidFrom DESC;
GO
