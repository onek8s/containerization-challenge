# Clinic Scheduling

Outpatient scheduling, records and billing for **Southridge Medical Group**, a six-site primary care
and specialty group (family medicine, pediatrics, cardiology, orthopedics and an imaging centre).
The system was started in 2008 on .NET Framework 3.5 SP1 and extended through 2013 on .NET 4.0 and 4.5.2;
the libraries kept the language level and framework of the release that introduced them.

Front desk staff book, reschedule, check in and check out patients in the staff site; providers chart
vitals, notes, diagnoses, charges and lab orders. Patients book visits, cancel, fill in intake forms and
download statements in the patient site. Specialty referrals go through a long-running approval workflow.
The interface engine exchanges HL7 v2 messages with St. Catherine hospital (registrations, imaging
bookings, imaging charges) and Cascade Reference Lab (orders and results). The nightly billing batch
creates 837P claim files for the clearinghouse, posts 999 acknowledgments and 835 remittances, prints
patient statements and the A/R aging workbook. A reminders job sends appointment reminders by SMS and
e-mail, marks no-shows and offers freed times to the waitlist.

## Components

| Project | Host | Description |
| --- | --- | --- |
| `src/Southridge.Clinic.StaffWeb` | `clinic.corp.local` (IIS) | Staff site, ASP.NET Web Forms 4.5 with master pages, themes, UpdatePanels and the AJAX Control Toolkit: day schedule, booking, check-in and check-out, patient registration and search, encounters (vitals, notes, ICD-9/ICD-10 diagnoses, CPT charges, lab orders), referrals, claims work queue, payments and remittances, statements, reports (Reporting Services with ReportViewer RDLC fallback), user, settings, interface and API client administration; forms authentication with SQL membership, roles and profiles; ELMAH |
| `src/Southridge.Clinic.PatientWeb` | `patients.corp.local` (IIS) | Patient site: Web Forms pages (sign-in, registration, account linking, booking wizard, locations) and an ASP.NET MVC 4 area `MyHealth` (appointments, statements, intake form) with Razor views and Ninject; calls the booking web service through a WSDL web reference |
| `src/Southridge.Clinic.WebServices` | `clinic-ws.corp.local` (IIS) | ASMX web services (`BookingService.asmx` for the patient site, `KioskService.asmx` for the check-in kiosks), SOAP header API keys |
| `src/Southridge.Clinic.Services` | `clinic-svc.corp.local` (IIS, WCF) | WCF services: scheduling (`basicHttpBinding`), records and billing (`wsHttpBinding` with message security and a service certificate), `ClinicApi.svc` (REST, `webHttpBinding`, JSON and XML) and the referral workflow host |
| `src/Southridge.Clinic.Workflow` | | Windows Workflow Foundation 3.5 referral approval workflow (state machine, delays and escalation), SQL persistence and tracking |
| `src/Southridge.Clinic.InterfaceEngine` | `clinic-int.corp.local` (Windows service `SouthridgeInterfaceEngine`) | HL7 v2.3 over MLLP: listeners for hospital ADT and lab ORU, outbound queue for ADT, SIU, ORM and DFT with retry and ACK handling |
| `src/Southridge.Clinic.BillingBatch` | `clinic-int.corp.local` (scheduled tasks) | `SouthridgeBilling.exe`: claims, 837P files, clearinghouse upload over SFTP (SSH.NET) or drop folders, 999/835 posting, statement PDFs (iTextSharp) and manifest, A/R aging workbook (NPOI) |
| `src/Southridge.Clinic.Reminders` | `clinic-int.corp.local` (scheduled tasks) | `SouthridgeReminders.exe`: appointment reminders (SMS gateway, SMTP), no-shows, waitlist offers |
| `src/Southridge.Clinic.Scheduling` | | Slot engine, availability, booking, patient registration, HL7 notifications |
| `src/Southridge.Clinic.Referrals` | | Referral domain on NHibernate 3 (mapping files, optimistic concurrency) |
| `src/Southridge.Clinic.Billing` | | Fee schedule, claim scrubber, 837P writer, 835 and 999 readers, statement policy |
| `src/Southridge.Clinic.Hl7` | | HL7 v2 parser and builders, MLLP framing (.NET 3.5) |
| `src/Southridge.Clinic.Documents` | | Appointment letters, statements, referral letters and visit summaries (iTextSharp), Excel exports (NPOI), report rendering (Reporting Services SOAP, ReportViewer local reports) |
| `src/Southridge.Clinic.Data` | | LINQ to SQL model, typed DataSets with table adapters for reports, Enterprise Library Data Access Application Block repositories over stored procedures |
| `src/Southridge.Clinic.Infrastructure` | | Settings, Enterprise Library logging and exception handling, log4net, Unity container, mail, API keys, job locks |
| `src/Southridge.Clinic.Domain` | | Appointment rules, clinic calendar, MRN, NPI, ICD and CPT codes, phone numbers, money (.NET 4.0) |
| `src/Southridge.Clinic.Contracts` | | WCF data and service contracts |
| `db` | `clinic-sql.corp.local` | SQL Server 2008 R2 scripts: databases, schema, stored procedures, reference data, demo data, logins, SQL Server Agent jobs, ELMAH |
| `reports` | `reports.corp.local` | Reporting Services 2008 R2 reports (daily schedule, provider productivity, claim summary, A/R aging) and shared data source |
| `tools/Southridge.Clinic.LabStubs` | `clinic-lab.corp.local` (lab only) | Stand-ins for the hospital and lab HL7 interfaces, the clearinghouse drop folders, the report server execution endpoint and the SMS gateway |
| `tests/Southridge.Clinic.Tests` | | MSTest: slot engine, clinic rules, billing rules, 837/835/999 |
| `tests/Southridge.Clinic.Hl7.Tests` | | xUnit: HL7 parsing, builders and readers, MLLP |
| `deploy` | | PowerShell deployment scripts |

Other systems:

| Name | Purpose |
| --- | --- |
| `clinic-sql.corp.local` | SQL Server 2008 R2 or later: `SouthridgeClinic` (clinic data, ASP.NET membership, ELMAH) and `SouthridgeWorkflow` (workflow persistence and tracking); SQL Server authentication |
| `reports.corp.local` | SQL Server Reporting Services 2008 R2, folder `/Southridge/Clinic`, basic authentication with a local account |
| `stc-hl7.stcatherine.example` | St. Catherine hospital interface engine: receives SIU and ADT on 7661, DFT on 7663; sends ADT to the clinic on 6661 |
| `mllp.cascadelab.example` | Cascade Reference Lab: receives ORM and ADT on 7662; sends ORU results to the clinic on 6662 |
| `sftp.cascadeclaims.example` | Clearinghouse SFTP: claim files to `/inbound`, 999 and 835 files from `/outbound` |
| `api.textlinkpro.example` | SMS gateway (HTTP form post) |
| `smtp.corp.local` | SMTP relay |

## Interfaces

| Direction | Partner | Messages |
| --- | --- | --- |
| Inbound MLLP 6661 | Hospital | `ADT^A01`, `A03`, `A04`, `A08` (A01/A04 may create a chart; MRN in PID-3 with authority `SMG`, hospital number with `STC`) |
| Inbound MLLP 6662 | Lab | `ORU^R01` results, matched to orders by placer order number (OBR-2) |
| Outbound MLLP | Hospital (`HOSP`) | `SIU^S12/S13/S15/S26` for imaging and echo bookings, `ADT^A04/A08` for patients with a hospital number |
| Outbound MLLP | Hospital billing (`HOSPBILL`) | `DFT^P03` for charges at the imaging centre |
| Outbound MLLP | Lab (`LAB`) | `ORM^O01` orders, `ADT^A04/A08` registrations |
| Files | Clearinghouse | 837P 005010X222A1 (`SMG837P_<control>.txt`), 999 005010X231A1, 835 005010X221A1 |
| HTTP | SMS gateway | `account`, `token`, `from`, `to`, `text`; reply `OK:<id>` or `ERR:<reason>` |

## Requirements

Build:

- Visual Studio 2013 (MSBuild 12) with the .NET Framework 3.5 SP1, 4.0 and 4.5.2 targeting packs, or the
  .NET SDK 8 (`dotnet build`), which uses the reference assembly packages
- NuGet package restore

Servers:

- Windows Server 2008 R2 SP1 or 2012 R2
- Web servers: IIS 7.5 or later with ASP.NET 4.5, .NET Framework 3.5 SP1 (workflow runtime on `clinic-svc`),
  WCF HTTP activation on `clinic-svc`
- `clinic-int`: .NET Framework 4.5.2 and 3.5 SP1, inbound TCP 6661 and 6662 from the hospital and lab,
  outbound TCP 7661 to 7663, SFTP to the clearinghouse
- SQL Server 2008 R2 or later with SQL Server Agent; Reporting Services 2008 R2 or later

## Build and test

```
dotnet build Southridge.Clinic.sln
dotnet test Southridge.Clinic.sln
```

or `msbuild Southridge.Clinic.sln /p:Configuration=Release`. Solution configurations are `Debug`,
`Release` and `Lab`; the web projects transform `Web.config` with `Web.<configuration>.config` when published.

## Run in development

1. Database (SQL Server Express named instance `.\SQLEXPRESS`, as in the development connection strings):

   ```
   cd db
   sqlcmd -S .\SQLEXPRESS -E -i 01-create-database.sql -v DataPath="DEFAULT" LogPath="DEFAULT"
   aspnet_regsql -S .\SQLEXPRESS -E -d SouthridgeClinic -A mrp -Q
   sqlcmd -S .\SQLEXPRESS -E -d SouthridgeClinic -i 02-schema.sql
   sqlcmd -S .\SQLEXPRESS -E -d SouthridgeClinic -i 03-procedures.sql
   sqlcmd -S .\SQLEXPRESS -E -d SouthridgeClinic -i 04-reference-data.sql
   sqlcmd -S .\SQLEXPRESS -E -d SouthridgeClinic -i 08-elmah.sql
   sqlcmd -S .\SQLEXPRESS -E -d SouthridgeClinic -i 05-demo-data.sql
   sqlcmd -S .\SQLEXPRESS -E -i 06-logins.sql -v WebPassword=dev-web ServicesPassword=dev-services InterfacePassword=dev-interface BatchPassword=dev-batch PortalPassword=dev-portal ReportsPassword=dev-reports
   ```

   The workflow persistence store is created in `SouthridgeWorkflow` with `SqlPersistenceService_Schema.sql`
   and `SqlPersistenceService_Logic.sql` from `%windir%\Microsoft.NET\Framework\v3.0\Windows Workflow Foundation\SQL\EN`.
   `07-agent-jobs.sql` (run in `msdb`) adds the SQL Server Agent jobs.

2. Lab stand-ins (hospital, lab, clearinghouse, report server, SMS):

   ```
   cd tools\Southridge.Clinic.LabStubs
   dotnet run
   ```

   The status page `http://localhost:8095/` lists the HL7, clearinghouse and SMS traffic and has a form
   that sends a hospital registration (ADT) to the interface engine. Lab orders are answered with
   results after a few seconds. The clearinghouse picks up claim files from
   `%TEMP%\southridge-clearinghouse\to-clearinghouse` and writes acknowledgments and remittances to
   `from-clearinghouse` (`Clearinghouse:Outbound` and `Clearinghouse:Inbound` in `appsettings.json`).

3. Web applications in IIS Express (or as IIS applications):

   ```
   iisexpress /path:%CD%\src\Southridge.Clinic.WebServices /port:8081
   iisexpress /path:%CD%\src\Southridge.Clinic.Services /port:8082
   iisexpress /path:%CD%\src\Southridge.Clinic.PatientWeb /port:8083
   iisexpress /path:%CD%\src\Southridge.Clinic.StaffWeb /port:8080
   ```

   Open `http://localhost:8080/Setup.aspx` once to create the first administrator, then add staff users in
   *Admin > Users*. Patients create their own account on the patient site and link it with their MRN,
   last name and date of birth (demo patients are in `05-demo-data.sql`).

4. Interface engine, billing batch and reminders from their output folders:

   ```
   SouthridgeInterfaceEngine.exe /console      (or /once to process the outbound queue once)
   SouthridgeBilling.exe claims send
   SouthridgeBilling.exe responses
   SouthridgeBilling.exe statements aging [/date:2013-03-31]
   SouthridgeReminders.exe reminders           (also noshows, waitlist)
   ```

   Log files go to `..\logs` relative to each program, e-mails to the pickup folder `..\mail\pickup`,
   billing output to `..\work` and `..\output`. Set `Billing:ExchangeOutbound` and `Billing:ExchangeInbound`
   to the clearinghouse stand-in folders.

Development API keys: the patient site uses `lab-portal` for the booking service; kiosks and other clients
are added in *Admin > API clients* (keys are stored as SHA-256 hashes).

## Deploy

The scripts in `deploy` read `environment.ps1` (production) or `lab.ps1` (single server `clinic-lab`
with the lab stand-ins). Passwords and keys come from a secrets file (`-SecretsFile`); copy
`secrets.example.ps1` outside the repository and fill it in. Run in an elevated PowerShell 3.0 or later session.

| Step | Script | Where |
| --- | --- | --- |
| DNS records | `Register-Dns.ps1` | DNS administrator workstation |
| Databases, `aspnet_regsql`, workflow store, logins, Agent jobs | `Install-Database.ps1` | any server with `sqlcmd` and .NET 3.5/4.x |
| Build and stage, `Web.config` transforms and tokens, `.exe.config` values | `Publish-Applications.ps1` | build server, output in `deploy\out\<environment>` |
| Application pools and sites | `Install-WebSites.ps1 [-Sites ...]` | web servers |
| Interface engine service (`installutil`), firewall rules, scheduled tasks | `Install-Services.ps1` | `clinic-int` |
| Report data source and reports | `Publish-Reports.ps1` | any workstation |

`Deploy-Clinic.ps1 -Environment Lab` runs the steps in order on the lab server. The WCF service certificate
(`clinic-svc.pfx`) is copied to the services `App_Data` folder from the path in the secrets file.

## Configuration

Application settings are in `appSettings` with the `Clinic:` prefix unless noted.

| Setting | Application | Meaning |
| --- | --- | --- |
| connection string `Clinic` | all | `SouthridgeClinic` database (SQL authentication, one login per application) |
| connection string `Workflow` | services | Workflow persistence and tracking database |
| `ReportMode`, `ReportServerUrl`, `ReportFolder`, `ReportServerUser`, `ReportServerPassword` | staff site, batch | `Server`, `Local` (RDLC) or `Auto` (server with local fallback) |
| `ServicesUrl`, `InternalServiceKey`, endpoint `ReferralWorkflow` | staff site, services | WCF services and the key the staff site sends in `X-Clinic-Key` |
| `ServiceCertificatePath`, `ServiceCertificatePassword` | services | Certificate for `wsHttpBinding` message security |
| `WorkflowEnabled`, `WorkflowLoadingInterval`, `ReferralEscalationMail` | services | Referral workflow runtime |
| `BookingServiceUrl`, `BookingApiKey`, `BookingTimeoutSeconds` | patient site | Booking web service |
| `OnlineMaxSlots`, `OnlineCancelMinHours`, `KioskBalanceLimit` | web services | Online booking and kiosk limits |
| `interfaceEngine` section (`listeners`, `senders`) | interface engine | MLLP ports, accepted message types, partner hosts, batch size, retries |
| `Hl7.HospitalAuthority`, `Hl7.*Application`, `Hl7.*Facility` | interface engine, staff site | HL7 identifiers |
| `Billing:*` (no `Clinic:` prefix) | billing batch | Folders, `Transport` (`Sftp` or `Folder`), SFTP host and credentials, submitter and receiver ids, timely filing |
| `Sms:GatewayUrl`, `Sms:Account`, `Sms:Token`, `Sms:SenderId` | reminders | SMS gateway |
| `MailFrom`, `MailFromName`, `system.net/mailSettings` | all | Sender and SMTP relay (pickup folder in development) |

Settings stored in the database (`dbo.AppSetting`, *Admin > Settings*): reminder lead time and SMS switch,
statement minimum and dunning, claim lag, interface switches, referral review days.
