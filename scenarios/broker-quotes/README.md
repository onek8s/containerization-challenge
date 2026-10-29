# Broker Quotes

Online quoting and policy administration for **Humongous Insurance**, a managing general agent
that writes *Business Combined* cover (material damage, business interruption, employers'
liability, public and products liability) for small businesses through independent brokers under
a binding authority from Lucerne Capacity.

Brokers quote, refer and bind cover in the broker portal. Referred quotes are handled by
underwriters in the underwriting workbench. Policies, mid-term changes, cancellations, renewal
invitations, documents and the monthly premium bordereau are processed by two background services.
Premiums are rated by **RateCore**, the pricing team's rating component, with the rate tables
signed off by Pricing for each product version.

## Components

| Project | Host | Description |
| --- | --- | --- |
| `src/Humongous.Identity` | `login.corp.local` (IIS) | Sign-in for brokers and staff: OpenID Connect provider (OpenIddict 5) on ASP.NET Core Identity, user administration |
| `src/Humongous.BrokerPortal` | `brokers.corp.local` (IIS) | Broker portal: React 18 + TypeScript app (`ClientApp`, Vite) served by an ASP.NET Core host that signs the user in and forwards `/api` and `/hubs` to the quote API (YARP) |
| `src/Humongous.Workbench` | `workbench.corp.local` (IIS) | Underwriting workbench: Blazor Server (referral queue, decisions and loadings, quotes, policies, agencies, bordereaux, rating status) |
| `src/Humongous.Quoting.Api` | `quote-api.corp.local` (IIS, 32-bit application pool) | Quote API (minimal API, JWT bearer, Swagger), background rating queue, SignalR hub `/hubs/quotes` for live quote and policy updates |
| `src/Humongous.PolicyService` | `app01.corp.local` (Windows service `HumongousPolicyService`) | Binding and policy issue, mid-term changes, cancellations, renewal invitations, quote expiry, lapses, monthly premium bordereau (Excel, SFTP to Lucerne Capacity) |
| `src/Humongous.Documents` | `app01.corp.local` (Windows service `HumongousDocuments`) | Quote schedules, policy schedules, EL certificates, endorsements, cancellation notices and renewal invitations (PDF), e-mail to brokers |
| `src/Humongous.Quoting.Rating` | | RateCore interop (COM, single-threaded apartment worker threads) and the `Simulator` rating provider |
| `src/Humongous.Quoting.Domain` | | Products, trades, risk model and validation, referral rules, premium build-up (minimums, loadings, commission, IPT, fee), pro rata |
| `src/Humongous.Quoting.Data` | | EF Core 8 model (SQL Server; SQLite in development), Dapper queries for lists, dashboard, referral queue and bordereau |
| `src/Humongous.Quoting.Contracts` | | API models, bus messages and hub contract |
| `src/Humongous.Quoting.Messaging` | | MassTransit 8 on RabbitMQ |
| `tests/Humongous.Quoting.Tests` | | xUnit tests |
| `legacy/RateCore` | | RateCore source (Visual Basic 6 ActiveX DLL), release notes and rate table layout |
| `deploy` | | DNS, RateCore, IIS, Windows service, RabbitMQ and SQL scripts |

Other systems:

| Name | Purpose |
| --- | --- |
| `sql01.corp.local` | SQL Server 2019 or later, databases `HumongousQuoting` and `HumongousIdentity`, SQL Server authentication |
| `mq01.corp.local` | RabbitMQ 3.12, virtual host `humongous` |
| `smtp.corp.local` | SMTP relay for broker documents |
| `files01.corp.local` | Pricing release share `\\files01.corp.local\PricingRelease$\RateCore\<version>` (RateCore releases) |
| `sftp.lucernecapacity.example` | Capacity provider's SFTP server for bordereaux (key authentication) |

### Quote flow

1. The broker enters the risk in the portal; the portal host posts it to `POST /api/quotes` with the
   broker's access token. The API validates the risk and answers `202 Accepted` with the quote reference.
2. The rating queue in the API rates the quote with RateCore (rate tables of the product version in
   force at inception), applies the binding authority rules (`ReferralRules`), builds the premium and
   sets the quote to *Quoted*, *Referred* or *Declined*. The portal page updates through the hub.
3. Referred quotes appear in the workbench. An underwriter accepts (optionally with section loadings)
   or declines; quotes over the authority limit need a senior underwriter.
4. The broker binds a quoted or accepted quote. The API publishes `BindRequested`; the policy service
   issues the policy and asks the documents service for the schedule and, with EL cover, the EL certificate.

## Requirements

Build:

- .NET 8 SDK
- Node.js 20 or later (broker portal app, built by `dotnet publish` of the portal)

Servers:

- Windows Server 2019 or later in the `CORP` domain, with DNS
- Web server (`login`, `brokers`, `workbench`, `quote-api`): IIS 10 with WebSockets, ASP.NET Core 8
  Hosting Bundle (installs x64 and x86), .NET 8 Windows Desktop Runtime **x86**, a certificate for the four host names
- Application server (`app01`): .NET 8 Runtime x64 and x86, .NET 8 Windows Desktop Runtime **x86**,
  service account (e.g. `CORP\svc-hq-app`)
- RateCore on `quote-api` and `app01`: a RateCore release from the pricing release share, installed
  with `deploy\Install-RateCore.ps1` (needs the 32-bit Windows components msvbvm60.dll, dao360.dll and scrrun.dll)
- SQL Server 2019 or later on `sql01`
- RabbitMQ 3.12 with Erlang/OTP 25 on `mq01`
- Signing and encryption certificates for the sign-in service in `LocalMachine\My` (the development
  setting uses temporary keys)
- SFTP key and host key fingerprint from Lucerne Capacity for the bordereau

## Build and test

```
dotnet build BrokerQuotes.sln
dotnet test BrokerQuotes.sln
```

The broker portal app:

```
cd src/Humongous.BrokerPortal/ClientApp
npm ci
npm run build        # output to ../wwwroot
```

## Run locally

In the `Development` environment the applications use a SQLite database in `App_Data/` (created and
filled with sample agencies by the quote API), the quote API and policy service use the `Simulator`
rating provider (flat indicative rates, tables `SIMULATOR`; not product premiums), documents are
written as `.eml` files to `App_Data/mail` and bordereaux to `App_Data/bordereaux`. RabbitMQ is
needed locally (`localhost`, user `guest`).

Start the quote API first; it creates the database.

```
dotnet run --project src/Humongous.Identity        # http://localhost:5300
dotnet run --project src/Humongous.Quoting.Api     # http://localhost:5310/swagger
dotnet run --project src/Humongous.PolicyService
dotnet run --project src/Humongous.Documents
dotnet run --project src/Humongous.BrokerPortal    # http://localhost:5320 (after npm run build)
dotnet run --project src/Humongous.Workbench       # http://localhost:5330
```

For work on the React app run `npm run dev` in `ClientApp` (http://localhost:5173, forwards to the portal host).

Development users (password `Humongous!2024`):

| User | Role |
| --- | --- |
| `broker@alpine-brokers.example` | Broker, agency ALP01 |
| `admin@alpine-brokers.example` | Broker and broker administrator, agency ALP01 |
| `broker@northbridge.example` | Broker, agency NWB02 |
| `uw1@humongousinsurance.example` | Underwriter |
| `suw@humongousinsurance.example` | Senior underwriter |
| `itadmin@humongousinsurance.example` | Administrator (user administration on the sign-in site) |

Run a policy service job once (development or server):

```
dotnet run --project src/Humongous.PolicyService -- run-job renewals
dotnet run --project src/Humongous.PolicyService -- run-job bordereau --period 2024-09
```

Jobs: `renewals` (daily 02:00), `quote-expiry` (hourly), `lapse` (daily 00:30), `bordereau` (1st of the month 06:00).

## Deploy

1. Publish (build machine):

   ```
   .\deploy\Publish-All.ps1 -Output D:\Drops\BrokerQuotes\5.8.0
   ```

2. DNS (on a DNS server):

   ```
   .\deploy\Register-Dns.ps1 -WebIPAddress <web> -AppIPAddress <app01> -SqlIPAddress <sql01> -MqIPAddress <mq01>
   ```

3. SQL Server (on `sql01`):

   ```
   sqlcmd -S sql01.corp.local -E -i deploy\sql\001-databases-and-logins.sql -v QuotingPassword="..." PolicyPassword="..." DocumentsPassword="..." IdentityPassword="..."
   sqlcmd -S sql01.corp.local -E -d HumongousQuoting -i deploy\sql\010-quoting-schema.sql
   sqlcmd -S sql01.corp.local -E -d HumongousIdentity -i deploy\sql\020-identity-schema.sql
   sqlcmd -S sql01.corp.local -E -d HumongousQuoting -i deploy\sql\030-reference-data.sql
   ```

4. RabbitMQ (on `mq01`):

   ```
   .\deploy\rabbitmq\Configure-RabbitMq.ps1 -Passwords @{ 'quote-api' = '...'; 'policy-service' = '...'; 'documents' = '...' }
   ```

5. RateCore (on the web server and on `app01`):

   ```
   .\deploy\Install-RateCore.ps1 -ReleasePath \\files01.corp.local\PricingRelease$\RateCore\4.12.318 -ReadAccess 'IIS AppPool\quote-api'
   .\deploy\Install-RateCore.ps1 -ReleasePath \\files01.corp.local\PricingRelease$\RateCore\4.12.318 -ReadAccess 'CORP\svc-hq-app'
   ```

6. Web sites (on the web server):

   ```
   .\deploy\Install-WebSites.ps1 -PublishRoot D:\Drops\BrokerQuotes\5.8.0 -CertificateThumbprint <thumbprint> `
       -SigningCertificateThumbprint <thumbprint> -EncryptionCertificateThumbprint <thumbprint> `
       -SqlPasswords @{ Quoting = '...'; Identity = '...' } -ClientSecrets @{ BrokerPortal = '...'; Workbench = '...' } -MqPassword '...'
   ```

7. Services (on `app01`):

   ```
   .\deploy\Install-AppServices.ps1 -PublishRoot D:\Drops\BrokerQuotes\5.8.0 -ServiceAccount CORP\svc-hq-app -ServicePassword (Read-Host -AsSecureString) `
       -SqlPasswords @{ Policy = '...'; Documents = '...' } -MqPasswords @{ Policy = '...'; Documents = '...' } `
       -SftpHostKeyFingerprint 'SHA256:...'
   ```

8. Create staff users on `https://login.corp.local/Admin/Users` and the first broker administrator of each agency.

### Lab server

`deploy\lab.psd1` holds the settings for the single-server lab `host01.corp.local` (all names on
host01). The lab has no access to the pricing release share or to Lucerne Capacity and no mail relay, so:

- RateCore is not installed; the sites and services run with `-RatingProvider Simulator`,
- the bordereau is written to a folder (`-BordereauDelivery Folder`),
- documents are written to a pickup folder (`-MailMode Pickup`).

```
$lab = Import-PowerShellDataFile .\deploy\lab.psd1
.\deploy\Register-Dns.ps1 -ZoneName $lab.ZoneName -WebIPAddress <host01 IP>
.\deploy\Install-WebSites.ps1 -PublishRoot C:\Drops\bq -CertificateThumbprint <thumbprint> -SqlServer $lab.SqlServer -MqServer $lab.MqServer `
    -RatingProvider $lab.RatingProvider -SiteRoot $lab.SiteRoot -LogRoot C:\Logs\Humongous `
    -SqlPasswords @{ Quoting = '...'; Identity = '...' } -ClientSecrets @{ BrokerPortal = '...'; Workbench = '...' } -MqPassword '...'
.\deploy\Install-AppServices.ps1 -PublishRoot C:\Drops\bq -ServiceAccount CORP\svc-hq-app -ServicePassword (Read-Host -AsSecureString) `
    -SqlServer $lab.SqlServer -MqServer $lab.MqServer -SmtpServer $lab.SmtpServer -RatingProvider $lab.RatingProvider -ServiceRoot $lab.ServiceRoot `
    -BordereauDelivery $lab.BordereauDelivery -BordereauFolder C:\Humongous\Bordereaux -MailMode $lab.MailMode -MailPickupFolder C:\Humongous\MailPickup `
    -LogRoot C:\Logs\Humongous -SqlPasswords @{ Policy = '...'; Documents = '...' } -MqPasswords @{ Policy = '...'; Documents = '...' }
```

## Configuration

| Setting | Applications | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Quoting` | API, policy service, documents | Quoting database |
| `ConnectionStrings:Identity` | Sign-in | Identity database |
| `Database:Provider` | all with a database | `SqlServer` or `Sqlite` |
| `Database:EnsureCreated`, `SeedSampleData`, `SeedSampleUsers` | | Development only |
| `Rating:Provider` | API, policy service | `RateCore` or `Simulator` |
| `Rating:ProgId`, `TablesPath` | API, policy service | RateCore class and rate table folder |
| `Rating:Workers`, `RecycleAfterCalls`, `CallTimeoutSeconds` | API, policy service | RateCore worker threads |
| `Bus:*` | API, policy service, documents | RabbitMQ host, virtual host, user, password, queue prefix |
| `Authentication:Authority`, `Audience` | API | Token issuer and audience |
| `Identity:Issuer`, `SigningCertificateThumbprint`, `EncryptionCertificateThumbprint` | Sign-in | Issuer and token keys (`LocalMachine\My`) |
| `Clients` | Sign-in | Registered applications (portal and workbench), secrets and redirect URIs |
| `Oidc:*` | Portal, workbench | Sign-in service, client id and secret |
| `ReverseProxy` | Portal | Quote API address for `/api` and `/hubs` |
| `QuoteApi:BaseAddress` | Workbench | Quote API address |
| `Jobs:Schedules`, `Jobs:TimeZone` | Policy service | Cron schedules of the jobs |
| `Renewals:DaysBeforeExpiry` | Policy service | Renewal invitation lead time |
| `Bordereau:*` | Policy service | UMR, delivery (`Sftp` or `Folder`), SFTP host, user, key, host key |
| `Documents:*` | Documents | Font and insurer details on documents |
| `Mail:*` | Documents | `Smtp` or `Pickup`, relay, sender |
| `Serilog` | all | Log level and files (`D:\Logs\Humongous\<app>`) |

Health endpoints: `/health` on every site (quote API: database, rating engine and bus).
RateCore trace: `HKLM\SOFTWARE\WOW6432Node\Humongous\RateCore\TraceFile` (see `Install-RateCore.ps1 -TraceFile`).
