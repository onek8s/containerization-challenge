# Furnace Records

Heat treatment records for Adatum Thermal Processing. MES plans furnace loads with their work
orders and parts; the acquisition service records the furnace thermocouples and controller events
while the load runs and evaluates the load against its recipe (soak time and tolerance, heating
rate, quench delay). Quality engineers review the curve on the portal and release or reject the
load; released loads get a heat treatment certificate. Temperature uniformity surveys are planned
and recorded the same way and renew the furnace survey due date.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/FurnaceRecords.Portal` | `furnace.corp.local` (IIS site `furnace`) | Razor Pages portal: live furnaces, loads and curves, release decisions, certificates, recipes, sensors and calibrations. Windows sign-in. |
| `src/FurnaceRecords.Api` | `furnace-api.corp.local` (IIS site `furnace-api`) | REST API for MES (`/api/v1`), OpenAPI at `/swagger`, API key per client |
| `src/FurnaceRecords.Daq` | `daq01.corp.local` | Acquisition Windows service: one acquisition thread per furnace, live readings at `http://daq01.corp.local:5200/live` |
| `src/FurnaceRecords.Worker` | `furnace-app01.corp.local` | Windows service: load events to MES, PDF certificate archive, daily calibration reminder |
| `src/FurnaceRecords.Acquisition` | | TCX thermocouple boards (Calvera TCX SDK), furnace controller through the HMI OPC server, simulator |
| `src/FurnaceRecords.Data` | | EF Core data model and load services |
| `src/FurnaceRecords.Core` | | Thermocouple linearisation, sensor corrections, furnace classes, load and survey evaluation, CSV and PDF output |
| `tests/FurnaceRecords.Tests` | | Unit, data, API and worker tests |
| `vendor/Calvera/TcxSdk-4.3` | | TCX SDK type library description |
| `deploy` | | DNS, IIS, service install, share and SQL scripts |

Other hosts:

| Host | Use |
| --- | --- |
| `furnace-hmi01.corp.local` | Furnace HMI with the Vantor FurnaceLink OPC server (`Vantor.FurnaceLink.DA.2`) publishing the PLC tags of F01 and F02 |
| `sql01.corp.local` | SQL Server, database `FurnaceRecords` |
| `files01.corp.local` | Share `FurnaceRecords$` for archived certificates and survey reports |
| `mes.corp.local` | MES, receives load events at `https://mes.corp.local/api/heat-treat/events/{topic}` |
| `smtp.corp.local` | Mail relay |

### Load flow

1. MES plans a load: `POST /api/v1/loads` with furnace, recipe and parts. The API checks that the furnace
   class is qualified for the recipe tolerance and that the uniformity survey and system accuracy test
   are not overdue, and returns the load number (`F01-26-00412`).
2. The operator starts the recipe on the furnace HMI. When the controller reports `Cycle.Active`, the
   acquisition service starts recording the oldest planned load of that furnace.
3. The boards are scanned 10 times a second. Each reading is cold-junction compensated, linearised
   (type K or N) and corrected with the sensor calibration, and stored as one value per channel every
   10 seconds. Door, quench, alarm and setpoint changes are stored as events.
4. When the cycle ends the load is evaluated: soak starts when the coldest load sensor reaches the lower
   limit and ends when the door opens; all load sensors must stay within tolerance, the soak must be long
   enough, heating must not be too fast and the quench must follow the door opening in time.
5. A quality engineer releases or rejects the load on the portal. A load with findings can only be
   released with a comment (for example the MRB decision).
6. The worker sends `load.completed`, `load.released` and `load.rejected` events to MES and writes the
   PDF certificate to `\\files01.corp.local\FurnaceRecords$\Certificates\<year>`.

### Furnace controller tags

Per furnace, with the tag prefix of the furnace (`F01.`, `F02.`):

| Tag | Type | Use |
| --- | --- | --- |
| `Cycle.Active` | bool | Recipe running |
| `Cycle.Recipe` | string | Recipe code selected on the HMI |
| `Zone1.SP`, `Zone1.PV` | float | Setpoint and control temperature, °C |
| `Door.Open` | bool | Door open |
| `Quench.Down` | bool | Load in the quench tank |
| `Alarm.Word` | int | Alarm bits |
| `OTC.Tripped` | bool | Over-temperature controller tripped |

Tag names can be changed per furnace under `Acquisition:Furnaces:<n>:Tags`.

## Requirements

Build:

- .NET 8 SDK

Servers:

- Windows Server 2019 or later, Active Directory domain `CORP` with DNS
- `furnace`, `furnace-api`: IIS 10 with Windows Authentication, ASP.NET Core 8 Hosting Bundle
- `daq01`:
  - Calvera TCX-3216 or TCX-3232 thermocouple boards, one per furnace, thermocouples wired to the board terminal blocks
  - Calvera TCX SDK 4.3 (`TCX_SDK_4.3.msi`, installs the board driver and `TcxBoard.dll`)
  - .NET 8 runtime and ASP.NET Core 8 runtime (x64)
  - DCOM access to the OPC server on `furnace-hmi01` for the service account (remote launch, activation and access)
- `furnace-app01`: .NET 8 runtime
- `sql01`: SQL Server 2019 or later
- Service accounts: `CORP\svc-furnace-web` (application pools), group managed service accounts
  `CORP\svc-furnace-daq$` (acquisition) and `CORP\svc-furnace-wrk$` (worker)
- AD groups `CORP\FR-Operators`, `CORP\FR-Quality` (release decisions) and `CORP\FR-Admins` (recipes, furnaces, sensors)

## Build and test

```
dotnet build FurnaceRecords.sln
dotnet test FurnaceRecords.sln
```

## Run locally

In the `Development` environment all applications use the SQLite database
`src/FurnaceRecords.Portal/App_Data/furnacerecords.db` with sample furnaces, recipes and the MES client
`MES01`, and the acquisition service uses the `Simulator` provider: simulated furnaces that run the recipe
of the next planned load at 60 times real time. The portal signs everybody in as `CORP\quality1` with all
groups (`?as=CORP\op1&groups=CORP\FR-Operators` signs in as another user).

```
dotnet run --project src/FurnaceRecords.Portal      # http://localhost:5220
dotnet run --project src/FurnaceRecords.Daq         # http://localhost:5200/live
dotnet run --project src/FurnaceRecords.Api         # http://localhost:5210/swagger
dotnet run --project src/FurnaceRecords.Worker
```

Start the portal first; it creates the database. Sample API key: `fr_dev_mes01_7d3e91a4`.

```
curl -X POST http://localhost:5210/api/v1/loads \
  -H "X-Api-Key: fr_dev_mes01_7d3e91a4" -H "Content-Type: application/json" \
  -d '{"furnace":"F01","recipe":"ST-7075","parts":[{"workOrder":"WO-118842","partNumber":"7Q4410-203","lot":"H2611","quantity":24}]}'

curl -X POST http://localhost:5210/api/v1/loads \
  -H "X-Api-Key: fr_dev_mes01_7d3e91a4" -H "Content-Type: application/json" \
  -d '{"furnace":"F01","recipe":"TUS-480","kind":"Survey"}'
```

A simulated load takes about two minutes. In Development the worker posts MES events to
`http://localhost:5299/`, writes certificates to `App_Data/Certificates` and mails to `App_Data/Mail`.

## Deploy

1. DNS (on the DNS server; all names may point to one server in a lab):

   ```
   .\deploy\register-dns.ps1 -ZoneName corp.local -IPAddress <web server IP> -DaqIPAddress <acquisition server IP> -HmiIPAddress <HMI IP> -SqlIPAddress <SQL server IP> -FilesIPAddress <file server IP>
   ```

2. Database (on `sql01`):

   ```
   sqlcmd -S sql01.corp.local -E -i deploy\sql\01-create-database.sql
   sqlcmd -S sql01.corp.local -E -i deploy\sql\02-logins.sql
   sqlcmd -S sql01.corp.local -E -i deploy\sql\03-reference-data.sql
   ```

3. Certificate share (on `files01`):

   ```
   .\deploy\create-certificate-share.ps1 -WorkerAccount 'CORP\svc-furnace-wrk$'
   ```

4. Web (on the IIS server):

   ```
   dotnet publish src\FurnaceRecords.Portal -c Release -o publish\portal
   dotnet publish src\FurnaceRecords.Api -c Release -o publish\api
   .\deploy\install-web.ps1 -PublishRoot publish -AppPoolAccount CORP\svc-furnace-web
   setspn -S HTTP/furnace.corp.local CORP\svc-furnace-web
   ```

   Issue the MES API key as printed by the script (`FurnaceRecords.Api.exe issue-key MES01`).

5. Acquisition (on `daq01`):

   ```
   dotnet publish src\FurnaceRecords.Daq -c Release -r win-x64 --self-contained false -o publish\daq
   .\deploy\install-daq.ps1 -PublishPath publish\daq -ServiceAccount 'CORP\svc-furnace-daq$'
   ```

   The script checks the TCX SDK, the board driver and the boards, installs the `FurnaceRecords.Daq`
   service and opens port 5200. `-Simulator` installs the service with simulated furnaces (no boards
   or OPC server needed).

6. Worker (on `furnace-app01`):

   ```
   dotnet publish src\FurnaceRecords.Worker -c Release -r win-x64 --self-contained false -o publish\worker
   .\deploy\install-worker.ps1 -PublishPath publish\worker -ServiceAccount 'CORP\svc-furnace-wrk$'
   ```

## Configuration

All applications: `ConnectionStrings:FurnaceRecords`, `Database:Provider` (`SqlServer` or `Sqlite`),
`Database:SeedSampleData`.

Portal (`Portal`): `Authentication` (`Negotiate` or `Development`), `OperatorsGroup`, `QualityGroup`,
`AdminsGroup`, `DaqAddress`, `TimeZone`.

API: `Plant:TimeZone` (CSV export times).

Acquisition (`Acquisition`):

| Setting | Purpose |
| --- | --- |
| `Provider` | `Hardware` or `Simulator` |
| `SampleRateHz` | Board scan rate |
| `RecordIntervalSeconds` | Stored value interval |
| `ControllerPollMilliseconds` | OPC read interval |
| `Furnaces:<n>:Code` | Furnace code as in the database |
| `Furnaces:<n>:BoardIndex` | TCX board of the furnace, 0 for the first board |
| `Furnaces:<n>:OpcHost`, `OpcProgId`, `OpcClsid` | OPC server of the furnace HMI |
| `Furnaces:<n>:TagPrefix`, `Tags` | Controller tag names |
| `Simulator:TimeScale`, `AutoStartPlannedLoads` | Simulator |

Channels, thermocouple types and sensor corrections are maintained on the portal (**Furnaces**) and
read when the acquisition service starts.

Worker (`Worker`): `MesAddress`, `CertificateFolder`, `SmtpHost`, `SmtpPort`, `MailPickupFolder`,
`MailFrom`, `ReminderRecipients`, `ReminderTime`, `ReminderDays`, `TimeZone`.
