# Field Service

Work order system for the facilities maintenance team.

- **FieldService.Web** is the dispatcher and technician portal. Dispatchers
  create and assign work orders, technicians start and complete them, record
  labor and parts, and attach site photos.
- **FieldService.Worker** runs in the background. It imports job request files
  that key accounts drop into the inbox share, and once a night it writes the
  invoice line file for the finance system and the SLA breach report.
- **FieldService.Data** holds the shared data model and business rules.

## Requirements

- .NET 8 SDK
- SQL Server (production), or nothing extra for local development (SQLite)

## Build and test

```
dotnet build
dotnet test
```

## Run locally

Both applications use the `Development` environment by default when started
with `dotnet run`, which uses a SQLite database and local folders under
`App_Data/` in this directory.

```
cd src/FieldService.Web
dotnet run
```

The portal listens on http://localhost:5090. Sample customers, technicians and
work orders are created on first start. Users:

| User | Password | Role |
| --- | --- | --- |
| admin | ChangeMe!1 | Admin |
| dispatch | dispatch | Dispatcher |
| maria | maria | Technician |
| aisha | aisha | Technician |

In a second terminal:

```
cd src/FieldService.Worker
dotnet run
```

To run the nightly jobs immediately and exit:

```
dotnet run -- --run-nightly
```

## Production installation

- Web: publish `FieldService.Web` and host it in IIS with the ASP.NET Core
  Hosting Bundle (`web.config` is included). The application pool identity
  needs access to the database and to the photo share.
- Worker: publish `FieldService.Worker` and register it as a Windows service:

  ```
  sc.exe create "FieldService Worker" binPath= "D:\FieldService\Worker\FieldService.Worker.exe" start= delayed-auto obj= "CORP\svc-fieldservice" password= ********
  ```

  The service account needs access to the database and to the inbox, finance
  and report shares. `sc.exe` does not grant the *Log on as a service* right,
  so assign it to the account (Local Security Policy or group policy) before
  starting the service. Register the event log source once from an elevated
  prompt, because the service account cannot create it:

  ```
  New-EventLog -LogName Application -Source "FieldService Worker"
  ```

- Set *Load User Profile* to `True` on the portal's application pool so the
  sign-in cookie keys survive application pool recycles.

## Configuration

Both applications read `appsettings.json` and `appsettings.{Environment}.json`.

Shared:

| Setting | Purpose |
| --- | --- |
| `Database:Provider` | `SqlServer` or `Sqlite` |
| `ConnectionStrings:FieldService` | Database connection string |

FieldService.Web:

| Setting | Purpose |
| --- | --- |
| `Portal:CompanyName` | Shown in the page footer |
| `Portal:PhotoRoot` | Folder where uploaded photos are stored, one subfolder per work order |
| `Portal:MaxPhotoBytes` | Upload size limit |
| `Portal:SeedSampleData` | Create sample data on an empty database |

FieldService.Worker:

| Setting | Purpose |
| --- | --- |
| `Worker:InboxFolder` | Folder polled for `*.csv` job request files |
| `Worker:ArchiveFolder` | Imported files are moved here |
| `Worker:ErrorFolder` | Files with no valid rows are moved here, with an `.errors.txt` file |
| `Worker:OutboxFolder` | Invoice line files for the finance system |
| `Worker:ReportFolder` | Daily SLA breach reports |
| `Worker:StateFolder` | Lock files and the last nightly run date |
| `Worker:InboxPollSeconds` | Inbox polling interval |
| `Worker:NightlyRunAt` | Local time of the nightly run |
| `Worker:TimeZone` | Time zone used for the nightly run |

## Job request file format

Semicolon separated, UTF-8, with a header row:

```
Account;Reference;Site;Summary;Description;Priority
ACME;PO-1001;12 Market Street, Unit 4;Leaking tap;Kitchen sink, 2nd floor;P3
```

Priority is `P1` (emergency) to `P4` (low), or the name of the priority.
Empty means normal.
