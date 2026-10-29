# Batch Release

QA batch disposition for Proseware Pharmaceuticals, Turku site. The MES sends each executed batch
record with its laboratory results and deviations; QA reviewers check the record, and a Qualified
Person (QP) releases or rejects the batch with an electronic signature. ERP collects the decision
and the signed batch certificate before stock is moved from quarantine to available.

## Components

| Project | Runs on | Description |
| --- | --- | --- |
| `src/BatchRelease.Portal` | `release.corp.local` (IIS) | Razor Pages portal: release queue, batch file, signing, signers, release policies. Windows sign-in |
| `src/BatchRelease.Api` | `release-api.corp.local` (IIS) | REST API for the MES (submit batches and documents) and ERP (status, dispositions, certificates). API keys |
| `src/BatchRelease.SigningHost` | `release-sign.corp.local` | Windows service: signs decisions with the QPs' keys, keeps the record vault, checks access, writes the security audit trail. Also the admin command line |
| `src/BatchRelease.Vault` | | Record vault, release policy access checks and security audit |
| `src/BatchRelease.Contracts` | | Signing host messages and HTTP client |
| `src/BatchRelease.Data` | | EF Core data model, queries and MES intake |
| `src/BatchRelease.Core` | | Batch model, disposition rules (SOP QA-0412), certificate content, release rights |
| `tests/BatchRelease.Tests` | | Unit and API tests |
| `db` | `release-sql.corp.local` | Database scripts |
| `deploy` | | DNS, database, IIS, vault, signing host, policy and API key scripts |
| `samples` | | Sample batch T2600418 with its batch record and certificate of analysis |

### Decisions

| Decision | Who (release policy right) | Result |
| --- | --- | --- |
| Review | Review | Batch `Reviewed` |
| Release | Release | Batch `Released`, certificate `RC-<site>-<year>-<nnnnn>` |
| Reject | Reject | Batch `Rejected`, certificate `RJ-<site>-<year>-<nnnnn>` |
| Hold / Resume | Hold | Batch `OnHold` / back to the previous step |

Every decision is signed with the user's Windows account and password and the confirmed meaning of the
signature. The signing host logs the user on, checks the product family's release policy against the
user's token, runs the disposition rules (a different person must release than reviewed) and records
the decision. Release and reject decisions are signed with the QP's own signing key as CMS/PKCS #7 over
the certificate content (JSON with the hashes of the batch documents) and stored in the vault.

Release policies are per product family. Rights: ReadRecord `0x1`, Review `0x2`, Release `0x4`,
Reject `0x8`, Hold `0x10`, ManageSigners `0x20`, ManagePolicy `0x40`.

### Security log events (source *Proseware Batch Release*)

| Id | Event |
| --- | --- |
| 4100 | Decision signed |
| 4101 | Signature credentials rejected |
| 4102 | Decision denied (rights or rules) |
| 4103 / 4104 | Record opened / access denied |
| 4105 | Record integrity check failed |

## Requirements

Build:

- .NET 8 SDK

Servers:

- Active Directory domain `CORP` (`corp.local`) with DNS
- `release`, `release-api`: Windows Server 2019 or later, IIS 10 with Windows Authentication, ASP.NET Core 8 Hosting Bundle
- `release-sign`:
  - Windows Server 2019 or later, .NET 8 runtime and ASP.NET Core 8 runtime (x64)
  - TPM 2.0, enabled and owned (`Get-Tpm`), for the QP signing keys
  - Local NTFS volume for the vault (default `D:\ReleaseVault`), EFS enabled, an EFS data recovery agent
  - Server certificate `CN=release-sign.corp.local` in `LocalMachine\My`
  - Signing CA certificate with private key in `LocalMachine\My` (`deploy\new-signing-ca.ps1` or issued by the corporate PKI)
- `release-sql`: SQL Server 2019 or later
- Accounts:
  - `CORP\svc-release-web`, `CORP\svc-release-api`: IIS application pools
  - `CORP\svc-release-sign`: signing host service. Member of *Windows Authorization Access Group*;
    rights *Log on as a service* and *Generate security audits*
  - Groups for the release policies, for example `CORP\BR-QA-Readers`, `CORP\BR-QA-Reviewers`,
    `CORP\BR-QP-OSD`, `CORP\BR-QP-STI`, `CORP\BR-QA-Systems`
  - QPs and reviewers sign with their own domain accounts; they need *Access this computer from the network* on the signing host

## Build and test

```
dotnet build BatchRelease.sln -c Release
dotnet test BatchRelease.sln
```

## Deploy

Run from an elevated PowerShell on each server, in this order.

1. DNS (on a DNS server). For a single test server point all names at it:

   ```
   .\deploy\register-dns.ps1 -IPAddress 10.10.0.21
   ```

2. Database:

   ```
   .\deploy\install-database.ps1 -SqlInstance release-sql.corp.local
   .\deploy\set-release-policy.ps1 -Family OSD -Readers 'CORP\BR-QA-Readers' -Reviewers 'CORP\BR-QA-Reviewers' `
       -QualifiedPersons 'CORP\BR-QP-OSD' -Administrators 'CORP\BR-QA-Systems'
   ```

   Repeat `set-release-policy.ps1` for `STI` and `TOP`.

3. Signing host (`release-sign`):

   ```
   .\deploy\create-vault.ps1 -Path D:\ReleaseVault -ServiceAccount 'CORP\svc-release-sign'
   .\deploy\new-signing-ca.ps1 -ExportPath C:\Temp\batch-release-signing-ca.cer
   .\deploy\install-signing-host.ps1 -ServiceAccount 'CORP\svc-release-sign' -ServicePassword (Read-Host -AsSecureString) `
       -CertificateThumbprint <server cert> -IssuerThumbprint <signing CA> `
       -TrustedCallers 'CORP\svc-release-web','CORP\svc-release-api'
   ```

   On a server without a TPM add `-KeyStorageProvider 'Microsoft Software Key Storage Provider' -AllowSoftwareKeys`.

4. Enrol each Qualified Person (elevated, on the signing host, as a member of `BR-QA-Systems`):

   ```
   & 'C:\Services\BatchReleaseSigning\BatchRelease.SigningHost.exe' enroll --account CORP\avirtanen --name "Anna Virtanen"
   & 'C:\Services\BatchReleaseSigning\BatchRelease.SigningHost.exe' list-signers
   & 'C:\Services\BatchReleaseSigning\BatchRelease.SigningHost.exe' check-access --account CORP\avirtanen --family OSD
   ```

   `revoke --account` disables the enrolment and deletes the key.

5. Web sites (`release`, `release-api`):

   ```
   .\deploy\install-web.ps1 -CertificateThumbprint <cert> `
       -PortalPoolAccount 'CORP\svc-release-web' -PortalPoolPassword (Read-Host -AsSecureString) `
       -ApiPoolAccount 'CORP\svc-release-api' -ApiPoolPassword (Read-Host -AsSecureString)
   ```

6. API keys:

   ```
   .\deploy\new-api-key.ps1 -Name 'MES Turku' -Submit -SiteCode TKU
   .\deploy\new-api-key.ps1 -Name 'ERP' -Read
   ```

## Run

Send the sample batch and its documents as the MES:

```
.\samples\submit-sample.ps1 -ApiKey <MES key>
```

Open `https://release.corp.local/`, review T2600418 as a reviewer, then release it as an enrolled QP.
ERP reads the result:

```
GET /api/v1/batches/T2600418               X-Api-Key: <ERP key>
GET /api/v1/batches/T2600418/certificate   -> RC-TKU-2026-00001.p7s
GET /api/v1/dispositions?since=2026-10-01
```

The certificate verifies against the signing CA certificate, for example
`certutil -verify` after extracting the signer, or `SignedCms.CheckSignature`.

### API

| Method | Path | Key | Description |
| --- | --- | --- | --- |
| POST | `/api/v1/batches` | submit | Submit or resubmit a batch (JSON, see `samples/T2600418.json`); frozen once reviewed |
| POST | `/api/v1/batches/{batch}/documents` | submit | `multipart/form-data`: `kind` (`ExecutedBatchRecord`, `CertificateOfAnalysis`, `DeviationReport`, `Other`), `file` |
| GET | `/api/v1/batches/{batch}` | read | Status, decisions, documents |
| GET | `/api/v1/batches/{batch}/certificate` | read | Signed batch certificate (`application/pkcs7-mime`) |
| GET | `/api/v1/dispositions?since=` | read | Released and rejected batches since a time (default 7 days) |
| GET | `/health` | | Health check |

## Configuration

| Setting | Where | Default |
| --- | --- | --- |
| `ConnectionStrings:BatchRelease` | all | `release-sql.corp.local`, integrated security |
| `SigningHost:BaseAddress` | portal, API | `https://release-sign.corp.local:5443/` |
| `SigningHost:Urls` | signing host | `https://release-sign.corp.local:5443/` |
| `Signing:KeyStorageProvider` | signing host | `Microsoft Platform Crypto Provider` |
| `Signing:RequireHardwareKeys` | signing host | `true` |
| `Signing:IssuerThumbprint` | signing host | signing CA thumbprint |
| `Signing:TrustedCallers` | signing host | application pool accounts |
| `Signing:DefaultDomain` | signing host | `CORP` |
| `Vault:Root` | signing host | `D:\ReleaseVault` |
| `Vault:RequireEncryption` | signing host | `true` |
| `Vault:SecurityAuditing` | signing host | `true` |
