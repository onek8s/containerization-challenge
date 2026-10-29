-- sqlcmd variables: PortalAccount, ServiceAccount, JobsAccount (DOMAIN\name, the IIS application pool and service accounts)
USE Inspections;
GO
IF SUSER_ID(N'$(PortalAccount)') IS NULL CREATE LOGIN [$(PortalAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(ServiceAccount)') IS NULL CREATE LOGIN [$(ServiceAccount)] FROM WINDOWS;
IF SUSER_ID(N'$(JobsAccount)') IS NULL CREATE LOGIN [$(JobsAccount)] FROM WINDOWS;
GO
IF DATABASE_PRINCIPAL_ID(N'$(PortalAccount)') IS NULL CREATE USER [$(PortalAccount)] FOR LOGIN [$(PortalAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(ServiceAccount)') IS NULL CREATE USER [$(ServiceAccount)] FOR LOGIN [$(ServiceAccount)];
IF DATABASE_PRINCIPAL_ID(N'$(JobsAccount)') IS NULL CREATE USER [$(JobsAccount)] FOR LOGIN [$(JobsAccount)];
GO
ALTER ROLE db_datareader ADD MEMBER [$(PortalAccount)];
ALTER ROLE db_datawriter ADD MEMBER [$(PortalAccount)];
ALTER ROLE db_datareader ADD MEMBER [$(ServiceAccount)];
ALTER ROLE db_datawriter ADD MEMBER [$(ServiceAccount)];
ALTER ROLE db_datareader ADD MEMBER [$(JobsAccount)];
ALTER ROLE db_datawriter ADD MEMBER [$(JobsAccount)];
GO
-- ASP.NET session state database (created with aspnet_regsql -ssadd -sstype p)
USE ASPState;
GO
IF DATABASE_PRINCIPAL_ID(N'$(PortalAccount)') IS NULL CREATE USER [$(PortalAccount)] FOR LOGIN [$(PortalAccount)];
GRANT EXECUTE TO [$(PortalAccount)];
GO
