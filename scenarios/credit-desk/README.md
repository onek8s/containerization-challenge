# Credit Desk

Credit limit management for Fabrikam Wholesale. Sales reps request a new credit limit for a
customer on the **Credit Desk** intranet site, credit controllers and finance managers approve or
reject the request, and the customer receives a signed confirmation letter on company letterhead.
A PDF copy of every letter is filed on the letters share.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/CreditDesk.Web` | `creditdesk.fabrikam.local` (IIS) | ASP.NET MVC 5 intranet site, Windows sign-in |
| `src/CreditDesk.CreditService` | `credit01.fabrikam.local` | Windows service hosting the WCF credit service (net.tcp, port 8524) |
| `src/CreditDesk.LetterService` | `docs01.fabrikam.local` | Windows service producing letters with Microsoft Word from the letter queue |
| `src/CreditDesk.Contracts` | | WCF service contract shared by the site and the credit service |
| `src/CreditDesk.Core` | | Credit policy CP-04 rules, data contracts and letter fields |
| `tests/CreditDesk.Tests` | | Unit tests for the policy and letter fields |
| `deploy` | | DNS, Kerberos and install scripts, database scripts, letter template script |

Other servers used: SQL Server `sql01.fabrikam.local` (database `CreditDesk`), file server
`fs01.fabrikam.local` (shares `templates$` and `letters$`) and print server `print01.fabrikam.local`
(queue `FIN-LETTERHEAD-01`, tray with pre-printed letterhead).

### Request flow

1. The user opens `http://creditdesk.fabrikam.local/` and is signed in with Windows authentication.
2. The site calls the credit service as the signed-in user (Kerberos delegation from the IIS app pool account).
3. The credit service checks the user's Credit Desk groups and calls SQL Server as that user, so
   SQL Server records who requested and who approved every change.
4. When a request is decided, the credit service updates the database and sends the letter message
   to the transactional queue `docs01\private$\creditdesk.letters` in one distributed transaction (MSDTC).
5. The letter service fills `CreditLimitLetter.dotx`, saves PDF and DOCX copies to
   `\\fs01.fabrikam.local\letters$\credit\yyyy\MM`, prints the letter and stores the file path on the request.
   Messages that fail three times are moved to `private$\creditdesk.letters.failed`.

## Requirements

- Active Directory domain `FABRIKAM` (`fabrikam.local`) with DNS
- Windows Server 2019 or later for all servers, .NET Framework 4.8
- `creditdesk`: IIS 10 with Windows Authentication and ASP.NET 4.8
- `credit01`: Net.Tcp Port Sharing Service, MSMQ, MSDTC with network access
- `docs01`: MSMQ, MSDTC with network access, Microsoft Word (Office LTSC 2021 or Microsoft 365 Apps), printer connection to `\\print01.fabrikam.local\FIN-LETTERHEAD-01`
- SQL Server 2019 or later with Windows authentication
- Service accounts:

  | Account | Used by | Needs |
  | --- | --- | --- |
  | `FABRIKAM\svc-creditdesk-web` | IIS app pool | SPN `HTTP/creditdesk.fabrikam.local`, constrained delegation to `CreditDesk/credit01.fabrikam.local` |
  | `FABRIKAM\svc-creditdesk-app` | credit service | SPN `CreditDesk/credit01.fabrikam.local`, constrained delegation to `MSSQLSvc/sql01.fabrikam.local`, send rights on the letter queue |
  | `FABRIKAM\svc-creditdesk-doc` | letter service | Read on `templates$`, modify on `letters$`, print permission, `cd.MarkLetterSent` in the database |

- Domain groups (created by `configure-kerberos.ps1`):

  | Group | Role |
  | --- | --- |
  | `FABRIKAM\CreditDesk Sales` | Request limit changes, see own requests |
  | `FABRIKAM\CreditDesk Controllers` | Approve up to 50,000 |
  | `FABRIKAM\CreditDesk Finance Managers` | Approve up to 250,000 |
  | `FABRIKAM\CreditDesk Committee` | Approve any amount |

## Build

```powershell
dotnet build CreditDesk.sln -c Release
dotnet test CreditDesk.sln -c Release
```

Visual Studio 2022 or the .NET 8 SDK with the .NET Framework 4.8 Developer Pack. Build output:

- site: `src\CreditDesk.Web` (the project folder itself, with `bin`)
- credit service: `src\CreditDesk.CreditService\bin\Release\net48`
- letter service: `src\CreditDesk.LetterService\bin\Release\net48`

## Configuration

| File | Setting | Default |
| --- | --- | --- |
| Site `Web.config` | `system.serviceModel/client/endpoint[@name='CreditService']` address and SPN | `net.tcp://credit01.fabrikam.local:8524/CreditDesk/CreditService`, `CreditDesk/credit01.fabrikam.local` |
| `CreditDesk.CreditService.exe.config` | connection string `CreditDesk` | `sql01.fabrikam.local`, database `CreditDesk`, integrated security |
| | `LetterQueue` | `FormatName:DIRECT=OS:docs01.fabrikam.local\private$\creditdesk.letters` |
| | `Group.*` | Domain groups above |
| | `Policy.*` | Thresholds of credit policy CP-04 (automatic up to +2,500, controllers 50,000, finance managers 250,000, max 3x current limit, max 60 days overdue, 6 months customer history above 50,000, approvals valid 365 days) |
| | WCF base address and SPN | `net.tcp://credit01.fabrikam.local:8524/CreditDesk/`, `CreditDesk/credit01.fabrikam.local` |
| `CreditDesk.LetterService.exe.config` | `QueuePath`, `FailedQueuePath` | `.\private$\creditdesk.letters`, `.\private$\creditdesk.letters.failed` |
| | `TemplatePath` | `\\fs01.fabrikam.local\templates$\CreditDesk\CreditLimitLetter.dotx` |
| | `OutputFolder` | `\\fs01.fabrikam.local\letters$\credit` |
| | `Printer`, `PrintCopies` | `\\print01.fabrikam.local\FIN-LETTERHEAD-01`, `1` |
| | `MaxAttempts`, `RecycleWordAfter` | `3`, `50` (Word is restarted after this many letters) |
| | connection string `CreditDesk` | `sql01.fabrikam.local` |

The install scripts write host names, SPNs and paths into the installed `.config` files.

## Deploy

Run each script from an elevated PowerShell on the server named.

1. **DNS** (DNS server): register the host names. For a test installation all roles can run on one
   server; every name then points to the same address.

   ```powershell
   .\deploy\register-dns.ps1 -ZoneName fabrikam.local -ServerAddress 10.20.0.15
   # separate servers:
   .\deploy\register-dns.ps1 -ZoneName fabrikam.local -ServerAddress 10.20.0.15 -CreditAddress 10.20.0.21 -DocsAddress 10.20.0.22 -SqlAddress 10.20.0.30
   ```

2. **Kerberos** (domain admin): create the three service accounts, then

   ```powershell
   .\deploy\configure-kerberos.ps1 -Domain FABRIKAM -DnsZone fabrikam.local
   ```

3. **Database** (`sql01`):

   ```powershell
   sqlcmd -S sql01.fabrikam.local -E -i deploy\sql\CreditDesk.sql -v Domain=FABRIKAM
   sqlcmd -S sql01.fabrikam.local -E -i deploy\sql\SampleCustomers.sql   # test environments only
   ```

   Production customer data is loaded by the SQL Agent job *ERP customer sync* (maintained by the ERP team).

4. **Letter template** (any machine with Word, once): `.\deploy\new-letter-template.ps1`, then hand the
   template to Credit Control for letterhead layout.

5. **Letter service** (`docs01`):

   ```powershell
   $pw = Read-Host -AsSecureString 'svc-creditdesk-doc password'
   .\deploy\install-letter-service.ps1 -SourcePath .\src\CreditDesk.LetterService\bin\Release\net48 -ServicePassword $pw
   ```

   Follow the printed steps (Word first run and printer connection as the service account, DCOM
   launch permission), then `Start-Service CreditDeskLetters`.

6. **Credit service** (`credit01`):

   ```powershell
   $pw = Read-Host -AsSecureString 'svc-creditdesk-app password'
   .\deploy\install-credit-service.ps1 -SourcePath .\src\CreditDesk.CreditService\bin\Release\net48 -ServicePassword $pw
   ```

7. **Site** (`creditdesk`):

   ```powershell
   $pw = Read-Host -AsSecureString 'svc-creditdesk-web password'
   .\deploy\install-web.ps1 -SourcePath .\src\CreditDesk.Web -AppPoolPassword $pw [-CertificateThumbprint <thumbprint>]
   ```

   Add `creditdesk.fabrikam.local` to the Local intranet zone by group policy so browsers sign in
   without a prompt.

## Run and check

- Browse to `http://creditdesk.fabrikam.local/` as a member of a Credit Desk group.
- `http://creditdesk.fabrikam.local/home/identity` shows the identity seen by the site and by the
  credit server. The credit server must show the user's account with impersonation level `Delegation`
  or `Impersonation`.
- Both services can be started in a console for troubleshooting (stop the Windows service first,
  run as the service account):

  ```powershell
  & 'C:\Program Files\Fabrikam\Credit Desk\Credit Service\CreditDesk.CreditService.exe' /console
  & 'C:\Program Files\Fabrikam\Credit Desk\Letter Service\CreditDesk.LetterService.exe' /console
  ```

- Events are written to the Application log with sources `Credit Desk Service` and `Credit Desk Letters`.
- Pending and failed letters: *Computer Management > Services and Applications > Message Queuing > Private Queues* on `docs01`.
