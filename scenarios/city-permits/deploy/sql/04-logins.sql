-- Windows logins for service accounts (back office, map service, jobs, intake, admin) and SQL
-- logins for the DMZ-facing apps. Passwords are supplied by sqlcmd variables.
USE master;
GO
IF SUSER_ID(N'CORP\svc-permits-web') IS NULL CREATE LOGIN [CORP\svc-permits-web] FROM WINDOWS;
IF SUSER_ID(N'CORP\svc-permits-app') IS NULL CREATE LOGIN [CORP\svc-permits-app] FROM WINDOWS;
IF SUSER_ID(N'CORP\svc-recordsone') IS NULL CREATE LOGIN [CORP\svc-recordsone] FROM WINDOWS;
IF SUSER_ID(N'CORP\PRM-Admins') IS NULL CREATE LOGIN [CORP\PRM-Admins] FROM WINDOWS;
IF SUSER_ID(N'permits_citizen') IS NULL CREATE LOGIN permits_citizen WITH PASSWORD = N'$(CitizenPassword)', CHECK_POLICY = ON;
IF SUSER_ID(N'permits_inspections') IS NULL CREATE LOGIN permits_inspections WITH PASSWORD = N'$(InspectionsPassword)', CHECK_POLICY = ON;
IF SUSER_ID(N'permits_opendata') IS NULL CREATE LOGIN permits_opendata WITH PASSWORD = N'$(OpenDataPassword)', CHECK_POLICY = ON;
GO
USE CityPermits;
GO
DECLARE @u TABLE (name sysname, login sysname, roles nvarchar(200));
INSERT @u VALUES
 (N'svc-permits-web', N'CORP\svc-permits-web', N'db_datareader,db_datawriter'),
 (N'svc-permits-app', N'CORP\svc-permits-app', N'db_datareader,db_datawriter'),
 (N'svc-recordsone', N'CORP\svc-recordsone', N''),
 (N'PRM-Admins', N'CORP\PRM-Admins', N'db_owner'),
 (N'permits_citizen', N'permits_citizen', N'db_datareader,db_datawriter'),
 (N'permits_inspections', N'permits_inspections', N''),
 (N'permits_opendata', N'permits_opendata', N'');
DECLARE @n sysname, @l sysname, @r nvarchar(200), @sql nvarchar(max);
DECLARE c CURSOR LOCAL FOR SELECT name, login, roles FROM @u;
OPEN c; FETCH NEXT FROM c INTO @n, @l, @r;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF USER_ID(@n) IS NULL EXEC (N'CREATE USER ' + N'[' + @n + N'] FOR LOGIN [' + @l + N'] WITH DEFAULT_SCHEMA = permits');
    SELECT @sql = STRING_AGG(N'ALTER ROLE ' + value + N' ADD MEMBER [' + @n + N'];', N' ') FROM STRING_SPLIT(@r, N',') WHERE value <> N'';
    IF @sql IS NOT NULL EXEC (@sql);
    FETCH NEXT FROM c INTO @n, @l, @r;
END
GO
GRANT SELECT ON SCHEMA::permits TO permits_opendata;
GRANT SELECT ON SCHEMA::permits TO permits_inspections;
GRANT UPDATE ON permits.Inspections TO permits_inspections;
GRANT INSERT ON permits.CaseEvents TO permits_inspections;
-- FileTable share access follows these permissions.
GRANT SELECT ON permits.CaseDocuments TO [svc-recordsone];
GRANT SELECT, INSERT, UPDATE ON permits.CaseDocuments TO [svc-permits-web], [svc-permits-app], permits_citizen;
GO
