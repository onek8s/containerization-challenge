-- Run on permits-sql as sysadmin.
IF DB_ID(N'CityPermits') IS NULL
BEGIN
    CREATE DATABASE CityPermits
    ON PRIMARY (NAME = CityPermits, FILENAME = N'E:\SQLData\CityPermits.mdf', SIZE = 2GB, FILEGROWTH = 512MB),
    FILEGROUP ArchiveFS CONTAINS FILESTREAM (NAME = CityPermitsArchive, FILENAME = N'F:\SQLFileStream\CityPermitsArchive')
    LOG ON (NAME = CityPermits_log, FILENAME = N'L:\SQLLogs\CityPermits_log.ldf', SIZE = 512MB, FILEGROWTH = 256MB);
END
GO
ALTER DATABASE CityPermits SET FILESTREAM (NON_TRANSACTED_ACCESS = FULL, DIRECTORY_NAME = N'PermitsArchive');
GO
USE CityPermits;
GO
IF SCHEMA_ID(N'permits') IS NULL EXEC (N'CREATE SCHEMA permits');
GO
