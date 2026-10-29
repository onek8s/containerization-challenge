# Product Returns

Online returns for Wide World Importers. Customers start a return on the returns site, customer service
approves it, a return label is booked with the carrier and e-mailed to the customer, the warehouse scans
the parcel in at the returns dock and a supervisor completes the inspection. Refunds go to finance every
weekday morning as a batch file.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `deploy/proxy` | `returns.corp.local` (IIS site `returns`) | Front end: URL Rewrite + ARR forward to the portal and to the scanner API |
| `src/ProductReturns.Portal` | `returns-web.corp.local` (Windows service `WWIReturnsPortal`, HTTPS 5011) | ASP.NET Core 3.1 MVC: customer return wizard, return status, staff queue |
| `src/ProductReturns.Api` | `returns-api.corp.local` (Windows service `WWIReturnsApi`, HTTPS 5021) | ASP.NET Core 5.0 Web API for the portal and the warehouse scanners |
| `src/ProductReturns.Labels` | `returns-jobs.corp.local` (Windows service `WWIReturnsLabels`) | .NET 5 worker: books ParcelLink return labels for approved returns and mails them |
| `src/ProductReturns.Jobs` | `returns-jobs.corp.local` (Task Scheduler) | .NET Core 3.1 console: `refunds`, `reminders`, `expire`, `purge` |
| `src/ProductReturns.Data` | | EF Core 3.1 model (SQL Server, SQLite for development) |
| `src/ProductReturns.Core` | | Return policy, refund calculation, RMA numbers, refund batch file, API models |
| `tests/ProductReturns.Tests` | | Unit and API tests |
| `deploy` | | DNS, IIS, service, scheduled task, share and database scripts |

Other servers: SQL Server `sql01.corp.local` (database `ProductReturns`), file server `files01.corp.local`
(shares `Returns$` and `Finance$`), SMTP relay `smtp.corp.local`, web proxy `proxy.corp.local:8080` for
calls to the carrier.

### Flow

1. The customer enters the order number and e-mail address on `https://returns.corp.local/`, selects items
   and reasons and submits. The portal keeps the draft in the session and calls `POST /api/returns`.
2. An agent approves or rejects the return in the staff queue (`/staff`).
3. The label service picks up approved returns every minute, books a label with ParcelLink, stores the PDF in
   `\\files01.corp.local\Returns$\Labels\yyyy\MM\<RMA>.pdf` and e-mails it to the customer.
4. The warehouse scans the parcel at the returns dock: the handhelds call `https://returns.corp.local/api/receiving/...`
   with their API key and grade each item (`AsNew`, `Opened`, `Used`, `Unsellable`).
5. A supervisor completes the inspection in the portal; the API calculates the refund (restocking fee, label fee).
6. `ProductReturns.Jobs refunds` writes `REFUND_yyyyMMdd_HHmm.csv` to `\\files01.corp.local\Finance$\Refunds\Inbound`
   for the payment run and mails the totals to accounts payable.

### Return policy

Configured in the `ReturnPolicy` section of the API settings: 30 days from delivery (Electronics 14, Furniture 60),
deliveries from 15 November to 24 December can be returned until 31 January, Food, GiftCards and Hygiene
cannot be returned, final sale items cannot be returned. Customers returning by choice pay the label fee (5.95);
opened Electronics 15 % and Appliances 10 % restocking fee; used items 50 %.

## Requirements

Build:

- .NET SDK **5.0.408** (pinned in `global.json`; builds the `netcoreapp3.1` and `net5.0` projects)
- Windows PowerShell 5.1 for the deploy scripts

Servers (Windows Server 2019 or later, Active Directory domain `CORP`):

- `returns`: IIS 10, URL Rewrite 2.1, Application Request Routing 3.0, certificate `CN=returns.corp.local`
- `returns-web`: ASP.NET Core Runtime **3.1.32** (x64), certificate `CN=returns-web.corp.local` in `LocalMachine\My`
- `returns-api`: ASP.NET Core Runtime **5.0.17** (x64), certificate `CN=returns-api.corp.local` in `LocalMachine\My`
- `returns-jobs`: .NET Runtime **5.0.17** and **3.1.32** (x64)
- `sql01`: SQL Server 2017 or later; `files01`: file server; `smtp`: SMTP relay accepting the service accounts
- Data disk `D:` on `returns-web`, `returns-api` and `returns-jobs` (`D:\ReturnsData`)
- Service accounts: `CORP\svc-returns-web`, `CORP\svc-returns-api`, `CORP\svc-returns-labels`, `CORP\svc-returns-jobs`
- AD groups `CORP\Returns-Agents` and `CORP\Returns-Supervisors` for portal staff
- ParcelLink business account number and API key

## Build and test

```
dotnet build ProductReturns.sln
dotnet test ProductReturns.sln
```

## Run locally

In the `Development` environment all applications share a SQLite database
(`src/ProductReturns.Api/App_Data/returns-dev.db`, created with sample orders on first start), the label
service uses the `File` carrier (no ParcelLink calls) and mails are written to `App_Data/mail` folders.

```
dotnet run --project src/ProductReturns.Api        # http://localhost:5020/swagger
dotnet run --project src/ProductReturns.Portal     # http://localhost:5010
dotnet run --project src/ProductReturns.Labels
dotnet run --project src/ProductReturns.Jobs -- refunds
```

Sample orders: `SO-1048213` / `anna.devries@example.com`, `SO-1048377` / `jonas.peeters@example.com`.
Staff sign-in: `agent` / `agent`, `supervisor` / `supervisor`. The portal calls the API with the development
key `dev-portal-key`; the development scanner key is `dev-scanner-0001`.

## Configuration

Each application reads `appsettings.json`, `appsettings.<Environment>.json` and then the machine settings file
written by the install scripts:

| Application | Machine settings |
| --- | --- |
| Portal | `C:\ProgramData\WideWorldImporters\ProductReturns\portal.settings.json` |
| API | `C:\ProgramData\WideWorldImporters\ProductReturns\api.settings.json` |
| Labels | `C:\ProgramData\WideWorldImporters\ProductReturns\labels.settings.json` |
| Jobs | `C:\ProgramData\WideWorldImporters\ProductReturns\jobs.settings.json` |

Important settings:

| Key | Application | Description |
| --- | --- | --- |
| `ConnectionStrings:Returns` | API, Labels, Jobs | SQL Server, Windows authentication of the service account |
| `Api:BaseUrl`, `Api:UseDefaultCredentials` | Portal | API address; the portal authenticates with its service account (Kerberos) |
| `Security:PortalAccounts` | API | Windows accounts allowed to call the API for the portal |
| `DataProtection:KeysFolder` | Portal | Key ring for cookies and anti-forgery tokens, protected with the machine key |
| `Proxy:KnownProxies` | Portal | Address of `returns.corp.local`, trusted for `X-Forwarded-*` headers |
| `StaffDirectory` | Portal | Domain and AD groups for staff sign-in |
| `CatalogCache:Folder` | API | Item master snapshot used when the ERP replica cannot be read at start-up |
| `Business:TimeZone` | API | Head office time zone for return windows (`W. Europe Standard Time`) |
| `Labels:SharePath`, `Labels:StateFolder`, `Labels:DailyQuota` | Labels | Label share, local state (daily quota counter), labels per day |
| `Carrier:ApiKeyProtected` | Labels | ParcelLink key encrypted with `ProductReturns.Labels.exe protect-secret <key>` on the server |
| `Jobs:RefundOutbox`, `Jobs:TimeZone` | Jobs | Finance inbound folder, cut-off time zone |

Logs go to the Windows Application event log (sources `Product Returns Portal`, `Product Returns API`,
`Product Returns Labels`, `Product Returns Jobs`); the jobs also write `D:\ReturnsData\Jobs\Logs\<job>-yyyyMMdd.log`.

## Deploy

Run the scripts in `deploy` as administrator. All scripts take `-ZoneName` (default `corp.local`).

1. DNS server: `.\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP <address>` (or one address per host).
2. `files01`: `.\New-ReturnsShares.ps1 -Domain CORP`.
3. Database: `.\Initialize-Database.ps1 -SqlServer sql01.corp.local -Domain CORP`. The ERP team's nightly
   export fills the `erp` schema.
4. Build server: `.\Publish.ps1 -Output C:\Build\ProductReturns` and copy the folder to the servers.
5. `returns-api`: `.\Install-ReturnsService.ps1 -Role Api -PublishPath C:\Build\ProductReturns -ServiceAccount CORP\svc-returns-api`
6. `returns-web`: `.\Install-ReturnsService.ps1 -Role Portal -PublishPath C:\Build\ProductReturns -ServiceAccount CORP\svc-returns-web`
7. `returns-jobs`: `.\Install-ReturnsService.ps1 -Role Labels -PublishPath C:\Build\ProductReturns -ServiceAccount CORP\svc-returns-labels -CarrierAccount <account>`
   (asks for the ParcelLink API key) and `.\Register-ReturnsJobs.ps1 -PublishPath C:\Build\ProductReturns -ServiceAccount CORP\svc-returns-jobs`.
8. `returns`: `.\Install-ProxySite.ps1 -CertificateThumbprint <thumbprint>`.
9. Register each handheld: `.\New-ScannerKey.ps1 -Name RTN-DOCK-01 -Location "Rotterdam returns dock 1"`.

To show the maintenance page during an update, create an empty file `maintenance.on` in `C:\inetpub\sites\returns`.
