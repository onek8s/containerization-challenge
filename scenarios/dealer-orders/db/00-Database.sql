-- Dealer Orders database on sql01 (SQL Server 2016, default instance).
-- Run as a sysadmin with sqlcmd; the data and log folders follow the server defaults.
IF DB_ID(N'DealerOrders') IS NULL
BEGIN
    CREATE DATABASE [DealerOrders];
END
GO
ALTER DATABASE [DealerOrders] SET RECOVERY FULL;
ALTER DATABASE [DealerOrders] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [DealerOrders] SET ALLOW_SNAPSHOT_ISOLATION ON;
GO
