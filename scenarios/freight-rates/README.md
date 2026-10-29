# Freight Rates

LTL freight quoting for Contoso Freight Brokerage. Customers and the broker's
TMS request rates through a REST API; pricing staff maintain customer discounts,
the weekly fuel surcharge and the monthly tariff releases in the admin portal.
Line haul rates come from the Meridian RateEngine 7.2 tariff engine.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/FreightRates.Api` | `rates.corp.local` (IIS) | REST API for quotes (`/api/v1`), OpenAPI at `/swagger`, API key per customer |
| `src/FreightRates.Admin` | `rates-admin.corp.local` (IIS) | Razor Pages admin portal, Windows sign-in |
| `src/FreightRates.RatingHost` | `ratehost01.corp.local` | Windows service hosting the gRPC rating service on top of RateEngine |
| `src/FreightRates.Contracts` | | gRPC contract (`Protos/rating.proto`) and rating client |
| `src/FreightRates.Data` | | EF Core data model, quote and customer services |
| `src/FreightRates.Core` | | Shipment validation, freight class, fuel surcharge and quote rules |
| `tests/FreightRates.Tests` | | Unit and API tests |
| `vendor/Meridian/RateEngine-7.2` | | RateEngine SDK header and export list |
| `deploy` | | DNS, IIS, service install and SQL scripts |

The database `FreightRates` is on SQL Server `sql01.corp.local`.

### Quote flow

1. The client posts a shipment to `POST /api/v1/quotes` with its `X-Api-Key`.
2. The API validates the shipment and accessorial codes, then calls `Rating.Rate` on the
   rating host over gRPC (HTTP/2, TLS, port 5101).
3. The rating host rates the shipment with RateEngine against the installed tariff and returns
   gross charge, tariff minimum, deficit weight and transit days.
4. The API applies the customer discount and minimum, the fuel surcharge for the ship week and
   the accessorial charges, stores the quote and returns it with its quote number.

### Tariff releases

Meridian publishes a tariff release every month. Pricing uploads the release files on the
admin portal **Tariffs** page. The portal writes them to `\\ratehost01.corp.local\MRSData$\Incoming`
followed by a `release.ok` marker; the rating host copies them into its tariff folder, archives the
previous files and reloads the engine.

## Requirements

Build:

- .NET 8 SDK

Servers:

- Windows Server 2019 or later, Active Directory domain `CORP` with DNS
- `rates`, `rates-admin`: IIS 10 with Windows Authentication, ASP.NET Core 8 Hosting Bundle
- `ratehost01`:
  - .NET 8 runtime and ASP.NET Core 8 runtime, **x86**
  - Meridian RateEngine 7.2 SDK (`MRS_RateEngine_7.2.1184_x86.msi`), installed to
    `C:\Program Files (x86)\Meridian\RateEngine 7.2`
  - Meridian licence key attached to the server, licence file `*.mrl` and licence password from Meridian
  - Monthly tariff data in `D:\MRS\Tariffs`
  - Server certificate `CN=ratehost01.corp.local` in `LocalMachine\My`
- `sql01`: SQL Server 2019 or later
- Service accounts: `CORP\svc-freightrates` (IIS application pools, database, tariff share) and
  `CORP\svc-ratehost` (rating host service)
- AD group `CORP\Freight Rates Admins` for admin portal users

## Build and test

```
dotnet build FreightRates.sln
dotnet test FreightRates.sln
```

## Run locally

In the `Development` environment the API and the admin portal use a SQLite database in
`App_Data/` with sample customers, and the rating host uses the `Simulator` provider, which
returns fixed test rates (tariff `SIMULATOR`) without the RateEngine SDK. Simulator rates are
not tariff rates.

```
dotnet run --project src/FreightRates.RatingHost      # gRPC on http://localhost:5101, health on http://localhost:5100/health
dotnet run --project src/FreightRates.Api             # http://localhost:5080/swagger
dotnet run --project src/FreightRates.Admin           # http://localhost:5090
```

Sample API key for customer `ACME01`: `fr_dev_acme01_0b7c4a2e`.

```
curl -X POST http://localhost:5080/api/v1/quotes \
  -H "X-Api-Key: fr_dev_acme01_0b7c4a2e" -H "Content-Type: application/json" \
  -d '{"originZip":"60601","destinationZip":"30301","shipDate":"2026-11-02",
       "lines":[{"freightClass":70,"weightLbs":1200},{"weightLbs":300,"lengthIn":48,"widthIn":40,"heightIn":40}],
       "accessorials":["LGDL"],"customerReference":"PO-1"}'
```

## Deploy

1. DNS (on the DNS server; all names may point to one server in a lab):

   ```
   .\deploy\register-dns.ps1 -ZoneName corp.local -IPAddress <web server IP> -RatingHostIPAddress <rating server IP> -SqlIPAddress <SQL server IP>
   ```

2. Database: run `deploy\sql\create-logins.sql` on `sql01`.

3. Rating host (on `ratehost01`, as administrator):

   ```
   dotnet publish src\FreightRates.RatingHost -c Release -r win-x86 --self-contained false -o publish\ratinghost
   .\deploy\install-ratehost.ps1 -PublishPath publish\ratinghost -ServiceAccount CORP\svc-ratehost -AdminPortalAccount CORP\svc-freightrates -LicenceFile <path to .mrl>
   ```

   The script checks the SDK and licence key, creates `D:\MRS` and the `MRSData$` share,
   installs the `FreightRates.RatingHost` service, stores the licence password protected for
   the server (`FreightRates.RatingHost.exe protect-password`) in the service environment and
   opens ports 5101 and 5100.

4. Web (on the IIS server, as administrator):

   ```
   dotnet publish src\FreightRates.Api -c Release -o publish\api
   dotnet publish src\FreightRates.Admin -c Release -o publish\admin
   .\deploy\install-web.ps1 -PublishRoot publish -DomainName corp.local -AppPoolAccount CORP\svc-freightrates
   setspn -S HTTP/rates-admin.corp.local CORP\svc-freightrates
   ```

5. Issue API keys for customers on the admin portal (**Customers**, *Issue new API key*).

## Configuration

API and admin portal (`appsettings.json`):

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:FreightRates` | Database connection |
| `Database:Provider` | `SqlServer` or `Sqlite` |
| `Database:SeedSampleData` | Create sample customers and fuel prices on an empty database |
| `RatingHost:Address` | Rating host gRPC address |
| `RatingHost:TimeoutSeconds` | Rating call deadline |
| `Admin:Authentication` | `Negotiate` or `Development` (admin portal) |
| `Admin:AdminGroup` | Group allowed to use the admin portal |
| `Admin:TariffIncomingShare` | Rating host incoming tariff folder |

Rating host (`appsettings.json`, `appsettings.Production.json`, service environment):

| Setting | Purpose |
| --- | --- |
| `RatingEngine:Provider` | `Meridian` or `Simulator` |
| `RatingEngine:SdkPath` | RateEngine SDK install folder |
| `RatingEngine:DataPath` | Tariff data folder |
| `RatingEngine:LicenceFile` | Licence file |
| `RatingEngine:EncryptedPassword` | Licence password from `protect-password` on this server |
| `RatingEngine:DefaultTariff` | Tariff used for rating |
| `RatingEngine:DeficitRating` | Rate at the next weight break when cheaper |
| `RatingEngine:IncomingPath`, `ArchivePath` | Tariff release folders |
| `Kestrel:Endpoints` | gRPC (HTTPS, HTTP/2) and health endpoints |

Health endpoints: API `/health` (database and rating host), rating host `http://<host>:5100/health`
(tariff, engine version, licence key serial and days left).
