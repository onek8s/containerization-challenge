# City Permits

Permit and licence casework for the City of Ashford planning and licensing department. Residents and
businesses apply online for building permits, business licences and residents' parking permits, pay the
fees by card and follow their cases. Case officers check the site against the council's map layers
(zoning, conservation areas, flood zones, easements, tree preservation orders), consult other
departments, decide and issue the decision letter and permit with a site map. Building inspectors record
site visits on a tablet. Paper forms sent by post are scanned and turned into draft cases. Decided
cases are published as open data.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/CityPermits.Citizen` | `permits.corp.local` (IIS site `permits`, published as permits.ashford.gov.uk) | Citizen portal (Razor Pages): applications, uploads, fee payment through Paystream, case status and documents. Sign-in with the national eID (SAML 2.0) |
| `src/CityPermits.BackOffice` | `permits-office.corp.local` (IIS site `permits-office`) | Case officer application (MVC): intake, validation, site checks, consultations, conditions, decisions, fee waivers, search and Excel reports. Sign-in through ADFS (WS-Federation) |
| `src/CityPermits.Inspections` | `inspect.corp.local` (IIS site `inspect`) | Inspector app: Vue single-page app (`ClientApp`) with offline result queue and its API. ADFS tokens |
| `src/CityPermits.MapService` | `maps.corp.local` (IIS site `maps`) | Map tiles, parcel lookup, site constraint checks and map exports for the other applications (`X-Map-Key`) |
| `src/CityPermits.OpenData` | `opendata.corp.local` (IIS site `opendata`) | Public API of decided cases: JSON, CSV, GeoJSON, statistics (`/swagger`) |
| `src/CityPermits.Jobs` | `permits-app01.corp.local` (Windows service `CityPermitsJobs`) | Scheduled jobs (Quartz.NET): outbox mails, records filing, renewals and expiry, GIS replica sync, payment settlement, consultation reminders |
| `src/CityPermits.MailIntake` | `permits-app01.corp.local` (Windows service `CityPermitsMailIntake`) | Reads scanned TIFF forms from the scan share (OCR), recognises forms BP1, BL2 and PP1 and creates draft cases |
| `src/CityPermits.Admin` | | Command line tool `permits-admin`: database, sample data, site check, GIS health |
| `src/CityPermits.Domain` | | Case model, workflow, fees, target dates, reference numbers, planning and renewal rules, validators |
| `src/CityPermits.Contracts` | | DTOs shared by the APIs and their clients |
| `src/CityPermits.Data` | | EF Core model (SQL Server; SQLite in development), outbox, reference numbers, report queries, sample data |
| `src/CityPermits.Gis` | | Geomatix Pro map engine host (site checks, tiles, layouts); development provider over the sample GeoJSON layers |
| `src/CityPermits.Archive` | | Document archive on a SQL Server FileTable with full-text search; folder archive for development |
| `src/CityPermits.Documents` | | PDF decision letters, permits, renewal reminders and invoices (MigraDoc) |
| `src/CityPermits.Integration` | | Paystream payments, address register, RecordsOne capture service (SOAP), mail |
| `tests/CityPermits.Tests` | | Unit and integration tests |
| `samples` | | GIS layers (EPSG:27700), address register extract, scanned form |
| `deploy` | | DNS, map engine, archive, IIS, service, publish and SQL scripts |

Other hosts:

| Host | Use |
| --- | --- |
| `permits-sql.corp.local` | SQL Server, database `CityPermits` (schema `permits`) and FileTable `PermitsArchive` (share `PERMITSFS`) |
| `gis-sql.corp.local` | Enterprise geodatabase of the GIS team, map document and layouts on `GisDeploy$` |
| `records01.corp.local` | RecordsOne records management, capture service |
| `adfs.corp.local` | AD FS: case officer sign-in and inspector tokens |
| `smtp.corp.local` | Mail relay |
| Paystream, national eID | External card payment and identity services |

## Requirements

- Windows Server 2022 or later with IIS and the ASP.NET Core 8 Hosting Bundle, .NET 6 runtime for
  `inspect`, .NET 8 Desktop Runtime on hosts running the back office, map service, jobs and admin tool.
- Geomatix Pro 11 (CoreHost) with a licence authorised for each host that runs the map engine
  (`maps`, `permits-office`, `permits-app01`), the map document, layouts and the OS MasterMap basemap.
- SQL Server 2019 or later with FILESTREAM enabled, Full-Text Search and the Adobe PDF iFilter.
- Windows OCR language en-GB on `permits-app01`.
- .NET SDK 8.0.400 or later (and 6.0 for the inspections app), Node.js 18 or later.

## Build and test

```
dotnet build CityPermits.sln
dotnet test CityPermits.sln
cd src/CityPermits.Inspections/ClientApp && npm ci && npm run build
```

`npm run build` writes the single-page app to `src/CityPermits.Inspections/wwwroot`; `dotnet publish` of the
inspections project runs it too.

## Deploy

Run from an elevated PowerShell on the target hosts:

1. `deploy/Register-Dns.ps1` creates the host records in `corp.local`.
2. `deploy/sql/01-database.sql` to `04-logins.sql` on `permits-sql` (database, schema, FileTable, logins).
   `permits-admin script-schema` regenerates `02-schema.sql` from the model.
3. `deploy/Install-Archive.ps1` on `permits-sql` enables FILESTREAM access and full-text with the PDF iFilter.
4. `deploy/Install-GisEngine.ps1 -ServiceAccount CORP\svc-permits-web` on each map engine host: checks
   the Geomatix installation and licence, copies the map document, layouts and replica from `GisDeploy$`.
5. `deploy/Publish-All.ps1 -Output D:\Publish` builds all deployables.
6. `deploy/Install-WebSites.ps1 -PublishRoot D:\Publish -CertificateThumbprint <thumbprint>` creates the
   app pools and sites under `C:\inetpub\sites\<site>` and writes `appsettings.Production.json`.
7. `deploy/Install-Services.ps1 -PublishRoot D:\Publish -ServiceAccount CORP\svc-permits-app` installs
   `CityPermitsJobs` and `CityPermitsMailIntake` and the `Scans$` share.

### Lab server

`deploy/lab.psd1` describes a single Windows Server host: every name points at it and simulators or
development providers are used for Geomatix, FILESTREAM, eID, ADFS, Paystream and RecordsOne. Pass
`-Lab` to `Install-WebSites.ps1` and `Install-Services.ps1` and `-LabTarget host01` to `Register-Dns.ps1`.

## Run locally

In Development every app uses the SQLite database `App_Data/permits-dev.db`, the sample GIS layers,
the folder archive and simulators.

```
dotnet run --project src/CityPermits.Admin -- create-db
dotnet run --project src/CityPermits.Admin -- seed
dotnet run --project src/CityPermits.MapService      # http://localhost:5103, key dev-map-key
dotnet run --project src/CityPermits.Citizen         # http://localhost:5101
dotnet run --project src/CityPermits.BackOffice      # http://localhost:5102
dotnet run --project src/CityPermits.Inspections     # http://localhost:5104
dotnet run --project src/CityPermits.OpenData        # http://localhost:5105
dotnet run --project src/CityPermits.Jobs -- run renewals
dotnet run --project src/CityPermits.MailIntake      # reads App_Data/scans
```

The citizen portal and back office show a development sign-in page; the inspections app issues
development tokens at `/api/dev-token`. Copy `samples/scans/*` to `App_Data/scans` to try the mail intake.
