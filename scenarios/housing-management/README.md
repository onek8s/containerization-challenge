# Fabrikam Housing

Housing management platform for Fabrikam Homes, a housing association with around 3,000 homes in and around
Bristol. Tenants use the tenant portal to see their rent account, pay, set up a direct debit, report repairs and
message their housing officer. Staff run tenancies, rent and arrears, repairs, contractors, compliance certificates
and documents in the back office. Repair contractors work through the contractor API. Scheduled jobs post the weekly
rent, collect direct debits, run arrears and gas safety reminders, pay contractors by Bacs and export journals to the
finance system.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/Fabrikam.Housing.Identity` | `identity.corp.local` (IIS site `identity`) | ASP.NET Core 3.1, IdentityServer4: sign-in for tenants (local accounts) and staff (AD over LDAPS), contractor API clients |
| `src/Fabrikam.Housing.TenantPortal` | `tenants.corp.local` (IIS site `tenants`), published as `tenants.fabrikamhomes.co.uk` | ASP.NET Core 3.1 MVC tenant portal |
| `src/Fabrikam.Housing.BackOffice` | `housing.corp.local` (IIS site `housing`) | ASP.NET Core 5.0 MVC staff back office with a live repairs board (SignalR) |
| `src/Fabrikam.Housing.ContractorApi` | `contractors-api.corp.local` (IIS site `contractors-api`), published as `contractors-api.fabrikamhomes.co.uk` | ASP.NET Core 5.0 Web API for contractors and the Paystream webhook |
| `src/Fabrikam.Housing.Jobs` | `housing-jobs.corp.local` (IIS site `housing-jobs`) | ASP.NET Core 5.0 host running Hangfire with the scheduled jobs and the Hangfire dashboard |
| `src/Fabrikam.Housing.DocumentService` | `docs01.corp.local` (Windows service `FabrikamDocumentService`, port 8085) | .NET Core 3.1 document store, scanned post intake and full-text search |
| `src/Fabrikam.Housing.Domain` | | Rent calendar, direct debit schedule, arrears policy, repairs rules, compliance rules, Bacs Standard 18, journals |
| `src/Fabrikam.Housing.Data` | | EF Core 3.1 model, Dapper report queries, development data |
| `src/Fabrikam.Housing.Integration` | | Paystream, SMS gateway, SMTP, postcode lookup, GIS patch lookup, Bacs bureau SFTP, document service client, simulators |
| `src/Fabrikam.Housing.Documents` | | Letters (wkhtmltopdf), statements, Excel reports, housing benefit schedule import, search index, thumbnails |
| `src/Fabrikam.Housing.Hosting` | | Shared start-up: Unity container, NLog, sign-in, data protection, health endpoint |
| `tests/Fabrikam.Housing.Tests` | | Domain, data, integration and document tests |
| `tests/Fabrikam.Housing.Api.Tests` | | Contractor API, jobs and back office tests |
| `db` | | SQL Server scripts |
| `deploy` | | DNS, service account, database, IIS and service install scripts |

Other servers: SQL Server `sql01.corp.local` (database `FabrikamHousing`), domain controller `dc01.corp.local`
(LDAPS), SMTP relay `smtp.corp.local`, web proxy `proxy.corp.local:8080`, ArcGIS Server `gis.corp.local` (housing
patch layer). External services: Paystream (direct debits and card payments), Textline (SMS), postcodes.io and the
Bacs bureau SFTP server.

Main libraries: IdentityServer4 4.1, EF Core 3.1, Dapper, Hangfire 1.7, Unity 5, NLog 4.7, AutoMapper 9, RestSharp 106,
Polly 7, MailKit 2, SSH.NET 2016.1, EPPlus 4.5, NPOI 2.5, iTextSharp (LGPL), DinkToPdf, Lucene.Net 4.8, Topshelf 4.3,
FluentValidation 9, Swashbuckle 5.

## Requirements

Build:

- .NET SDK **5.0.408** (pinned in `global.json`; builds the `netcoreapp3.1` and `net5.0` projects)
- Windows PowerShell 5.1 for the deploy scripts, SQL Server command line utilities (`sqlcmd`)

Servers (Windows Server 2019 or later, Active Directory domain `CORP`):

- Web servers: IIS 10 with WebSocket Protocol and Application Initialization, ASP.NET Core Hosting Bundle **3.1.32**
  (identity, tenants) and **5.0.17** (housing, contractors-api, housing-jobs), a server certificate per host name,
  data disk `D:`
- `identity`: token signing certificate in `LocalMachine\My`
- `docs01`: .NET Core Runtime and ASP.NET Core Runtime **3.1.32** (x64), data disk `D:`
- Letters: `libwkhtmltox.dll` (wkhtmltopdf 0.12.6, 64-bit) copied to the `native` folder of the tenants, housing and
  housing-jobs sites
- SQL Server 2017 or later on `sql01`
- Group managed service accounts `CORP\svc-housing-web$`, `CORP\svc-housing-jobs$` and `CORP\svc-housing-docs$`
  (domain KDS root key needed) and the AD groups `HSG-Officers`, `HSG-Repairs`, `HSG-Income`, `HSG-Compliance`,
  `HSG-Finance` and `HSG-Admins`; staff accounts under `OU=Staff,DC=corp,DC=local`
- Paystream, Textline and Bacs bureau accounts (access token, webhook secret, API key, SFTP key)

The external services, AD and SQL Server are not needed for development (see below).

## Build and test

```powershell
dotnet build HousingManagement.sln -c Release
dotnet test HousingManagement.sln
```

## Run locally

In Development every application uses a SQLite database in `.devdata\housing.db` (created and filled with sample
data on first start), simulators instead of Paystream, SMS, e-mail, postcode/GIS lookup and the Bacs bureau (they
write what would be sent under `.devdata\simulators`), the basic PDF renderer, and simulated directory accounts.

Start the applications from their project folders, identity server first:

```powershell
cd src\Fabrikam.Housing.Identity;        dotnet run    # http://localhost:5100
cd src\Fabrikam.Housing.DocumentService; dotnet run -- --console   # http://localhost:5105
cd src\Fabrikam.Housing.BackOffice;      dotnet run    # http://localhost:5102
cd src\Fabrikam.Housing.TenantPortal;    dotnet run    # http://localhost:5101
cd src\Fabrikam.Housing.ContractorApi;   dotnet run    # http://localhost:5103/swagger
cd src\Fabrikam.Housing.Jobs;            dotnet run    # http://localhost:5104/hangfire
```

Development accounts:

| Account | Password | Use |
| --- | --- | --- |
| `CORP\j.okafor` (North patch officer), `CORP\d.price`, `CORP\s.kowalska` | `Passw0rd!` | Back office |
| `CORP\repairs.desk`, `CORP\m.hughes` (finance, income), `CORP\c.bennett`, `CORP\admin` | `Passw0rd!` | Back office, jobs dashboard (admin, finance) |
| Lead tenants of the first five tenancies (e-mail shown in the back office) | `Tenant123!` | Tenant portal |
| Client `contractor-<code>`, e.g. `contractor-avonph` | `dev-secret-<code>`, e.g. `dev-secret-avonph` | Contractor API (client credentials, scope `contractor-api`) |

A scheduled job can be started at once with `POST /jobs/{name}/run` on the jobs host (admin). Scanned post is picked
up from `.devdata\scans\Inbound`; name files `TENANCY_<ref>_<yyyyMMdd>_<HHmm>.pdf` or `PROPERTY_<ref>_...` to link
them.

## Deploy

Run the scripts in `deploy` elevated, in this order. Each script has help with all parameters (`Get-Help .\<script> -Full`).

1. `new-service-accounts.ps1` (domain admin): gMSAs and HSG-* groups. Then `Install-ADServiceAccount` on each server.
2. `register-dns.ps1 -IPAddress <address>`: A records for `identity`, `tenants`, `housing`, `contractors-api`,
   `housing-jobs` and `docs01` in `corp.local` (use `-Records` to place them on different servers).
3. `install-database.ps1`: database, schema, reference data, logins (SQL logins `housing_identity`, `housing_portal`,
   `housing_api`; Windows logins for the gMSAs).
4. `install-document-service.ps1` on `docs01`: service, folders, `Scans$` share for the scanners, firewall rule.
5. `install-identity.ps1`, `install-back-office.ps1`, `install-tenant-portal.ps1`, `install-contractor-api.ps1` and
   `install-jobs.ps1` on the web servers. Sites go to `C:\inetpub\sites\<site>`; passwords, client secrets and API
   keys are written to `appsettings.Production.json` in the site folder.
6. Copy `libwkhtmltox.dll` into `native` of the tenants, housing and housing-jobs sites.
7. In the back office (Admin > Contractors), issue an API secret for each contractor.
8. In the Paystream dashboard, point the webhook at `https://contractors-api.fabrikamhomes.co.uk/webhooks/paystream`.

Logs are written to `D:\Logs\Housing\<application>` and errors also to `housing.ErrorLog`. Each application has
`/health`.

## Configuration

Settings are in `appsettings.json` of each application, overridden by `appsettings.Production.json` written by the
deploy scripts. Main sections:

| Section | Used by | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Housing`, `Database:Provider` | all | Database (`SqlServer` or `Sqlite`) |
| `Authentication` | portal, back office, jobs, API | Identity server address, client id and secret |
| `DataProtection` | web applications | Key folder (`D:\HousingData\keys\<app>`), DPAPI protection |
| `Integration` | all | `Mode` (`Live` or `Simulated`), proxy, Paystream, SMS, SMTP, postcodes, GIS, Bacs bureau, document service |
| `Documents` | portal, back office, jobs | PDF engine, template folder |
| `Jobs` | jobs | Time zone, schedules (cron, `disabled` to switch a job off), interface folders, team mailboxes |
| `Arrears` | back office, jobs | Arrears policy thresholds |
| `Storage`, `Intake`, `Api:Keys` | document service | Document store, index, scan folders, API keys |
| `Signing`, `Ldap`, `Clients` | identity | Token signing certificate, directory, client secrets |
| `Logging:Folder` | all | NLog file folder |
