-- sqlcmd -v DatabaseName=TradeRebates FilesRoot="\\rebates-files.corp.local\Rebates$" ErpEndpoint="https://erp-ap.corp.local/ApService.svc" ErpPostingMode=Live
:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE [$(DatabaseName)];
GO

MERGE rebate.Setting AS t
USING (VALUES
    ('DropFolder',            N'$(FilesRoot)\Inbound\Files',      N'Distributor sell-out files (CSV and SO fixed-width)'),
    ('DropFilePattern',       N'*.*',                              N'Files picked up from the drop folder'),
    ('DropCodePage',          N'850',                              N'Code page of SO files; CSV files with a BOM are read as UTF-8'),
    ('WorkbookFolder',        N'$(FilesRoot)\Inbound\Workbooks',   N'Sell-out workbooks (.xlsx, sheet SellOut)'),
    ('ProcessedFolder',       N'$(FilesRoot)\Processed',           N'Imported files are moved here in yyyyMM folders'),
    ('RejectedFolder',        N'$(FilesRoot)\Rejected',            N'Files that could not be read'),
    ('StatementFolder',       N'$(FilesRoot)\Statements',          N'Rendered statements, one folder per distributor'),
    ('WorkbookProvider',      N'Microsoft.ACE.OLEDB.16.0',         N'OLE DB provider for workbooks'),
    ('PriceBookServer',       N'LEGACYPRICEBOOK',                  N'Linked server holding the net price book'),
    ('PriceBookCheck',        N'On',                               N'On: compare claimed prices with the price book'),
    ('PriceTolerancePercent', N'15',                               N'Lines priced further below the price book get a warning'),
    ('ErpEndpoint',           N'$(ErpEndpoint)',                   N'Finance AP service'),
    ('ErpPostingMode',        N'$(ErpPostingMode)',                N'Live posts credit notes; Offline records local document numbers'),
    ('ErpTimeoutSeconds',     N'30',                               N'Finance AP service timeout'),
    ('EventLogEnabled',       N'1',                                N'Write import and statement errors to the event log')
) AS s (Name, Value, Description)
ON t.Name = s.Name
WHEN MATCHED AND (s.Name IN ('ErpEndpoint', 'ErpPostingMode') OR s.Name LIKE '%Folder') THEN
    UPDATE SET Value = s.Value, Description = s.Description
WHEN NOT MATCHED THEN
    INSERT (Name, Value, Description) VALUES (s.Name, s.Value, s.Description);
GO

CREATE OR ALTER FUNCTION rebate.GetSetting (@name varchar(60)) RETURNS nvarchar(400)
AS
BEGIN
    RETURN (SELECT Value FROM rebate.Setting WHERE Name = @name);
END
GO
