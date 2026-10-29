# Account Statements

Customer statements and receivables reporting for Woodgrove Leasing. The ERP loads the
accounts receivable ledger, invoices and lease contracts into the `AccountStatements`
database every night. Credit control and finance use the portal to look up customers,
create statements and invoice copies and open the aged debt and portfolio reports. The
distribution service runs report subscriptions and the monthly statement run: every
customer gets a statement by e-mail, in their folder on the statement share, or as a
printed letter in the print pack for the print room. The ERP and the CRM fetch
statements and invoice copies from the document API.

| Component | Description | Host |
| --- | --- | --- |
| `AccountStatements.Web` | Portal (ASP.NET MVC 5, .NET Framework 4.8): customers, statements, invoice copies, report catalog, report viewer page, subscriptions, delivery problems, API clients | `statements.<zone>` (IIS site `statements`) |
| `AccountStatements.Api` | Document API (ASP.NET Web API 2, .NET Framework 4.8): statements, invoice copies and reports on request, customer balance; `X-Api-Key` header | `statements-api.<zone>` (IIS site `statements-api`) |
| `AccountStatements.Distribution` | Windows service `WoodgroveStatementDistribution` (.NET Framework 4.8): subscription schedules, statement runs, print packs, e-mail and share delivery, retries | `statements-dist.<zone>` |
| `AccountStatements.Reporting` | Report definitions (`Reports\*.rdlc`) and rendering, Excel workbooks, print packs, bank barcodes (.NET Framework 4.8) | |
| `AccountStatements.Data` | Data access (.NET Framework 4.8, Dapper) | |
| `AccountStatements.Core` | Statements, aging, schedules, subscription parameters, file names, API keys, bank barcode (.NET Standard 2.0) | |
| SQL Server | Database `AccountStatements` | `sql01.<zone>` |
| File server | Shares `Statements$`, `StatementArchive$`, folder `Finance$\AR\Daily` | `files01.<zone>` |
| Mail relay | SMTP, Windows authentication | `smtp.<zone>` |

The default configuration uses the zone `corp.local` and the domain `CORP`. All names
can point to the same server.

## Reports

| Code | Report | Formats |
| --- | --- | --- |
| `STATEMENT` | Customer account statement with open items, aging and payment slip (Finnish, Swedish or English by customer language) | PDF, Excel, Word |
| `STATEMENT-RUN` | Monthly statement run for all customers (subscriptions only) | PDF |
| `INVOICE` | Invoice copy with payment slip and bank barcode | PDF |
| `AGED-DEBT` | Aged debt by credit controller, drillthrough to the customer statement | PDF, Excel |
| `PORTFOLIO` | Lease portfolio by asset category and maturity | PDF, Excel |
| `TRANSACTIONS` | Ledger transactions workbook | Excel |

## Requirements

- Windows Server 2019 or later with IIS (ASP.NET 4.8, Windows Authentication) for the portal and the API
- .NET Framework 4.8 on the web server and the distribution server
- SQL Server 2016 or later
- Active Directory groups `STM-Users`, `STM-CreditControl`, `STM-Admins` (portal roles)
- Service accounts: `svc-stm-web` (portal pool), `svc-stm-api` (API pool), `svc-stm-dist` (distribution service)
- SMTP relay that accepts the distribution service account
- Build: .NET 8 SDK and Visual Studio 2022 or Build Tools with the ASP.NET and web development workload

## Build and test

```
dotnet build AccountStatements.sln
dotnet test tests\AccountStatements.Tests
```

## Database

```
deploy\Initialize-Database.ps1 -SqlServer sql01.corp.local -PortalAccount CORP\svc-stm-web -ApiAccount CORP\svc-stm-api -DistributionAccount CORP\svc-stm-dist
```

`db\03-reference-data.sql` loads the company profile, holidays, twelve test customers with
contracts, a ledger for January to October 2026 and the standard subscriptions; leave it
out with `-SkipReferenceData`.

## Deploy

1. DNS records (on the DNS server):
   ```
   deploy\Install-DnsRecords.ps1 -ZoneName corp.local -PortalIP <ip> -ApiIP <ip> -DistributionIP <ip>
   ```
2. Publish on the build machine:
   ```
   deploy\Publish.ps1 -OutputPath C:\Build\AccountStatements
   ```
3. Shares (on the file server):
   ```
   deploy\New-StatementShares.ps1 -DistributionAccount CORP\svc-stm-dist -PortalAccount CORP\svc-stm-web -CreditControlGroup CORP\STM-CreditControl -PrintRoomGroup CORP\PrintRoom
   ```
4. Web sites (on the web server), files under `C:\inetpub\sites\statements` and `C:\inetpub\sites\statements-api`:
   ```
   deploy\Install-WebSites.ps1 -PublishPath C:\Build\AccountStatements -ZoneName corp.local -PortalPoolAccount CORP\svc-stm-web -ApiPoolAccount CORP\svc-stm-api
   ```
5. Distribution service (on the distribution server):
   ```
   deploy\Install-DistributionService.ps1 -PublishPath C:\Build\AccountStatements -ServiceAccount CORP\svc-stm-dist
   ```
   On a test system send customer e-mails to one mailbox with
   `-Settings @{ RedirectCustomerMailTo = 'test@woodgrove-leasing.fi' }`.

## Run

- Portal: `https://statements.corp.local` (Windows sign-in). Customer search, statements in
  PDF, Excel or Word, invoice copies, reports, subscriptions. Create API keys under API clients
  (role `STM-Admins`).
- Document API:
  ```
  curl -H "X-Api-Key: wgs_erp_..." https://statements-api.corp.local/api/documents/invoice/70100190 -o invoice.pdf
  curl -H "X-Api-Key: wgs_erp_..." -H "Content-Type: application/json" -d "{\"customerNo\":\"C10005\",\"statementDate\":\"2026-09-30\"}" https://statements-api.corp.local/api/documents/statement -o statement.pdf
  curl -H "X-Api-Key: wgs_crm_..." https://statements-api.corp.local/api/customers/C10005/balance
  curl https://statements-api.corp.local/api/health
  ```
- Distribution service commands (as the service account or an administrator):
  ```
  AccountStatements.Distribution.exe console          poll loop in a console window
  AccountStatements.Distribution.exe run 1            run subscription 1 now
  AccountStatements.Distribution.exe retry            resend failed deliveries now
  ```
  Logs: `D:\AccountStatements\Logs` and the Application event log (source `Woodgrove Statements`).

### Subscription schedules and parameters

Schedules: `DAILY 05:30`, `WORKDAYS 06:00`, `WEEKLY MON,THU 07:30`, `MONTHLY 1 06:00`,
`MONTHLY LAST 18:00`, `MONTHLY WD3 06:00` (third working day, holidays from `stm.Holiday`).

Parameters: `Name=Value;Name=Value`, with date tokens `{Today}`, `{Yesterday}`,
`{MonthStart}`, `{PrevMonthStart}`, `{PrevMonthEnd}`, `{WeekStart}`, `{PrevWeekStart}`,
`{YearStart}`, `{QuarterStart}`, `{PrevQuarterEnd}` and offsets such as `{Today-7}`.

File names: `{RunDate:yyyyMMdd}`, any parameter, and in statement runs `{CustomerNo}` and
`{StatementDate:yyyyMMdd}`.
