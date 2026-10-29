# Dealer Orders

Dealer order portal and sales operations of Tailspin Machinery, a maker of forest machines (harvesters,
forwarders), compact loaders and attachments, selling through independent dealers in the Nordics, Germany and
North America. Dealers configure machines, make quotes for their customers, order machines and spare parts, follow
their orders and download documents in the portal. The sales desk approves discounts and credit exceptions, fixes
orders that the ERP refused and watches the integrations. The two largest North American dealer groups send
purchase orders by EDI. The ERP is Microsoft Dynamics AX 2012 R3 (company `TSM`); every quote, order, reservation
and customer is created there, and prices, stock, balances and order status are read back.

Built from 2014 (portal, .NET Framework 4.5.2, later retargeted to 4.6.1) to 2019 (sales desk, EDI and document
service, .NET Framework 4.7.2; shared logic moved to .NET Standard 2.0 libraries in the 2018 EDI rewrite).

## Components

| Component | Description | Host |
| --- | --- | --- |
| `Tailspin.DealerOrders.DealerPortal` | Dealer portal (ASP.NET MVC 5, .NET 4.6.1): machine configurator (Knockout), quotes, orders, parts quick order, stock, documents, user administration; ASP.NET Identity 2 with OWIN cookie sign-in, lockout and password reset; SignalR 2 hub for live order status | `dealers.<zone>`, IIS |
| `Tailspin.DealerOrders.SalesDesk` | Internal sales desk (MVC 5 + Web API 2, .NET 4.7.2, Windows authentication, AD groups, Autofac): approvals, orders, dealers, catalog, EDI monitor, AX command queue, status API | `salesdesk.<zone>`, IIS |
| `Tailspin.DealerOrders.DocumentService` | WCF service (basicHttpBinding, `DocumentService.svc`): quotes, order confirmations and dealer agreements from Word templates, Excel price lists, stored in SQL | `docs.<zone>`, IIS |
| `Tailspin.DealerOrders.AxConnector` | Windows service `TailspinDealerAxConnector` (Topshelf): runs the queued AX commands (customers, configurations, quotations, sales orders, changes, cancellations, reservations) and reads changed orders, confirmations and packing slips back | `axint01.<zone>` |
| `Tailspin.DealerOrders.PriceSync` | Windows service `TailspinDealerPriceSync` (Topshelf + Quartz.NET): prices, stock, items, exchange rates and dealer balances from AX into the cache tables; quote expiry, daily digest, document clean-up | `axint01.<zone>` |
| `Tailspin.DealerOrders.EdiGateway` | Windows service `TailspinDealerEdiGateway` (Topshelf, OWIN self-hosted Web API): AS2 receiver on port 4080 with synchronous or asynchronous MDN, SFTP polling, X12 850 in, 855, 856 and 997 out | `edi01.<zone>` |
| `Tailspin.DealerOrders.Notifications` | Windows service `TailspinDealerNotifications` (Topshelf): mail through the SMTP relay, push to the portal over a SignalR client connection | `app01.<zone>` |
| `Tailspin.DealerOrders.Domain`, `.Contracts` | Domain model (.NET Standard 2.0), AX gateway interfaces and DTOs | libraries |
| `Tailspin.DealerOrders.Configurator` | Option rules language (requires, excludes, includes, market conditions), configuration ids, price build-up with tier discounts, campaigns, currency conversion and freight, lead times | library |
| `Tailspin.DealerOrders.Edi` | X12 4010 reader and writer, 850/855/856/997 maps, AS2 (S/MIME signing and encryption, MIC, MDN) | library |
| `Tailspin.DealerOrders.Documents` | Word templates with content controls (Open XML SDK), price lists and order exports (EPPlus) | library |
| `Tailspin.DealerOrders.Processing` | Quotes, orders, approvals, AX command processing, status sync, EDI intake and outbound, cache sync, mail templates | library |
| `Tailspin.DealerOrders.Data` | EF6 model with ASP.NET Identity 2 tables, Dapper queries, AX command queue, cache store, document store (.NET 4.6.1) | library |
| `Tailspin.DealerOrders.AxIntegration` | .NET Business Connector session pool and calls to the `TSDealerOrderService` X++ class, AIF document services over WCF NetTcp, AX Query Service client, gateway selection | library |
| `Tailspin.DealerOrders.Infrastructure` | log4net set-up, settings, SMTP, document service client, Unity registrations | library |
| `Tailspin.DealerOrders.LabStubs` | AX simulator (in memory or JSON state file), lab data, in-memory stores | test environments |
| `Tailspin.DealerOrders.LabTools` | `dolab` console: schema, seed, sync, AS2 certificates, AS2 dealer, Word templates, order flow walk-through | test environments |

Other servers referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `sql01.<zone>` | SQL Server 2016 or later, mixed mode authentication; database `DealerOrders` |
| `aos01.<zone>` | Dynamics AX 2012 R3 AOS: TCP 2712 (clients and Business Connector), 8201 (AIF services and Query Service over NetTcp) |
| `smtp.<zone>` | SMTP relay |
| Dealer AS2 and SFTP endpoints | Great Lakes Forestry (AS2), Northern Timber Machines (SFTP) |

The production configuration uses the zone `corp.local`.

## Technologies

- .NET Framework 4.6.1 and 4.7.2, C# 7.3, .NET Standard 2.0 libraries
- ASP.NET MVC 5.2, Web API 2.2, Razor, bundling and minification, Knockout 3.4, jQuery 3, Bootstrap 3 (CDN)
- ASP.NET Identity 2.2 with OWIN cookie authentication; Windows authentication with AD groups (sales desk)
- SignalR 2.4 (hub in the portal, .NET client in the notification service)
- WCF: basicHttpBinding service in IIS, NetTcp clients for AX AIF, AX Query Service client
- Dynamics AX 2012 R3: .NET Business Connector (`Microsoft.Dynamics.BusinessConnectorNet`), X++ class
  `TSDealerOrderService`, AIF document services, Query Service; AX objects in `ax`
- Entity Framework 6.2, Dapper 1.50, SQL Server (SqlBulkCopy, MERGE, application locks)
- Topshelf 4, Quartz.NET 2.6, OWIN self-host (Microsoft.Owin.Host.HttpListener)
- Unity 5 and Autofac 4, AutoMapper 6, Polly 5, log4net 2.0.8, Newtonsoft.Json 11
- X12 4010, AS2 with System.Security.Cryptography.Pkcs, SSH.NET 2016.1
- Open XML SDK 2.8, EPPlus 4.5

## Requirements

- Windows Server 2016 or later with IIS 8.5+ (ASP.NET 4.7, WCF HTTP activation, Windows authentication, WebSockets)
- .NET Framework 4.7.2
- SQL Server 2016 or later
- On `axint01`: Dynamics AX 2012 R3 client components with the .NET Business Connector, an AX client configuration
  (`.axc`) for the business connector, and AX users for the service account and the sales desk users
- AS2 certificate for Tailspin and the dealers' AS2 certificates; an SSH key for the dealers' SFTP servers
- TLS certificates for the web sites
- Build: Visual Studio 2017 (15.9) or the Build Tools with the web workload; the solution also builds with the
  .NET SDK (`dotnet build`) using the .NET Framework reference assemblies

## Build and test

```
dotnet build DealerOrders.sln
dotnet test tests\Tailspin.DealerOrders.Tests
```

Sample X12 files are in `samples\edi`, the Word templates in `templates` (`dolab templates` writes them again),
the machine catalog and option rules in `catalog`.

## Deploy

1. DNS (on a domain controller):

   ```
   deploy\Install-DnsRecords.ps1 -WebServer <ip> -AxIntegrationServer <ip> -EdiServer <ip> -AppServer <ip> [-SqlServer <ip> -AosServer <ip> -SmtpServer <ip>]
   ```

2. Publish (build machine):

   ```
   deploy\Publish.ps1 -Output C:\Build\DealerOrders
   ```

3. Database (sysadmin on `sql01`):

   ```
   deploy\Initialize-Database.ps1 -SqlServer sql01.corp.local -Passwords @{ PortalPassword = ..; DeskPassword = ..; DocsPassword = ..;
       AxConnectorPassword = ..; PriceSyncPassword = ..; EdiPassword = ..; NotifyPassword = .. }
   ```

4. AX client configuration (on `axint01`), then the AX services:

   ```
   deploy\Install-AxClientConfig.ps1 -AxcFile \\fs01\ax\TailspinAX-BC.axc -ServiceAccount CORP\svc-dealer-ax
   deploy\Install-Service.ps1 -Component AxConnector -PublishPath C:\Build\DealerOrders -ServiceAccount CORP\svc-dealer-ax -ServicePassword $p -SqlPassword $s
   deploy\Install-Service.ps1 -Component PriceSync -PublishPath C:\Build\DealerOrders -ServiceAccount CORP\svc-dealer-ax -ServicePassword $p -SqlPassword $s
   ```

5. EDI (on `edi01`):

   ```
   deploy\Import-Certificates.ps1 -PfxFile tailspin-as2.pfx -PfxPassword $pfx -ServiceAccount CORP\svc-dealer-edi -PartnerCertificates .\partners
   deploy\Install-Service.ps1 -Component EdiGateway -PublishPath C:\Build\DealerOrders -ServiceAccount CORP\svc-dealer-edi -ServicePassword $p -SqlPassword $s `
       -Settings @{ 'Edi.As2.CertificateThumbprint' = '<thumbprint>' }
   ```

6. Notifications (on `app01`):

   ```
   deploy\Install-Service.ps1 -Component Notifications -PublishPath C:\Build\DealerOrders -ServiceAccount CORP\svc-dealer-app -ServicePassword $p -SqlPassword $s `
       -Settings @{ 'Push.ServiceKey' = '<key>'; 'Mail.Host' = 'smtp.corp.local' }
   ```

7. Web sites (on `web01`):

   ```
   deploy\Install-WebSite.ps1 -App docs -PublishPath C:\Build\DealerOrders -CertificateThumbprint <tls> -SqlPassword $s `
       -Settings @{ 'DocumentService.ClientKeys' = 'portal=<key1>;salesdesk=<key2>' }
   deploy\Install-WebSite.ps1 -App portal -PublishPath C:\Build\DealerOrders -CertificateThumbprint <tls> -SqlPassword $s `
       -Settings @{ 'DocumentService.ClientKey' = '<key1>'; 'Push.ServiceKey' = '<key>' }
   deploy\Install-WebSite.ps1 -App salesdesk -PublishPath C:\Build\DealerOrders -CertificateThumbprint <tls> -SqlPassword $s `
       -PoolAccount CORP\svc-dealer-web -PoolPassword $pp -Settings @{ 'DocumentService.ClientKey' = '<key2>' }
   ```

## Test systems

Set `Ax.Mode` to `Simulator` in the AX connector, price sync and lab tools configuration to use the AX simulator
instead of the Business Connector, AIF and the Query Service. `Ax.Simulator.StateFile` keeps the simulated AX data
between restarts; quotes and orders move on from created to confirmed, in production, shipped and invoiced after a
few minutes. `Edi.Sftp.LocalRoot` replaces the dealers' SFTP servers with local folders, `Mail.PickupDirectory`
writes mail as `.eml` files and `Edi.As2.CertificatePfx` loads the AS2 certificate from a file.

```
dolab schema --server sql01 --var PortalPassword=.. ...    create the database
dolab seed                                                  lab dealers, EDI partners and portal users (d10010.admin, ...)
dolab sync                                                  fill the price and stock cache from the configured AX
dolab certs C:\DealerOrders\Edi\Certificates                AS2 certificates for Tailspin and Great Lakes Forestry
dolab send-850 samples\edi\850-glf-machine.x12              send a purchase order to the AS2 receiver as the dealer
dolab flow                                                  walk an order through the process in memory
```

Log files are written to `C:\DealerOrders\Logs` (`Logging.Folder`, or the `DEALERORDERS_LOG_DIR` environment
variable).
