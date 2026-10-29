# Consumer Loans

Consumer lending for **Lucerne Savings Bank**: online loan applications, credit decisions,
offers and e-signed agreements, loan booking and payout in the core banking system, collections
of overdue loans, general ledger journals and the monthly loan tape for the group risk warehouse.

Customers apply in the online loan service. Applications are scored automatically (credit bureau
report, core banking customer data, affordability, credit policy rules and scorecard); referred
applications are decided by underwriters in the back office. Accepted offers are signed with bank
credentials through the e-signature provider, booked as loan accounts in the core banking system
on **IBM i** (RPG programs and DB2 for i) and paid out to the customer's account. Loan events from
the core end-of-day (due, missed and paid instalments, days past due) drive the collections process:
reminder letters and fees, payment plans and promises, termination notices and hand-over to the
collection agency.

## Components

| Project | Host | Description |
| --- | --- | --- |
| `src/Lucerne.Lending.CustomerPortal` | `apply.corp.local` (IIS) | Online loan service: sign-in with ASP.NET Core Identity and an authenticator app, Vue 2 app (`ClientApp`, Vite) with loan calculator, application, offer acceptance, signing link, my applications and my loans; server side calls to the lending API |
| `src/Lucerne.Lending.Api` | `lending-api.corp.local` (IIS) | Lending API (API keys, Swagger): products and calculator, applications, offers, documents, customer loans and payments, e-signature callback; background scoring and booking |
| `src/Lucerne.Lending.BackOffice` | `lending-office.corp.local` (IIS) | Back office (MVC, ADFS sign-in with WS-Federation): referral queue, application file with core customer view, manual decisions, booking, collections cases, customer search |
| `src/Lucerne.Lending.Documents` | `lending-app01.corp.local` (Windows service `LucerneLendingDocuments`) | Pre-contract information (SECCI), agreements with repayment schedule, decline letters, collection letters (PDF); e-signature orders and completion; customer e-mails |
| `src/Lucerne.Lending.Collections` | `lending-app01.corp.local` (Windows service `LucerneLendingCollections`) | Core loan event listener (data queue `LNLIB/LNEVTQ`), daily collection stages, payment plan checks, agency hand-over file, GL journal export |
| `src/Lucerne.Lending.LoanTape` | `lending-app01.corp.local` (scheduled task) | Monthly loan tape to the risk warehouse (DB2 LUW `RISKDW`, schema `LOANTAPE`) |
| `src/Lucerne.Lending.Workflow` | | Application, scoring, underwriting, booking and collections services |
| `src/Lucerne.Lending.Decisioning` | | Credit policy rules (`Rules/credit-policy.json`, RulesEngine) and scorecard |
| `src/Lucerne.Lending.CoreBanking` | | Core banking interfaces, program parameter layouts and return codes |
| `src/Lucerne.Lending.CoreBanking.IbmI` | | Core banking access through IBM i Access Client Solutions: program calls and data queue (cwbx), DB2 for i SQL (ODBC driver) and transaction history (IBMDA400 OLE DB provider) |
| `src/Lucerne.Lending.CoreBanking.Simulator` | | Core banking simulator (SQLite) for development, training and the lab |
| `src/Lucerne.Lending.Data` | | Lending database (Dapper; SQL Server, SQLite in development), DbUp scripts |
| `src/Lucerne.Lending.Integration` | | Credit bureau (SOAP), e-signature (REST), payments hub, GL journal file, mail |
| `src/Lucerne.Lending.Domain` | | Products, application statuses, personal identity code, IBAN, payment references, annuity, APR, pricing, collections policy |
| `src/Lucerne.Lending.Contracts` | | API models |
| `tools/Lucerne.Lending.LabStubs` | `lending-lab.corp.local` (lab only) | Lab stand-ins for the credit bureau, e-signature service and payments hub, plus the core simulator operator page (end of day, payments) |
| `tests/Lucerne.Lending.Tests` | | xUnit tests |
| `legacy/core` | | IBM i sources of the lending programs in `LNLIB`: RPG (`CUSTINQ`, `CUSCRT`, `LNBOOK`, `LNDISB`, `LNFEE`, `LNPPLN`, `LNTERM`, `LNEOD`), DDS, CL and SQL procedures |
| `deploy` | | DNS, publish, SQL Server, IBM i Access, IIS, Windows service and scheduled task scripts; DB2 LUW DDL for the loan tape |

Other systems:

| Name | Purpose |
| --- | --- |
| `core.corp.local` | IBM i 7.4 or later, core banking: customer master `CIFLIB`, deposit accounts `DPLIB`, reference rates `RATLIB`, consumer loans `LNLIB` (programs, `LNMSTP`, `LNTRNP`, data queue `LNEVTQ`, SQL procedures `LN_CUSTOMER_LOANS`, `LN_LOAN_STATUS`, `LN_EXPOSURE_SUMMARY`); user profiles `LNWEB` and `LNCOLL` mapped from the Windows service accounts with Kerberos/EIM |
| `sql01.corp.local` | SQL Server 2019 or later, databases `ConsumerLending` and `ConsumerLendingPortal`, SQL Server authentication |
| `riskdw.corp.local` | DB2 LUW 11.5 group risk warehouse, database `RISKDW` |
| `adfs.corp.local` | ADFS 2019, relying party `https://lending-office.corp.local/` with role claims for the `LND-*` groups |
| `ws.nordiccredit.example` | Credit bureau consumer report service (SOAP 1.1) |
| `api.signhub.example` | E-signature service (REST, HMAC-signed callbacks) |
| `payhub.corp.local` | Payments hub (outgoing SEPA transfers) |
| `smtp.corp.local` | SMTP relay for customer e-mails |

### Application flow

1. The customer signs in to `apply` and sends the application. The portal passes it to
   `POST /api/applications` with its API key and the customer's identity code.
2. The scoring worker in the API fetches the credit bureau report and the core customer data
   (`CUSTINQ`, `LN_EXPOSURE_SUMMARY`, accounts), runs the credit policy and scorecard and either
   declines, refers to the back office or issues an offer priced from the reference rate (`RATLIB/REFRATE`).
3. The customer accepts the offer and gives the payout account. The documents service creates the
   agreement and the signing order; the customer signs with bank credentials.
4. After the signing callback the booking worker creates the core customer if needed (`CUSCRT`),
   books the loan (`LNBOOK`), pays it out (`LNDISB`) and writes the GL entries. Amounts over
   `Lending:AutoBookingLimit` are booked from the back office.
5. The core end-of-day (`LNEOD`) writes loan events to `LNLIB/LNEVTQ`; the collections service opens
   and closes cases, charges reminder fees (`LNFEE`), registers payment plans (`LNPPLN`) and
   terminates loans after the final demand (`LNTERM`).

## Requirements

Build:

- .NET SDK 6.0.4xx or later (projects target .NET 6)
- Node.js 18 or later (customer portal app, built by `dotnet publish` of the portal)

Servers:

- Windows Server 2019 or later in the `CORP` domain
- Web servers (`apply`, `lending-api`, `lending-office`): IIS 10, ASP.NET Core 6 Hosting Bundle,
  a certificate for the host names
- Application server (`lending-app01`): .NET 6 Runtime, service account (e.g. `CORP\svc-lending-app`)
- On `lending-api`, `lending-office` and `lending-app01`: **IBM i Access Client Solutions – Windows
  Application Package** 1.1.9 or later (ODBC driver, OLE DB provider, programming toolkit),
  installed with `deploy\Install-IbmIAccess.ps1`; network access to `core.corp.local` ports 449, 8471, 8472, 8475
- SQL Server 2019 or later on `sql01` (SQL Server Express is enough for the lab)
- DB2 LUW client access to `riskdw.corp.local` for the loan tape (IBM Data Server Driver, included in the
  `Net.IBM.Data.Db2` package)

## Build and test

```
dotnet build ConsumerLoans.sln
dotnet test ConsumerLoans.sln
cd src\Lucerne.Lending.CustomerPortal\ClientApp
npm ci
npm run build
```

`npm run build` writes the customer app to `src/Lucerne.Lending.CustomerPortal/wwwroot/app`.

## Run in development

Development settings use SQLite databases, the core banking simulator and the lab stand-ins; data
goes to `App_Data` in the scenario folder (created on first start). Start each project from its folder:

```
cd tools\Lucerne.Lending.LabStubs        && dotnet run        # http://localhost:5109 (core simulator page: /core)
cd src\Lucerne.Lending.Api               && dotnet run        # http://localhost:5102 (Swagger: /swagger)
cd src\Lucerne.Lending.Documents         && dotnet run
cd src\Lucerne.Lending.Collections       && dotnet run
cd src\Lucerne.Lending.BackOffice        && dotnet run        # http://localhost:5103
cd src\Lucerne.Lending.CustomerPortal    && dotnet run        # http://localhost:5101
```

- Online loan service: sign in as `aino.virtanen@example.fi`, `mikko.korhonen@example.fi`,
  `elina.heikkinen@example.fi` (not yet a bank customer) or `ville.lehtonen@example.fi`, password
  `Lucerne-2024`, or create an account. Authenticator app sign-in is optional in development.
- Back office: development sign-in with a user name and role check boxes (`Auth:Mode = Development`).
- Signing: the signing link in the portal opens the lab e-signature page; "Identify with test bank and sign"
  completes the order and posts the callback.
- Core end of day: `POST http://localhost:5109/core/eod?days=10` or the form on `/core`; payments with
  `POST /core/payments?account=...&amount=...`.
- Collections jobs on demand: `dotnet run -- run daily-stages` (also `payment-plans`, `gl-export`,
  `agency-handover`) in `src\Lucerne.Lending.Collections`.
- Loan tape: `dotnet run -- --date 2026-10-31` in `src\Lucerne.Lending.LoanTape` (development target is a CSV folder).
- E-mails are written as `.eml` files to `App_Data/mail`, interface files to `App_Data/interfaces`.

Training identity codes in the simulator and bureau stand-in:

| Identity code | Person | Notes |
| --- | --- | --- |
| 150385-2481 | Aino Virtanen | Customer, good history |
| 020790-317C | Mikko Korhonen | Customer |
| 110601A139N | Juha Mäkinen | Customer, young |
| 300888-562M | Sanna Hämäläinen | Payment defaults at the bureau |
| 051259-2152 | Pekka Laine | Customer, aged 67 |
| 190492-6440 | Elina Heikkinen | Not a customer |
| 080883-3776 | Tuomas Koskinen | Customer blocked in core, credit block |
| 271095-482J | Kaisa Järvinen | KYC review due |
| 140772-159C | Ville Lehtonen | Loan 90+ days past due |
| 090397-5289 | | Debt restructuring, not a customer |

## Deploy

All scripts are in `deploy` and read their settings from `environment.psd1` (production) or
`lab.psd1` (single server lab with the core simulator and the lab stand-ins). Passwords and keys are
passed in a separate secrets file; copy `secrets.example.psd1` outside the repository and fill it in.
Run the scripts in an elevated PowerShell 5.1 or 7 session.

1. DNS records: `.\Register-Dns.ps1 -Config .\environment.psd1`
2. Publish: `.\Publish-All.ps1` (add `-IncludeLabStubs` for the lab) – output in `.\out`
3. Databases and logins on `sql01`: `.\Install-Database.ps1 -Config .\environment.psd1 -Secrets D:\Deploy\consumer-loans-secrets.psd1`
4. IBM i Access on the web and application servers: `.\Install-IbmIAccess.ps1` (`-CheckOnly` to verify only)
5. Web sites on the web servers: `.\Install-WebSites.ps1 -Secrets ...` (`-Sites apply,lending-api` to install a subset)
6. Services and the loan tape task on `lending-app01`: `.\Install-AppServices.ps1 -Secrets ...`

`lending-api` and the portal create or upgrade their database schema on start (`Database:Upgrade`).
The scripts write `appsettings.Production.json` next to each application; the service accounts get
the *Log on as a service* and *Log on as a batch job* rights. Core banking objects are created on
IBM i from `legacy/core` (`LNSETUP` and `LNBUILD` CL programs, source files `QDDSSRC`, `QRPGLESRC`,
`QSQLSRC`, `QCLSRC` in `LNLIB`); the loan tape tables are created by the risk warehouse team from
`deploy/db2/riskdw-loantape.sql`.

Lab on one server:

```
.\Register-Dns.ps1 -Config .\lab.psd1
.\Publish-All.ps1 -IncludeLabStubs
.\Install-Database.ps1 -Config .\lab.psd1 -Secrets D:\Deploy\consumer-loans-secrets.psd1
.\Install-WebSites.ps1 -Config .\lab.psd1 -Secrets D:\Deploy\consumer-loans-secrets.psd1
.\Install-AppServices.ps1 -Config .\lab.psd1 -Secrets D:\Deploy\consumer-loans-secrets.psd1
```

The lab sites are `https://apply.corp.local`, `https://lending-api.corp.local/swagger`,
`https://lending-office.corp.local` and `https://lending-lab.corp.local/core`.

## Configuration

| Setting | Application | Meaning |
| --- | --- | --- |
| `Core:Provider` | API, back office, collections | `IbmI` (core banking system) or `Simulator` |
| `Core:SystemName`, `Core:UseKerberos`, `Core:MaxConnections` | API, back office, collections | IBM i system and sign-on; connections per process for program calls |
| `Core:OdbcConnectionString`, `Core:OleDbConnectionString` | API, back office, collections | DB2 for i SQL access |
| `Core:SimulatorDatabase` | all with the simulator, lab stand-ins | SQLite file of the core simulator |
| `Lending:AutoBookingLimit`, `Lending:SeniorApprovalLimit` | API, back office | Booking without back office review; decisions needing a senior underwriter |
| `Lending:RunBackgroundWorkers` | API | Scoring and booking workers in the API process |
| `Bureau:*`, `ESign:*`, `PaymentsHub:*` | API, documents, collections | External services |
| `Portal:RequireTwoFactor` | portal | Authenticator app required for the online service |
| `LoanTape:Target` | loan tape | `Db2` (risk warehouse) or `Folder` (CSV) |
