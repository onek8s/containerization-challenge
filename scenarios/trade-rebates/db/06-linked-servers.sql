-- Price book and workbook access through the ACE OLE DB provider (Microsoft Access Database Engine 2016 x64).
-- sqlcmd -v PriceBookPath="\\rebates-files.corp.local\Rebates$\PriceBook\PriceBook.accdb"
:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE master;
GO

EXEC sp_configure N'show advanced options', 1;
RECONFIGURE;
-- OPENROWSET over uploaded sell-out workbooks.
EXEC sp_configure N'Ad Hoc Distributed Queries', 1;
RECONFIGURE;
GO

-- ACE must run in-process; dynamic parameters are needed for parameterised OPENQUERY text.
EXEC master.dbo.sp_MSset_oledb_prop N'Microsoft.ACE.OLEDB.16.0', N'AllowInProcess', 1;
EXEC master.dbo.sp_MSset_oledb_prop N'Microsoft.ACE.OLEDB.16.0', N'DynamicParameters', 1;
GO

IF EXISTS (SELECT 1 FROM sys.servers WHERE name = N'LEGACYPRICEBOOK')
    EXEC master.dbo.sp_dropserver @server = N'LEGACYPRICEBOOK', @droplogins = 'droplogins';
GO

-- Sales operations maintain net prices in the Access price book (tables NetPrice and PriceGroup).
EXEC master.dbo.sp_addlinkedserver
    @server     = N'LEGACYPRICEBOOK',
    @srvproduct = N'Access',
    @provider   = N'Microsoft.ACE.OLEDB.16.0',
    @datasrc    = N'$(PriceBookPath)';

-- Access databases have no logins: everyone connects as the Access default user.
EXEC master.dbo.sp_addlinkedsrvlogin
    @rmtsrvname = N'LEGACYPRICEBOOK',
    @useself    = N'False',
    @locallogin = NULL,
    @rmtuser    = N'Admin',
    @rmtpassword = NULL;

EXEC master.dbo.sp_serveroption N'LEGACYPRICEBOOK', N'data access', N'true';
EXEC master.dbo.sp_serveroption N'LEGACYPRICEBOOK', N'rpc out', N'false';
EXEC master.dbo.sp_serveroption N'LEGACYPRICEBOOK', N'collation compatible', N'false';
EXEC master.dbo.sp_serveroption N'LEGACYPRICEBOOK', N'connect timeout', N'30';
EXEC master.dbo.sp_serveroption N'LEGACYPRICEBOOK', N'query timeout', N'60';
GO
