/*
    BAMPrimaryImport database: read access to the rpt views for the gateway, the SLA monitor and the tracking
    site. The views read partner data from the integration database through the b2b synonyms, so the same
    principals are members of B2B_Reporting there as well (004_Security.sql, which also creates the logins);
    cross-database ownership chaining stays off.

    sqlcmd variables: GatewayLogin, MonitorLogin, TrackingBamLogin.
*/
SET NOCOUNT ON;
GO

IF DATABASE_PRINCIPAL_ID(N'B2B_Reporting') IS NULL CREATE ROLE B2B_Reporting AUTHORIZATION dbo;
GO

GRANT SELECT ON SCHEMA::rpt TO B2B_Reporting;
GRANT EXECUTE ON SCHEMA::rpt TO B2B_Reporting;
GRANT SELECT ON dbo.bam_OrderLifecycle_AllInstances TO B2B_Reporting;
GRANT SELECT ON dbo.bam_InterchangeTracking_AllInstances TO B2B_Reporting;
GRANT SELECT ON SCHEMA::b2b TO B2B_Reporting;
GO

IF DATABASE_PRINCIPAL_ID(N'$(GatewayLogin)') IS NULL CREATE USER [$(GatewayLogin)] FOR LOGIN [$(GatewayLogin)];
IF DATABASE_PRINCIPAL_ID(N'$(MonitorLogin)') IS NULL CREATE USER [$(MonitorLogin)] FOR LOGIN [$(MonitorLogin)];
IF DATABASE_PRINCIPAL_ID(N'$(TrackingBamLogin)') IS NULL CREATE USER [$(TrackingBamLogin)] FOR LOGIN [$(TrackingBamLogin)];
GO

ALTER ROLE B2B_Reporting ADD MEMBER [$(GatewayLogin)];
ALTER ROLE B2B_Reporting ADD MEMBER [$(MonitorLogin)];
ALTER ROLE B2B_Reporting ADD MEMBER [$(TrackingBamLogin)];
GO
