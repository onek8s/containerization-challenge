# Database scripts

SQL Server 2019 scripts for the eleven service databases on `sql01.corp.local`. Run them in file name order;
`deploy\Initialize-Database.ps1` does that with `sqlcmd` and the passwords from the deployment vault.
Run `sqlcmd` with `-I` (QUOTED_IDENTIFIER ON); the filtered unique indexes in the EF scripts need it.

| Script | Database | Owner |
| --- | --- | --- |
| 00-databases.sql | all | creates databases, SQL logins and users |
| 10-catalog.sql | VanArsdelCatalog | catalog-api |
| 15-pricing.sql | VanArsdelPricing | pricing-api |
| 20-inventory.sql | VanArsdelInventory | inventory-api |
| 30-allocation.sql | VanArsdelAllocation | allocation-api |
| 40-orders.sql | VanArsdelOrders | orders-api (orders, saga state, outbox) |
| 50-returns.sql | VanArsdelReturns | returns-api |
| 60-storeops.sql | VanArsdelStoreOps | storeops-api |
| 70-erpconnector.sql | VanArsdelErpConnector | ERP connector service |
| 80-notifications.sql | VanArsdelNotifications | notification service |
| 85-reporting.sql | VanArsdelReporting | reporting-api (read model) |
| 90-scheduler-quartz.sql | VanArsdelScheduler | scheduler cluster (Quartz.NET job store) |

The EF Core scripts are generated, not hand written. After a model change, regenerate the script of that service:

```
cd src/VanArsdel.Orders.Api
dotnet run -- --print-schema ../../db/40-orders.sql
```

with `Database__Provider=SqlServer` and any SQL Server connection string in `ConnectionStrings__Orders`
(the database is not contacted). The pricing and reporting scripts are the embedded `Sql/schema.sqlserver.sql` files
of those services.

The customer register lives in MongoDB (`docdb01.corp.local`, replica set `rs0`); it has no script here.
The service creates its indexes on start.
