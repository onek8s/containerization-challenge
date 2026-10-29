-- Logins and permissions for the service accounts.
--   sqlcmd -S sql01.corp.local -E -d SupplierInvoices -i 05-logins.sql `
--     -v GatewayAccount="CORP\svc-inv-gw" ProcessorAccount="CORP\svc-inv-proc" ErpLinkAccount="CORP\svc-inv-erp" WebAccount="CORP\svc-inv-web"
SET NOCOUNT ON;
GO

IF DATABASE_PRINCIPAL_ID('inv_gateway') IS NULL CREATE ROLE inv_gateway;
IF DATABASE_PRINCIPAL_ID('inv_processor') IS NULL CREATE ROLE inv_processor;
IF DATABASE_PRINCIPAL_ID('inv_erplink') IS NULL CREATE ROLE inv_erplink;
IF DATABASE_PRINCIPAL_ID('inv_web') IS NULL CREATE ROLE inv_web;
GO

GRANT EXECUTE ON inv.GetActiveChannels TO inv_gateway;
GRANT EXECUTE ON inv.SetChannelPolled TO inv_gateway;
GRANT EXECUTE ON inv.RegisterInboundFile TO inv_gateway;
GRANT EXECUTE ON inv.SetFileArchived TO inv_gateway;

GRANT EXECUTE ON SCHEMA::inv TO inv_processor;
GRANT EXECUTE ON TYPE::inv.InvoiceLineType TO inv_processor;
GRANT EXECUTE ON TYPE::inv.IssueType TO inv_processor;
GRANT EXECUTE ON TYPE::inv.LineAccountType TO inv_processor;

GRANT EXECUTE ON inv.QueuePosting TO inv_erplink;
GRANT EXECUTE ON inv.CreatePostingBatch TO inv_erplink;
GRANT EXECUTE ON inv.GetPostingBatch TO inv_erplink;
GRANT EXECUTE ON inv.SetBatchWritten TO inv_erplink;
GRANT EXECUTE ON inv.ApplyAcknowledgement TO inv_erplink;
GRANT SELECT ON inv.PostingQueue TO inv_erplink;
GRANT SELECT ON inv.PostingBatch TO inv_erplink;

GRANT SELECT ON SCHEMA::inv TO inv_web;
GRANT INSERT, UPDATE ON inv.Supplier TO inv_web;
GRANT INSERT, UPDATE ON inv.Channel TO inv_web;
GRANT EXECUTE ON inv.RejectInvoice TO inv_web;
GRANT EXECUTE ON inv.AddInvoiceHistory TO inv_web;
GO

DECLARE @accounts TABLE (Account sysname, RoleName sysname);
INSERT @accounts VALUES
    (N'$(GatewayAccount)', N'inv_gateway'),
    (N'$(ProcessorAccount)', N'inv_processor'),
    (N'$(ErpLinkAccount)', N'inv_erplink'),
    (N'$(WebAccount)', N'inv_web');

DECLARE @account sysname, @role sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Account, RoleName FROM @accounts;
OPEN c;
FETCH NEXT FROM c INTO @account, @role;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF SUSER_ID(@account) IS NULL
    BEGIN
        SET @sql = N'CREATE LOGIN ' + QUOTENAME(@account) + N' FROM WINDOWS WITH DEFAULT_DATABASE = SupplierInvoices';
        EXEC (@sql);
    END
    IF DATABASE_PRINCIPAL_ID(@account) IS NULL
    BEGIN
        SET @sql = N'CREATE USER ' + QUOTENAME(@account) + N' FOR LOGIN ' + QUOTENAME(@account) + N' WITH DEFAULT_SCHEMA = inv';
        EXEC (@sql);
    END
    SET @sql = N'ALTER ROLE ' + QUOTENAME(@role) + N' ADD MEMBER ' + QUOTENAME(@account);
    EXEC (@sql);
    FETCH NEXT FROM c INTO @account, @role;
END
CLOSE c;
DEALLOCATE c;
GO
