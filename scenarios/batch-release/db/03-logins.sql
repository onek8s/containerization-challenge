-- Windows logins for the application accounts.
-- sqlcmd -S release-sql.corp.local -E -d BatchRelease -i 03-logins.sql ^
--   -v WebAccount="CORP\svc-release-web" ApiAccount="CORP\svc-release-api" SigningAccount="CORP\svc-release-sign" AdminAccount="CORP\BR-QA-Systems"
:on error exit
SET NOCOUNT ON;

IF SUSER_ID(N'$(WebAccount)') IS NULL CREATE LOGIN [$(WebAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(ApiAccount)') IS NULL CREATE LOGIN [$(ApiAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(SigningAccount)') IS NULL CREATE LOGIN [$(SigningAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(AdminAccount)') IS NULL CREATE LOGIN [$(AdminAccount)] FROM WINDOWS;
GO

IF DATABASE_PRINCIPAL_ID(N'br_portal') IS NULL CREATE ROLE br_portal;
IF DATABASE_PRINCIPAL_ID(N'br_api') IS NULL CREATE ROLE br_api;
IF DATABASE_PRINCIPAL_ID(N'br_signing') IS NULL CREATE ROLE br_signing;
IF DATABASE_PRINCIPAL_ID(N'br_admin') IS NULL CREATE ROLE br_admin;
GO

IF USER_ID(N'$(WebAccount)') IS NULL CREATE USER [$(WebAccount)] FOR LOGIN [$(WebAccount)];
IF USER_ID(N'$(ApiAccount)') IS NULL CREATE USER [$(ApiAccount)] FOR LOGIN [$(ApiAccount)];
IF USER_ID(N'$(SigningAccount)') IS NULL CREATE USER [$(SigningAccount)] FOR LOGIN [$(SigningAccount)];
IF USER_ID(N'$(AdminAccount)') IS NULL CREATE USER [$(AdminAccount)] FOR LOGIN [$(AdminAccount)];
ALTER ROLE br_portal ADD MEMBER [$(WebAccount)];
ALTER ROLE br_api ADD MEMBER [$(ApiAccount)];
ALTER ROLE br_signing ADD MEMBER [$(SigningAccount)];
ALTER ROLE br_admin ADD MEMBER [$(AdminAccount)];
GO

-- Portal: reads everything, never writes (decisions go through the signing host).
GRANT SELECT ON SCHEMA::br TO br_portal;
DENY SELECT ON br.ApiClient TO br_portal;

-- API: MES intake and ERP reads.
GRANT SELECT ON SCHEMA::br TO br_api;
GRANT INSERT, UPDATE, DELETE ON br.Batch TO br_api;
GRANT INSERT, UPDATE, DELETE ON br.RecordStep TO br_api;
GRANT INSERT, UPDATE, DELETE ON br.LabResult TO br_api;
GRANT INSERT, UPDATE, DELETE ON br.Deviation TO br_api;
GRANT UPDATE ON br.ApiClient TO br_api;

-- Signing host: documents, decisions, certificates and batch status.
GRANT SELECT ON SCHEMA::br TO br_signing;
GRANT UPDATE ON br.Batch TO br_signing;
GRANT INSERT ON br.BatchDocument TO br_signing;
GRANT INSERT ON br.Decision TO br_signing;
GRANT INSERT ON br.ReleaseCertificate TO br_signing;

-- Administration (enrolment runs elevated on the signing host as a member of this group).
GRANT SELECT, INSERT, UPDATE ON SCHEMA::br TO br_admin;
GO
