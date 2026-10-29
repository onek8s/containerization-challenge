# Expense Claims

Travel and expense claims for Lamna Healthcare. Employees enter their expenses with receipts and add
purchases made with their company card, line managers approve, finance checks the claims, and the
nightly interfaces pay employees through the bank and send the bookings to the ERP general journal.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/expenses-web` | `expenses.corp.local` (IIS, static site) | Angular 15 front end. Sign-in with ADFS (OpenID Connect, code flow with PKCE) |
| `src/ExpenseClaims.Api` | `expenses-api.corp.local` (IIS) | ASP.NET Core 6 Web API: claims, lines, receipts, approvals, finance check, company cards. JWT bearer tokens from ADFS |
| `src/ExpenseClaims.Jobs` | `expenses-jobs.corp.local` (IIS) | ASP.NET Core 6 host running the Quartz.NET scheduled jobs, with a job status page |
| `src/ExpenseClaims.Data` | | EF Core 6 model and migrations |
| `src/ExpenseClaims.Core` | | Expense policy (HR-POL-07), approval routing, currency conversion, VAT, journal lines, SEPA payment file |
| `tests/ExpenseClaims.Tests` | | Unit, job and API tests |
| `db` | `expenses-sql.corp.local` | Database, logins, Quartz tables and schema scripts |
| `deploy` | | DNS, database, IIS and ADFS scripts |
| `samples` | | Sample HR export, card transaction file and ECB rates |

### Claim workflow

| Status | Next step | By |
| --- | --- | --- |
| Draft / Returned | Send for approval (no policy errors) | Employee |
| Submitted | Approve, return or reject. Claims of 2 500 EUR or more need the line manager and their manager | Approver |
| Approved | Check (bank account required) or return | Finance |
| Checked | Payment run (13:00 on weekdays), accounting export (18:30) | Jobs |
| InPayment | Bank accepts the payment file | Jobs |
| Paid | | |

Lines paid with a company card are booked against the card clearing account and are not paid to the employee.

### Scheduled jobs

| Job | Default schedule (Europe/Helsinki) | Description |
| --- | --- | --- |
| `ExchangeRates` | 17:30 Mon-Fri | ECB euro reference rates (last 90 days) through the proxy |
| `HrEmployeeImport` | 05:15 daily | Employee register from the HR export `employees_*.csv` (full snapshot) |
| `CardTransactionImport` | 06:00 daily | Card company's transaction files `TRX_*.csv` |
| `ApproverReminders` | 08:00 Mon-Fri | Mail to approvers with claims waiting more than 3 days |
| `PaymentRun` | 13:00 Mon-Fri | SEPA credit transfer file (pain.001.001.03), one payment per employee, uploaded to the bank gateway |
| `PaymentStatus` | every 15 min 7-20 Mon-Fri | Re-sends undelivered files, completes claims when the bank accepts or rejects a file |
| `AccountingExport` | 18:30 Mon-Fri | Journal lines (expense accounts with cost center and project, VAT, employee payable, card clearing) to the Dynamics NAV journal batch `EXPCLAIMS` over OData |

Schedules are cron expressions in `Jobs:<name>:Cron`; `off` disables a job. On the jobs server a job can be
started immediately with `Invoke-RestMethod -Method Post http://localhost/jobs/<name>/run -Headers @{ Host = 'expenses-jobs.corp.local' }`.

### Interfaces

| Interface | Direction | Format | Location |
| --- | --- | --- | --- |
| HR system | in | CSV `;`, UTF-8 with BOM, see `samples/hr` | `D:\Interfaces\HR\Inbound` (processed files to `Processed`, failed to `Failed`) |
| Card company | in | CSV `;`, Windows-1252, Finnish number and date format, see `samples/cards` | `D:\Interfaces\Cards\Inbound` |
| ECB | in | eurofxref XML | `https://www.ecb.europa.eu/stats/eurofxref/` |
| Bank | out | pain.001.001.03 over HTTPS with client certificate, status by file reference | `Bank:GatewayUrl`, copies in `D:\Interfaces\Bank\Outbound` |
| Dynamics NAV 2018 | out | OData V4 page web service `ExpenseJournal`, Windows authentication | `Erp:ODataUrl` |
| Mail | out | SMTP | `smtp.corp.local:25` |

## Requirements

Build:

- .NET 6 SDK
- Node.js 16 or 18 and npm

Servers (Windows Server 2019 or later):

- `expenses`: IIS with Static Content and URL Rewrite 2.1
- `expenses-api`: IIS, ASP.NET Core 6 Hosting Bundle, local folders for receipts (`D:\ExpenseReceipts`) and logs (`D:\Logs\ExpenseClaims`)
- `expenses-jobs`: IIS with Application Initialization, ASP.NET Core 6 Hosting Bundle, interface folders under `D:\Interfaces`,
  the bank's client certificate with private key in `LocalMachine\My`, access to `proxy.corp.local:8080`, `smtp.corp.local` and `nav01.corp.local:7048`
- `expenses-sql`: SQL Server 2017 or later with SQL Server authentication enabled
- `adfs.corp.local`: AD FS 2019. See `deploy\new-adfs-application.ps1`; for a lab AD FS can run on the domain controller
- Server certificates for `expenses.corp.local`, `expenses-api.corp.local` (and `expenses-jobs.corp.local` if HTTPS is wanted)
- Active Directory groups for the Finance and Administrator roles, for example `CORP\EXP-Finance` and `CORP\EXP-Administrators`
- Dynamics NAV: page web service `ExpenseJournal` over the general journal, journal batch `GENERAL`/`EXPCLAIMS`, dimensions 1 = cost center and 2 = project,
  and a NAV user for the jobs host's pool identity

Without AD FS, the API and the front end can run with development sign-in (test users chosen from the employee register).

## Build and test

```
dotnet build ExpenseClaims.sln -c Release
dotnet test ExpenseClaims.sln

cd src\expenses-web
npm ci
npm run build
```

## Run locally

Development configuration uses a SQLite file under `App_Data`, development sign-in with seeded test users,
and folders under `App_Data` for receipts, interfaces, mail (`.eml` files) and payment files.

```
dotnet run --project src\ExpenseClaims.Api --launch-profile ExpenseClaims.Api       # http://localhost:5241/swagger
dotnet run --project src\ExpenseClaims.Jobs                                          # http://localhost:5251/
cd src\expenses-web && npm start                                                      # http://localhost:4200/
```

`src\expenses-web\src\assets\config.json` points the front end to the API and selects the sign-in mode. Test users:
Aino Laine (employee, company card), Jari Mäkinen (her manager), Sanna Koskinen, Helena Lindqvist,
Mikko Salonen (Finance), Laura Niemi (Finance, Administrator). Copy the files from `samples\hr` and `samples\cards`
to `src\ExpenseClaims.Jobs\App_Data\hr` and `...\App_Data\cards` and run the import jobs to load them; the sample
transactions are dated October 2022 and are older than the 90 days the policy allows.

## Deploy

Run from an elevated PowerShell, in this order.

1. DNS (on a DNS server). For a single test server point all names at it:

   ```
   .\deploy\register-dns.ps1 -IPAddress 10.10.0.31
   ```

2. AD FS application group (on the AD FS server):

   ```
   .\deploy\new-adfs-application.ps1 -FinanceGroup 'CORP\EXP-Finance' -AdministratorGroup 'CORP\EXP-Administrators'
   ```

   Note the client id it prints.

3. Database (`SqlServer` PowerShell module):

   ```
   .\deploy\install-database.ps1 -SqlInstance expenses-sql.corp.local `
       -AppPassword (Read-Host -AsSecureString 'expenses_app') -JobsPassword (Read-Host -AsSecureString 'expenses_jobs')
   ```

4. API (`expenses-api`):

   ```
   .\deploy\install-api.ps1 -SqlPassword (Read-Host -AsSecureString 'expenses_app') -CertificateThumbprint <expenses-api cert>
   ```

5. Jobs host (`expenses-jobs`):

   ```
   .\deploy\install-jobs.ps1 -SqlPassword (Read-Host -AsSecureString 'expenses_jobs') `
       -BankCertificateThumbprint <bank client cert> -BankCustomerId <customer id> `
       -DebtorIban FI7515723500050136 -DebtorBic OKOYFIHH -DebtorOrganisationId 2468135-7
   ```

   Without a bank gateway agreement use `-BankMode Folder`: payment files are only written to `D:\Interfaces\Bank\Outbound`.

6. Front end (`expenses`):

   ```
   .\deploy\install-web.ps1 -ClientId <client id from step 2> -CertificateThumbprint <expenses cert>
   ```

7. After the first HR import, link the company cards in the portal (Company cards) or run `db\04-reference-data.sql`.

Lab installation without AD FS and HTTPS:

```
.\deploy\install-api.ps1 -SqlPassword $pw -AuthMode Development -SpaOrigin http://expenses.corp.local -TrustServerCertificate
.\deploy\install-jobs.ps1 -SqlPassword $pw2 -BankMode Folder -TrustServerCertificate
.\deploy\install-web.ps1 -AuthMode development -ApiUrl http://expenses-api.corp.local
```

### Configuration

| File | Setting | Description |
| --- | --- | --- |
| API `appsettings.Production.json` | `ConnectionStrings:ExpenseClaims` | SQL login `expenses_app` |
| | `Authentication:Adfs` | Authority, audience (API identifier) and accepted issuers |
| | `Receipts:RootPath` | Receipt folder |
| | `Policy`, `Accounting:VatRates` | Rates and limits of HR-POL-07, deductible VAT by category |
| Jobs `appsettings.Production.json` | `ConnectionStrings:ExpenseClaims` | SQL login `expenses_jobs`, also used by the Quartz job store |
| | `Quartz:TimeZone` | Time zone of the cron schedules (`FLE Standard Time`) |
| | `Bank`, `Erp`, `Smtp`, `Proxy`, `HrImport`, `CardImport` | Interfaces |
| | `Accounting` | G/L accounts |
| Front end `assets\config.json` | `apiUrl`, `auth` | API address and sign-in |

Logs: `D:\Logs\ExpenseClaims\api-*.log` and `jobs-*.log` (Serilog, daily files, 60 days).
