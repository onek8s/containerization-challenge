# B2B Integration

Integration platform of VanArsdel Distribution, a wholesale distributor of electrical, plumbing and HVAC
supplies with customers in the US and Europe. Customers send orders by EDI (X12 850 over AS2 or SFTP,
EDIFACT ORDERS over AS2 or FTP) or as XML batches through the partner order gateway. Orders are validated
against business rules, credit checked in the ERP and handed over as IDocs. Despatch advices and invoices
from the ERP go back to the customers as 856/DESADV and 810/INVOIC, with 997/CONTRL acknowledgements both
ways. Purchase orders go to suppliers, and supplier invoices are parked in the ERP. Operations staff work in
the operations portal, customers follow their orders on the tracking site, and an SLA monitor e-mails the
support groups when documents are late.

Built 2014-2019 on BizTalk Server 2013 R2 and moved to BizTalk Server 2016 in 2017.

## Components

| Component | Description | Host |
| --- | --- | --- |
| `biztalk/VanArsdel.Integration.Schemas` | X12 004010 850/810/856/997, EDIFACT D96A ORDERS/INVOIC/DESADV and D3 CONTRL, ERP IDocs (ORDERS05, DESADV01, INVOIC02), canonical order/invoice/despatch, partner order batch, ERP service messages, property schema | BizTalk application `VanArsdel.Integration` |
| `biztalk/VanArsdel.Integration.Maps` | 18 maps (custom XSLT with scripting functoid classes from `VanArsdel.Integration.Mapping`) | BizTalk |
| `biztalk/VanArsdel.Integration.Pipelines` | Receive and send pipelines for AS2, SFTP/FTP files, gateway XML and IDoc files | BizTalk |
| `biztalk/VanArsdel.Integration.Orchestrations` | ProcessCustomerOrder, SendCustomerDocument, SendCustomerInvoice, ProcessFunctionalAck, ProcessSupplierInvoice, SupplierPurchaseOrder | host `B2BProcessingHost` |
| `VanArsdel.Integration.PipelineComponents` | Partner resolver, duplicate check, encoding fixer, archive, XML debatcher, IDoc splitter, context enricher, BAM interchange tracking | BizTalk (Pipeline Components, GAC) |
| `VanArsdel.Integration.Runtime` | Code called from orchestrations: routing slips (ESB style), rule execution, interchange register, BAM order lifecycle, SSO settings | GAC |
| `VanArsdel.Integration.WcfExtensions` | WCF endpoint behavior `vanArsdelErpHeaders` for the ERP send port | GAC, machine.config |
| `VanArsdel.Integration.Gateway` | Partner order gateway (WCF, WS-Security user name), forwards to the published BizTalk WCF receive location `B2BReceive/PartnerOrders.svc` | IIS site `B2BGateway` on the web server |
| `VanArsdel.Integration.OpsPortal` | Operations portal (ASP.NET Web Forms): suspended instances and resubmit, hosts and ports, partners and cross references, interchanges, resubmission history | IIS site `B2BOpsPortal`, Windows authentication |
| `VanArsdel.Integration.Tracking` | Customer tracking site (ASP.NET MVC 5, forms authentication): orders, interchanges, document volumes with Excel export, partner users | IIS site `B2BTracking` on the DMZ web server |
| `VanArsdel.Integration.SlaMonitor` | Windows service `VanArsdelB2BSlaMonitor`: acknowledgements, order milestones, credit holds, suspended instances, host instances, silent partners | first BizTalk server |
| `VanArsdel.Integration.Management` | BizTalkOperations, ExplorerOM and WMI wrappers used by the portal and the monitor | library |
| `VanArsdel.Integration.Domain`, `.Contracts`, `.Edi`, `.Mapping`, `.Rules`, `.Messaging`, `.Monitoring`, `.Data` | Partner and order model, service and canonical contracts, X12/EDIFACT parser and acknowledgement builder, functoid classes, BRE fact classes, pipeline helpers, SLA evaluation, data access (Enterprise Library Data) | libraries |
| `VanArsdel.Integration.BindingTool` | `b2bdeploy.exe`: settings export, bindings expansion and inspection, SSO application, BizTalk parties | deployment |
| `VanArsdel.Integration.Deployment` | Settings workbook reader, template expansion, bindings inspection | library |
| `rules` | Business Rules policies VanArsdel.OrderValidation 1.9, VanArsdel.CreditRelease 1.3, VanArsdel.Routing 2.4 | Business Rules Engine |
| `bam` | BAM activities OrderLifecycle and InterchangeTracking with their views | BAMPrimaryImport |
| `tools/VanArsdel.Integration.LabStubs` | `b2blab`: lab tool with ERP, AS2 and partner simulators and a pipeline/map runner | test environments |
| `tests/VanArsdel.Integration.Tests` | xUnit tests | build |

Other servers and parties referenced by the configuration:

| Name | Purpose |
| --- | --- |
| `BTS01`, `BTS02` | BizTalk Server 2016 group, hosts `B2BReceiveHost`, `B2BSendHost`, `B2BProcessingHost`, isolated hosts `B2BAS2Host` (AS2 over `BTSHTTPReceive.dll`) and `BizTalkIsolatedGateway` |
| `BTSSQL01` | SQL Server 2016: BizTalk databases, SSO, `BAMPrimaryImport`, `VanArsdelIntegration` |
| `B2BWEB01`, `B2BDMZ01` | Internal and DMZ web servers (IIS 10) |
| `b2bfs01` | File server, share `b2b$` (archive, ERP IDoc exchange, credit hold, resubmit folders) |
| `sapgw.corp.vanarsdel.com` | ERP SOAP gateway (`OrderManagementService`) |
| `smtp.corp.vanarsdel.com` | SMTP relay |
| Partners | Hartwell Electrical Supply and Bauwerk Haustechnik (AS2), Brightline Facilities (SFTP), Nordvik Byggvarer (FTP), Ridgeway Mechanical and Lakeshore Electric (gateway), Kestrel Cable (SFTP supplier), Pellham Valves (AS2 supplier) |

## Technologies

- BizTalk Server 2013 R2 / 2016: orchestrations, XSLT maps with scripting functoids, custom pipeline
  components, FILE, SFTP, FTP, HTTP (AS2), WCF-BasicHttp, WCF-Custom, WCF-SQL and SMTP adapters, EDI and AS2
  parties and agreements, BAM, Business Rules Engine with .NET fact classes, SSO configuration store
- .NET Framework 4.6.2 (BizTalk assemblies and libraries loaded by BizTalk) and 4.7.2 (web applications,
  service, tools); shared libraries target .NET Standard 2.0
- ASP.NET Web Forms with FriendlyUrls, ASP.NET MVC 5, WCF (service, WS-Security, behavior extensions)
- Enterprise Library 6 Data Access Block, Unity 4, AutoMapper 6, log4net 2.0, Newtonsoft.Json, EPPlus 4.5
- SQL Server 2016 (stored procedures, SQL Agent job), BAM views
- Deployment Framework for BizTalk 5.x, SpreadsheetML settings workbook, PowerShell 5, sqlcmd, bm.exe,
  BTSTask/ExplorerOM, InstallUtil

## Requirements

- Windows Server 2016 with BizTalk Server 2016 Enterprise and the BizTalk Adapter Pack (WCF-SQL)
- SQL Server 2016 SP1 or later with SQL Server Agent
- IIS 10 with ASP.NET 4.7, Windows authentication and the HTTP activation features for WCF
- Deployment Framework for BizTalk 5.7 or later on the BizTalk servers, SQL Server command line utilities
- Certificates: TLS for the web sites and AS2, AS2 signing/encryption certificates of VanArsdel and the
  partners (Personal and Other People stores of the `B2BAS2Host` account)
- Build: Visual Studio 2019 with the BizTalk Server 2016 developer tools (`B2BIntegration.BizTalk.sln`), .NET 8
  SDK (`B2BIntegration.sln`)

## Build and test

```
dotnet build B2BIntegration.sln -c Release
dotnet test tests/VanArsdel.Integration.Tests
```

`B2BIntegration.sln` contains the libraries, web applications, the SLA monitor, the deployment tool, the lab
tool and the tests. The .NET Framework projects build with the reference assemblies from NuGet and the web
projects use MSBuild.SDK.SystemWeb.

`B2BIntegration.BizTalk.sln` additionally contains the four BizTalk projects; build it in Visual Studio with
the BizTalk developer tools:

```
msbuild B2BIntegration.BizTalk.sln /p:Configuration=Release
```

Sample interchanges (X12, EDIFACT, gateway XML) are in `samples/inbound`, master data in `samples/seed`.

## Configuration

Environment values (DEV, LAB, TEST, PROD) are kept in `deploy/EnvironmentSettings/SettingsFileGenerator.xml`
(XML Spreadsheet 2003; edit in Excel). A blank cell takes the default column.

| Where | What |
| --- | --- |
| `deploy/PortBindingsMaster.xml` | Receive ports, send ports and orchestration bindings with `${Setting}` tokens and `ifdef` blocks |
| SSO application `VanArsdel.Integration` | Runtime settings of BizTalk code (workbook rows starting with `SSO_`, plus `ErpClientSecret`) |
| `Web.config` / `Web.Lab.config` / `Web.Prod.config` | Gateway, operations portal and tracking site; the deployment fills in the environment values |
| `VanArsdel.Integration.SlaMonitor.exe.config` | SLA monitor (interval, business hours, recipients) |
| `deploy/secrets.template.json` | Names of the secrets the deployment needs (ERP client secret, SFTP/FTP passwords, SQL logins, app pool and service accounts, machine keys) |
| `B2B_LOG_ROOT` | Machine environment variable with the log folder of all applications |
| Database `VanArsdelIntegration` | Partners, cross references, SLA definitions, notification groups and holidays (`deploy/db/010_Seed.sql`) |

## Deploy

On the first BizTalk server of the group, from an elevated PowerShell prompt, as a BizTalk and SSO
administrator and SQL Server sysadmin:

```
cd deploy
.\Deploy-B2BIntegration.ps1 -Environment PROD -SecretsFile \\b2bfs01\deploy$\vault\prod-secrets.json
```

The script exports the settings, then runs the steps in `deploy/scripts` in this order (`-Steps` runs a
subset):

| Step | Script | What it does |
| --- | --- | --- |
| Environment | `Set-B2BEnvironment.ps1` | `B2B_LOG_ROOT`, log folders; on DEV and LAB the `b2b$` share and its folders |
| Dns | `Install-Dns.ps1` | CNAME records for the AS2 host, the public B2B host and the operations portal |
| Database | `Install-Database.ps1` | `deploy/db` scripts: schema, procedures, roles and logins, purge job, seed |
| Sso | `Install-SsoSettings.ps1` | SSO application `VanArsdel.Integration` (`b2bdeploy sso`) |
| BizTalk | `Install-BizTalkApplication.ps1` | Bindings expansion and check, Deployment Framework `Deploy` target (`VanArsdel.Integration.Deployment.btdfproj`), partner passwords, host instance restarts |
| Rules | `Install-BusinessRules.ps1` | Imports, publishes and deploys the policies in `rules` |
| Bam | `Install-Bam.ps1` | `bm.exe deploy-all`/`update-all`, view accounts, online window, reporting views in BAMPrimaryImport |
| Parties | `Install-Parties.ps1` | Parties and agreement aliases from `partners.json` (`b2bdeploy parties`) |
| Websites | `Install-Websites.ps1` | Gateway and operations portal on the web server |
| Tracking | `Install-TrackingSite.ps1` | Tracking site on the DMZ web server, encrypted connection strings and machine key |
| SlaMonitor | `Install-SlaMonitor.ps1` | SLA monitor service, account and recovery options |

The deployment log is written to `%B2B_LOG_ROOT%\deploy`. The secrets file is deleted after the run unless
`-KeepSecretsFile` is given.

## Lab tool

`b2blab` runs the receive and send pipelines, maps and orchestration flows of the solution against simulated
partners and a simulated ERP, without a BizTalk group. It is used on LAB together with the `UsePartnerFolders`
setting, which turns the partner SFTP/FTP locations into FILE locations on the lab share.

```
dotnet run --project tools/VanArsdel.Integration.LabStubs -- e2e
dotnet run --project tools/VanArsdel.Integration.LabStubs -- pipeline FileEdiReceive samples/inbound/C1002_850_BF-2026-0311.x12
dotnet run --project tools/VanArsdel.Integration.LabStubs -- map X12_00401_850_to_Order <xml file>
dotnet run --project tools/VanArsdel.Integration.LabStubs -- serve --port 8090
```

| Command | What it does |
| --- | --- |
| `e2e` | Two business days of traffic: orders from all channels, credit holds, ERP confirmations, despatches, invoices, purchase orders, acknowledgements, a missing acknowledgement and the SLA evaluation; prints the checks and exits with 1 when one fails |
| `pipeline <pipeline> <file>` | Runs a receive pipeline (`--as2-from`, `--file-name` set the context) and prints the messages and generated acknowledgements, or writes them to `--out <folder>` |
| `map <map> <xml file>` | Runs a map with its extension objects on an XML message, for example `message-1.xml` written by `pipeline --out` |
| `serve` | Hosts the ERP `OrderManagementService` (`/erp/OrderManagementService.svc`) and the AS2 endpoints (`/b2b/as2`, `/partners/{as2id}/as2`) |

The work folder (partner folders, ERP inbound/outbound, mail, archive, suspended messages and `lab.log`) is
created under the system temp directory unless `--work` is given.

## Run

- Operations portal: `https://b2bops.corp.local/`
- Tracking site: `https://b2b.vanarsdel.com/tracking/` (reverse proxy to `B2BDMZ01`)
- Partner order gateway: `https://b2b.vanarsdel.com/partners/PartnerOrderService.svc`
- AS2: `https://b2b-as2.vanarsdel.com/B2BAS2/BTSHTTPReceive.dll`
- SLA monitor: `Start-Service VanArsdelB2BSlaMonitor`
- BizTalk: BizTalk Server Administration console, application `VanArsdel.Integration`
- Logs: `%B2B_LOG_ROOT%\<application>`
