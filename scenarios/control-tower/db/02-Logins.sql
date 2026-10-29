-- SQL logins, database users and roles for the Control Tower applications.
-- Each application signs in with its own SQL login (mixed mode authentication is required).
--   sqlcmd -S sql01.corp.local -E -i 02-Logins.sql -v BookingPassword="..." PortalPassword="..." TowerPassword="..." ScanPassword="..." `
--          GatewayPassword="..." EventsPassword="..." TelematicsPassword="..." JobsPassword="..." IdentityPassword="..."
-- Re-running the script resets the passwords.

:on error exit
USE [master];
GO

DECLARE @logins TABLE (Name sysname, Pwd nvarchar(128));
INSERT INTO @logins VALUES
    (N'ct_booking', N'$(BookingPassword)'),
    (N'ct_portal', N'$(PortalPassword)'),
    (N'ct_tower', N'$(TowerPassword)'),
    (N'ct_scan', N'$(ScanPassword)'),
    (N'ct_gateway', N'$(GatewayPassword)'),
    (N'ct_events', N'$(EventsPassword)'),
    (N'ct_telematics', N'$(TelematicsPassword)'),
    (N'ct_jobs', N'$(JobsPassword)'),
    (N'ct_identity', N'$(IdentityPassword)');

DECLARE @name sysname, @pwd nvarchar(128), @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Name, Pwd FROM @logins;
OPEN c;
FETCH NEXT FROM c INTO @name, @pwd;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF SUSER_ID(@name) IS NULL
        SET @sql = N'CREATE LOGIN ' + QUOTENAME(@name) + N' WITH PASSWORD = ' + QUOTENAME(@pwd, '''') + N', DEFAULT_DATABASE = [ControlTower], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
    ELSE
        SET @sql = N'ALTER LOGIN ' + QUOTENAME(@name) + N' WITH PASSWORD = ' + QUOTENAME(@pwd, '''') + N';';
    EXEC (@sql);
    FETCH NEXT FROM c INTO @name, @pwd;
END
CLOSE c;
DEALLOCATE c;
GO

USE [ControlTower];
GO

-- Users
DECLARE @users TABLE (Name sysname);
INSERT INTO @users VALUES (N'ct_booking'), (N'ct_portal'), (N'ct_tower'), (N'ct_scan'), (N'ct_gateway'), (N'ct_events'), (N'ct_telematics'), (N'ct_jobs');
DECLARE @u sysname, @s nvarchar(max);
DECLARE u CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @users;
OPEN u;
FETCH NEXT FROM u INTO @u;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF USER_ID(@u) IS NULL
    BEGIN
        SET @s = N'CREATE USER ' + QUOTENAME(@u) + N' FOR LOGIN ' + QUOTENAME(@u) + N' WITH DEFAULT_SCHEMA = [ct];';
        EXEC (@s);
    END
    IF DATABASE_PRINCIPAL_ID(N'role_' + @u) IS NULL
    BEGIN
        SET @s = N'CREATE ROLE ' + QUOTENAME(N'role_' + @u) + N';';
        EXEC (@s);
    END
    SET @s = N'ALTER ROLE ' + QUOTENAME(N'role_' + @u) + N' ADD MEMBER ' + QUOTENAME(@u) + N';';
    EXEC (@s);
    FETCH NEXT FROM u INTO @u;
END
CLOSE u;
DEALLOCATE u;
GO

-- Everybody reads the operational schema.
GRANT SELECT ON SCHEMA::[ct] TO [role_ct_booking], [role_ct_portal], [role_ct_tower], [role_ct_scan], [role_ct_gateway], [role_ct_events], [role_ct_telematics], [role_ct_jobs];
GO

-- Booking: shipments and everything that belongs to them, master data maintenance, documents.
GRANT INSERT, UPDATE, DELETE ON [ct].[Shipment] TO [role_ct_booking];
GRANT INSERT, UPDATE, DELETE ON [ct].[Package] TO [role_ct_booking];
GRANT INSERT, UPDATE, DELETE ON [ct].[Leg] TO [role_ct_booking];
GRANT INSERT, UPDATE ON [ct].[Milestone] TO [role_ct_booking];
GRANT INSERT, UPDATE, DELETE ON [ct].[Charge] TO [role_ct_booking];
GRANT INSERT, UPDATE, DELETE ON [ct].[CustomsDeclaration] TO [role_ct_booking];
GRANT INSERT, UPDATE, DELETE ON [ct].[DeclarationLine] TO [role_ct_booking];
GRANT INSERT, UPDATE ON [ct].[Customer] TO [role_ct_booking];
GRANT INSERT, UPDATE ON [ct].[Carrier] TO [role_ct_booking];
GRANT INSERT, UPDATE ON [ct].[Location] TO [role_ct_booking];
GRANT INSERT, UPDATE, DELETE ON [ct].[RateCard] TO [role_ct_booking];
GRANT INSERT, UPDATE, DELETE ON [ct].[RateBreak] TO [role_ct_booking];
GRANT INSERT, UPDATE ON [ct].[Vehicle] TO [role_ct_booking];
GRANT INSERT ON [ct].[Document] TO [role_ct_booking];
GRANT INSERT, UPDATE ON [ct].[DocumentContent] TO [role_ct_booking];
GRANT UPDATE ON [ct].[Alert] TO [role_ct_booking];
GRANT UPDATE ON OBJECT::[ct].[ShipmentNumbers] TO [role_ct_booking];
GO

-- Portal: booking requests from customers.
GRANT INSERT ON [ct].[Shipment] TO [role_ct_portal];
GRANT INSERT ON [ct].[Package] TO [role_ct_portal];
GRANT INSERT ON [ct].[Milestone] TO [role_ct_portal];
GRANT UPDATE ON OBJECT::[ct].[ShipmentNumbers] TO [role_ct_portal];
GO

-- Tower: alert handling only.
GRANT UPDATE ON [ct].[Alert] TO [role_ct_tower];
GO

-- Scanning: scans, label printing, proof of delivery documents.
GRANT INSERT ON [ct].[Scan] TO [role_ct_scan];
GRANT UPDATE ON [ct].[Package] TO [role_ct_scan];
GRANT INSERT ON [ct].[Document] TO [role_ct_scan];
GRANT INSERT, UPDATE ON [ct].[DocumentContent] TO [role_ct_scan];
GO

-- Carrier gateway: carrier references, status, customs and interchange log.
GRANT UPDATE ON [ct].[Leg] TO [role_ct_gateway];
GRANT INSERT, UPDATE ON [ct].[EdiInterchange] TO [role_ct_gateway];
GRANT UPDATE ON [ct].[CustomsDeclaration] TO [role_ct_gateway];
GRANT INSERT ON [ct].[Document] TO [role_ct_gateway];
GRANT INSERT, UPDATE ON [ct].[DocumentContent] TO [role_ct_gateway];
GO

-- Events: applies milestones, ETAs and rules.
GRANT UPDATE ON [ct].[Shipment] TO [role_ct_events];
GRANT UPDATE ON [ct].[Leg] TO [role_ct_events];
GRANT INSERT ON [ct].[Milestone] TO [role_ct_events];
GRANT INSERT, UPDATE ON [ct].[Alert] TO [role_ct_events];
GRANT UPDATE ON [ct].[Package] TO [role_ct_events];
GO

-- Telematics: positions and vehicle state.
GRANT INSERT ON [ct].[VehiclePosition] TO [role_ct_telematics];
GRANT UPDATE ON [ct].[Vehicle] TO [role_ct_telematics];
GO

-- Jobs: KPI rows, invoicing marks, retention, fuel surcharges, documents.
GRANT INSERT, UPDATE, DELETE ON [ct].[DailyKpi] TO [role_ct_jobs];
GRANT UPDATE ON [ct].[Charge] TO [role_ct_jobs];
GRANT UPDATE ON [ct].[RateCard] TO [role_ct_jobs];
GRANT DELETE ON [ct].[VehiclePosition] TO [role_ct_jobs];
GRANT INSERT, DELETE ON [ct].[Document] TO [role_ct_jobs];
GRANT INSERT, UPDATE, DELETE ON [ct].[DocumentContent] TO [role_ct_jobs];
GRANT EXECUTE ON [ct].[PurgeOrphanDocumentContent] TO [role_ct_jobs];
GO

-- Hangfire job storage lives in the same database (schema hangfire, see 03-Hangfire.sql).
IF SCHEMA_ID(N'hangfire') IS NULL EXEC(N'CREATE SCHEMA [hangfire] AUTHORIZATION [dbo];');
GO
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::[hangfire] TO [role_ct_jobs];
GO

-- Token service database
USE [ControlTowerIdentity];
GO
IF USER_ID(N'ct_identity') IS NULL CREATE USER [ct_identity] FOR LOGIN [ct_identity];
ALTER ROLE [db_datareader] ADD MEMBER [ct_identity];
ALTER ROLE [db_datawriter] ADD MEMBER [ct_identity];
-- The token service creates its tables on first deployment (ControlTower.Identity --initialize); afterwards
-- db_ddladmin can be removed again.
ALTER ROLE [db_ddladmin] ADD MEMBER [ct_identity];
GO
