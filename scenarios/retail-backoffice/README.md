# Retail Back Office

Back office of VanArsdel Fashion, a clothing retailer with about 120 stores in Finland, Sweden and the Baltics and a
web shop. Merchandisers maintain the catalog, price lists and promotions. Stock is kept per store and in the
distribution centre, and web orders are allocated to the DC or to a store for click and collect. Store staff pick,
hand over and take returns in the store manager app, the tills send their sales in nightly files, and everything
that touches money goes to the ERP. Customer service, finance and the support desk work in the back office.

## Components

| Component | Description | Host |
| --- | --- | --- |
| `VanArsdel.Gateway` | API gateway (Ocelot) for the back office, the store manager app, the lab tools and the PSP and carrier webhooks | `api.<zone>`, IIS site `Retail API Gateway` |
| `VanArsdel.BackOffice` | Staff application (Blazor Server): orders, returns, stock, products, promotions, customers, store dashboards, reports, service health page | `backoffice.<zone>`, IIS site `Retail Back Office` |
| `VanArsdel.StoreManager` + `ClientApp` | Store application (React): today's sales, picking, click and collect, cash-up | `stores.<zone>`, IIS site `Retail Store Manager` |
| `VanArsdel.Catalog.Api` | Styles, variants, EANs, product search | `catalog.<zone>`, IIS site `Retail Catalog` |
| `VanArsdel.Pricing.Api` | Price lists, markdowns, promotions and basket pricing | `pricing.<zone>`, IIS site `Retail Pricing` |
| `VanArsdel.Inventory.Api` | Windows service `VanArsdelInventory`: stock per site, reservations, movements, replenishment proposals (REST and gRPC on port 5001) | `inventory.<zone>` (`app03`) |
| `VanArsdel.Allocation.Api` | Chooses the shipping site for each order line and creates pick tasks | `allocation.<zone>`, IIS site `Retail Allocation` |
| `VanArsdel.Orders.Api` | Web shop orders: intake from the web shop queue, payment, shipments, click and collect, cancellations, order process state | `orders.<zone>`, IIS site `Retail Orders` |
| `VanArsdel.Customers.Api` | Club members, loyalty points and tiers, consents, erasure | `customers.<zone>`, IIS site `Retail Customers` |
| `VanArsdel.Returns.Api` | Store and web returns, refunds, return labels | `returns.<zone>`, IIS site `Retail Returns` |
| `VanArsdel.StoreOps.Api` | Store tasks, opening hours, cash-up, live store dashboards (SignalR) | `storeops.<zone>`, IIS site `Retail Store Ops` |
| `VanArsdel.Reporting.Api` | Read model and reports (sales flash, web orders, top styles, returns), Excel exports | `reporting.<zone>`, IIS site `Retail Reporting` |
| `VanArsdel.ErpConnector` | Windows service `VanArsdelErpConnector`: items, sales journals, invoices and credit notes, transfer orders and goods receipts with the ERP (SOAP) | `app03` |
| `VanArsdel.Notifications` | Windows service `VanArsdelNotifications`: order, collection, return and club e-mails and text messages | `app03` |
| `VanArsdel.PosFeed` | Windows service `VanArsdelPosFeed`: picks up the till export files from the till system SFTP server | `app03` |
| `VanArsdel.Scheduler` | Windows service `VanArsdelScheduler`, clustered on two hosts: nightly and periodic jobs | `app03`, `app02` |
| `VanArsdel.Contracts` | Message contracts, roles and shared constants | library |
| `VanArsdel.BuildingBlocks` | Hosting, logging, authentication, messaging, outbox, caching and API conventions shared by the services | library |
| `VanArsdel.Integration` | ERP, PSP, carrier, SMS and e-mail clients | library |
| `VanArsdel.LabStubs` | Sign-in, ERP, PSP, carrier and Postiq stand-ins, seed data and simulators | test environments |

Other servers referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `sql01.<zone>` | SQL Server 2019 or later, one database per service (see `db`) |
| `mq01.<zone>` | RabbitMQ 3.9 or later, virtual host `retail`, TLS on 5671 |
| `redis01.<zone>` | Redis 6 (cache, SignalR backplane, distributed locks), TLS on 6380 |
| `docdb01.<zone>` | MongoDB 4.4 replica set `rs0` (customers) |
| `search01.<zone>` | Elasticsearch 7.17 (product search) |
| `seq01.<zone>` | Seq (central logs) |
| `smtp.<zone>` | SMTP relay (fallback for e-mail) |
| `erp01.<zone>` | Merx ERP 9.2 integration web service (`/MerxIntegration/ErpIntegration.svc`) |
| `login.microsoftonline.com` | Entra ID tenant for staff, service identities and the store manager app |
| `adfs.<zone>` | ADFS for store PCs that are still in the old domain (back office only) |
| `vanarsdel-webshop.servicebus.windows.net` | Azure Service Bus namespace of the web shop platform (queue `webshop-orders`) |
| `sftp.tillsystems.example` | Till system SFTP server with the nightly export files |
| `api.klarnet.example` | Payment service provider (client certificate) |
| `api.nordpost.example` | Carrier for home delivery and return labels |
| `api.postiq.example` | E-mail and SMS provider |

The production configuration uses the zone `corp.local`. All names in the zone can point to one server.

## Technologies

- .NET Core 2.1 (POS feed), .NET Core 3.1 (gateway, catalog, pricing, store ops, store manager), .NET 5.0 (inventory,
  customers, ERP connector, scheduler) and .NET 6.0 (orders, allocation, returns, reporting, notifications, back office);
  .NET Standard 2.0 libraries
- ASP.NET Core in IIS (ASP.NET Core Module, in-process) and Kestrel; Windows services (`Microsoft.Extensions.Hosting.WindowsServices`, `System.ServiceProcess` on 2.1)
- Ocelot 16 with Polly QoS; Polly retry and circuit breaker policies on `HttpClientFactory`
- Entity Framework Core 3.1, 5.0 and 6.0 (SQL Server, SQLite); Dapper for reporting and pricing queries
- MongoDB C# driver 2.13; LiteDB 5 for local runs
- MassTransit 7 (RabbitMQ, Azure Service Bus, transactional outbox, saga state machine); MassTransit 5.5 in the POS feed
- StackExchange.Redis, `IDistributedCache`, RedLock.net
- Elasticsearch NEST 7
- gRPC (`Grpc.AspNetCore`, `Grpc.Net.ClientFactory`)
- SignalR with the Redis backplane; `@microsoft/signalr` in the store app
- Blazor Server; React 17 with React Router 5, oidc-client 1.11 and webpack 5
- OpenID Connect and JWT bearer authentication (Entra ID, ADFS), client credentials between services (IdentityModel)
- Quartz.NET 3 (SQL Server job store, clustered)
- Serilog with file and Seq sinks; ASP.NET Core health checks and HealthChecks UI
- AutoMapper, FluentValidation, MediatR, Swashbuckle, Refit, Newtonsoft.Json
- WCF client (`System.ServiceModel.Http`, basic authentication over HTTPS) for the ERP SOAP service
- SSH.NET for SFTP; MailKit for SMTP
- ClosedXML for Excel reports
- Windows certificate store (PSP client certificate), DPAPI (`ProtectedData`, ERP password file)
- xUnit, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`, MassTransit in-memory test harness
- PowerShell 7, IIS administration (`WebAdministration`), `DnsServer`, `sc.exe`, `SecretManagement`, `sqlcmd`

## Requirements

- Windows Server 2019 or later with IIS and the ASP.NET Core Hosting Bundles 3.1, 5.0 and 6.0
- .NET runtimes 2.1, 3.1, 5.0 and 6.0 on the service host
- SQL Server 2019 or later
- RabbitMQ 3.9 or later, Redis 6, MongoDB 4.4, Elasticsearch 7.17, Seq
- Certificates: TLS certificate `CN=*.corp.local` in `LocalMachine\My` on every app host, the PSP client certificate
  (`psp-merchant.pfx` for returns, the certificate store for orders), `inventory.corp.local.pfx` for the inventory service
- Entra ID app registrations for the back office, the store manager app and every service (client credentials),
  and the ADFS relying party for the back office
- Deployment agent with PowerShell 7.2, `sqlcmd` and a SecretManagement vault (see `deploy\lab.psd1`)
- Build: .NET 6 SDK and Node.js 16 or later

## Build and test

```
dotnet build RetailBackOffice.sln -c Release
dotnet test RetailBackOffice.sln
cd src\VanArsdel.StoreManager\ClientApp
npm ci
npm run build
```

`dotnet publish` of the store manager also builds the React application. Sample till files are in `samples\pos`.

## Deploy

The deployment scripts read the environment description (`deploy\lab.psd1`; copy it for test and production) and
take passwords and keys from the SecretManagement vault named in it.

```
deploy\Deploy-All.ps1 -Environment deploy\lab.psd1
```

or step by step:

1. Publish (on the build machine): `deploy\Publish.ps1 [-Version 4.12.0] [-SkipTests]`
2. DNS records: `deploy\Install-DnsRecords.ps1 -Environment deploy\lab.psd1`
3. Databases, logins and users: `deploy\Initialize-Database.ps1 -Environment deploy\lab.psd1` (runs the `db` scripts)
4. RabbitMQ virtual host, users and permissions: `deploy\Install-RabbitMq.ps1 -Environment deploy\lab.psd1`
5. Windows services: `deploy\Install-Service.ps1 -Environment deploy\lab.psd1 [-Project VanArsdel.Inventory.Api, ...]`
6. Web sites: `deploy\Install-WebSite.ps1 -Environment deploy\lab.psd1 [-Project VanArsdel.Gateway, ...]`

`Deploy-All.ps1` installs the services in dependency order: inventory, catalog, pricing and customers, then the
order flow services, the background services and the front end last.

The ERP password is stored with DPAPI (machine scope) on the service host. `Install-Service.ps1` does this, or by hand:

```
VanArsdel.ErpConnector.exe --protect-password <password> --file D:\VanArsdel\Secrets\erp.bin
```

## Run

- Web sites: `https://backoffice.corp.local/`, `https://stores.corp.local/`, `https://api.corp.local/`
- API documentation: `/swagger` on every API site
- Services: `Start-Service VanArsdelInventory, VanArsdelErpConnector, VanArsdelNotifications, VanArsdelPosFeed, VanArsdelScheduler`
- Health: `/health` on every API and the gateway, `https://backoffice.corp.local/health-ui` for all of them
- Logs: Seq, and `D:\Logs\VanArsdel\<app>-<date>.log` on each host (folder can be changed with the environment variable `VANARSDEL_LOG_DIR`)

### Development mode

Every application also runs with `dotnet run` in the `Development` environment. It then uses SQLite and LiteDB files in
`.devdata` (created on first start), the in-memory product search, the in-memory message bus, a local memory cache and
the lab services in `VanArsdel.LabStubs` for sign-in, ERP, PSP, carrier and Postiq.

| Application | URL |
| --- | --- |
| LabStubs (sign-in, ERP, PSP, carrier, Postiq) | `http://localhost:5100` |
| Gateway | `http://localhost:5000` |
| Catalog, Pricing, Inventory, Allocation, Orders, Customers, Returns, StoreOps, Reporting | `http://localhost:5201` to `5209` (inventory gRPC on `5213`) |
| Back office | `http://localhost:5210` |
| Store manager | `http://localhost:5211` |

The in-memory bus only works inside one process. To run the whole system, point every application at a RabbitMQ
(and optionally Redis and SQL Server) with environment variables:

```
Messaging__Transport=RabbitMq  Messaging__Host=localhost  Messaging__VirtualHost=/  Messaging__Username=guest  Messaging__Password=guest  Messaging__UseSsl=false
WebShopBus__Transport=RabbitMq WebShopBus__Host=localhost WebShopBus__VirtualHost=/ WebShopBus__Username=guest WebShopBus__Password=guest WebShopBus__UseSsl=false   (orders)
Redis__Configuration=localhost
Database__Provider=SqlServer   ConnectionStrings__Orders="Server=localhost;Database=VanArsdelOrders;..."   (any service with a database)
```

Applications built for .NET Core 2.1, 3.1 and 5.0 can run on a newer runtime with `DOTNET_ROLL_FORWARD=Major`.
The POS feed runs as a console application with `--console`.

Lab users (password `lab`): `admin`, `merch`, `cs`, `finance`, `fi012.manager`, `fi012.staff`, `se003.manager`,
`dc01.staff`. Service clients use the secret `dev-secret`.

Lab tools (run in `src/VanArsdel.LabStubs` with `dotnet run -- <command>`, the other applications running):

| Command | What it does |
| --- | --- |
| `serve` (default) | Runs the lab services |
| `seed [--styles 40] [--customers 40]` | Stores, products, prices, opening stock and club members through the APIs |
| `webshop [--orders 5] [--delivery Home\|ParcelLocker\|ClickAndCollect] [--store FI012] [--card <no>] [--decline]` | Places web shop orders on the web shop queue, with a PSP authorisation |
| `pack [--collect]` | Packs every open pick task; with `--collect` also hands over the click and collect parcels |
| `pos [--stores FI012,FI001] [--date 2026-10-29] [--receipts 25] [--inbox <folder>]` | Writes till export files into the POS feed inbox |
| `token [user]` | Prints an access token for a lab user |

The lab services also have `/erp/admin/state`, `/erp/admin/receipts` (post a purchase order receipt) and the
sent e-mails and text messages at `/postiq/messages`.
