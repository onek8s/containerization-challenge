# Staff Onboarding

Joiner, mover and leaver process for Litware Inc. HR raises a request in the **Staff Onboarding**
portal, the line manager (and IT security for privileged access) approve it, and the Active Directory
account, group memberships and home folder are created, changed or removed on the right day.

- **Joiner**: manager approval, IT approval when the role or requested groups are privileged, account
  created disabled three business days before the start date, welcome pack written for the manager,
  account enabled on the first day. HR can move the start date until the account is enabled.
- **Mover**: approval by the new manager, new access on the effective date, previous role access
  removed after a handover period (privileged groups at once).
- **Leaver**: manager confirms, account disabled at 18:00 on the last working day (or at once), home
  folder handed over and archived after 30 days or when the handover is confirmed.

Open tasks are reminded every two business days and escalated to the approver's own manager.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/StaffOnboarding.Portal` | `onboarding.corp.local` (IIS) | ASP.NET Core 8 Razor Pages portal, Windows sign-in, documents on the onboarding share |
| `src/StaffOnboarding.WorkflowHost` | `onboarding-flow.corp.local` | Windows service hosting the joiner, mover and leaver workflow services (port 8085) |
| `src/StaffOnboarding.Workflows` | | Workflow definitions, activities, mail and welcome pack |
| `src/StaffOnboarding.Provisioning` | `idm01.corp.local` | Windows service with the provisioning API (port 8090): AD accounts, groups, home folders |
| `src/StaffOnboarding.Contracts` | | Service contracts shared by the portal and the workflow host |
| `src/StaffOnboarding.Core` | | Request model, account naming standard IT-STD-07, access map, business calendar |
| `tests/StaffOnboarding.Tests` | | Unit tests for Core and the provisioning service |
| `deploy` | | DNS, directory, share, SQL and install scripts, access map, holidays, mail templates |

Other servers used:

| Server | Use |
| --- | --- |
| `sql01.corp.local` | SQL Server, databases `StaffOnboarding` and `StaffOnboardingInstances` |
| `files01.corp.local` | Share `Onboarding$` (`Config`, `Templates`, `Packs`, `Documents`) |
| `\\corp.local\dfs\home`, `\\corp.local\dfs\archive\home` | DFS folders for home folders and archived home folders |
| `dc01.corp.local` | Domain controller that all provisioning writes go to |
| `smtp.corp.local` | Mail relay |

### Request flow

1. HR opens `http://onboarding.corp.local/` and submits a request. The portal stores it in
   `StaffOnboarding` and sends it to the workflow host as the application pool account `CORP\svc-onbweb$`.
2. The workflow host starts a workflow instance, which creates approval tasks and mails the approvers.
   Instances are persisted in `StaffOnboardingInstances` while they wait for decisions and dates.
3. When a step is due, the workflow host calls the provisioning service on `idm01` with its own
   account `CORP\svc-onbflow$` (Kerberos).
4. The provisioning service (`CORP\svc-idm$`) creates or changes the account on `dc01`, sets group
   memberships, creates the home folder under `\\corp.local\dfs\home` with NTFS permissions and records
   the step so it is not done twice.

## Requirements

- Active Directory domain `CORP` (`corp.local`) with DNS, a KDS root key for group managed service accounts
- Windows Server 2019 or later for all servers, joined to the domain
- `onboarding`: IIS 10 with Windows Authentication, ASP.NET Core 8 Hosting Bundle
- `onboarding-flow`: .NET Framework 4.8
- `idm01`: ASP.NET Core 8 runtime (Windows x64)
- SQL Server 2019 or later with Windows authentication
- DFS Namespaces with a domain based namespace `\\corp.local\dfs`
- Accounts and groups (created by `deploy\prepare-directory.ps1`):

  | Account / group | Used by | Needs |
  | --- | --- | --- |
  | `CORP\svc-onbweb$` (gMSA) | portal app pool | SPN `HTTP/onboarding.corp.local`, modify on `Onboarding$\Documents`, read on `Config` |
  | `CORP\svc-onbflow$` (gMSA) | workflow host | SPN `HTTP/onboarding-flow.corp.local`, modify on `Onboarding$\Packs`, read on `Config` and `Templates`, instance store user |
  | `CORP\svc-idm$` (gMSA) | provisioning service | create/manage users in `OU=Users,OU=Litware`, reset password, write `member` on groups, full control and SeRestorePrivilege on the home and archive file servers |
  | `CORP\APP-Onboarding-HR` | HR | create and change requests |
  | `CORP\APP-Onboarding-IT`, `CORP\APP-Onboarding-IT-Leads` | IT security | approve privileged access, operate the workflow host |
  | `CORP\FS-Home-Admins` | file server admins | full control on every home folder |

## Build

Requires Visual Studio 2022 or the .NET 8 SDK on Windows (the workflow host and workflow library
target .NET Framework 4.8).

```powershell
dotnet build StaffOnboarding.sln -c Release
dotnet test StaffOnboarding.sln -c Release

dotnet publish src\StaffOnboarding.Portal -c Release -o out\Portal
dotnet publish src\StaffOnboarding.WorkflowHost -c Release -o out\WorkflowHost
dotnet publish src\StaffOnboarding.Provisioning -c Release -o out\Provisioning
```

## Deploy

All scripts are in `deploy` and take the host names, accounts and paths as parameters.

1. DNS: `.\register-dns.ps1 -IPAddress <address>` (in a lab all names can point to one server).
2. Directory: `.\prepare-directory.ps1 -AccessMap config\access-map.xml -PortalServer <web> -WorkflowServer <app> -ProvisioningServer <idm>`.
3. Database on `sql01`: run `sql\01-create-databases.sql`, `sql\02-onboarding-schema.sql`, then
   `.\install-instance-store.ps1`, then `sql\03-permissions.sql`.
4. Shares on `files01`: `.\prepare-shares.ps1 -SourceRoot <deploy folder> -HomeTarget \\<server>\home$ -ArchiveTarget \\<server>\archive$`,
   then follow the printed steps on the home and archive file servers.
5. Provisioning service on `idm01`: `.\install-provisioning.ps1 -SourcePath out\Provisioning`. Check
   `appsettings.json` (domain controller, OUs and DFS paths) first.
6. Workflow host on `onboarding-flow`: `.\install-workflow-host.ps1 -SourcePath out\WorkflowHost`. Check
   `StaffOnboarding.WorkflowHost.exe.config` (connection strings, share, provisioning URL, time zone).
7. Portal: `.\install-portal.ps1 -SourcePath out\Portal` (site under `C:\inetpub\sites\onboarding`).

Changes to `access-map.xml`, `holidays.txt` and the mail templates are made on the `Onboarding$`
share. Restart the workflow host after changing the access map or holidays.

## Run and operate

- Portal: `http://onboarding.corp.local/`. Approvers see their open tasks on the start page.
- Provisioning health: `http://idm01.corp.local:8090/health` (Windows sign-in, IT group).
- Workflow host commands, run on `onboarding-flow` as an administrator:

  ```powershell
  StaffOnboarding.WorkflowHost.exe console                       # run interactively instead of as a service
  StaffOnboarding.WorkflowHost.exe instances [Status]            # running requests, next timer, owning machine
  StaffOnboarding.WorkflowHost.exe suspend joiner <instanceId> <reason>
  StaffOnboarding.WorkflowHost.exe unsuspend joiner <instanceId>
  StaffOnboarding.WorkflowHost.exe terminate leaver <instanceId> <reason>
  ```

- A provisioning step that keeps failing ends the request with status `Failed` and mails the service
  desk. Temporary errors (domain controller or file server not reachable) are retried every 15 minutes,
  eight times.
- Events are written to the Application log with sources `Staff Onboarding Workflow` and
  `Staff Onboarding Provisioning`; WCF traces go to `D:\Logs\StaffOnboarding\WorkflowHost.svclog`.
- When the workflow definitions change, add the previous definition to `WorkflowCatalog` so persisted
  requests of the old version keep running (the joiner currently supports 4.1 and 4.2).
