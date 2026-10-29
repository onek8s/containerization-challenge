# Policy Changes

Policy servicing for Tailspin Mutual Insurance. Customer service staff and partner brokers submit
policy changes (address, payment method, beneficiaries, cancellation). The changes are entered in
the policy administration system **PolicyDesk 6.4** by desk stations, which create the endorsement
and keep screenshots of the PolicyDesk screens as evidence.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/PolicyChanges.Portal` | `changes.corp.local` (IIS) | Razor Pages portal for service staff, Windows sign-in |
| `src/PolicyChanges.Api` | `changes-api.corp.local` (IIS) | REST API for partners (`/api/v1/changes`), OpenAPI at `/swagger`, API key per partner |
| `src/PolicyChanges.DeskRunner` | `desk01.corp.local`, `desk02.corp.local` | Desk runner: takes queued changes and enters them in PolicyDesk |
| `src/PolicyChanges.Data` | | EF Core data model, change service and queue |
| `src/PolicyChanges.Core` | | Change validation, PolicyDesk screen flows |
| `tools/PolicyDesk.Lab` | test desk stations | PolicyDesk lab client with the PolicyDesk 6.4 screens used by the flows |
| `tests/PolicyChanges.Tests` | | Unit, flow and API tests |
| `deploy` | | DNS, IIS, file share, desk station and SQL scripts |

The database `PolicyChanges` is on SQL Server `sql01.corp.local`. Screenshots are stored on
`\\files01.corp.local\PolicyChanges$\Evidence`.

### How a change is entered

1. A change is submitted on the portal (**New change**) or with `POST /api/v1/changes`. It is
   validated and queued with a reference such as `PC-261029-0001`.
2. A desk station claims the next change (earliest effective date first). Each desk station is a
   Windows machine that signs in automatically as its desk user; the desk runner starts with the
   user's desktop, starts PolicyDesk and signs in with the PolicyDesk user stored in the desk user's
   Credential Manager.
3. The desk runner runs the screen flow for the change type from `flows\*.json`: it opens the
   policy, fills in the change dialog, saves and reads the endorsement number from the PolicyDesk
   message. Screenshots are taken along the way.
4. The result is written back: **Applied** with the endorsement number, **Rejected** when PolicyDesk
   refuses the change (message shown on the portal), or queued again after a failure (5, 15 and 60
   minutes). After four failed attempts the change waits for a service lead under **Review**.

Desk stations report their state on the portal overview (idle, working, waiting for desktop,
PolicyDesk error).

### Screen flows

A flow is a list of steps against PolicyDesk windows, for example:

```json
{ "action": "window", "title": "PolicyDesk - Change address - {Policy}" },
{ "action": "set", "field": "Postal code", "value": "{Field:PostalCode}" },
{ "action": "click", "button": "Save" },
{ "action": "dialog", "accept": [ "^Endorsement (?<Endorsement>\\d{7}) created" ], "reject": [ "not in force" ] }
```

Actions: `window`, `set`, `select`, `check`, `click`, `keys`, `verify`, `read`, `capture`, `dialog`,
`grid`, `forEach`, `when`, `pause`. Inputs are found by the label in front of them; grids are read
with Ctrl+Shift+C. Placeholders: `{Policy}`, `{Reference}`, `{Effective}`, `{Field:Name}`,
`{Var:Name}`, `{Item:n}`, `{Count:Field}`, with filters `|iban`, `|date`, `|upper`, `|trim`.
When PolicyDesk is upgraded, check the flows against the new screens (`clientVersion` in each flow).

## Requirements

Build:

- .NET 8 SDK (Windows for `tools/PolicyDesk.Lab`)

Servers:

- Windows Server 2019 or later, Active Directory domain `CORP` with DNS
- `changes`, `changes-api`: IIS 10 with Windows Authentication, ASP.NET Core 8 Hosting Bundle
- `desk01`, `desk02` (Windows 10/11 or Windows Server with Desktop Experience, physical or VM):
  - PolicyDesk 6.4 client in `C:\Program Files (x86)\Halcyon\PolicyDesk 6` (one seat licence per desk user)
  - .NET 8 runtime
  - Console display of at least 1280x1024
- `sql01`: SQL Server 2019 or later
- `files01`: file share `PolicyChanges$`
- Accounts:
  - `CORP\svc-policychanges`: IIS application pools, database, read access to the evidence share
  - `CORP\svc-desk01`, `CORP\svc-desk02`: desk users, members of `CORP\Policy Desk Stations`;
    each has its own PolicyDesk user
- AD groups `CORP\Policy Service` (portal users) and `CORP\Policy Service Leads` (review queue, partners)

## Build and test

```
dotnet build PolicyChanges.sln
dotnet test PolicyChanges.sln
```

## Run locally

In the `Development` environment the portal and the API use a SQLite database in `App_Data/` with
two sample partners, and the portal signs you in as a service lead.

```
dotnet run --project src/PolicyChanges.Api       # http://localhost:5280/swagger
dotnet run --project src/PolicyChanges.Portal    # http://localhost:5290
```

Sample partner API key: `pc_dev_nordbroker_5f2a91c3`.

```
curl -X POST http://localhost:5280/api/v1/changes \
  -H "X-Api-Key: pc_dev_nordbroker_5f2a91c3" -H "Content-Type: application/json" \
  -d '{"policyNumber":"HL-1042118","changeType":"AddressChange","effectiveDate":"2026-11-02",
       "partnerReference":"NB-1001",
       "fields":{"Street":"Hämeenkatu 12 B 7","PostalCode":"33100","City":"Tampere","Country":"FI"}}'
```

To enter changes, run the desk runner on a Windows desktop with PolicyDesk or the lab client
(see **Lab desk station**):

```
dotnet run --project src/PolicyChanges.DeskRunner -- store-credential PolicyDesk/TEST
dotnet run --project src/PolicyChanges.DeskRunner
```

Leave the desktop alone while the runner works; it types into PolicyDesk and uses the clipboard.

## Deploy

1. DNS (on the DNS server; all names may point to one server in a lab):

   ```
   .\deploy\register-dns.ps1 -ZoneName corp.local -IPAddress <web server IP> -DeskIPAddresses <desk01 IP>,<desk02 IP>
   ```

2. Database (on `sql01`):

   ```
   sqlcmd -S sql01.corp.local -E -i deploy\sql\create-database.sql
   sqlcmd -S sql01.corp.local -E -i deploy\sql\create-logins.sql -v WebAccount="CORP\svc-policychanges" DeskGroup="CORP\Policy Desk Stations"
   ```

3. Evidence share (on `files01`, as administrator):

   ```
   .\deploy\create-evidence-share.ps1 -WebAccount CORP\svc-policychanges -DeskGroup "CORP\Policy Desk Stations"
   ```

4. Web (on the IIS server, as administrator):

   ```
   dotnet publish src\PolicyChanges.Portal -c Release -o publish\portal
   dotnet publish src\PolicyChanges.Api -c Release -o publish\api
   .\deploy\install-web.ps1 -PublishRoot publish -DomainName corp.local -AppPoolAccount CORP\svc-policychanges
   setspn -S HTTP/changes.corp.local CORP\svc-policychanges
   ```

5. Desk stations (on each desk station, as administrator):

   ```
   dotnet publish src\PolicyChanges.DeskRunner -c Release -r win-x64 --self-contained false -o publish\desk
   .\deploy\install-desk.ps1 -PublishPath publish\desk -DeskUser CORP\svc-desk01 -AutologonTool C:\Tools\Autologon64.exe
   ```

   The script installs the runner to `C:\PolicyChanges\DeskRunner`, sets up automatic sign-in of
   the desk user, turns off the lock screen, screen saver and sleep, and registers the scheduled
   task *Policy Changes desk runner* that starts the runner when the desk user signs in. Restart,
   then as the desk user run `PolicyChanges.DeskRunner.exe store-credential` and
   `PolicyChanges.DeskRunner.exe check`.

   Do not lock the desk station or disconnect from it with Remote Desktop: leave a Remote Desktop
   session with `C:\PolicyChanges\DeskRunner\return-to-console.cmd`.

6. Add partners and issue their API keys on the portal (**Partners**).

### Lab desk station

For a test environment without PolicyDesk, install the lab client in its place:

```
dotnet publish tools\PolicyDesk.Lab -c Release -r win-x64 --self-contained false -o publish\lab-client
.\deploy\install-lab-client.ps1 -PublishPath publish\lab-client
```

Sample policies: `HL-1042118` (motor), `HL-1042126` (home), `HL-2056331` (life),
`HL-3008711` (cancelled), `HL-3008729` (locked by another user), `HL-4120903` (life).
The lab client accepts any user ID with a non-empty password. `-ResetData` restores the sample policies.

## Configuration

Portal and API (`appsettings.json`):

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:PolicyChanges` | Database connection |
| `Database:Provider` | `SqlServer` or `Sqlite` |
| `Database:SeedSampleData` | Create sample partners on an empty database |
| `Portal:Authentication` | `Negotiate` or `Development` |
| `Portal:UsersGroup`, `Portal:LeadsGroup` | Portal user and service lead groups |
| `Portal:EvidenceRoot` | Evidence share |
| `Portal:StationOfflineMinutes` | When a desk station is shown offline |

Desk runner (`appsettings.json`, `appsettings.Production.json`):

| Setting | Purpose |
| --- | --- |
| `Desk:StationName` | Name shown on the portal (default: computer name) |
| `Desk:PollSeconds`, `Desk:DesktopWaitSeconds` | Queue polling and wait when the desktop is not usable |
| `Desk:FlowsPath` | Screen flow folder |
| `Desk:EvidenceRoot` | Evidence share |
| `Desk:LogPath` | Log folder (`desk-yyyyMMdd.log`) |
| `PolicyDesk:ExecutablePath`, `Arguments`, `Environment` | PolicyDesk client and environment (`PROD`, `TEST`) |
| `PolicyDesk:CredentialTarget` | Credential Manager entry with the PolicyDesk user |
| `PolicyDesk:SignInTitle`, `MainWindowTitle` | PolicyDesk window titles |
| `PolicyDesk:RestartAfterChanges` | Restart PolicyDesk after this many changes |
| `Queue:LeaseMinutes`, `MaxAttempts`, `RetryDelayMinutes` | Claim lease and retries |

Health endpoint: API `/health` (database).
