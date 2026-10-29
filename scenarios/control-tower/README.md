# Control Tower

Operations platform of Proseware Freight, a freight forwarder with road, sea and air services and terminals
in Vantaa, Turku, Gothenburg and Tallinn. Staff book shipments and plan the legs. Carriers report status by
EDIFACT, REST APIs and webhooks, and customs declarations are filed electronically. Trucks send positions and
reefer temperatures. Terminal staff scan packages with handhelds, and customers follow their shipments in a
portal. The operations desk watches everything live in the control tower.

## Components

| Component | Description | Host |
| --- | --- | --- |
| `ControlTower.Identity` | Token service (IdentityServer4) with staff and customer accounts, two-factor sign-in for staff, password reset | `login.<zone>`, IIS site `login` |
| `ControlTower.Booking.Web` | Staff application (MVC): bookings with quotes, legs and carrier bookings, milestones, customs declarations, documents, labels, customers, rate cards | `booking.<zone>`, IIS site `booking` |
| `ControlTower.Portal` | Customer portal (Razor Pages): shipments, live tracking, documents, booking requests, public tracking page | `track.<zone>`, IIS site `track` |
| `ControlTower.Tower.Api` + `ClientApp` | Control tower API and single-page application (Vue): overview, live map, shipments, alerts, KPIs, carriers, terminals | `tower.<zone>`, IIS site `tower` |
| `ControlTower.Scanning.Api` | Handheld scanner API: scans, package lookup, expected arrivals, label printing | `scan.<zone>`, IIS site `scan` |
| `ControlTower.CarrierGateway` | Windows service `ControlTowerCarrierGateway`: carrier bookings (IFTMIN over SFTP or REST), inbound IFTSTA and CONTRL, tracking polls, parcel webhooks (port 8085), customs declarations | `integ01.<zone>` |
| `ControlTower.Telematics` | Windows service `ControlTowerTelematics`: truck positions and reefer readings from the MQTT broker, geofences | `events01.<zone>` |
| `ControlTower.Events` | Windows service `ControlTowerEvents`: milestones, ETA, alert rules, live updates | `events01.<zone>` |
| `ControlTower.Jobs` | Windows service `ControlTowerJobs` (Hangfire, dashboard on port 5080): KPIs, customer reports, invoice export, document generation, clean-up, fuel surcharge import | `jobs01.<zone>` |
| `ControlTower.Domain`, `.Data` | Domain model, EF Core model, seed data | libraries |
| `ControlTower.Integration` | Message bus (EasyNetQ), EDIFACT, SFTP, carrier and customs clients | library |
| `ControlTower.Rules` | Alert rules (NRules) | library |
| `ControlTower.Routing` | Road routing (Itinero), ETA and driving time, multi-stop run optimisation | library |
| `ControlTower.Documents` | CMR, house bill of lading, air waybill, delivery note and dangerous goods note PDFs, ZPL labels, document store | library |
| `ControlTower.LabTools` | Seed data, schema script, simulators for carriers, customs, trucks, printers and handhelds | test environments |

Other servers referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `sql01.<zone>` | SQL Server 2019 or later with FILESTREAM enabled; databases `ControlTower` and `ControlTowerIdentity` |
| `mq01.<zone>` | RabbitMQ 3.9, virtual host `controltower` |
| `mqtt01.<zone>` | Mosquitto 2.0 MQTT broker, TLS on 8883 |
| `smtp.<zone>` | SMTP relay |
| `zt-vantaa-1`, `zt-vantaa-2`, `zt-turku-1`, `zt-gothenburg-1` | Zebra label printers (raw TCP 9100) |
| Carrier SFTP servers and APIs | Nordic Linehaul, Vänern Linjer, Oceanix, North Sea Feeder Line (EDIFACT); Baltic Express, Aerofreight, ParcelNord (REST) |
| Customs service | Electronic customs declarations (SOAP, client certificate) |

The production configuration uses the zone `corp.local`. All names can point to one server.

## Requirements

- Windows Server 2019 or later with IIS and the ASP.NET Core 6.0 Hosting Bundle
- .NET 6.0 and .NET 7.0 runtimes (Telematics and Jobs run on .NET 7.0)
- SQL Server 2019 or later, mixed mode authentication, FILESTREAM enabled for file I/O access
- Erlang/OTP 24 and RabbitMQ 3.9
- Mosquitto 2.0
- wkhtmltopdf 0.12.6 (64-bit) on the hosts that create documents (booking, jobs)
- Certificates: TLS certificates for the web sites and the MQTT broker, a token signing certificate for
  `login`, a client certificate for the customs service
- Optional: an OpenStreetMap router database for road routing (`build-routerdb`, see below)
- Build: .NET 8 SDK and Node.js 18 or later

## Build and test

```
dotnet build ControlTower.sln -c Release
dotnet test tests\ControlTower.Tests
cd src\ControlTower.Tower.Api\ClientApp
npm ci
npm run build
```

Sample messages and payloads are in `samples`.

## Deploy

1. DNS records (on the DNS server):

   ```
   deploy\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP <ip>
   ```

   or one address per name with `-LoginIP`, `-BookingIP`, `-TrackIP`, `-TowerIP`, `-ScanIP`, `-Integ01IP`,
   `-Events01IP`, `-Jobs01IP`, `-SqlIP`, `-MqIP`, `-MqttIP`, `-SmtpIP` and `-PrinterIPs @{ 'zt-vantaa-1' = '...' }`.

2. Publish (on the build machine). This also builds the single-page application:

   ```
   deploy\Publish.ps1 -OutputPath C:\Build\ControlTower
   ```

3. Database (as a sysadmin of the instance):

   ```
   $pw = @{ Booking = '..'; Portal = '..'; Tower = '..'; Scan = '..'; Gateway = '..'; Events = '..'; Telematics = '..'; Jobs = '..'; Identity = '..' }
   deploy\Initialize-Database.ps1 -SqlServer sql01.corp.local -DataPath E:\SQLData -LogPath F:\SQLLogs -Passwords $pw `
       [-Seed -LabToolsPath C:\Build\ControlTower\ControlTower.LabTools]
   ```

   `-Seed` loads master data (customers, carriers, terminals, vehicles, rate cards) and sample shipments.

4. RabbitMQ (on `mq01`):

   ```
   $mq = @{ 'ct-booking' = '..'; 'ct-portal' = '..'; 'ct-tower' = '..'; 'ct-scan' = '..'; 'ct-gateway' = '..'; 'ct-events' = '..'; 'ct-telematics' = '..'; 'ct-jobs' = '..' }
   deploy\Install-RabbitMq.ps1 -ErlangInstaller otp_win64_24.3.4.exe -RabbitMqInstaller rabbitmq-server-3.9.29.exe -AdminPassword $admin -ComponentPasswords $mq
   ```

5. MQTT broker (on `mqtt01`), one account per telematics unit:

   ```
   deploy\Install-Mosquitto.ps1 -Installer mosquitto-2.0.15-install-windows-x64.exe -CertificateFile mqtt01.crt -KeyFile mqtt01.key -CaFile corp-ca.crt `
       -ServicePassword $svc -Units @{ 'tu-1001' = '..'; 'tu-1002' = '..' }
   ```

6. Certificates (on each host that needs them), granting read access to the private key:

   ```
   deploy\Import-Certificates.ps1 -PfxFile login-signing.pfx -Password $pfx -GrantRead 'IIS AppPool\login'
   deploy\Import-Certificates.ps1 -PfxFile customs-client.pfx -Password $pfx -GrantRead '.\svc-ct-gateway'
   ```

7. wkhtmltopdf (on the booking and jobs servers):

   ```
   deploy\Install-Wkhtmltopdf.ps1 -Installer wkhtmltox-0.12.6-1.msvc2015-win64.exe
   ```

8. Web sites, each on its server. The token service goes first, followed by its database tables and the first administrator:

   ```
   deploy\Install-WebSite.ps1 -App login -PublishPath C:\Build\ControlTower -ZoneName corp.local -CertificateThumbprint <tls> `
       -SqlPassword $identitySql -Settings @{ 'IdentityServer:SigningCertificateThumbprint' = '<signing>' }
   C:\inetpub\sites\login\ControlTower.Identity.exe --initialize --admin-user admin --admin-password <pw> --admin-email ops@proseware-freight.example --admin-name "Operations Admin"

   deploy\Install-WebSite.ps1 -App booking -PublishPath C:\Build\ControlTower -CertificateThumbprint <tls> -SqlPassword $bookingSql -RabbitMqPassword $bookingMq
   deploy\Install-WebSite.ps1 -App track   -PublishPath C:\Build\ControlTower -CertificateThumbprint <tls> -SqlPassword $portalSql  -RabbitMqPassword $portalMq
   deploy\Install-WebSite.ps1 -App tower   -PublishPath C:\Build\ControlTower -CertificateThumbprint <tls> -SqlPassword $towerSql   -RabbitMqPassword $towerMq
   deploy\Install-WebSite.ps1 -App scan    -PublishPath C:\Build\ControlTower -CertificateThumbprint <tls> -SqlPassword $scanSql    -RabbitMqPassword $scanMq
   ```

   Client secrets of the web applications and services are set with `-Settings` (`Authentication:ClientSecret`)
   and must match the `IdentityServer:Clients` section of the token service.

9. Services, each on its server (`-CreateLocalAccount` creates the account as a local user):

   ```
   deploy\Install-Service.ps1 -Component Events -PublishPath C:\Build\ControlTower -ServiceAccount .\svc-ct-events -ServicePassword $p `
       -SqlPassword $eventsSql -RabbitMqPassword $eventsMq -CreateLocalAccount
   deploy\Install-Service.ps1 -Component Telematics -PublishPath C:\Build\ControlTower -ServiceAccount .\svc-ct-telematics -ServicePassword $p `
       -SqlPassword $telematicsSql -RabbitMqPassword $telematicsMq -Settings @{ 'Telematics:Password' = '..' }
   deploy\Install-Service.ps1 -Component CarrierGateway -PublishPath C:\Build\ControlTower -ServiceAccount .\svc-ct-gateway -ServicePassword $p `
       -SqlPassword $gatewaySql -RabbitMqPassword $gatewayMq -Settings @{ 'Customs:CertificateThumbprint' = '..' }
   deploy\Install-Service.ps1 -Component Jobs -PublishPath C:\Build\ControlTower -ServiceAccount .\svc-ct-jobs -ServicePassword $p `
       -SqlPassword $jobsSql -RabbitMqPassword $jobsMq -Settings @{ 'Jobs:Dashboard:Password' = '..' }
   ```

   Carrier SFTP accounts, API keys and the ParcelNord webhook secret are set in `appsettings.Production.json`
   of the carrier gateway (sections `Carriers:Sftp:<carrier>` and `Carriers:Api:<carrier>`).

10. Road routing (optional): build the router database from an OpenStreetMap extract and set
    `Routing:RouterDbPath` on the events server:

    ```
    ControlTower.LabTools.exe build-routerdb --input nordics-latest.osm.pbf --output D:\ControlTower\Routing\nordics-baltics.routerdb
    ```

    Without a router database, distances and ETAs are estimated from straight-line distances.

## Test environment without the external parties

| Party | How to stand in |
| --- | --- |
| EDIFACT carriers | `ControlTower.LabTools.exe edi-partner` answers IFTMIN files with IFTSTA status sequences, on a local folder or an SFTP account |
| REST carriers | `Carriers:Api:<carrier>:Mode = Simulator` on the carrier gateway |
| ParcelNord webhooks | `ControlTower.LabTools.exe parcel-webhook` sends a signed sample event |
| Customs | `ControlTower.LabTools.exe customs-stub` (listens on `http://+:8099/declaration/v2/`) or `Customs:Mode = Simulator` |
| Trucks | `ControlTower.LabTools.exe telematics-sim` publishes positions along the open road legs |
| Label printers | `ControlTower.LabTools.exe printer-sink` saves each print job as a `.zpl` file, or use printer kind `File` |
| Handhelds | `ControlTower.LabTools.exe scan` posts scans to the scanning API |
| SMTP | any SMTP sink |

Run `ControlTower.LabTools.exe` without arguments for all commands and options.

## Run

- Web sites: `https://login.corp.local/`, `https://booking.corp.local/`, `https://track.corp.local/`,
  `https://tower.corp.local/app/`, `https://scan.corp.local/swagger`
- Services: `Start-Service ControlTowerEvents, ControlTowerTelematics, ControlTowerCarrierGateway, ControlTowerJobs`
- Health: `/health` on every web site, `http://integ01.corp.local:8085/health`, `http://jobs01.corp.local:5080/health`
- Jobs dashboard: `http://jobs01.corp.local:5080/hangfire`
- Logs: `D:\ControlTower\Logs\<app>-<date>.log` (folder can be changed with the environment variable `CONTROLTOWER_LOG_DIR`)

### Development mode

Every application also runs with `dotnet run` in the `Development` environment. It uses a local SQLite database in
`.devdata` (created and seeded on first start), the in-process message bus, simulators for carriers and customs,
and development sign-in. The token service creates development users (admin, ops.anna, booking.mikko,
nwf.portal, wwi.portal) with the password `Proseware#2024`. Mail is written to `.devdata/mail`.
