/*
    Credit Desk database. Run on sql01 as a sysadmin:
        sqlcmd -S sql01.fabrikam.local -E -i CreditDesk.sql -v Domain=FABRIKAM

    dbo.Customer is refreshed every 15 minutes from the ERP by the SQL Agent job "ERP customer sync".
    Users connect with their own Windows identity (delegated through the credit service), so access
    is granted to the Credit Desk domain groups.
*/
:setvar DatabaseName CreditDesk

IF DB_ID(N'$(DatabaseName)') IS NULL
    CREATE DATABASE [$(DatabaseName)];
GO
USE [$(DatabaseName)];
GO

IF SCHEMA_ID(N'cd') IS NULL EXEC (N'CREATE SCHEMA cd AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'dbo.Customer') IS NULL
CREATE TABLE dbo.Customer
(
    AccountNumber     varchar(20)    NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    Name              nvarchar(200)  NOT NULL,
    ContactName       nvarchar(100)  NULL,
    AddressLine1      nvarchar(200)  NULL,
    AddressLine2      nvarchar(200)  NULL,
    PostalCode        nvarchar(20)   NULL,
    City              nvarchar(100)  NULL,
    CountryCode       char(2)        NOT NULL CONSTRAINT DF_Customer_Country DEFAULT ('US'),
    SalesRep          nvarchar(100)  NULL,
    CreditLimit       decimal(18,2)  NOT NULL CONSTRAINT DF_Customer_Limit DEFAULT (0),
    OpenBalance       decimal(18,2)  NOT NULL CONSTRAINT DF_Customer_Open DEFAULT (0),
    OverdueBalance    decimal(18,2)  NOT NULL CONSTRAINT DF_Customer_Overdue DEFAULT (0),
    OldestOverdueDays int            NOT NULL CONSTRAINT DF_Customer_OverdueDays DEFAULT (0),
    CustomerSince     date           NULL,
    OnStop            bit            NOT NULL CONSTRAINT DF_Customer_OnStop DEFAULT (0),
    SyncedAt          datetime2(0)   NOT NULL CONSTRAINT DF_Customer_Synced DEFAULT (SYSDATETIME())
);
GO

IF OBJECT_ID(N'cd.CreditRequest') IS NULL
CREATE TABLE cd.CreditRequest
(
    RequestId       int IDENTITY(1000,1) NOT NULL CONSTRAINT PK_CreditRequest PRIMARY KEY,
    AccountNumber   varchar(20)    NOT NULL CONSTRAINT FK_CreditRequest_Customer REFERENCES dbo.Customer (AccountNumber),
    CurrentLimit    decimal(18,2)  NOT NULL,
    RequestedLimit  decimal(18,2)  NOT NULL,
    Reason          nvarchar(1000) NOT NULL,
    Status          tinyint        NOT NULL CONSTRAINT DF_CreditRequest_Status DEFAULT (0),
    RequiredLevel   tinyint        NOT NULL,
    RequestedBy     nvarchar(128)  NOT NULL CONSTRAINT DF_CreditRequest_By DEFAULT (SUSER_SNAME()),
    RequestedAt     datetime2(0)   NOT NULL CONSTRAINT DF_CreditRequest_At DEFAULT (SYSDATETIME()),
    DecidedBy       nvarchar(128)  NULL,
    DecidedAt       datetime2(0)   NULL,
    DecisionComment nvarchar(1000) NULL,
    ValidUntil      date           NULL,
    LetterPath      nvarchar(400)  NULL,
    LetterSentAt    datetime2(0)   NULL
);
GO

IF OBJECT_ID(N'cd.CreditLimitHistory') IS NULL
CREATE TABLE cd.CreditLimitHistory
(
    HistoryId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CreditLimitHistory PRIMARY KEY,
    AccountNumber varchar(20)   NOT NULL,
    OldLimit      decimal(18,2) NOT NULL,
    NewLimit      decimal(18,2) NOT NULL,
    RequestId     int           NULL,
    ChangedBy     nvarchar(128) NOT NULL CONSTRAINT DF_History_By DEFAULT (SUSER_SNAME()),
    ChangedAt     datetime2(0)  NOT NULL CONSTRAINT DF_History_At DEFAULT (SYSDATETIME()),
    ExportedToErp bit           NOT NULL CONSTRAINT DF_History_Exported DEFAULT (0)
);
GO

CREATE OR ALTER VIEW cd.RequestView
AS
SELECT r.RequestId, r.AccountNumber, c.Name AS CustomerName, r.CurrentLimit, r.RequestedLimit, r.Reason,
       r.Status, r.RequiredLevel, r.RequestedBy, r.RequestedAt, r.DecidedBy, r.DecidedAt, r.DecisionComment,
       r.ValidUntil, r.LetterPath
FROM cd.CreditRequest r
JOIN dbo.Customer c ON c.AccountNumber = r.AccountNumber;
GO

CREATE OR ALTER PROCEDURE cd.SearchCustomers @Text nvarchar(100), @Max int = 50
AS
SET NOCOUNT ON;
SELECT TOP (@Max) AccountNumber, Name, ContactName, AddressLine1, AddressLine2, PostalCode, City, CountryCode,
       SalesRep, CreditLimit, OpenBalance, OverdueBalance, OldestOverdueDays, CustomerSince, OnStop
FROM dbo.Customer
WHERE AccountNumber = @Text OR Name LIKE N'%' + @Text + N'%' OR City LIKE @Text + N'%'
ORDER BY CASE WHEN AccountNumber = @Text THEN 0 ELSE 1 END, Name;
GO

CREATE OR ALTER PROCEDURE cd.GetCustomer @AccountNumber varchar(20)
AS
SET NOCOUNT ON;
SELECT AccountNumber, Name, ContactName, AddressLine1, AddressLine2, PostalCode, City, CountryCode,
       SalesRep, CreditLimit, OpenBalance, OverdueBalance, OldestOverdueDays, CustomerSince, OnStop
FROM dbo.Customer
WHERE AccountNumber = @AccountNumber;
GO

CREATE OR ALTER PROCEDURE cd.InsertRequest
    @AccountNumber varchar(20), @CurrentLimit decimal(18,2), @RequestedLimit decimal(18,2),
    @Reason nvarchar(1000), @RequiredLevel tinyint
AS
SET NOCOUNT ON;
DECLARE @id int;
INSERT cd.CreditRequest (AccountNumber, CurrentLimit, RequestedLimit, Reason, RequiredLevel)
VALUES (@AccountNumber, @CurrentLimit, @RequestedLimit, @Reason, @RequiredLevel);
SET @id = SCOPE_IDENTITY();
SELECT * FROM cd.RequestView WHERE RequestId = @id;
GO

CREATE OR ALTER PROCEDURE cd.GetRequest @RequestId int
AS
SET NOCOUNT ON;
SELECT * FROM cd.RequestView WHERE RequestId = @RequestId;
GO

CREATE OR ALTER PROCEDURE cd.GetMyRequests
AS
SET NOCOUNT ON;
SELECT TOP (200) * FROM cd.RequestView WHERE RequestedBy = SUSER_SNAME() ORDER BY RequestedAt DESC;
GO

CREATE OR ALTER PROCEDURE cd.GetPendingRequests @MaxLevel tinyint
AS
SET NOCOUNT ON;
SELECT * FROM cd.RequestView WHERE Status IN (0, 3) AND RequiredLevel <= @MaxLevel ORDER BY RequestedAt;
GO

CREATE OR ALTER PROCEDURE cd.GetRequestsForCustomer @AccountNumber varchar(20)
AS
SET NOCOUNT ON;
SELECT * FROM cd.RequestView WHERE AccountNumber = @AccountNumber ORDER BY RequestedAt DESC;
GO

/* Status: 0 Submitted, 1 Approved, 2 Rejected, 3 Escalated, 4 Withdrawn */
CREATE OR ALTER PROCEDURE cd.DecideRequest
    @RequestId int, @Status tinyint, @Comment nvarchar(1000) = NULL, @ValidUntil date = NULL
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @account varchar(20), @current decimal(18,2), @requested decimal(18,2), @old tinyint;
SELECT @account = AccountNumber, @current = CurrentLimit, @requested = RequestedLimit, @old = Status
FROM cd.CreditRequest WITH (UPDLOCK, HOLDLOCK)
WHERE RequestId = @RequestId;

IF @account IS NULL
    THROW 50001, N'Request not found.', 1;
IF @old NOT IN (0, 3)
    THROW 50001, N'Request has already been decided.', 1;
IF @Status = 1 AND EXISTS (SELECT 1 FROM dbo.Customer WHERE AccountNumber = @account AND CreditLimit <> @current)
    THROW 50001, N'The customer''s credit limit changed after the request was made. Submit a new request.', 1;

UPDATE cd.CreditRequest
SET Status = @Status, DecidedBy = SUSER_SNAME(), DecidedAt = SYSDATETIME(), DecisionComment = @Comment, ValidUntil = @ValidUntil
WHERE RequestId = @RequestId;

IF @Status = 1
BEGIN
    UPDATE dbo.Customer SET CreditLimit = @requested WHERE AccountNumber = @account;
    INSERT cd.CreditLimitHistory (AccountNumber, OldLimit, NewLimit, RequestId) VALUES (@account, @current, @requested, @RequestId);
END

COMMIT;
SELECT * FROM cd.RequestView WHERE RequestId = @RequestId;
GO

CREATE OR ALTER PROCEDURE cd.MarkLetterSent @RequestId int, @LetterPath nvarchar(400)
AS
SET NOCOUNT ON;
UPDATE cd.CreditRequest SET LetterPath = @LetterPath, LetterSentAt = SYSDATETIME() WHERE RequestId = @RequestId;
GO

/* Windows logins */
IF SUSER_ID(N'$(Domain)\CreditDesk Sales') IS NULL CREATE LOGIN [$(Domain)\CreditDesk Sales] FROM WINDOWS;
IF SUSER_ID(N'$(Domain)\CreditDesk Controllers') IS NULL CREATE LOGIN [$(Domain)\CreditDesk Controllers] FROM WINDOWS;
IF SUSER_ID(N'$(Domain)\CreditDesk Finance Managers') IS NULL CREATE LOGIN [$(Domain)\CreditDesk Finance Managers] FROM WINDOWS;
IF SUSER_ID(N'$(Domain)\CreditDesk Committee') IS NULL CREATE LOGIN [$(Domain)\CreditDesk Committee] FROM WINDOWS;
IF SUSER_ID(N'$(Domain)\svc-creditdesk-doc') IS NULL CREATE LOGIN [$(Domain)\svc-creditdesk-doc] FROM WINDOWS;
GO

IF DATABASE_PRINCIPAL_ID(N'CreditDeskUser') IS NULL CREATE ROLE CreditDeskUser;
IF DATABASE_PRINCIPAL_ID(N'CreditDeskLetters') IS NULL CREATE ROLE CreditDeskLetters;
GRANT EXECUTE ON SCHEMA::cd TO CreditDeskUser;
GRANT EXECUTE ON OBJECT::cd.MarkLetterSent TO CreditDeskLetters;
GO

DECLARE @g sysname, @sql nvarchar(400);
DECLARE groups CURSOR LOCAL FAST_FORWARD FOR
    SELECT N'$(Domain)\CreditDesk Sales' UNION ALL SELECT N'$(Domain)\CreditDesk Controllers'
    UNION ALL SELECT N'$(Domain)\CreditDesk Finance Managers' UNION ALL SELECT N'$(Domain)\CreditDesk Committee';
OPEN groups;
FETCH NEXT FROM groups INTO @g;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF DATABASE_PRINCIPAL_ID(@g) IS NULL
    BEGIN
        SET @sql = N'CREATE USER ' + QUOTENAME(@g) + N' FOR LOGIN ' + QUOTENAME(@g);
        EXEC (@sql);
    END
    SET @sql = N'ALTER ROLE CreditDeskUser ADD MEMBER ' + QUOTENAME(@g);
    EXEC (@sql);
    FETCH NEXT FROM groups INTO @g;
END
CLOSE groups;

IF DATABASE_PRINCIPAL_ID(N'$(Domain)\svc-creditdesk-doc') IS NULL
    CREATE USER [$(Domain)\svc-creditdesk-doc] FOR LOGIN [$(Domain)\svc-creditdesk-doc];
ALTER ROLE CreditDeskLetters ADD MEMBER [$(Domain)\svc-creditdesk-doc];
GO
