/*
    VanArsdelIntegration database: database roles and their members.

      B2B_Runtime   BizTalk host instances (pipeline components, orchestrations, BRE fact retrievers) and the
                    order gateway application pool
      B2B_Portal    operations portal application pool (partner maintenance, resubmission log, interchange search)
      B2B_Tracking  tracking site (SQL login, the site runs in the DMZ outside the domain)
      B2B_Monitor   SLA monitor service
      B2B_Reporting readers of the rpt views in BAMPrimaryImport (006_ReportingSecurity.sql), which read
                    dbo.TradingPartner through a synonym

    sqlcmd variables (Install-Database.ps1 passes them from the environment settings):
      RuntimeLogin      Windows group of the BizTalk host accounts, e.g. CORP\BizTalk Application Users
      GatewayLogin      application pool identity of the gateway, e.g. CORP\svc-b2b-gateway
      PortalLogin       application pool identity of the operations portal
      MonitorLogin      service account of the SLA monitor
      TrackingLogin     SQL login of the tracking site (b2b_tracking)
      TrackingPassword  its password (from the deployment secrets file, never stored in the settings workbook)
      TrackingBamLogin, TrackingBamPassword
                        SQL login of the tracking site for BAMPrimaryImport (b2b_tracking_bam)
*/
SET NOCOUNT ON;
GO

IF DATABASE_PRINCIPAL_ID(N'B2B_Runtime') IS NULL CREATE ROLE B2B_Runtime AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'B2B_Portal') IS NULL CREATE ROLE B2B_Portal AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'B2B_Tracking') IS NULL CREATE ROLE B2B_Tracking AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'B2B_Monitor') IS NULL CREATE ROLE B2B_Monitor AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'B2B_Reporting') IS NULL CREATE ROLE B2B_Reporting AUTHORIZATION dbo;
GO

/* ---- runtime */
GRANT EXECUTE ON dbo.usp_GetTradingPartners TO B2B_Runtime;
GRANT EXECUTE ON dbo.usp_GetPartnerXref TO B2B_Runtime;
GRANT EXECUTE ON dbo.usp_RegisterOutboundInterchange TO B2B_Runtime;
GRANT EXECUTE ON dbo.usp_ApplyFunctionalAck TO B2B_Runtime;
GRANT EXECUTE ON dbo.usp_GetInterchangesAwaitingAck TO B2B_Runtime;
GRANT EXECUTE ON dbo.usp_RegisterReceivedInterchange TO B2B_Runtime;
GRANT EXECUTE ON dbo.usp_NextControlNumber TO B2B_Runtime;
GRANT EXECUTE ON dbo.usp_PollOverdueAcks TO B2B_Runtime;
GRANT SELECT ON dbo.PartnerCredential TO B2B_Runtime;
GRANT SELECT ON dbo.Holiday TO B2B_Runtime;
GO

/* ---- operations portal */
GRANT EXECUTE ON dbo.usp_GetTradingPartners TO B2B_Portal;
GRANT EXECUTE ON dbo.usp_GetPartnerXref TO B2B_Portal;
GRANT EXECUTE ON dbo.usp_SavePartnerXref TO B2B_Portal;
GRANT EXECUTE ON dbo.usp_DeletePartnerXref TO B2B_Portal;
GRANT EXECUTE ON dbo.usp_SetPartnerActive TO B2B_Portal;
GRANT EXECUTE ON dbo.usp_SearchInterchanges TO B2B_Portal;
GRANT EXECUTE ON dbo.usp_GetInterchangesAwaitingAck TO B2B_Portal;
GRANT EXECUTE ON dbo.usp_LogResubmission TO B2B_Portal;
GRANT SELECT ON dbo.ResubmitLog TO B2B_Portal;
GRANT UPDATE (OriginalTerminated) ON dbo.ResubmitLog TO B2B_Portal;
GRANT SELECT ON dbo.PartnerChangeLog TO B2B_Portal;
GRANT SELECT ON dbo.AlertLog TO B2B_Portal;
GO

/* ---- tracking site */
GRANT EXECUTE ON dbo.usp_GetTradingPartners TO B2B_Tracking;
GRANT EXECUTE ON dbo.usp_SavePortalUser TO B2B_Tracking;
GRANT EXECUTE ON dbo.usp_RecordPortalLogin TO B2B_Tracking;
GRANT SELECT ON dbo.PortalUser TO B2B_Tracking;
GO

/* ---- SLA monitor */
GRANT EXECUTE ON dbo.usp_GetTradingPartners TO B2B_Monitor;
GRANT EXECUTE ON dbo.usp_GetSlaDefinitions TO B2B_Monitor;
GRANT EXECUTE ON dbo.usp_SaveAlertState TO B2B_Monitor;
GRANT EXECUTE ON dbo.usp_LogAlert TO B2B_Monitor;
GRANT EXECUTE ON dbo.usp_GetInterchangesAwaitingAck TO B2B_Monitor;
GRANT EXECUTE ON dbo.usp_SearchInterchanges TO B2B_Monitor;
GRANT SELECT ON dbo.Holiday TO B2B_Monitor;
GRANT SELECT ON dbo.NotifyGroup TO B2B_Monitor;
GRANT SELECT, UPDATE ON dbo.AlertState TO B2B_Monitor;
GRANT SELECT ON dbo.AlertLog TO B2B_Monitor;
GO

/* ---- rpt views in BAMPrimaryImport */
GRANT SELECT ON dbo.TradingPartner (PartnerId, Active, ExpectsDailyTraffic) TO B2B_Reporting;
GO

/* ---- logins and members */
IF SUSER_ID(N'$(RuntimeLogin)') IS NULL CREATE LOGIN [$(RuntimeLogin)] FROM WINDOWS WITH DEFAULT_DATABASE = [master];
IF SUSER_ID(N'$(GatewayLogin)') IS NULL CREATE LOGIN [$(GatewayLogin)] FROM WINDOWS WITH DEFAULT_DATABASE = [master];
IF SUSER_ID(N'$(PortalLogin)') IS NULL CREATE LOGIN [$(PortalLogin)] FROM WINDOWS WITH DEFAULT_DATABASE = [master];
IF SUSER_ID(N'$(MonitorLogin)') IS NULL CREATE LOGIN [$(MonitorLogin)] FROM WINDOWS WITH DEFAULT_DATABASE = [master];
IF SUSER_ID(N'$(TrackingLogin)') IS NULL
    CREATE LOGIN [$(TrackingLogin)] WITH PASSWORD = N'$(TrackingPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = [master];
ELSE
    ALTER LOGIN [$(TrackingLogin)] WITH PASSWORD = N'$(TrackingPassword)';
IF SUSER_ID(N'$(TrackingBamLogin)') IS NULL
    CREATE LOGIN [$(TrackingBamLogin)] WITH PASSWORD = N'$(TrackingBamPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = [master];
ELSE
    ALTER LOGIN [$(TrackingBamLogin)] WITH PASSWORD = N'$(TrackingBamPassword)';
GO

IF DATABASE_PRINCIPAL_ID(N'$(RuntimeLogin)') IS NULL CREATE USER [$(RuntimeLogin)] FOR LOGIN [$(RuntimeLogin)];
IF DATABASE_PRINCIPAL_ID(N'$(GatewayLogin)') IS NULL CREATE USER [$(GatewayLogin)] FOR LOGIN [$(GatewayLogin)];
IF DATABASE_PRINCIPAL_ID(N'$(PortalLogin)') IS NULL CREATE USER [$(PortalLogin)] FOR LOGIN [$(PortalLogin)];
IF DATABASE_PRINCIPAL_ID(N'$(MonitorLogin)') IS NULL CREATE USER [$(MonitorLogin)] FOR LOGIN [$(MonitorLogin)];
IF DATABASE_PRINCIPAL_ID(N'$(TrackingLogin)') IS NULL CREATE USER [$(TrackingLogin)] FOR LOGIN [$(TrackingLogin)];
IF DATABASE_PRINCIPAL_ID(N'$(TrackingBamLogin)') IS NULL CREATE USER [$(TrackingBamLogin)] FOR LOGIN [$(TrackingBamLogin)];
GO

ALTER ROLE B2B_Runtime ADD MEMBER [$(RuntimeLogin)];
ALTER ROLE B2B_Runtime ADD MEMBER [$(GatewayLogin)];
ALTER ROLE B2B_Portal ADD MEMBER [$(PortalLogin)];
ALTER ROLE B2B_Monitor ADD MEMBER [$(MonitorLogin)];
ALTER ROLE B2B_Tracking ADD MEMBER [$(TrackingLogin)];
ALTER ROLE B2B_Reporting ADD MEMBER [$(GatewayLogin)];
ALTER ROLE B2B_Reporting ADD MEMBER [$(MonitorLogin)];
ALTER ROLE B2B_Reporting ADD MEMBER [$(TrackingBamLogin)];
GO
