# Property Inspections

Claim inspection photos and reports for Northwind Insurance. Loss adjusters
record property inspections, upload photos from site visits, and send the
inspection report with a contact sheet to the claim handler. Field camera sync
and partner adjusters can also drop photos into a shared intake folder. The
claims system and partners read inspection status through a SOAP endpoint.

| Component | Description | Host |
| --- | --- | --- |
| `Inspections.Web` | ASP.NET MVC 5 portal for adjusters and supervisors | `inspections.<zone>` (IIS site `inspections`) |
| `Inspections.Service` | WCF photo service (basicHttpBinding, MTOM) and partner claim status service (wsHttpBinding), photo media handler | `inspections-svc.<zone>` (IIS site `inspections-svc`) |
| `Inspections.Intake` | Windows service `NorthwindInspectionsIntake`, imports photos from the intake folder | `inspections-app.<zone>` |
| `Inspections.Jobs` | Console application run by Task Scheduler: report e-mails, photo purge, administration commands | `inspections-app.<zone>` |
| `Inspections.Infrastructure` | Settings, SQL Server data access, photo processing and storage, event log, performance counters | library |
| `Inspections.Contracts` | WCF service and data contracts | library |
| `Inspections.Core` | Claim numbers, storage layout, image geometry, report content, retention rules | library |

Other servers referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `sql01.<zone>` | SQL Server, databases `Inspections` and `ASPState` |
| `files01.<zone>` | File share `Inspections$` (photos, reports, `Intake` folder) |
| `smtp.<zone>` | Internal SMTP relay |

The production configuration uses the zone `corp.local`. All names can point
to the same server.

## Requirements

- Windows Server 2019 or later
- IIS with ASP.NET 4.8, WCF HTTP Activation, IP and Domain Restrictions,
  HTTP Redirection, IIS URL Rewrite 2.1 and Application Request Routing 3.0
- .NET Framework 4.8
- SQL Server 2016 or later
- A server certificate for `inspections.<zone>` and `inspections-svc.<zone>`
- Visual Studio 2022 with the ASP.NET and web development workload, or Build
  Tools for Visual Studio 2022 with the web build tools
- Service accounts (domain accounts, or local accounts on a single server):
  portal application pool, service application pool, application server
  (intake service and scheduled tasks) and optionally a separate account for
  the photo share

## Build and test

```
msbuild PropertyInspections.sln /t:Restore;Build
dotnet test tests\Inspections.Core.Tests
```

## Database

```
sqlcmd -S sql01.corp.local -E -i db\01-create-database.sql
sqlcmd -S sql01.corp.local -E -d Inspections -i db\02-schema.sql
sqlcmd -S sql01.corp.local -E -d Inspections -i db\03-seed.sql
aspnet_regsql.exe -S sql01.corp.local -E -ssadd -sstype p
sqlcmd -S sql01.corp.local -E -i db\04-logins.sql -v PortalAccount="CORP\svc-insp-web" ServiceAccount="CORP\svc-insp-svc" JobsAccount="CORP\svc-insp-jobs"
```

Demo sign-in: `akelly` (supervisor), `pbrennan`, `smurphy`, password
`Inspect!2024`. Partner endpoint user `claims-core`, password `Partner!2024`.

## Deploy

1. DNS records (on the DNS server):

   ```
   deploy\Install-DnsRecords.ps1 -ZoneName corp.local -PortalIP <ip> -ServiceIP <ip> -AppServerIP <ip> -SqlServerIP <ip> -FileServerIP <ip> -SmtpServerIP <ip>
   ```

2. Photo share (on `files01`):

   ```
   deploy\New-PhotoShare.ps1 -Path D:\Shares\Inspections -Accounts CORP\svc-insp-share -IntakeWriters CORP\GG-Inspection-Upload
   ```

3. Publish (on the build machine):

   ```
   deploy\Publish.ps1 -OutputPath C:\Build\Inspections
   ```

4. Settings (on `inspections-svc` and `inspections-app`):

   ```
   deploy\Set-InspectionSettings.ps1 -ZoneName corp.local -ShareUser CORP\svc-insp-share -SecretReaders CORP\svc-insp-svc,CORP\svc-insp-jobs
   ```

5. IIS sites (on the web server(s)):

   ```
   deploy\Install-IisSites.ps1 -PublishPath C:\Build\Inspections -ZoneName corp.local -CertificateThumbprint <thumbprint> `
       -PortalPoolAccount CORP\svc-insp-web -ServicePoolAccount CORP\svc-insp-svc -PortalServerIPs <portal server ip> `
       -UrlRewriteMsi rewrite_amd64_en-US.msi -ArrMsi requestRouter_amd64.msi
   ```

6. Application server (on `inspections-app`):

   ```
   deploy\Install-AppServer.ps1 -PublishPath C:\Build\Inspections -ServiceAccount CORP\svc-insp-jobs
   ```

7. Event log sources and performance counters on the service host (elevated):

   ```
   C:\Build\Inspections\Inspections.Jobs\Inspections.Jobs.exe install
   ```

8. Passwords. Each account stores its own copy, readable only by that account:

   ```
   runas /user:CORP\svc-insp-svc  "C:\Build\Inspections\Inspections.Jobs\Inspections.Jobs.exe protect-secret SharePassword"
   runas /user:CORP\svc-insp-jobs "C:\Program Files\Northwind\Inspections\Inspections.Jobs.exe protect-secret SharePassword"
   runas /user:CORP\svc-insp-jobs "C:\Program Files\Northwind\Inspections\Inspections.Jobs.exe protect-secret SmtpPassword"
   ```

   Then start the intake service: `Start-Service NorthwindInspectionsIntake`.

   `SmtpPassword` is only needed when `SmtpUser` is set. Without `ShareUser`
   the share is accessed as the application pool or service account.

## Run

- Portal: `https://inspections.corp.local/`
- Photo service: `http://inspections-svc.corp.local/PhotoService.svc`
- Partner service: `https://inspections-svc.corp.local/ClaimStatusService.svc`
- Intake: copy `<claim number>_<name>.jpg` (and optionally `<file>.txt` with
  the caption) to `\\files01.corp.local\Inspections$\Intake`
- Scheduled tasks `\Northwind\Inspections - send reports` (every 5 minutes)
  and `\Northwind\Inspections - purge photos` (daily 02:30)
- `Inspections.Jobs.exe status` shows the settings, queue and share access for
  the current account; `Inspections.Intake.exe --console` runs the intake
  interactively

## Configuration

| Location | Content |
| --- | --- |
| `HKLM\SOFTWARE\Northwind Insurance\Inspections` | `ConnectionString`, `PhotoShare`, `ShareUser`, `SmtpHost`, `SmtpPort`, `SmtpFrom`, `SmtpUser`, `PortalUrl`, `MaxPhotoEdge`, `ThumbnailEdge`, `JpegQuality`, `WatermarkFont`, `PurgeAfterDays`, `ReportBatchSize` |
| `...\Inspections\Secrets\<DOMAIN_account>` | `SharePassword`, `SmtpPassword` (binary, protected per account) |
| `Inspections.Web\Web.config` | Connection string (encrypted at install), session state, machine key, rewrite rules, photo service endpoint |
| `Inspections.Service\Web.config` | WCF bindings and services, machine key (same as the portal), allowed portal addresses |
| `Inspections.Intake.exe.config` | Intake folder (relative to `PhotoShare`), poll and settle intervals |
| Event log | `Northwind Inspections` (sources `Inspections Portal`, `Inspections Service`, `Inspections Jobs`) |
| Performance counters | Category `Northwind Inspections` |
| Log files | `C:\ProgramData\Northwind\Inspections\Logs` |
