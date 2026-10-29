# Supplier Invoices

Accounts payable invoice hub for Wingtip Supply. Suppliers deliver invoices as
EDIFACT INVOIC (D.96A), UBL 2.1 / Peppol BIS 3 XML or Wingtip CSV files, either
to their folder on the supplier FTP server or to the drop share (VPN suppliers
and the paper invoice scanning service). The hub collects the files, matches
each invoice against the purchase order and goods receipts in the ERP, and
transfers approved invoices to the ERP accounts payable ledger through import
files. Accounts payable clerks handle exceptions in the web application.

| Component | Description | Host |
| --- | --- | --- |
| `Invoices.Admin` | ASP.NET Core 8 Razor Pages application for accounts payable: overview, invoices, exceptions, received files, ERP batches, supplier set-up | `invoices.<zone>` (IIS site `invoices`) |
| `Invoices.Gateway` | Windows service `InvoiceGateway`: polls supplier FTP folders and the drop share, archives originals, queues files for processing | `invoices-gw.<zone>` |
| `Invoices.Processor` | Windows service `InvoiceProcessor`: parses, validates and matches invoices, releases matched invoices for posting, control endpoint for the web application (net.tcp 8096) | `invoices-proc.<zone>` |
| `Invoices.ErpLink` | Windows service `InvoiceErpLink`: vendor and purchase order lookups from the ERP database (tcp 8095), ERP import files and result files | `invoices-erp.<zone>` |
| `Invoices.Infrastructure` | Settings, SQL Server data access, message queues, event log, performance counters, service host | library |
| `Invoices.Contracts` | Control service contract and ERP lookup interface | library |
| `Invoices.Core` | Invoice model, EDIFACT/UBL/CSV parsers, validation, three-way matching, ERP import file format | library |

Message flow:

```
FTP / drop share -> InvoiceGateway -> [invoices-inbound] -> InvoiceProcessor -> [invoices-posting] -> InvoiceErpLink -> \\erp01\Import\AP
                                                                 |  ^                                    ^      |
                                                       ERP lookup (tcp 8095) ----------------------------+      +-- \\erp01\Export\AP (results)
                    Invoices.Admin -- control (net.tcp 8096) ---+
```

Queues are private transactional MSMQ queues: `invoices-inbound` and
`invoices-poison` on `invoices-proc`, `invoices-posting` on `invoices-erp`.

Other servers referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `sql01.<zone>` | SQL Server, database `SupplierInvoices` |
| `files01.<zone>` | Shares `InvoiceDrop$` and `InvoiceArchive$` |
| `ftp.<zone>` | Supplier FTP site (IIS FTP, explicit FTPS) |
| `erp01.<zone>` | ERP database (read-only reporting login) and the `Import` / `Export` shares of the ERP batch jobs |

The production configuration uses the zone `corp.local` and the domain `CORP`.
All names can point to the same server.

## Requirements

- Windows Server 2019 or later
- .NET Framework 4.8 (services) and the ASP.NET Core 8 Hosting Bundle (web application)
- IIS with Windows Authentication; IIS FTP Server for the supplier FTP site
- Message Queuing (MSMQ) on the gateway, processor and ERP link servers
- SQL Server 2016 or later
- Active Directory groups `AP-Hub-Clerks` and `AP-Hub-Admins`
- Service accounts: `svc-inv-gw` (gateway), `svc-inv-proc` (processor),
  `svc-inv-erp` (ERP link), `svc-inv-web` (application pool), `svc-inv-ftp`
  (FTP login of the gateway)
- A server certificate for `invoices.<zone>` and `ftp.<zone>`
- Build: .NET 8 SDK and the .NET Framework 4.8 developer pack (Visual Studio 2022 or Build Tools)

## Build and test

```
dotnet build SupplierInvoices.sln -c Release
dotnet test tests\Invoices.Core.Tests
```

Sample supplier files are in `samples`.

## Database

```
deploy\Initialize-Database.ps1 -SqlServer sql01.corp.local -GatewayAccount CORP\svc-inv-gw -ProcessorAccount CORP\svc-inv-proc `
    -ErpLinkAccount CORP\svc-inv-erp -WebAccount CORP\svc-inv-web
```

In test and lab environments add `-ErpLabDatabase -ErpSqlServer <server>` to
create the ERP test database `WingtipErp` with the vendors and purchase orders
used by the sample files.

## Deploy

1. DNS records (on the DNS server):

   ```
   deploy\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP <ip>
   ```

   or one address per name with `-WebIP`, `-GatewayIP`, `-ProcessorIP`,
   `-ErpLinkIP`, `-SqlServerIP`, `-FileServerIP`, `-FtpServerIP`, `-ErpServerIP`.

2. Publish (on the build machine):

   ```
   deploy\Publish.ps1 -OutputPath C:\Build\SupplierInvoices
   ```

3. Shares (on `files01`) and the FTP site (on `ftp`):

   ```
   deploy\New-InvoiceShares.ps1 -Root D:\Shares -GatewayAccount CORP\svc-inv-gw -ProcessorAccount CORP\svc-inv-proc -WebAccount CORP\svc-inv-web -DropWriters CORP\GG-Invoice-Drop
   deploy\Install-FtpSite.ps1 -Root D:\Ftp -GatewayAccount CORP\svc-inv-ftp -CertificateThumbprint <thumbprint> -Suppliers NF,KTL
   ```

   Lab only, on `erp01`: `deploy\lab\New-ErpShares.ps1 -Root D:\Erp -ErpLinkAccount CORP\svc-inv-erp`.

4. Message queues:

   ```
   deploy\Install-Queues.ps1 -Role Gateway                                                                     # on invoices-gw
   deploy\Install-Queues.ps1 -Role Processor -SenderAccounts CORP\svc-inv-gw -ReceiverAccount CORP\svc-inv-proc  # on invoices-proc
   deploy\Install-Queues.ps1 -Role ErpLink -SenderAccounts CORP\svc-inv-proc -ReceiverAccount CORP\svc-inv-erp   # on invoices-erp
   ```

   On a single server use `-Role All -SenderAccounts CORP\svc-inv-gw,CORP\svc-inv-proc` and run the
   script once per receiving account.

5. Services (ERP link first, then processor, then gateway):

   ```
   deploy\Install-HubService.ps1 -Role ErpLink   -PublishPath C:\Build\SupplierInvoices -ServiceAccount CORP\svc-inv-erp
   deploy\Install-HubService.ps1 -Role Processor -PublishPath C:\Build\SupplierInvoices -ServiceAccount CORP\svc-inv-proc
   deploy\Install-HubService.ps1 -Role Gateway   -PublishPath C:\Build\SupplierInvoices -ServiceAccount CORP\svc-inv-gw
   ```

   Other settings can be passed with `-Settings @{ Key = 'value' }`. On a single
   server, set `-Settings @{ InboundQueue = '.\private$\invoices-inbound' }` for the
   gateway and `-Settings @{ PostingQueue = '.\private$\invoices-posting' }` for the
   processor. After installing the gateway, enter the FTP password in
   `Invoices.Gateway.exe.config` and run `Invoices.Gateway.exe protect-config`.

6. Web application (on `invoices`):

   ```
   deploy\Install-AdminSite.ps1 -PublishPath C:\Build\SupplierInvoices -PoolAccount CORP\svc-inv-web -CertificateThumbprint <thumbprint> -HostingBundle dotnet-hosting-8.0.x-win.exe
   ```

## Run

- Services: `Start-Service InvoiceErpLink, InvoiceProcessor, InvoiceGateway`. Each
  service also runs in a console with `<exe> console`.
- Logs: event log `Wingtip Invoices` and `D:\InvoiceHub\Logs`; performance
  counters in the category `Wingtip Supplier Invoices`.
- Web application: `https://invoices.corp.local/` (members of `AP-Hub-Clerks` or
  `AP-Hub-Admins`; supplier set-up and resubmitting files need `AP-Hub-Admins`).
- Try it: copy `samples\BP-2026-0815.xml` to `\\files01.corp.local\InvoiceDrop$\BP`,
  `samples\LOS_77810.csv` to `...\InvoiceDrop$\LOS` and upload
  `samples\NF_INVOIC_9100231.edi` to `suppliers/NF/out` on the FTP site.
  Invoice dates in the samples must be within the open ERP periods.
- Lab ERP: `deploy\lab\Invoke-ErpImport.ps1` imports the files written to
  `\\erp01\Import\AP` and writes the result files the ERP link reads back.

Matching settings (`PriceTolerancePercent`, `TotalToleranceAmount`,
`AutoApproveLimit`) are in `Invoices.Processor.exe.config`; suppliers can
override the price tolerance and use two-way matching.
