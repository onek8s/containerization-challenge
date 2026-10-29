# Field Sales

Order taking for the Wingtip Foods sales reps. Reps visit grocery stores, kiosks and
cafés with a laptop, write orders at the customer's premises with or without a network
connection, and the orders are sent to the office when the laptop is online again.
Orders with a manual discount above the rep's limit wait for a sales manager's approval.
The ERP fetches confirmed orders and keeps the customer register up to date.

| Component | Description | Host |
| --- | --- | --- |
| `FieldSales.Client` | WPF desktop application for the sales reps, installed and updated with ClickOnce | rep laptops, installed from `salesapp.<zone>` |
| `FieldSales.Api` | ASP.NET Web API 2 on OWIN: token endpoint, catalog and customer sync, order submit, ERP interface, stock lookup | `salesapi.<zone>` (IIS site `FieldSalesApi`) |
| `FieldSales.Backoffice` | ASP.NET MVC 5 sales office site: approvals, orders, customers, price list import, promotions, users | `salesadmin.<zone>` (IIS site `FieldSalesOffice`) |
| ClickOnce deployment site | Static IIS site with the published client (`publish.htm`, `FieldSales.application`) | `salesapp.<zone>` (IIS site `FieldSalesApp`) |
| `FieldSales.Core` | Pricing, discount policy, delivery calendar, order validation, price list CSV reader (shared by client and server) | library |
| `FieldSales.Contracts` | API request and response types | library |
| `FieldSales.Data` | Entity Framework 6 context, ASP.NET Identity 2 user store, stored procedure calls, confirmation mail | library |

Other systems referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `sql01.<zone>` | SQL Server, database `FieldSales`, SQL Server authentication |
| `smtp.<zone>` | SMTP relay for order confirmations and error mails (port 25) |
| `wms.<zone>` | Warehouse management system REST interface for stock levels (`/wms/api/v2/stock`) |
| ERP | Calls `https://salesapi.<zone>/api/erp/...` with an API key |

The production configuration uses the zone `corp.local`. All names can point to the same server.

## Requirements

- Windows Server 2016 or later with IIS 10, ASP.NET 4.7 and the IIS Management Scripts and Tools
- .NET Framework 4.7.2 on the servers and on the laptops (Windows 10 or later)
- SQL Server 2016 SP1 or later with SQL Server authentication enabled, and the `sqlcmd` utility
- Visual Studio 2017 15.9 or later (or Build Tools) with the .NET desktop and ASP.NET workloads
- .NET 8 SDK for the unit tests
- A TLS certificate for the three host names (a self-signed one is created for the lab if none is given)
- A code signing certificate for the ClickOnce manifests (optional; `New-SigningCertificate.ps1` creates one for the lab)

## Build and test

```
msbuild FieldSales.sln /t:Restore;Build /p:Configuration=Release
dotnet test tests\FieldSales.Core.Tests
```

## Database

```
.\deploy\Install-Database.ps1 -SqlServer sql01.corp.local -AppLogin fieldsales_app
```

The script runs `db\01-create-database.sql` to `db\05-login.sql`. `04-seed.sql` adds demo
products, customers, promotions and the users below; use `-SkipDemoData` for an empty
database. The scripts can also be run one by one with `sqlcmd`.

| User | Role | Rep code | Discount limit |
| --- | --- | --- | --- |
| `admin` | Administrator | | |
| `mgr.virtanen` | Sales manager, administrator | | 15 % |
| `mgr.holm` | Sales manager | | 15 % |
| `rep.jones` | Sales rep | RJ | 5 % |
| `rep.nieminen` | Sales rep | AN | 5 % |
| `rep.lindqvist` | Sales rep | EL | 7 % |
| `rep.korhonen` | Sales rep, sales manager | PK | 5 % |

All demo users have the password `FieldSales2017`.

## Deploy

1. Create the DNS records on the DNS server:

   ```
   .\deploy\Install-DnsRecords.ps1 -ZoneName corp.local -WebServerIP 192.168.8.136 -SqlServerIP 192.168.8.136 `
       -SmtpServerIP 192.168.8.136
   ```

   `-ApiServerIP` and `-DeploymentServerIP` default to the web server address.

2. Create the code signing certificate (once) and publish on the build machine. The client
   version must be higher than the previous publish.

   ```
   .\deploy\New-SigningCertificate.ps1 -OutputPath C:\Deploy\FieldSales
   .\deploy\Publish.ps1 -OutputPath C:\Deploy\FieldSales -ZoneName corp.local -ClientVersion 3.2.0.1 `
       -SigningThumbprint <thumbprint>
   ```

3. Install the sites on the web server(s). Files go to `C:\inetpub\sites\<site>`; each site
   has its own application pool and an HTTPS binding for its host name.

   ```
   .\deploy\Install-IisSites.ps1 -PublishPath C:\Deploy\FieldSales -ZoneName corp.local `
       -SqlServer sql01.corp.local -SqlLogin fieldsales_app
   ```

   | Parameter | Meaning |
   | --- | --- |
   | `-Sites` | `Api`, `Office`, `Deployment` (default all three) |
   | `-CertificateThumbprint` | TLS certificate in `LocalMachine\My`; without it a self-signed certificate is created and exported to `fieldsales-web.cer` |
   | `-MachineKeyPath` | Machine key file shared by all API and sales office servers (created on the first server) |
   | `-ErpApiKey` | Key for the ERP interface; generated and printed when not given |
   | `-WarehousePassword` | Password of the `fieldsales` user in the warehouse system |
   | `-MinimumClientVersion` | Oldest client version the API accepts |
   | `-SmtpPickupFolder` | Write mails to a folder instead of sending them (lab without an SMTP relay) |
   | `-LogFolder` | log4net files, default `C:\Logs\FieldSales` |

4. On each laptop (or with Group Policy), trust the certificates and add the deployment site
   to the Local intranet zone, then open `https://salesapp.corp.local/publish.htm`:

   ```
   .\deploy\Install-DemoLaptop.ps1 -ZoneName corp.local -WebCertificate C:\Deploy\FieldSales\fieldsales-web.cer `
       -SigningCertificate C:\Deploy\FieldSales\fieldsales-codesigning.cer
   ```

## Run

- Sales office: `https://salesadmin.corp.local/`, sign in as `mgr.virtanen`.
- Client: start **Field Sales** from the Start menu after the ClickOnce install and sign in
  as a sales rep. The first sign-in needs a connection to the API; after that the client
  starts offline with the cached customers and price list and synchronizes every 10 minutes
  or with **Synchronize**.
- API status: `https://salesapi.corp.local/api/status`.

For development, run the API and the sales office site from Visual Studio (IIS Express) and
start the client with `ApiBaseUrl` in `App.config` pointing to the API.

### Configuration

API (`Web.config`, `appSettings`):

| Key | Default | Meaning |
| --- | --- | --- |
| `FieldSales:MinimumClientVersion` | `3.2.0.0` | Older clients get HTTP 426 and are asked to update |
| `FieldSales:OrderCutOff` | `14:00` | Orders received after this time are delivered one working day later |
| `FieldSales:AccessTokenHours` | `8` | Access token lifetime |
| `FieldSales:RefreshTokenDays` | `14` | How long a laptop stays signed in without contact |
| `FieldSales:ErpApiKey` | | Key the ERP sends in the `X-Api-Key` header |
| `FieldSales:WarehouseApiUrl` | `http://wms.corp.local/wms/api/v2/` | Warehouse stock interface |
| `FieldSales:StockCacheMinutes` | `5` | How long stock answers are cached |

Sales office site: `FieldSales:PriceListCulture` (list and decimal separator of the price
list CSV, default `fi-FI`), `FieldSales:ClientInstallUrl`, `FieldSales:MailFrom`,
`FieldSales:MailOfficeCopy`. Both sites read the SMTP relay from `system.net/mailSettings`
and log with log4net (`log4net` section).

Client (`App.config`): `ApiBaseUrl` (set by `Publish.ps1`), `SyncIntervalMinutes`,
`RequestTimeoutSeconds`. Local data is kept in `%LOCALAPPDATA%\Wingtip\FieldSales`.

### ERP interface

All calls need the header `X-Api-Key`.

| Call | Purpose |
| --- | --- |
| `GET /api/erp/orders?max=200` | Confirmed orders not yet exported |
| `POST /api/erp/orders/ack` | `[{ "orderNo": "SO100001", "erpOrderNo": "4500123" }]` marks orders exported |
| `PUT /api/erp/customers` | Customer register update, see `samples\erp-customers.json` |

`samples\pricelist-update.csv` is an example of the price list file imported in the sales
office site.

### SQL Server Agent

Schedule `EXEC dbo.usp_PurgeRefreshTokens` daily on the `FieldSales` database.
