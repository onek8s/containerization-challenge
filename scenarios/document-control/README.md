# Document Control

Engineering document control for Litware Engineering. Engineers submit drawings, specifications and other
project documents with their revisions, document controllers review and approve them, and approved revisions
are issued to clients and contractors as transmittals: a zip package with the files, a manifest with checksums
and a cover sheet, mailed to the recipient. Document numbers and revision codes follow the Litware procedure
EP-07 (`P1042-LIT-ME-DRG-0012`, revisions `P01`..`P99` then `C01`..`C99`).

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/DocControl.Web` | `doccontrol.corp.local` (IIS site `doccontrol`) | ASP.NET Core 8 MVC portal: register, revision upload, review, transmittals, diagnostics; JSON API for the register workbook |
| `src/DocControl.Packager` | `doccontrol-pkg.corp.local` (Windows service `LitwareDocPackager`) | Builds queued transmittal packages, publishes them to `Transmittals$` and mails them |
| `src/DocControl.Indexer` | `doccontrol-idx.corp.local` (Windows service `LitwareDocIndexer`) | Scans the project drop folders and registers dropped files as submitted revisions; DocTrack archive import command |
| `src/DocControl.Register` | User workstations (COM component) | .NET Framework 4.8 client used by the Excel transmittal register macros |
| `src/DocControl.Core` | | Numbering and revision rules, transmittal rules, package writer, manifest, cover sheet PDF |
| `src/DocControl.Data` | | Dapper data access (SQL Server; SQLite for development), vault storage |
| `src/DocControl.Platform` | | File system, disk space, host information, install location and event log helpers |
| `tests/DocControl.Tests` | | Unit and repository tests |
| `deploy` | | DNS, file share, database, IIS, service and workstation install scripts |

Other servers: SQL Server `sql01.corp.local` (database `DocControl`), file server `files01.corp.local`
(shares `DocVault$`, `Transmittals$`, `DocDrop$`), SMTP relay `smtp.corp.local`.

### Flow

1. An engineer registers a new document by submitting its first revision in the portal (`/Revisions/Upload`),
   or drops a file named `<document number>_<revision>.<ext>` into `\\files01.corp.local\DocDrop$\<project>`
   for a document that is already registered. The indexer checks the name, the revision sequence and, for
   PDFs, the title written by the plot driver, copies the file to the vault and moves the original to
   `_processed\yyyyMMdd`. Rejected files go to `_rejected` with a `.reason.txt` next to them.
2. A document controller approves or rejects the revision (`/Revisions/Review`). Approving supersedes the
   previously approved revision. Nobody can approve their own submission.
3. The controller creates a transmittal (`/Transmittals/Create`, or `IssueTransmittal` in the register workbook):
   project, purpose (IFR, IFA, IFC, IFI), recipient and documents. IFR may include submitted revisions; IFC only
   C revisions.
4. The packager claims the queued transmittal, writes `TR-<project>-<nnnnn>.zip` to
   `\\files01.corp.local\Transmittals$\<yyyy>`, marks it issued and mails the recipient with the cover sheet and,
   up to 15 MB, the package attached. The controller is copied. Failed builds are retried three times.

### Vault layout

```
\\files01.corp.local\DocVault$\P1042\P1042-LIT-ME-DRG-0012\P02\P1042-LIT-ME-DRG-0012_P02.pdf
\\files01.corp.local\Transmittals$\2024\TR-P1042-00003.zip
    TR-P1042-00003/documents/...
    TR-P1042-00003/manifest.csv
    TR-P1042-00003/TR-P1042-00003.pdf
```

## Requirements

Build:

- .NET SDK 8.0.400 or later (`global.json`)
- `DocControl.Register` targets .NET Framework 4.8; the reference assemblies come from NuGet
- Windows PowerShell 5.1 for the deploy scripts

Servers (Windows Server 2019 or later, Active Directory domain `CORP`):

- `doccontrol`: IIS 10 with Windows Authentication and Request Filtering, ASP.NET Core 8 Hosting Bundle,
  certificate `CN=doccontrol.corp.local` for HTTPS
- `doccontrol-pkg`, `doccontrol-idx`: .NET 8 Runtime (x64), data disk `D:` (`D:\DocControl`)
- `sql01`: SQL Server 2016 or later; `files01`: file server; `smtp`: relay accepting the packager account
- Service accounts: `CORP\svc-dc-web` (application pool), `CORP\svc-dc-packager`, `CORP\svc-dc-indexer`
- AD groups `CORP\DocControl-Engineers` and `CORP\DocControl-Controllers`
- SPN `HTTP/doccontrol.corp.local` on `CORP\svc-dc-web`
- For the DocTrack import only: `dtzip.dll` from the DocTrack 5 viewer installation

Workstations: Excel 2016 or later, .NET Framework 4.8.

## Build and test

```
dotnet build DocumentControl.sln
dotnet test DocumentControl.sln
```

`deploy\Publish.ps1 -Output C:\Build\DocControl` runs the tests and publishes every component into
`Web`, `Packager`, `Indexer` and `Register` sub-folders.

## Deploy

Run the scripts as administrator on the server named in each step. All scripts take `-ZoneName` (default
`corp.local`); host names are derived from it.

```powershell
# DNS server: one A record per host (or -AllIP for a single-server lab)
.\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP 192.168.8.136

# files01
.\New-DocControlShares.ps1 -Root D:\Shares\DocControl -Domain CORP -Projects P1042,P1057

# any machine with the SqlServer module
.\Initialize-Database.ps1 -SqlServer sql01.corp.local -Domain CORP

# doccontrol (IIS site under C:\inetpub\sites\doccontrol)
.\Install-PortalSite.ps1 -PublishPath C:\Build\DocControl\Web -AppPoolAccount CORP\svc-dc-web -CertificateThumbprint <thumbprint>

# doccontrol-pkg
.\Install-DocControlService.ps1 -Component Packager -PublishPath C:\Build\DocControl\Packager -ServiceAccount CORP\svc-dc-packager

# doccontrol-idx
.\Install-DocControlService.ps1 -Component Indexer -PublishPath C:\Build\DocControl\Indexer -ServiceAccount CORP\svc-dc-indexer

# workstations (or through software distribution)
.\Install-RegisterAddIn.ps1 -PublishPath \\files01.corp.local\Software$\DocControl\Register
```

The install scripts write `appsettings.Production.json` next to the binaries with the server names, and
`HKLM\SOFTWARE\Litware\DocControl\<Component>` (`InstallPath`, `DataPath`). Service installs also create the
event log source and grant *Log on as a service* to the account. Updates are installed by running the same
script again; `appsettings.Production.json` is kept.

## Configuration

| Setting | Component | Default |
| --- | --- | --- |
| `DocControl:Database:Provider` / `ConnectionString` | all | `SqlServer`, `sql01.corp.local`, integrated security |
| `DocControl:Storage:VaultRoot` | all | `\\files01.corp.local\DocVault$` |
| `DocControl:Storage:TransmittalRoot` | Web, Packager | `\\files01.corp.local\Transmittals$` |
| `DocControl:Storage:DropRoot` | Web, Indexer | `\\files01.corp.local\DocDrop$` |
| `DocControl:Portal:ControllersGroup` / `EngineersGroup` | Web | `CORP\DocControl-Controllers` / `CORP\DocControl-Engineers` |
| `DocControl:Packager:MaxParallelBuilds` | Packager | `0` (half of the server's logical processors) |
| `DocControl:Packager:WorkFolder` | Packager | `<DataPath>\work` |
| `DocControl:Packager:MinFreeBytes` | Packager | 5 GB on the work folder and on `Transmittals$` |
| `DocControl:Packager:Mail:*` | Packager | `smtp.corp.local:25`, default credentials, attachments up to 15 MB |
| `DocControl:Indexer:ScanSeconds` / `MinFileAgeSeconds` | Indexer | `60` / `30` |

## Run locally

In the `Development` environment the portal signs everybody in as the user in
`Authentication:Development` (header `X-Dev-User` overrides the name), all components share a SQLite database
and vault under `src/DocControl.Web/App_Data`, sample projects and documents are created on first start, and
the packager writes mails to `App_Data/mail` instead of sending them. Start each component from its project
folder:

```
cd src/DocControl.Web      && dotnet run    # http://localhost:5080
cd src/DocControl.Packager && dotnet run
cd src/DocControl.Indexer  && dotnet run    # or: dotnet run -- --scan-once
```

Drop test files into `src/DocControl.Web/App_Data/drop/P1042`.

## Operations

- Event log sources: `Litware DocControl Packager`, `Litware DocControl Indexer` (Application log).
- `/Diagnostics` (controllers only) shows the server, runtime, install path and free space on the shares.
- A failed transmittal can be queued again from its page; the error is shown there.
- DocTrack archives: `Litware.DocControl.Indexer.exe --import-doctrack D:\DocTrackExport\P0988 --project P0988`
  unpacks `.dta` archives into the project drop folder, and the next scan registers the revisions. Files that do
  not follow the numbering are put in `_unmatched` for manual handling.
