-- Registers the database assemblies. ClrPath is a folder on the database server that the
-- SQL Server service account can read, e.g. D:\TradeRebates\Clr.
-- sqlcmd -v DatabaseName=TradeRebates ClrPath="D:\TradeRebates\Clr"
:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE master;
GO
EXEC sp_configure N'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure N'clr enabled', 1;
RECONFIGURE;
GO

-- clr strict security is on: both assemblies are added to the trusted assembly list by hash.
DECLARE @files TABLE (Name sysname, FilePath nvarchar(400));
INSERT @files VALUES (N'TradeRebates.SqlClr', N'$(ClrPath)\TradeRebates.SqlClr.dll'),
                     (N'TradeRebates.SqlClr.Integration', N'$(ClrPath)\TradeRebates.SqlClr.Integration.dll');

DECLARE @name sysname, @path nvarchar(400), @hash varbinary(64), @sql nvarchar(max), @description nvarchar(4000);
DECLARE f CURSOR LOCAL FAST_FORWARD FOR SELECT Name, FilePath FROM @files;
OPEN f;
FETCH NEXT FROM f INTO @name, @path;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'SELECT @h = HASHBYTES(''SHA2_512'', BulkColumn) FROM OPENROWSET(BULK N''' + REPLACE(@path, N'''', N'''''') + N''', SINGLE_BLOB) AS b;';
    EXEC sys.sp_executesql @sql, N'@h varbinary(64) OUTPUT', @h = @hash OUTPUT;

    IF NOT EXISTS (SELECT 1 FROM sys.trusted_assemblies WHERE hash = @hash)
    BEGIN
        SET @description = @name + N', version=$(AssemblyVersion), culture=neutral, publickeytoken=null, processorarchitecture=msil';
        EXEC sys.sp_add_trusted_assembly @hash = @hash, @description = @description;
    END
    FETCH NEXT FROM f INTO @name, @path;
END
CLOSE f;
DEALLOCATE f;
GO

USE [$(DatabaseName)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.assemblies WHERE name = N'TradeRebates.SqlClr')
    CREATE ASSEMBLY [TradeRebates.SqlClr] AUTHORIZATION dbo
        FROM N'$(ClrPath)\TradeRebates.SqlClr.dll' WITH PERMISSION_SET = SAFE;
ELSE
BEGIN
    BEGIN TRY
        ALTER ASSEMBLY [TradeRebates.SqlClr] FROM N'$(ClrPath)\TradeRebates.SqlClr.dll' WITH UNCHECKED DATA;
    END TRY
    BEGIN CATCH
        -- 6285: the binary is identical to the registered one.
        IF ERROR_NUMBER() <> 6285 THROW;
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.assemblies WHERE name = N'TradeRebates.SqlClr.Integration')
    CREATE ASSEMBLY [TradeRebates.SqlClr.Integration] AUTHORIZATION dbo
        FROM N'$(ClrPath)\TradeRebates.SqlClr.Integration.dll' WITH PERMISSION_SET = UNSAFE;
ELSE
BEGIN
    BEGIN TRY
        ALTER ASSEMBLY [TradeRebates.SqlClr.Integration] FROM N'$(ClrPath)\TradeRebates.SqlClr.Integration.dll';
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 6285 THROW;
    END CATCH
END
GO

----------------------------------------------------------------------------------------------
-- Types, functions and aggregates (TradeRebates.SqlClr)
----------------------------------------------------------------------------------------------
IF TYPE_ID(N'rebate.TierSchedule') IS NULL
    CREATE TYPE rebate.TierSchedule EXTERNAL NAME [TradeRebates.SqlClr].[ContosoFoods.TradeRebates.SqlClr.TierScheduleType];
GO

CREATE OR ALTER FUNCTION rebate.GtinIsValid (@gtin nvarchar(14)) RETURNS bit
    WITH RETURNS NULL ON NULL INPUT
    AS EXTERNAL NAME [TradeRebates.SqlClr].[ContosoFoods.TradeRebates.SqlClr.ScalarFunctions].GtinIsValid;
GO
CREATE OR ALTER FUNCTION rebate.IbanIsValid (@iban nvarchar(42)) RETURNS bit
    WITH RETURNS NULL ON NULL INPUT
    AS EXTERNAL NAME [TradeRebates.SqlClr].[ContosoFoods.TradeRebates.SqlClr.ScalarFunctions].IbanIsValid;
GO
CREATE OR ALTER FUNCTION rebate.IbanMask (@iban nvarchar(42)) RETURNS nvarchar(42)
    WITH RETURNS NULL ON NULL INPUT
    AS EXTERNAL NAME [TradeRebates.SqlClr].[ContosoFoods.TradeRebates.SqlClr.ScalarFunctions].IbanMask;
GO
CREATE OR ALTER FUNCTION rebate.TierScheduleError (@text nvarchar(400)) RETURNS nvarchar(400)
    AS EXTERNAL NAME [TradeRebates.SqlClr].[ContosoFoods.TradeRebates.SqlClr.ScalarFunctions].TierScheduleError;
GO
IF OBJECT_ID(N'rebate.WeightedRate', N'AF') IS NULL
    EXEC (N'CREATE AGGREGATE rebate.WeightedRate (@rate decimal(9,4), @weight decimal(19,2)) RETURNS decimal(9,4)
            EXTERNAL NAME [TradeRebates.SqlClr].[ContosoFoods.TradeRebates.SqlClr.WeightedRate];');
GO

----------------------------------------------------------------------------------------------
-- File, finance and protection routines (TradeRebates.SqlClr.Integration)
----------------------------------------------------------------------------------------------
CREATE OR ALTER FUNCTION rebate.clr_ListFiles (@folder nvarchar(400), @pattern nvarchar(100))
RETURNS TABLE (FileName nvarchar(260), FullPath nvarchar(400), SizeBytes bigint, LastWriteUtc datetime2(0), IsLocked bit)
AS EXTERNAL NAME [TradeRebates.SqlClr.Integration].[ContosoFoods.TradeRebates.SqlClr.Integration.DropFolder].ListFiles;
GO
CREATE OR ALTER FUNCTION rebate.clr_ReadClaimFile (@path nvarchar(400), @codePage int)
RETURNS TABLE (LineNumber int, Kind nchar(1), DistributorCode nvarchar(20), Period int, InvoiceNo nvarchar(20), InvoiceDate date,
               CustomerNo nvarchar(20), Gtin nchar(14), Quantity decimal(19,4), NetAmount decimal(19,2), Message nvarchar(400))
AS EXTERNAL NAME [TradeRebates.SqlClr.Integration].[ContosoFoods.TradeRebates.SqlClr.Integration.DropFolder].ReadClaimFile;
GO
CREATE OR ALTER PROCEDURE rebate.clr_MoveFile @path nvarchar(400), @targetFolder nvarchar(400), @newPath nvarchar(400) OUTPUT
AS EXTERNAL NAME [TradeRebates.SqlClr.Integration].[ContosoFoods.TradeRebates.SqlClr.Integration.DropFolder].MoveFile;
GO
CREATE OR ALTER PROCEDURE rebate.clr_WriteTextFile @path nvarchar(400), @content nvarchar(max), @codePage int
AS EXTERNAL NAME [TradeRebates.SqlClr.Integration].[ContosoFoods.TradeRebates.SqlClr.Integration.DropFolder].WriteTextFile;
GO
CREATE OR ALTER FUNCTION rebate.clr_ProtectBankAccount (@value nvarchar(64)) RETURNS varbinary(1024)
    WITH RETURNS NULL ON NULL INPUT
    AS EXTERNAL NAME [TradeRebates.SqlClr.Integration].[ContosoFoods.TradeRebates.SqlClr.Integration.BankDetails].Protect;
GO
CREATE OR ALTER FUNCTION rebate.clr_UnprotectBankAccount (@value varbinary(1024)) RETURNS nvarchar(64)
    WITH RETURNS NULL ON NULL INPUT
    AS EXTERNAL NAME [TradeRebates.SqlClr.Integration].[ContosoFoods.TradeRebates.SqlClr.Integration.BankDetails].Unprotect;
GO
CREATE OR ALTER PROCEDURE rebate.clr_PostCreditNote
    @endpoint nvarchar(400), @vendorNo nvarchar(20), @amount decimal(19,2), @currency nvarchar(3),
    @reference nvarchar(40), @text nvarchar(200), @timeoutSeconds int, @documentNo nvarchar(20) OUTPUT
AS EXTERNAL NAME [TradeRebates.SqlClr.Integration].[ContosoFoods.TradeRebates.SqlClr.Integration.ErpPosting].PostCreditNote;
GO
CREATE OR ALTER PROCEDURE rebate.clr_WriteEvent @eventId int, @level nchar(1), @message nvarchar(4000)
AS EXTERNAL NAME [TradeRebates.SqlClr.Integration].[ContosoFoods.TradeRebates.SqlClr.Integration.EventLogWriter].WriteEvent;
GO
