-- Reference data checks after loading master data with
--   ControlTower.LabTools seed --connection "Server=sql01.corp.local;Database=ControlTower;User ID=...;Password=...;TrustServerCertificate=True"
-- (production master data is maintained in the booking application).

USE [ControlTower];
GO

SELECT 'Customers' AS [Table], COUNT(*) AS [Rows] FROM ct.Customer
UNION ALL SELECT 'Carriers', COUNT(*) FROM ct.Carrier
UNION ALL SELECT 'Locations', COUNT(*) FROM ct.Location
UNION ALL SELECT 'Vehicles', COUNT(*) FROM ct.Vehicle
UNION ALL SELECT 'Rate cards', COUNT(*) FROM ct.RateCard
UNION ALL SELECT 'Shipments', COUNT(*) FROM ct.Shipment;
GO

-- Locations whose time zone id the servers do not know produce wrong local times in documents and reports.
SELECT DISTINCT TimeZoneId FROM ct.Location ORDER BY TimeZoneId;
GO

-- Restart the shipment number sequence above the highest loaded number.
DECLARE @max bigint = (SELECT ISNULL(MAX(CAST(SUBSTRING(Number, 6, 6) AS bigint)), 0) FROM ct.Shipment);
DECLARE @sql nvarchar(200) = N'ALTER SEQUENCE ct.ShipmentNumbers RESTART WITH ' + CAST(@max + 1 AS nvarchar(20)) + N';';
EXEC (@sql);
GO
