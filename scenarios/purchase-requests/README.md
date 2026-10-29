# Purchase Requests

Purchasing workflow for Contoso Manufacturing. Employees raise purchase
requests, the cost center manager, finance and directors approve them
according to the purchasing policy, buyers turn approved requests into purchase
orders, and the orders are handed over to the ERP system.

| Component | Description | Host |
| --- | --- | --- |
| `Purchasing.Web` | ASP.NET MVC 5 portal used by all employees | `purchasing.<zone>` (IIS site `Purchasing`) |
| `Purchasing.Api` | ASP.NET Web API 2 service, owns the database and attachments | `purchasing-api.<zone>` (IIS site `PurchasingApi`) |
| `Purchasing.Jobs` | Console application run by Task Scheduler: approval reminders and ERP order export | `purchasing-app.<zone>` |
| `Purchasing.Core` | Approval policy, totals, document numbers and ERP file format | library |
| `Purchasing.Data` | SQL Server data access | library |

Other servers referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `sql01.<zone>` | SQL Server, database `Purchasing` |
| `files01.<zone>` | File share `Purchasing$` with folders `Attachments` and `ErpExport` |
| `erp01.<zone>` | ERP inbound share, folder `Inbound\PURCH01` |
| `smtp.<zone>` | Internal SMTP relay (port 25) |

The production configuration uses the zone `corp.local`. All names can point to
the same server.

## Requirements

- Windows Server 2019 or later with IIS, ASP.NET 4.8 and Windows Authentication
- .NET Framework 4.8
- SQL Server 2016 or later
- Visual Studio 2022 with the ASP.NET and web development workload, or Build
  Tools for Visual Studio 2022 with the web build tools
- Active Directory for production, or local accounts for a single-server
  installation

## Build and test

```
msbuild PurchaseRequests.sln /t:Restore;Build
dotnet test tests\Purchasing.Core.Tests
```

## Database

```
sqlcmd -S sql01.corp.local -E -i db\01-create-database.sql
sqlcmd -S sql01.corp.local -E -d Purchasing -i db\02-schema.sql
sqlcmd -S sql01.corp.local -E -d Purchasing -i db\03-seed.sql -v AccountDomain="CORP"
sqlcmd -S sql01.corp.local -E -i db\04-logins.sql -v ApiAccount="CORP\svc-purch-api" JobsAccount="CORP\svc-purch-jobs"
```

`AccountDomain` is the domain (or computer name) of the cost center managers.

## Deploy

1. Publish the Release build (applies `Web.Release.config`):

   ```
   .\deploy\Publish.ps1 -OutputPath C:\Deploy\Purchasing
   ```

2. Create the DNS records on the DNS server:

   ```
   .\deploy\Install-DnsRecords.ps1 -ZoneName corp.local -WebServerIP 192.168.8.136 -ApiServerIP 192.168.8.136 `
       -AppServerIP 192.168.8.136 -SqlServerIP 192.168.8.136
   ```

3. Install the IIS sites on the web server(s). Files go to
   `C:\inetpub\sites\<site>`, each site has its own application pool and a host
   header binding on port 80 with only Windows Authentication enabled.

   ```
   .\deploy\Install-IisSites.ps1 -PublishPath C:\Deploy\Purchasing -ZoneName corp.local `
       -WebPoolAccount CORP\svc-purch-web -ApiPoolAccount CORP\svc-purch-api
   ```

   Use `-Sites Purchasing.Api` or `-Sites Purchasing.Web` to install only one
   site on a server. Without pool accounts the sites run as
   `IIS AppPool\<pool>`.

4. Register the scheduled tasks on the application server:

   ```
   .\deploy\Install-ScheduledTasks.ps1 -PublishPath C:\Deploy\Purchasing -RunAsAccount CORP\svc-purch-jobs
   ```

   | Task | Schedule |
   | --- | --- |
   | `\Contoso\Purchasing\Approval reminders` | Weekdays 07:00 |
   | `\Contoso\Purchasing\ERP order export` | Every 30 minutes, 06:00 to 20:00 |

5. Give the service accounts access to the shares: the API account needs
   modify rights on `\\files01\Purchasing$\Attachments`, the jobs account on
   `\\erp01\Inbound\PURCH01` and `\\files01\Purchasing$\ErpExport`.

### Calls from the portal to the API

`Purchasing.ApiAuthentication` in the portal `Web.config` selects how the
portal calls the API:

- `Delegate` (default): calls run as the signed-in user. Register the SPNs
  printed by `Install-IisSites.ps1` and allow the portal pool account to
  delegate to `HTTP/purchasing-api.<zone>`.
- `ServiceAccount`: calls run as the portal pool account and pass the user name
  in the `X-Purchasing-User` header. Add the portal pool account to
  `Purchasing.TrustedCallers` in the API `Web.config`.

### Single-server test installation

`deploy\Install-DemoAccounts.ps1` creates local users (`jkoski`, `mlind`,
`ksalo`, `pvirta`, `ahalme`, `tniemi`) and the groups `PUR-Finance`,
`PUR-Directors`, `PUR-Buyers` and `PUR-Admins`. Then:

- run `db\03-seed.sql` with `AccountDomain` set to the computer name,
- set the `Purchasing.Group.*` values in the API `Web.config` to
  `<computer>\PUR-...`,
- use `ServiceAccount` mode with `Purchasing.TrustedCallers` set to
  `IIS AppPool\Purchasing`.

## Configuration

API (`Purchasing.Api\Web.config`):

| Setting | Description |
| --- | --- |
| `connectionStrings/Purchasing` | SQL Server connection (integrated security) |
| `Purchasing.Group.Finance`, `.Directors`, `.Buyers`, `.Admins` | Windows groups for the approval roles |
| `Purchasing.DirectoryDomain` | Domain used to look up names and e-mail addresses |
| `Purchasing.TrustedCallers` | Accounts allowed to call on behalf of other users |
| `Purchasing.AttachmentRoot` | Folder or share for attachments |
| `Purchasing.MaxAttachmentKb` | Maximum attachment size |

Portal (`Purchasing.Web\Web.config`): `Purchasing.ApiBaseUrl` and
`Purchasing.ApiAuthentication`.

Jobs (`Purchasing.Jobs.exe.config`): connection string, `PortalUrl`, mail
settings (`system.net/mailSettings`, `Mail.*`), reminder intervals, export and
archive folders and `LogFolder`. The install script keeps an existing config
file.

Logs are written to `D:\Logs\Purchasing` (`api.log`, `web.log`,
`jobs-yyyyMMdd.log`).

## Run

Open `http://purchasing.<zone>/` with a browser that sends Windows credentials
to the zone (Local intranet zone in Internet Options).

Run a job by hand on the application server:

```
C:\Apps\Purchasing.Jobs\Purchasing.Jobs.exe reminders
C:\Apps\Purchasing.Jobs\Purchasing.Jobs.exe export
```

Exit codes: 0 success, 1 unexpected error, 2 usage, 3 database error, 4 file
error, otherwise the number of reminder mails that could not be sent.

For development, run both sites in IIS Express from Visual Studio: the API on
port 51820 with LocalDB, the portal pointing to `http://localhost:51820/`.
