-- Customer Care database. Run as a sysadmin on the SQL Server instance.
IF DB_ID(N'CustomerCare') IS NULL
BEGIN
    CREATE DATABASE CustomerCare;
END
GO
ALTER DATABASE CustomerCare SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE CustomerCare SET RECOVERY FULL;
GO
