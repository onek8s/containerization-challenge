# Lucerne Project Sites

Engineering project collaboration platform of Lucerne Engineering, a consulting engineering firm
(structural, civil, geotechnical, MEP, electrical, environmental and fire engineering; about 900 staff in
eight offices in Switzerland, Germany and Austria).

Every engineering project in the ERP gets a SharePoint project site created from the company web template.
A site has a document library for each discipline with engineering document content types and automatic
document numbering (`P19041-LUC-STR-DWG-0012`), a deliverables register, RFIs, risks, actions, meeting
minutes and the project team. A hub site collection holds the project register, site requests, the staff
directory and the status roll-up of all projects. Approved documents are published to the external
document control system.

The platform was built by the in-house collaboration team between 2014 and 2019:

- 2014: farm solution for SharePoint Server 2013 (.NET 4.5): web template, content types, event receivers,
  timer jobs, application pages.
- 2017: migrated to SharePoint Server 2016 (.NET 4.6.2); claims provider for the people picker.
- 2017-2018: provider-hosted dashboards add-in (ASP.NET MVC 5, CSOM, high-trust server-to-server tokens)
  and the ERP/HR sync Windows service (.NET 4.7.2).
- 2018: rules shared by the timer jobs, pages, add-in and service moved to .NET Standard 2.0 libraries so
  they could be unit tested.

## Components

| Project | Framework | Runs on | Purpose |
| --- | --- | --- | --- |
| `Lucerne.ProjectSites.Domain` | netstandard2.0 | library | Projects, stages, disciplines, staff, timesheets, deliverables, risks, RFIs, document numbering, revision rules (P01-P99, C01-C99), ISO weeks, RAG rules, retention schedule |
| `Lucerne.ProjectSites.Contracts` | netstandard2.0 | library | List, field, content type and property names; ERP WCF contract; HR and document control DTOs |
| `Lucerne.ProjectSites.SharePoint` | netstandard2.0 | library | Abstraction over sites, lists, items and groups; CAML builder; high-trust token issuer (JWT signed with an X.509 certificate) and realm discovery |
| `Lucerne.ProjectSites.Core` | netstandard2.0 | library | Status roll-up, document retention, resource sync, site provisioning, document number allocation and document rules |
| `Lucerne.ProjectSites.Integration` | netstandard2.0 | library | ERP SOAP client (WCF `ChannelFactory`), HR REST client (RestSharp), document control client (OAuth client credentials, multipart upload), Polly retries, AutoMapper profile |
| `Lucerne.ProjectSites.Documents` | netstandard2.0 | library | Status reports and transmittal sheets (Open XML SDK), portfolio, deliverable register and hours workbooks (EPPlus) |
| `Lucerne.ProjectSites.Sync` | netstandard2.0 | library | Sync engine and jobs: staff, projects, timesheets, approval publishing; watermarks and run history |
| `Lucerne.ProjectSites.Data` | net472 | library | Entity Framework 6 model of the `ProjectSitesHub` database (KPI history, subscriptions, sync state) |
| `Lucerne.ProjectSites.SharePoint.Csom` | net472 | library | SharePoint 2016 CSOM implementation of the abstraction (app-only, user + app and Windows contexts) |
| `Lucerne.ProjectSites.ServerObjectModel` | net462 | SharePoint farm (GAC) | Server object model implementation (`SPQuery`, `SPSecurity.RunWithElevatedPrivileges`, `SPContentDatabase.Sites.Add`) and ULS logging |
| `Lucerne.ProjectSites.Farm` | net462 | SharePoint farm (`Lucerne.ProjectSites.wsp`) | Features and feature receivers, web template (`onet.xml`), site columns and content types, list instances, list and document event receivers, timer jobs (status roll-up, retention, resource sync, site provisioning), application pages in `_layouts/15` (status report with `SPLongOperation`, retention preview, manual status), web parts, custom actions, JSOM/jQuery scripts |
| `Lucerne.ProjectSites.Claims` | net462 | SharePoint farm (`Lucerne.ProjectSites.Claims.wsp`) | Custom claims provider (office, discipline and all-staff claims; people picker search and resolve from a directory cache stored as an `SPPersistedObject`) and the timer job that refreshes the cache |
| `Lucerne.ProjectSites.Dashboards` | net472, ASP.NET MVC 5 | IIS, `dashboards.corp.local` | Provider-hosted add-in: portfolio and project dashboards, Excel exports, status subscriptions; Unity, log4net, Razor views with the SharePoint chrome control |
| `Lucerne.ProjectSites.SyncService` | net472 | Windows service `LucerneProjectSync` on `sync01` | Runs the sync jobs on intervals, writes KPI snapshots, sends status digest mails; InstallUtil installer, Event Log and log4net |
| `Lucerne.ProjectSites.AdminTool` | net472 | admin workstation (`psadmin.exe`) | CSOM bulk tasks: list sites, export the register, audit permissions, run the roll-up, request sites, re-apply the template |
| `Lucerne.ProjectSites.LabStubs` | net8.0 | test environments | In-memory SharePoint with a CAML evaluator, ERP SOAP simulator (CoreWCF), HR REST and document control simulators, seed data, end-to-end runner |
| `Lucerne.ProjectSites.Tests` | net8.0 | | xUnit tests |

Other folders:

- `addin/`: add-in manifest, app parts and `New-AppPackage.ps1` for the `.app` package.
- `db/`: schema, SQL logins and the SQL Agent job of the `ProjectSitesHub` database.
- `deploy/`: PowerShell module and scripts for DNS, publishing, solution packages, farm install, the
  project hub, server-to-server trust, the add-in site, the sync service and the database.
- `samples/`: ERP SOAP, HR and document control requests and responses.

Libraries: SharePoint Server 2016 server object model (`Microsoft.SharePoint.dll`), SharePoint 2016 CSOM
16.0.4690, System.IdentityModel.Tokens.Jwt 5.2, Entity Framework 6.2, ASP.NET MVC 5.2, Unity 5.8,
log4net 2.0.8, AutoMapper 7, Newtonsoft.Json 11, Polly 6, RestSharp 106, System.ServiceModel 4.5,
DocumentFormat.OpenXml 2.8, EPPlus 4.5.3, jQuery 1.12, Bootstrap 3 and Chart.js 2.

## How the parts work together

```
                    ERP (SOAP, erp01)     HR (REST, hr01)     Document control (REST)
                            \                  |                     ^
                             \                 |                     | approved documents
                         LucerneProjectSync (Windows service, sync01) --- CSOM, app-only S2S token ---+
                                   |  KPI snapshots, sync state                                          |
                                   v                                                                     v
 dashboards.corp.local  ---->  ProjectSitesHub (SQL, sql01)                 SharePoint 2016 farm (projects.corp.local)
 (MVC 5 add-in, IIS)   --- CSOM, user + app S2S token ------------------->  hub site collection + one site collection
                                                                            per project (/projects/p19041)
                                                                            farm solution: features, receivers,
                                                                            timer jobs, pages, web parts, claims
```

1. The sync service reads new and changed projects from the ERP and adds them to the project register on
   the hub. Active projects without a site get a site request.
2. The site provisioning timer job creates a site collection for each request from the project web template
   in the content database of the year, creates the project groups and adds the project manager and
   director.
3. Event receivers allocate document numbers, enforce revision rules and stop issued documents and records
   from being deleted. Deliverable and risk receivers keep the project status up to date.
4. The sync service copies approved timesheet hours into the project sites and publishes approved documents
   to document control. The resource sync timer job keeps the team list and the members group in line with
   booked hours; the status roll-up timer job writes the RAG status of every project to the hub.
5. After close-out the retention timer job declares design documents as records, moves them to the archive
   library and deletes correspondence and work in progress according to the retention schedule.
6. The dashboards add-in shows the portfolio and project status from the hub and the KPI history from SQL.

## Requirements

Production:

- SharePoint Server 2016 farm (two front ends, one application server) with Windows claims authentication,
  App Management and Subscription Settings service applications and an app domain.
- Windows Server 2016 with IIS 10 and .NET Framework 4.7.2 for the add-in site.
- Windows Server 2016 with .NET Framework 4.7.2 for the sync service.
- SQL Server 2016 for `ProjectSitesHub` (the farm databases are on the farm's own SQL instance).
- Certificate with private key for the server-to-server trust, on the add-in and sync servers.

Development:

- Visual Studio 2017/2019 with the .NET Framework 4.6.2 and 4.7.2 targeting packs, or the .NET SDK 8.
- `makecab.exe` (part of Windows) to build the `.wsp` files.

## Build and test

All projects build with the .NET SDK. The .NET Framework projects compile against reference assemblies;
`Microsoft.SharePoint.dll` comes from the `SharePoint.dll.2016.SSOM` reference package (set
`SharePointIsapiPath` to the farm's `ISAPI` folder to compile against the installed assemblies instead).

```
dotnet build ProjectSites.sln -c Release
dotnet test tests/Lucerne.ProjectSites.Tests
```

The add-in project uses `MSBuild.SDK.SystemWeb`. When building with the .NET SDK, pass the location of the
`Microsoft.WebApplication.targets` (from the `MSBuild.Microsoft.VisualStudio.Web.targets` package) as
`-p:VSToolsPath=...`; Visual Studio MSBuild finds them itself and also precompiles the Razor views in
Release builds.

The farm assemblies are strong-named with `Lucerne.snk` because they are deployed to the GAC. The `.wsp`
packages are built on Windows by `deploy/New-SolutionPackage.ps1` (called by `deploy/Publish.ps1`).

## Run locally

The lab runner exercises the shared libraries end to end with simulators: an in-memory SharePoint
(sites, lists, CAML queries, groups, property bags), the ERP SOAP service, the HR API and document control.

```
dotnet run --project tools/Lucerne.ProjectSites.LabStubs -- run --today 2019-06-14 --out /tmp/project-sites-lab
```

It syncs 84 employees and 16 projects, provisions the project sites, seeds deliverables, risks, RFIs and
documents, imports timesheets, publishes approved documents, runs team sync, status roll-up and retention,
checks the results and writes the reports (portfolio, status report, deliverable register, hours,
transmittal) and a JSON dump of the in-memory farm to the output folder.

`serve` starts only the simulators (default port 5081) for use with the sync service or `psadmin`:

```
dotnet run --project tools/Lucerne.ProjectSites.LabStubs -- serve --port 5081
```

## Deploy

Scripts are in `deploy/` and read `deploy/Settings.psd1`. Typical order for a new environment:

1. `Register-DnsRecords.ps1`: `projects`, `dashboards`, `sync01`, `sql01`, back-end names and the app
   domain wildcard.
2. `Publish.ps1 -Version 5.3.1` on the build server: builds the solution and writes the drop (farm
   solutions, add-in site and `.app`, sync service, admin tool, SQL scripts).
3. `Initialize-Database.ps1`: database, `ps_dashboards` and `ps_sync` logins, purge job.
4. `Install-FarmSolution.ps1` in the SharePoint 2016 Management Shell: adds or updates both solutions,
   sets the web application properties, adds the `/projects` managed path and activates the farm and web
   application features (web template, timer jobs, claims provider, directory cache job).
5. `New-ProjectHub.ps1`: hub site collection and its own content database, hub features.
6. `Register-HighTrustAddIn.ps1`: trusted root authority, token issuer and app principals for the add-in
   and the sync service.
7. `Install-DashboardsSite.ps1` on the add-in server: IIS site, app pool, Windows authentication, signing
   certificate. Upload `Lucerne.ProjectSites.Dashboards.app` to the app catalog.
8. `Install-SyncService.ps1` on `sync01`: files, config, event log source, InstallUtil, one `/once` run.

Upgrades run steps 2, 4, 7 and 8; `Install-FarmSolution.ps1` uses `Update-SPSolution` when the solution
is already deployed.

## Configuration

| Where | Setting | Purpose |
| --- | --- | --- |
| Web application properties | `lucerne.projectsites.huburl`, `lucerne.projectsites.recordsmanagers` | Hub URL and records managers group used by timer jobs, receivers and pages |
| Hub web property bag | `lucerne.projectsites.*` | Template version, manual status overrides, document number counters (on project sites) |
| `Dashboards/Web.config` | `ClientId`, `IssuerId`, `Realm`, `ClientSigningCertificateThumbprint`, `HubUrl`, connection string `ProjectSitesHub` | Add-in identity, S2S trust and database |
| `SyncService/App.config` | `ClientId`, `IssuerId`, `Realm`, `SigningCertificateThumbprint`, `Erp.*`, `Hr.*`, `DocControl.*`, `Interval.*`, `SmtpHost` | Service identity, back-end endpoints and schedules |
| `log4net.config` | log file paths | Add-in and service logs (`D:\Logs\...` in production) |
