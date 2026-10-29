-- Hangfire 1.7 job storage for ControlTower.Jobs, schema [hangfire] in the ControlTower database.
--
-- The tables are created by Hangfire itself. For the first start, run the jobs service once with
-- "Jobs:PrepareSchema": true and a login that may create tables (or temporarily add ct_jobs to
-- db_ddladmin); then set PrepareSchema back to false. 02-Logins.sql creates the schema and grants
-- ct_jobs access to it.
--
-- The script below only checks the result.

USE [ControlTower];
GO

SELECT t.name AS [Table], p.rows AS [Rows]
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE SCHEMA_NAME(t.schema_id) = N'hangfire'
ORDER BY t.name;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE SCHEMA_NAME(schema_id) = N'hangfire' AND name = N'Schema')
    PRINT N'Hangfire schema has not been created yet.';
ELSE
    SELECT [Version] AS HangfireSchemaVersion FROM [hangfire].[Schema];
GO
