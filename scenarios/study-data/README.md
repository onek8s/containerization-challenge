# Study Data

Clinical trial data capture and monitoring for Fabrikam Clinical Research, a contract research
organisation. Site staff register subjects and enter visit data in electronic case report forms
(eCRFs); derived values (BMI, body surface area, creatinine clearance, eGFR, carboplatin and study drug
doses) come from the sponsor-approved calculation workbook of each study. Monitors verify the data
against source documents and raise queries, medical coders code adverse events and medications,
investigators sign casebooks, and sponsors receive data exports and DSMB safety reports.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/StudyData.Edc.Web` | `edc.corp.local` (IIS site `edc`) | Site portal (ASP.NET Core MVC): subjects, visit dates and windows, eCRF entry with edit checks and workbook derivations, query answers, source document upload, randomisation, investigator signature. Sign-in with e-mail, password and authenticator app |
| `src/StudyData.Monitor.Api` | `monitor.corp.local` (IIS site `monitor`) | Monitoring application for CRO staff: React single-page app (`ClientApp`) and its API, source data verification, query management, casebook viewer, medical coding, live activity (SignalR). Sign-in through ADFS |
| `src/StudyData.Sponsor.Api` | `sponsor-api.corp.local` (IIS site `sponsor-api`) | REST API for sponsor systems (`/api/v1`, OpenAPI at `/swagger`): enrolment status, subjects, CDISC ODM exports, safety reports. ADFS client credentials |
| `src/StudyData.Jobs` | `trials-app01.corp.local` (Windows service `StudyDataJobs`) | Scheduled jobs (Quartz.NET): casebook PDFs, ODM exports, monthly DSMB safety workbook, autocoding, central lab import over SFTP, query reminder mails |
| `src/StudyData.Core` | | Study design, subjects and visits, query workflow, edit check expression language, visit schedule, randomisation lists, lab grading |
| `src/StudyData.Data` | | EF Core model (SQL Server; SQLite in development), form entry, query, review and subject services, sample study FAB-ONC-201 |
| `src/StudyData.Calculation` | | Calculation workbooks in Microsoft Excel (pool of automation instances), workbook validation hash check, report workbooks |
| `src/StudyData.Documents` | | Casebook layout (XAML templates), WPF page rendering, XPS and PDF printing |
| `src/StudyData.Coding` | | MedDRA and WHODrug coding through Coderis AutoCode 6, coding workflow |
| `src/StudyData.Integration` | | Central lab transfer (SFTP, SSH.NET), CDISC ODM 1.3.2 export with AES zip (DotNetZip), mail |
| `tests/StudyData.Tests` | | Unit, data, calculation, integration and web tests |
| `studies/FAB-ONC-201` | | Validated calculation workbook (`calculations`) and DSMB report template (`reports`) of the sample study |
| `vendor/Coderis` | | Coderis AutoCode type library description |
| `samples/centrallab` | | Central lab transfer file |
| `deploy` | | DNS, IIS, service, ADFS, file share, Excel automation, printer, coding engine and SQL scripts |

Other hosts:

| Host | Use |
| --- | --- |
| `sql01.corp.local` | SQL Server, database `StudyData` (schema `edc`) |
| `files01.corp.local` | Share `StudyData$`, published as `\\corp.local\dfs\StudyData` |
| `adfs.corp.local` | AD FS: sign-in of CRO staff (monitor) and sponsor system tokens (sponsor API) |
| `smtp.corp.local` | Mail relay |
| `sftp.centrallab.example` | Central laboratory SFTP server (partner) |

### Data flow

1. Site staff register a subject (subject number `101-004`) and enter the screening visit. When a form is
   saved, field checks run first; then the form's inputs are written into the study workbook, Excel
   recalculates it and the derived fields are read back; then the study's edit checks run. Failed checks
   open automatic queries, which close themselves when the data is corrected.
2. Changing saved data needs a reason for change; every change is in the audit trail, resets source data
   verification of the field and voids the investigator signature of the form.
3. Eligible subjects are randomised from pre-generated permuted block lists per site and stratum. Visit
   target dates and windows follow from the cycle 1 day 1 date.
4. Monitors verify fields, raise and close queries and read the casebook as rendered pages. Coders review
   the terms that the autocoder could not code. Investigators sign the casebook with their password.
5. The jobs service writes casebook PDFs nightly for changed subjects, builds ODM exports requested
   through the sponsor API, fills the DSMB safety workbook template monthly (listings, exposure, lab shifts;
   arms coded X and Y), imports central lab files and flags grade 3-4 values, and mails query reminders.

### Edit checks and derivations

Edit checks are expressions over the fields of the form (`VSSYSBP > VSDIABP`), of other forms
(`DM.BRTHDAT`, `IC.ICDAT`) or of a given visit (`SCR.VSHEIGHT`), with `AND`, `OR`, `NOT`, arithmetic and the
functions `ISBLANK`, `NOTBLANK`, `TODAY`, `YEARS`, `DATEDIFF`, `YEAR`, `ABS`, `MIN`, `MAX`, `LEN`, `IN`, `IF`.
Severity `Hard` refuses the save, `Query` opens an automatic query and `Warning` is only shown.

Workbook inputs are named ranges whose values are given by expressions (`HEIGHT_CM` =
`IF(NOTBLANK(VS.VSHEIGHT), VS.VSHEIGHT, SCR.VSHEIGHT)`); derived fields name the output range they show.
A workbook is used only when the SHA-256 of the file on the share matches its validation record.

## Requirements

Build:

- .NET 8 SDK (with the Windows desktop workload for `StudyData.Documents`, `StudyData.Monitor.Api` and `StudyData.Jobs`)
- Node.js 20 or later (monitor single-page app)

Servers:

- Windows Server 2019 or later, Active Directory domain `CORP` with DNS, AD FS 2019 or later
- `edc`, `monitor`, `sponsor-api`: IIS 10, ASP.NET Core 8 Hosting Bundle, TLS certificate
- `edc` web server and `trials-app01`: Microsoft Excel 64-bit (Microsoft 365 Apps or Office LTSC), activated,
  the build named on the workbook's Validation sheet
- `monitor` web server and `trials-app01`:
  - .NET 8 Windows Desktop runtime (x64)
  - Microsoft Print to PDF feature
  - Coderis AutoCode 6.4 with a licence file for the server and the MedDRA and WHODrug dictionaries
- `sql01`: SQL Server 2019 or later
- Service accounts: `CORP\svc-studydata-web` (application pools) and the group managed service account
  `CORP\svc-studydata-job$` (jobs service)
- AD groups `CORP\SD-Monitors`, `CORP\SD-DataManagers`, `CORP\SD-MedicalCoders`, `CORP\SD-StudyAdmins`
- Central lab SFTP account and private key, sponsor archive passwords (`Exports:ArchivePasswords`)

## Build and test

```
dotnet build StudyData.sln
dotnet test StudyData.sln
cd src/StudyData.Monitor.Api/ClientApp && npm ci && npm run build
```

`npm run build` writes the single-page app to `src/StudyData.Monitor.Api/wwwroot/app`; `dotnet publish` of
the monitor project runs it too.

## Deploy

From the scenario folder on a build machine:

```
dotnet publish src\StudyData.Edc.Web -c Release -o publish\edc
dotnet publish src\StudyData.Monitor.Api -c Release -o publish\monitor
dotnet publish src\StudyData.Sponsor.Api -c Release -o publish\sponsor-api
dotnet publish src\StudyData.Jobs -c Release -r win-x64 --self-contained false -o publish\jobs
```

Then, in this order (each script has its parameters and examples in its header):

| Step | Where | Script |
| --- | --- | --- |
| 1 | DNS server | `deploy\register-dns.ps1 -IPAddress <web server>` (add `-AppServerIPAddress`, `-SqlIPAddress` ... for separate servers) |
| 2 | `sql01` | `sqlcmd -E -i deploy\sql\01-create-database.sql`, then `02-logins.sql` and `03-audit-protection.sql` |
| 3 | `files01` | `deploy\create-folders.ps1 -Path D:\Shares\StudyData -StudiesSource studies` |
| 4 | AD FS | `deploy\configure-adfs.ps1` (note the client secrets it prints) |
| 5 | web server, `trials-app01` | `deploy\install-office-automation.ps1 -Accounts ...` |
| 6 | web server, `trials-app01` | `deploy\install-pdf-printer.ps1 -Accounts ...` and `deploy\install-coding-engine.ps1 -Accounts ...` |
| 7 | web server | `deploy\install-web.ps1 -PublishRoot publish -AppPoolAccount CORP\svc-studydata-web -CertificateThumbprint ...` |
| 8 | `trials-app01` | `deploy\install-jobs.ps1 -PublishPath publish\jobs -ServiceAccount 'CORP\svc-studydata-job$'` |
| 9 | web server | load the study and site users: `StudyData.Edc.Web.exe setup-sample-study`, `StudyData.Edc.Web.exe add-site-user ...` (printed by step 7) |

All four applications can also be installed on one server; the scripts take the host names, SQL server and
data folder as parameters.

### Lab server

On a lab server without Excel, Coderis, AD FS or a central lab, run the applications with
`ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT` set to `Development` (`install-web.ps1 -Environment Development
-Http`, and `DOTNET_ENVIRONMENT=Development` for the service). Development settings use:

- the SQLite database `App_Data\studydata.db` (or set `ConnectionStrings__StudyData` to the SQL Server database)
- built-in formulas of the FAB-ONC-201 workbook instead of Excel (`Calculation:Provider=Development`)
- SVG page images and HTML casebooks instead of WPF and the PDF printer (`Documents:Provider=Development`)
- a small built-in term list instead of Coderis (`Coding:Provider=Development`)
- central lab files from `App_Data\LabTransfers` instead of SFTP, mails written to `App_Data\Mail`
- sign-in to the monitor site without AD FS as `CORP\monitor1` with all roles (`?as=CORP\cra2&roles=Monitor`
  for another user), and the `X-Client-Id` header instead of a token on the sponsor API

## Run locally

```
dotnet run --project src/StudyData.Edc.Web          # http://localhost:5300
dotnet run --project src/StudyData.Monitor.Api      # http://localhost:5310
dotnet run --project src/StudyData.Sponsor.Api      # http://localhost:5320/swagger
dotnet run --project src/StudyData.Jobs
```

Start the site portal first: it creates the database with the sample study FAB-ONC-201, four sites, three
subjects with data and queries, and site accounts with the password `Fab201!site`:

| Account | Site | Role |
| --- | --- | --- |
| `aino.virtanen@hus.example` | 101 Helsinki | Investigator (signs) |
| `liisa.makela@hus.example` | 101 Helsinki | Study coordinator |
| `mikko.laine@tays.example` | 102 Tampere | Investigator |
| `kadri.tamm@regionaalhaigla.example` | 201 Tallinn | Investigator |
| `erik.holm@karolinska.example` | 301 Stockholm | Study coordinator |

For the monitor app during development, `npm run dev` in `ClientApp` serves it on http://localhost:5173 with
the API proxied to port 5310.

Sponsor API, development:

```
curl -H "X-Client-Id: tailspin-cdms" http://localhost:5320/api/v1/studies/FAB-ONC-201
curl -X POST -H "X-Client-Id: tailspin-cdms" -H "Content-Type: application/json" -d "{}" http://localhost:5320/api/v1/studies/FAB-ONC-201/exports
```

Copy `samples/centrallab/*.csv` to `App_Data/LabTransfers` to have the jobs service import central lab results.
Run one job immediately with `dotnet run --project src/StudyData.Jobs -- run labs` (`documents`, `casebooks`,
`safety`, `coding`, `labs`, `reminders`).
