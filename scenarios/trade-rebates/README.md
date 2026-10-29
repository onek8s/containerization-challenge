# Trade Rebates

Distributor rebate management for Contoso Foods: rebate agreements with tiered rates, monthly sell-out claims from
distributors (API, file drop, Excel workbooks), claim validation against the price book, accruals, and quarterly
rebate statements that are posted to finance as credit notes.

## Components

| Host name | Component | Path |
| --- | --- | --- |
| `rebates.corp.local` | Portal, ASP.NET Core 8 Razor Pages on IIS (Windows authentication) | `src/TradeRebates.Portal` |
| `rebates-api.corp.local` | Distributor API, ASP.NET Core 8 minimal API on IIS (API keys) | `src/TradeRebates.Api` |
| `rebates-sql.corp.local` | SQL Server 2022, database `TradeRebates` | `db/` |
| `rebates-sql.corp.local` | Database assemblies loaded into SQL Server | `src/TradeRebates.SqlClr`, `src/TradeRebates.SqlClr.Integration` |
| `rebates-sql.corp.local` | Statement worker, started by the Service Broker External Activator | `src/TradeRebates.StatementWorker` |
| `rebates-sql.corp.local` | Sell-out workbook import package (SQL Server Agent job) | `ssis/SellOutWorkbookImport.dtsx` |
| `rebates-files.corp.local` | Shares `Rebates$` and `RebatesArchive$`, Access price book | `deploy/create-shares.ps1`, `deploy/pricebook` |

Shared code: `src/TradeRebates.Rules` (tier schedules, sell-out file layouts, GTIN/IBAN checks; also compiled into the
database assemblies), `src/TradeRebates.Core` (models, statement rendering), `src/TradeRebates.Data` (stored procedure calls).

### Processing

1. Claims arrive through `POST /api/v1/claims`, as CSV or SO fixed-width files in `Rebates$\Inbound\Files`
   (job *Import drop folder*, every 15 minutes), or as workbooks in `Rebates$\Inbound\Workbooks` or uploaded in the portal.
2. `rebate.ValidateClaim` checks GTINs, catalogue, period, duplicates and the price book (`LEGACYPRICEBOOK` linked server).
3. Approvers approve or reject validated claims in the portal.
4. Job *Calculate accruals* (02:00) computes year-to-date tier rebates per agreement.
5. Job *Quarterly statements* (5th of Jan/Apr/Jul/Oct) queues one statement request per distributor; the activator
   starts the statement worker, which writes `Statements\<distributor>\<quarter>\TR-<quarter>-<code>.html/.csv` and calls
   `rebate.CompleteStatement`, which posts the credit note to the finance AP service.

## Requirements

- Windows Server 2022/2025 with IIS (Windows Authentication role service) and the ASP.NET Core 8 Hosting Bundle
- SQL Server 2022 with CLR integration; SQL Server Agent and Integration Services (Standard, Enterprise or Developer edition)
  for the scheduled jobs and the workbook package
- Microsoft Access Database Engine 2016 Redistributable (x64) on the database server, for the price book and workbooks
- Microsoft SQL Server Service Broker External Activator on the database server, .NET 8 runtime (x64)
- DNS records for the host names above (`deploy/register-dns.ps1`)
- Accounts: portal and API application pool identities, activator service account, file share account for the
  SQL Server Agent proxy, AD groups `TR-Analysts`, `TR-Approvers`, `TR-Admins`
- Build: .NET 8 SDK (the database assemblies target .NET Framework 4.8)

## Build and test

```powershell
dotnet build TradeRebates.sln -c Release
dotnet test TradeRebates.sln -c Release
```

## Deploy

Run from an elevated PowerShell on the respective server, in this order:

```powershell
cd deploy
.\register-dns.ps1 -IPAddress 10.10.0.21                       # on the DNS server
.\create-shares.ps1 -SqlServiceAccount 'CORP\svc-sql01$' -ActivatorAccount 'CORP\svc-rebates-ea$' `
    -PortalAccount 'CORP\svc-rebates-web$' -ApiAccount 'CORP\svc-rebates-api$' -FilesAccount 'CORP\svc-rebates-files' -JobAccounts 'CORP\svc-sqlagent01$'
.\pricebook\create-pricebook.ps1                                # creates PriceBook.accdb on the share
.\install-ssis.ps1
.\install-database.ps1 -PortalAccount 'CORP\svc-rebates-web$' -ApiAccount 'CORP\svc-rebates-api$' `
    -ActivatorAccount 'CORP\svc-rebates-ea$' -FilesAccount 'CORP\svc-rebates-files' -FilesPassword (Read-Host -AsSecureString)
.\install-activator.ps1 -ServiceAccount 'CORP\svc-rebates-ea$'
.\install-web.ps1 -CertificateThumbprint <thumbprint> -PortalPoolAccount 'CORP\svc-rebates-web$' -ApiPoolAccount 'CORP\svc-rebates-api$'
.\new-api-key.ps1 -DistributorCode KESPRO -Name 'Kespro EDI gateway'
```

All scripts take parameters for host names, paths and accounts; see the help block at the top of each script.

### Single lab server

All host names can point to one server (`register-dns.ps1 -IPAddress <lab IP>`), with local accounts and groups.

- `-ErpPostingMode Offline` on `install-database.ps1` records credit note numbers locally (`OFFLINE-000123`) instead of
  calling the finance AP service. Switch later with `UPDATE rebate.Setting SET Value = N'Live' WHERE Name = 'ErpPostingMode'`.
- On SQL Server Express (no SQL Server Agent, no Integration Services) `install-database.ps1` skips the Agent jobs; run
  `install-scheduled-tasks.ps1` instead. Workbooks are then imported with `rebate.ImportWorkbookFolder`.
- Without the Access Database Engine the price book check is turned off (`PriceBookCheck = Off`) and workbooks cannot be imported;
  CSV/SO files and the API work.

## Run

- Portal: `https://rebates.corp.local/` (members of the configured groups; Admins also see Distributors and Audit)
- API: `https://rebates-api.corp.local/ping`, `GET/POST /api/v1/claims`, `GET /api/v1/claims/{id}`, `GET /api/v1/agreements`
  with header `X-Api-Key`

```powershell
$body = @{ period = 202609; reference = 'SO-1001'; lines = @(
    @{ invoiceNo = 'K260901'; invoiceDate = '2026-09-01'; customerNo = '401220'; gtin = '6414000010015'; quantity = 240; netAmount = 2160 }) } | ConvertTo-Json -Depth 4
Invoke-RestMethod https://rebates-api.corp.local/api/v1/claims -Method Post -Headers @{ 'X-Api-Key' = '<key>' } -Body $body -ContentType 'application/json'
```

- Sample input in `samples/`: copy `kespro-sellout-202609.csv` and `heinon-sellout-202609.so` to `Rebates$\Inbound\Files`,
  `metro-sellout-202609.xlsx` to `Rebates$\Inbound\Workbooks` or upload it in the portal.
- Jobs can be started by hand:

```sql
EXEC rebate.ImportDropFolder;
EXEC rebate.ImportWorkbookFolder;
EXEC rebate.CalculateAccruals @Period = 202609;
EXEC rebate.RequestStatements @Quarter = '2026Q3';
```

- Logs: event log *Contoso Trade Rebates* (database routines and statement worker), External Activator log in
  `C:\Program Files\Service Broker\External Activator\Log`, `rebate.AuditLog`.
