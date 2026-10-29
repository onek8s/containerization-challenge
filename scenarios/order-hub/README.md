# Order Hub

Order integration hub of Litware Outdoor, a Nordic outdoor-equipment wholesaler with its own webshop.
Orders from three sales channels are collected, validated against the ERP product list and sent to the
third-party warehouse (A. Datum Logistics) over SFTP. Shipment confirmations from the warehouse are
invoiced, reported back to the sales channel and, for the retail partner, answered with a dispatch notice.
Operations staff follow orders and handle rejected ones in the portal.

| Channel | How orders arrive | What goes back |
| --- | --- | --- |
| Webshop (WooCommerce) | Signed "order updated" webhook to the intake service | Order completed with tracking via the REST API, shipment e-mail to the customer, stock levels |
| Marketplace (Trailmarket) | Semicolon CSV export picked up from the SFTP account `trailmarket` | Shipping confirmation CSV and stock file on the same SFTP account |
| Retail partner (Contoso Retail) | Drop-ship purchase orders on the partner's Azure Service Bus queue | Dispatch notice with invoice number and tracking on the dispatch queue |

## Components

| Component | Description | Host |
| --- | --- | --- |
| `OrderHub.Portal` | ASP.NET Core 2.1 Razor Pages on .NET Framework 4.7.2: overview, order search and history, revalidate / resend, invoice PDFs, daily sales workbooks, products, users (ASP.NET Core Identity) | `hub.<zone>`, IIS site `orderhub` (`C:\inetpub\sites\orderhub`) |
| `OrderHub.Intake` | Windows service `OrderHubIntake` (Topshelf): webhook endpoint (OWIN self-hosted Web API, port 8085), marketplace SFTP polling | `intake.<zone>` |
| `OrderHub.Fulfilment` | Windows service `OrderHubFulfilment`: validates and stores orders, writes warehouse order files, reads shipment confirmations | `fulfil.<zone>` |
| `OrderHub.Partners` | Windows service `OrderHubPartners`: invoices shipped orders (PDF from the report server), partner Service Bus queues, nightly sales workbook (EPPlus) | `partners.<zone>` |
| `OrderHub.ShopSync` | Windows service `OrderHubShopSync`: webshop REST API updates, customer e-mails (Handlebars templates, MailKit), stock sync from the warehouse stock snapshots | `shopsync.<zone>` |
| `OrderHub.Integration` | Message consumers (MassTransit), SQL Server data access (Dapper), SFTP (SSH.NET), retries (Polly), Service Bus, report server and webshop clients | library |
| `OrderHub.Core`, `OrderHub.Contracts` | Channel formats, warehouse formats, validation, message contracts | libraries |
| `OrderHub.LabTools` | Test tools: send a signed webhook, webshop API stub, warehouse simulator | test environments |

Message flow (RabbitMQ virtual host `orderhub`, one queue per consumer endpoint):

```
webshop webhook --> Intake --+                                  +--> ShopSync --> webshop API, customer e-mail, marketplace SFTP
marketplace SFTP -> Intake --+--> [OrderSubmitted] --> Fulfilment --[OrderAccepted]--> Fulfilment --> warehouse SFTP /in
partner queue ---> Partners -+          |                                                           warehouse SFTP /out
                                [OrderRejected] --> ShopSync                                              |
                                                                       [OrderShipped] <-- Fulfilment <----+
                                                                            |
                                                      Partners (invoice PDF from SSRS) --[OrderInvoiced]--> Partners --> partner dispatch queue
Portal --[ResubmitOrder, ResendToWarehouse]--> Fulfilment
```

Other servers referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `mq.<zone>` | RabbitMQ 3.8 (management plug-in on 15672) |
| `sql01.<zone>` | SQL Server 2016 SP1 or later, database `OrderHub`, SQL logins `orderhub_svc`, `orderhub_portal`, `orderhub_reports` |
| `sftp.<zone>` | Litware SFTP server; accounts `adatum-wms` (warehouse) and `trailmarket` (marketplace) |
| `reports.<zone>` | SQL Server Reporting Services 2017 or later, report `/Order Hub/Invoice` |
| `smtp.<zone>` | SMTP relay for customer e-mail |
| `shop.litware-outdoor.example` | Webshop (WooCommerce REST API v3), hosted by the web agency |
| `contoso-retail-b2b.servicebus.windows.net` | Partner Service Bus namespace (queues `litware-orders`, `litware-dispatch`) |

The production configuration uses the zone `corp.local`. All names can point to one server.

## Requirements

- Windows Server 2016 or later with .NET Framework 4.7.2 or later
- IIS with the ASP.NET Core Module V2 (any ASP.NET Core Hosting Bundle from 2.1 on) for the portal
- Erlang/OTP 23 and RabbitMQ 3.8
- SQL Server 2016 SP1 or later with mixed mode authentication
- SQL Server Reporting Services 2017 or later
- An SFTP server; `deploy\Install-SftpServer.ps1` sets up the Windows OpenSSH server
- Outbound HTTPS to the webshop and to the partner Service Bus namespace (AMQP 5671 or HTTPS 443)
- Service accounts for the four services (local or domain accounts)
- Build: .NET 8 SDK and the .NET Framework 4.7.2 targeting pack (Visual Studio 2019/2022 or Build Tools)

## Build and test

```
dotnet build OrderHub.sln -c Release
dotnet test tests\OrderHub.Tests
```

The tests use the in-memory transport and local folders by default. They also run against real servers when
these variables are set:

| Variable | Value |
| --- | --- |
| `ORDERHUB_TEST_RABBITMQ` | `rabbitmq://host/vhost;user;password` |
| `ORDERHUB_TEST_SFTP` | `host;port;user;password;directory[;private key file]` |
| `ORDERHUB_TEST_SQL` | connection string of `orderhub_svc` to a database created with `db\*.sql` |

Sample input files are in `samples`.

## Deploy

1. DNS records (on the DNS server):

   ```
   deploy\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP <ip>
   ```

   or one address per name with `-HubIP`, `-IntakeIP`, `-FulfilmentIP`, `-PartnersIP`, `-ShopSyncIP`,
   `-MqIP`, `-SftpIP`, `-ReportsIP`, `-SqlIP`, `-SmtpIP`.

2. Publish (on the build machine):

   ```
   deploy\Publish.ps1 -OutputPath C:\Build\OrderHub
   ```

3. Database (as a sysadmin of the instance):

   ```
   deploy\Initialize-Database.ps1 -SqlServer sql01.corp.local -ServicePassword $svc -PortalPassword $portal -ReportsPassword $rs
   ```

4. RabbitMQ (on `mq`), with the installers downloaded from erlang.org and rabbitmq.com:

   ```
   $pw = @{ intake = '...'; fulfilment = '...'; partners = '...'; shopsync = '...'; portal = '...' }
   deploy\Install-RabbitMq.ps1 -ErlangInstaller otp_win64_23.3.exe -RabbitMqInstaller rabbitmq-server-3.8.19.exe -AdminPassword $admin -ComponentPasswords $pw
   ```

5. SFTP server (on `sftp`):

   ```
   deploy\Install-SftpServer.ps1 -Root D:\Sftp -WarehousePassword $wms -MarketplacePassword $tm -AuthorizedKeys @{ 'adatum-wms' = 'orderhub-wms.pub' }
   ```

   The script prints the host key fingerprint for the `*.Sftp.HostKeyFingerprint` settings. The private key of
   `adatum-wms` goes to `C:\OrderHub\Keys\adatum-wms.key` (PEM format) on the fulfilment and shop sync servers.

6. Report server (on any machine, as Content Manager):

   ```
   deploy\Publish-Reports.ps1 -ReportPortalUrl http://reports.corp.local/Reports -SqlServer sql01.corp.local -ReportsPassword $rs -BrowserAccount REPORTS\svc-orderhub-rs
   ```

7. Services, each on its server (`-CreateLocalAccount` creates the account as a local user):

   ```
   deploy\Install-HubService.ps1 -Component Fulfilment -PublishPath C:\Build\OrderHub -ServiceAccount .\svc-orderhub-ful -ServicePassword $p `
       -SqlPassword $svc -RabbitMqPassword $mqFul -Settings @{ 'Warehouse.Sftp.HostKeyFingerprint' = '..' }
   deploy\Install-HubService.ps1 -Component Partners -PublishPath C:\Build\OrderHub -ServiceAccount .\svc-orderhub-prt -ServicePassword $p `
       -SqlPassword $svc -RabbitMqPassword $mqPrt -Settings @{ 'Partner.ServiceBus.ConnectionString' = '..'; 'Reports.Password' = '..' }
   deploy\Install-HubService.ps1 -Component ShopSync -PublishPath C:\Build\OrderHub -ServiceAccount .\svc-orderhub-shop -ServicePassword $p `
       -SqlPassword $svc -RabbitMqPassword $mqShop -Settings @{ 'Webshop.ConsumerKey' = 'ck_..'; 'Webshop.ConsumerSecret' = 'cs_..'; 'Marketplace.Sftp.Password' = '..' }
   deploy\Install-HubService.ps1 -Component Intake -PublishPath C:\Build\OrderHub -ServiceAccount .\svc-orderhub-int -ServicePassword $p `
       -RabbitMqPassword $mqInt -Settings @{ 'Webshop.WebhookSecret' = '..'; 'Marketplace.Sftp.Password' = '..' }
   ```

   Install Fulfilment first: the services declare their queues and subscriptions when they start.
   All other settings are in `<component>.exe.config` (see the `appSettings` section) and can be passed with `-Settings`.

8. Portal (on `hub`):

   ```
   deploy\Install-Portal.ps1 -PublishPath C:\Build\OrderHub -HostName hub.corp.local -SqlPassword $portal -RabbitMqPassword $mqPortal `
       -AdminUser hubadmin -AdminPassword $hubadmin -HostingBundle dotnet-hosting-8.0.x-win.exe [-CertificateThumbprint <thumbprint>]
   ```

   More portal users are added on the Users page. `OrderHub.Portal.exe create-user <name> <password> [Admin|Operator]`
   works on the server too.

9. Webshop: create a webhook (topic "Order updated", delivery URL `http://intake.corp.local:8085/api/webshop/orders`
   through the reverse proxy, secret = `Webshop.WebhookSecret`) and a REST API key with read/write access for the
   shop sync service.

## Test environment without the external parties

| Party | Setting |
| --- | --- |
| Webshop | `OrderHub.LabTools.exe webshop-stub http://+:8090/` and `Webshop.ApiUrl = http://localhost:8090/wp-json/wc/v3/` on shop sync; send orders with `OrderHub.LabTools.exe send-webshop-order samples\webshop-order-10482.json http://intake.corp.local:8085/api/webshop/orders <secret>` |
| Warehouse | `OrderHub.LabTools.exe warehouse-sim D:\Sftp\adatum-wms` ships every order file within a few seconds and writes stock snapshots |
| Marketplace | copy `samples\trailmarket_orders_*.csv` to `D:\Sftp\trailmarket\orders` |
| Partner | `Partner.Transport = Folder` on the partners service; copy `samples\contoso-po-4500123456.json` to `C:\OrderHub\Partner\Inbound`, dispatch notices appear in `C:\OrderHub\Partner\Outbound` |
| Report server | `Reports.InvoicePdf = false` invoices without a PDF when no report server is available |
| SMTP | any SMTP sink on `Mail.SmtpHost` / `Mail.SmtpPort` |

## Run

- Services: `Start-Service OrderHubFulfilment, OrderHubPartners, OrderHubShopSync, OrderHubIntake`. Each service
  also runs in a console: start the exe without arguments.
- Intake health check: `http://intake.corp.local:8085/api/health`
- Logs: `C:\OrderHub\Logs\<component>-<date>.log`; failed messages end up in the `*_error` queues in RabbitMQ
  (management UI `http://mq.corp.local:15672/`, virtual host `orderhub`).
- Portal: `http://hub.corp.local/`. Operators can validate a rejected order again after fixing the product list
  and send an order file to the warehouse again; administrators also manage products and users.
- The sales workbook of the previous day is created after `Reports.DailySalesAt` (02:00 by default) and is
  downloaded from the Reports page.
