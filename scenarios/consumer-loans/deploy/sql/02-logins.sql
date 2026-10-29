-- SQL logins of the lending applications. Passwords come from the secrets file.
-- The API owns the schema (DbUp scripts run when Database:Migrate = true), the portal owns its identity tables.
DECLARE @logins TABLE (Name sysname, Pwd nvarchar(128), Db sysname, Roles nvarchar(200));
INSERT @logins VALUES
 (N'lending_api',    N'$(PwdApi)',    N'$(LendingDb)', N'db_datareader,db_datawriter,db_ddladmin'),
 (N'lending_office', N'$(PwdOffice)', N'$(LendingDb)', N'db_datareader,db_datawriter'),
 (N'lending_docs',   N'$(PwdDocs)',   N'$(LendingDb)', N'db_datareader,db_datawriter'),
 (N'lending_coll',   N'$(PwdColl)',   N'$(LendingDb)', N'db_datareader,db_datawriter'),
 (N'lending_tape',   N'$(PwdTape)',   N'$(LendingDb)', N'db_datareader'),
 (N'lending_portal', N'$(PwdPortal)', N'$(PortalDb)',  N'db_datareader,db_datawriter,db_ddladmin');

DECLARE @name sysname, @pwd nvarchar(128), @db sysname, @roles nvarchar(200), @sql nvarchar(max), @role sysname;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Name, Pwd, Db, Roles FROM @logins;
OPEN c;
FETCH NEXT FROM c INTO @name, @pwd, @db, @roles;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF SUSER_ID(@name) IS NULL
        SET @sql = N'CREATE LOGIN ' + QUOTENAME(@name) + N' WITH PASSWORD = ' + QUOTENAME(@pwd, '''') + N', CHECK_POLICY = ON, DEFAULT_DATABASE = ' + QUOTENAME(@db);
    ELSE
        SET @sql = N'ALTER LOGIN ' + QUOTENAME(@name) + N' WITH PASSWORD = ' + QUOTENAME(@pwd, '''');
    EXEC (@sql);

    SET @sql = N'USE ' + QUOTENAME(@db) + N'; IF USER_ID(' + QUOTENAME(@name, '''') + N') IS NULL CREATE USER ' + QUOTENAME(@name) + N' FOR LOGIN ' + QUOTENAME(@name) + N';';
    EXEC (@sql);
    DECLARE r CURSOR LOCAL FAST_FORWARD FOR SELECT LTRIM(value) FROM STRING_SPLIT(@roles, N',');
    OPEN r;
    FETCH NEXT FROM r INTO @role;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'USE ' + QUOTENAME(@db) + N'; ALTER ROLE ' + QUOTENAME(@role) + N' ADD MEMBER ' + QUOTENAME(@name) + N';';
        EXEC (@sql);
        FETCH NEXT FROM r INTO @role;
    END
    CLOSE r; DEALLOCATE r;
    FETCH NEXT FROM c INTO @name, @pwd, @db, @roles;
END
CLOSE c; DEALLOCATE c;
GO
