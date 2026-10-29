# Customer Care

Case management for the Tailspin Energy contact centre. Agents record customer
contacts as cases, work them through queues and answer within the service level
targets; supervisors follow the queues on a live board, maintain the knowledge
base and receive escalations and a daily summary. The billing system, the
outage management system and partner contact centres create and follow cases
through a SOAP service. Customer master data comes from billing every night.

| Component | Description | Host |
| --- | --- | --- |
| `CustomerCare.Web` | Agent and supervisor portal: ASP.NET Web Forms pages (dashboard, cases, new case wizard, customers, SLA report, administration) and ASP.NET MVC 5 (knowledge base, queue board, search), SignalR live updates, Hangfire background jobs (SLA escalation, daily summary, statistics, clean-up) | `care.<zone>` (IIS site `care`) |
| `CustomerCare.Integration` | WCF case service for billing, outage management and partners (basicHttpBinding, Windows authentication) | `care-api.<zone>` (IIS site `care-api`) |
| `CustomerCare.Import` | Console application run by Task Scheduler: nightly customer import from the billing extract | portal server |
| `CustomerCare.Archive` | Console application run by Task Scheduler: archives closed cases older than the retention period | portal server |
| `CustomerCare.Data` | Entity Framework 6 model, stored procedure access, reference data, case operations | library |
| `CustomerCare.Domain` | Case numbers, business calendar, SLA policy, routing, workflow, customer file parser, knowledge base text | library |

Other servers referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `sql01.<zone>` | SQL Server, database `CustomerCare` (application data and background job storage) |
| `smtp.<zone>` | Internal SMTP relay |

The production configuration uses the zone `corp.local` and the domain `CORP`.
All names can point to the same server.

## Requirements

- Windows Server 2019 or later, IIS with ASP.NET 4.8, Windows Authentication,
  WebSocket Protocol, Application Initialization and WCF HTTP Activation
- .NET Framework 4.8
- SQL Server 2016 or later
- Active Directory groups: `CC-Agents`, `CC-Supervisors`, `CC-Admins` and
  `CC-Integration` (callers of the integration service)
- Service accounts for the portal application pool, the integration
  application pool and the scheduled tasks (or the default application pool
  identities and a local account on a single server)
- Visual Studio 2022 with the ASP.NET and web development workload, or Build
  Tools for Visual Studio 2022 with the web build tools
- The portal pages load jQuery and the SignalR client from `ajax.aspnetcdn.com`

## Build and test

```
msbuild CustomerCare.sln /t:Restore;Build /p:Configuration=Release
dotnet test tests\CustomerCare.Domain.Tests
```

Release builds precompile the `.aspx` pages and the Razor views.

## Database

```
deploy\Initialize-Database.ps1 -SqlServer sql01.corp.local -WebAccount CORP\svc-care-web -ApiAccount CORP\svc-care-api -ToolsAccount CORP\svc-care-tools
```

The script runs `db\01-create-database.sql` to `db\06-logins.sql` with
`sqlcmd`. `04-reference-data.sql` adds queues, categories, routing rules, SLA
targets and holidays; `05-demo-data.sql` adds demo customers, cases and
knowledge base articles (left out with `-SkipDemoData`). The portal creates the
`HangFire` tables on first start.

## Deploy

1. DNS records (on the DNS server):

   ```
   deploy\Install-DnsRecords.ps1 -ZoneName corp.local -PortalIP <ip> [-IntegrationIP <ip>] [-SqlServerIP <ip>] [-SmtpServerIP <ip>]
   ```

2. Publish (on the build machine):

   ```
   deploy\Publish.ps1 -OutputPath C:\drop\CustomerCare
   ```

3. IIS sites (on the web server, from the published folder):

   ```
   deploy\Install-IisSites.ps1 -PublishPath C:\drop\CustomerCare -ZoneName corp.local -PortalPoolAccount CORP\svc-care-web -ApiPoolAccount CORP\svc-care-api [-CertificateThumbprint <thumbprint>]
   ```

   The script writes the zone and a generated shared key
   (`CustomerCare:NotifyKey`) into both `Web.config` files. When the two sites
   are on different servers, run it on each with `-Sites` and the same
   `-NotifyKey`.

4. Tools and scheduled tasks:

   ```
   deploy\Install-Tools.ps1 -PublishPath C:\drop\CustomerCare -ZoneName corp.local -TaskAccount CORP\svc-care-tools [-DataRoot D:\CustomerCare] [-BillingDropAccount CORP\svc-billing]
   ```

   Billing drops `customer_YYYYMMDD.csv` into `<DataRoot>\Import\Inbox`
   (shared as `CareImport$` when `-BillingDropAccount` is given). Archived
   cases are written to `<DataRoot>\Archive\yyyy\MM`.

## Run

- Portal: `http(s)://care.<zone>/` (signed in with Windows authentication)
- Queue board: `http(s)://care.<zone>/board/<queue code>`
- Background jobs dashboard (supervisors): `http(s)://care.<zone>/admin/jobs`
- Integration service: `http://care-api.<zone>/CaseIntegration.svc` (WSDL at `?wsdl`)
- Import manually: `CustomerCare.Import.exe [--file <path> | --validate <path>]`
- Archive manually: `CustomerCare.Archive.exe [--months 24] [--batch 500] [--dry-run]`

Logs are written to `C:\ProgramData\Tailspin\CustomerCare\Logs` (sites) and
`<DataRoot>\Logs` (tools).

## Configuration

`appSettings` in `CustomerCare.Web\Web.config` (the integration service uses
the same keys where they apply):

| Key | Default | Description |
| --- | --- | --- |
| `CustomerCare:AgentGroup`, `SupervisorGroup`, `AdminGroup` | `CORP\CC-...` | Groups for the application roles |
| `CustomerCare:TimeZone` | `FLE Standard Time` | Time zone of the contact centre (Windows time zone id) |
| `CustomerCare:OfficeOpens`, `OfficeCloses` | `08:00`, `17:00` | Business hours used by the SLA clocks |
| `CustomerCare:DefaultQueue` | `GENERAL` | Queue when no routing rule or category queue applies |
| `CustomerCare:PortalUrl` | `https://care.corp.local/` | Link in e-mails |
| `CustomerCare:ManagerEmail` | | Receives level 3 escalations and the daily summary |
| `CustomerCare:MaxAttachmentKB` | `10240` | Largest attachment |
| `CustomerCare:ReportFolder` | `~/App_Data/Reports` | Generated report files |
| `CustomerCare:UploadFolder` | `%TEMP%\CustomerCare\Uploads` | Attachments of new cases that are not saved yet |
| `CustomerCare:JobWorkers` | `2` | Background job worker threads |
| `CustomerCare:NotifyKey` | | Shared key between the integration service and the portal |
| `CustomerCare:IntegrationGroups` | `CORP\CC-Integration` | Integration service only: groups allowed to call it |
