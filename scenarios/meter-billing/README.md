# Meter Billing

Water meter billing for Coho Water. Meter readers download their handheld
route files at the docking stations, and the drive-by AMR collection PC writes
its files to the read share. The billing engine imports the reads, opens a
bill run when a billing cycle reaches its cut-off date, checks every read
against the customer's normal use, estimates missing reads, rates the bills
against the domestic and commercial tariffs and, after review, writes the
print file for the print and mail vendor and the SEPA direct debit file for
the bank. Billing operators follow and steer the runs in Billing Monitor;
customer service looks up bills and orders reprints in the bill lookup site.

| Component | Description | Host |
| --- | --- | --- |
| `MeterBilling.Engine` | Windows service `CohoMeterBilling` (.NET Framework 4.8): read file import, bill runs, print and direct debit files, reprints, control endpoint (HTTP 8441, Windows authentication) | `billing-engine.<zone>` |
| `MeterBilling.Monitor` | Billing Monitor (Windows Forms, .NET Framework 4.8): bill runs, run events, read exceptions, read files, engine log, pause and continue | billing operator workstations |
| `MeterBilling.Lookup` | ASP.NET Core 8 Razor Pages site for customer service: customer search, bills, reads, reprint requests | `billing.<zone>` (IIS site `billing`) |
| `MeterBilling.Core` | Read file formats, read checks and estimates, tariff rating, print file, SEPA pain.008, creditor references (.NET Standard 2.0) | |
| `MeterBilling.Data` | Data access for the engine and Billing Monitor (.NET Framework 4.8, ADO.NET) | |
| SQL Server | Database `MeterBilling` | `sql01.<zone>` |
| File server | Shares `MeterReads$` and `BillingOut$` | `files01.<zone>` |

The default configuration uses the zone `corp.local` and the domain `CORP`.
All names can point to the same server.

## Requirements

- Windows Server 2019 or later (engine, web server, file server)
- .NET Framework 4.8 on the engine server and the operator workstations
- ASP.NET Core 8 Hosting Bundle and IIS with Windows Authentication on the web server
- SQL Server 2016 or later
- Active Directory groups `Billing-Operators` and `Billing-CustomerService`
- Service accounts: `svc-billing` (engine), `svc-billing-web` (application pool),
  `svc-transfer` (print vendor and bank transfer jobs)
- A group for the docking stations and the AMR collection PC that may write read files, e.g. `GG-Meter-Read-Upload`
- A server certificate for `billing.<zone>`
- Build: .NET 8 SDK and the .NET Framework 4.8 developer pack (Visual Studio 2022 or Build Tools)

## Build and test

```
dotnet build MeterBilling.sln -c Release
dotnet test tests\MeterBilling.Core.Tests
```

Sample read files are in `samples`.

## Database

```
deploy\Initialize-Database.ps1 -SqlServer sql01.corp.local -EngineAccount CORP\svc-billing -WebAccount CORP\svc-billing-web -OperatorsGroup CORP\Billing-Operators
```

`db\03-reference-data.sql` loads the tariffs, the billing cycles `N1`, `S1`
and `COMM` and the test accounts used by the sample files; leave it out with
`-SkipReferenceData`.

## Deploy

1. DNS records (on the DNS server):

   ```
   deploy\Install-DnsRecords.ps1 -ZoneName corp.local -AllIP <ip>
   ```

   or one address per name with `-LookupIP`, `-EngineIP`, `-SqlServerIP`, `-FileServerIP`.

2. Publish (on the build machine):

   ```
   deploy\Publish.ps1 -OutputPath C:\Build\MeterBilling
   ```

3. Shares (on `files01`):

   ```
   deploy\New-BillingShares.ps1 -Root D:\Shares -EngineAccount CORP\svc-billing -ReadWriters CORP\GG-Meter-Read-Upload -TransferAccount CORP\svc-transfer -OperatorsGroup CORP\Billing-Operators
   ```

4. Engine (on `billing-engine`, as administrator):

   ```
   deploy\Install-EngineService.ps1 -PublishPath C:\Build\MeterBilling -ServiceAccount CORP\svc-billing -OperatorsGroup CORP\Billing-Operators
   ```

   Other settings from `MeterBilling.Engine.exe.config` can be passed with
   `-Settings @{ Key = 'value' }`, e.g. `AutoReleaseLimit`, `HighFactor`,
   `CreditorIban`. The script also creates `D:\MeterBilling\Logs`, the share
   `BillingLogs$`, the URL reservation for the control endpoint and the
   firewall rule.

5. Bill lookup site (on `billing`):

   ```
   deploy\Install-LookupSite.ps1 -PublishPath C:\Build\MeterBilling -PoolAccount CORP\svc-billing-web -CertificateThumbprint <thumbprint> -HostingBundle dotnet-hosting-8.0.x-win.exe
   setspn -S HTTP/billing.corp.local CORP\svc-billing-web
   ```

6. Billing Monitor (on each operator workstation):

   ```
   deploy\Install-Monitor.ps1 -PublishPath \\files01.corp.local\Software\MeterBilling
   ```

## Run

- Engine: `Start-Service CohoMeterBilling`, or `MeterBilling.Engine.exe console`
  in a command prompt. The engine works through the inbox, the due cycles and
  the open runs every minute (`WorkIntervalSeconds`).
- Logs: Application event log, source `Coho Meter Billing`, and
  `D:\MeterBilling\Logs\engine-yyyyMMdd.log`.
- Bill runs: a run opens on the cycle's cut-off date and is rated with the
  reads up to that date. Reads older than the cycle's read window, missing
  reads and meters not read (`NA`, `DM`) are estimated. High, low, zero and
  negative consumption, leak and tamper flags are exceptions: an operator
  accepts the read, bills on an estimate or leaves the account out of the run
  in Billing Monitor. Runs with no open exceptions are released automatically
  up to `AutoReleaseLimit`; larger runs are approved in Billing Monitor.
- Output: `\\files01\BillingOut$\Print\CW_<cycle>_<run>_<date>.prt` (layout
  CWPRT3, code page 1252), reprints in `Print\Reprint`, direct debits in
  `\\files01\BillingOut$\Bank\CWDD<date><run>.xml` (pain.008.001.02).
- Bill lookup: `https://billing.corp.local/` (members of `Billing-CustomerService`
  or `Billing-Operators`). Reprint requests are sent to the engine as
  `svc-billing-web`, which must be listed in the engine setting `ReprintCallers`.
- Try it: with the reference data loaded, copy `samples\N1_R12_20260428.csv`
  to `\\files01.corp.local\MeterReads$\Inbox\Handheld` and
  `samples\AMR0004471.dat` to `...\Inbox\Amr`. Cycles `N1` and `COMM` have
  their cut-off on 30 April 2026, so runs open on the next pass; open
  Billing Monitor to see the exceptions of the `N1` run.
