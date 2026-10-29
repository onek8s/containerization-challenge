<#
.SYNOPSIS
    Installs the policy service and the documents service as Windows services on the application server.
.DESCRIPTION
    HumongousPolicyService  binds and issues policies, mid-term changes, cancellations, renewal
                            invitations, quote expiry, lapses and the monthly bordereau (32-bit,
                            RateCore in process; run Install-RateCore.ps1 on this server first).
    HumongousDocuments      quote and policy documents (PDF) and broker e-mails.

    Requires the .NET 8 Runtime x64 and x86 and the .NET 8 Windows Desktop Runtime x86.
    Both services run as the given service account, which gets "Log on as a service".
.EXAMPLE
    .\Install-AppServices.ps1 -PublishRoot D:\Drops\BrokerQuotes\5.8.0 -ServiceAccount CORP\svc-hq-app -ServicePassword $pw `
        -SqlPasswords @{ Policy = '...'; Documents = '...' } -MqPasswords @{ Policy = '...'; Documents = '...' } `
        -SftpKeyPath D:\Humongous\Keys\lucerne_bdx.key -SftpHostKeyFingerprint 'SHA256:...'
#>
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [Parameter(Mandatory)] [securestring] $ServicePassword,
    [Parameter(Mandatory)] [hashtable] $SqlPasswords,
    [Parameter(Mandatory)] [hashtable] $MqPasswords,
    [string] $ZoneName = 'corp.local',
    [string] $ServiceRoot = 'C:\Services\Humongous',
    [string] $SqlServer = 'sql01',
    [string] $MqServer = 'mq01',
    [string] $SmtpServer = 'smtp',
    [ValidateSet('RateCore', 'Simulator')] [string] $RatingProvider = 'RateCore',
    [string] $RateCoreTablesPath = 'D:\RateCore\Tables',
    [ValidateSet('Sftp', 'Folder')] [string] $BordereauDelivery = 'Sftp',
    [string] $BordereauFolder = 'D:\Humongous\Bordereaux',
    [string] $SftpKeyPath = 'D:\Humongous\Keys\lucerne_bdx.key',
    [string] $SftpHostKeyFingerprint = '',
    [ValidateSet('Smtp', 'Pickup')] [string] $MailMode = 'Smtp',
    [string] $MailPickupFolder = 'D:\Humongous\MailPickup',
    [string] $LogRoot = 'D:\Logs\Humongous'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Assert-Administrator

if ($RatingProvider -eq 'RateCore' -and -not (Test-RateCoreRegistered)) {
    throw 'RateCore is not registered on this server. Run Install-RateCore.ps1 first, or use -RatingProvider Simulator on a training server.'
}

$sql = "$SqlServer.$ZoneName"
$credential = New-Object Management.Automation.PSCredential($ServiceAccount, $ServicePassword)
Grant-LogonAsService -Account $ServiceAccount

$services = @(
    @{ Name = 'HumongousPolicyService'; Folder = 'policy-service'; Exe = 'Humongous.PolicyService.exe'
       DisplayName = 'Humongous Policy Service'; Description = 'Broker Quotes policy administration, renewals and bordereaux.'
       Settings = @{
           ConnectionStrings = @{ Quoting = "Server=$sql;Database=HumongousQuoting;User Id=hq_policy;Password=$($SqlPasswords.Policy);Encrypt=True;Application Name=Policy Service" }
           Rating = @{ Provider = $RatingProvider; TablesPath = $RateCoreTablesPath }
           Bus = @{ Host = "$MqServer.$ZoneName"; Password = $MqPasswords.Policy }
           Bordereau = @{ Delivery = $BordereauDelivery; FolderPath = $BordereauFolder; Sftp = @{ PrivateKeyPath = $SftpKeyPath; HostKeyFingerprint = $SftpHostKeyFingerprint } }
           Serilog = @{ WriteTo = @(@{ Name = 'File'; Args = @{ path = "$LogRoot\PolicyService\policy-service-.log"; rollingInterval = 'Day' } }) }
       } },
    @{ Name = 'HumongousDocuments'; Folder = 'documents'; Exe = 'Humongous.Documents.exe'
       DisplayName = 'Humongous Documents'; Description = 'Broker Quotes quote and policy documents.'
       Settings = @{
           ConnectionStrings = @{ Quoting = "Server=$sql;Database=HumongousQuoting;User Id=hq_documents;Password=$($SqlPasswords.Documents);Encrypt=True;Application Name=Documents" }
           Bus = @{ Host = "$MqServer.$ZoneName"; Password = $MqPasswords.Documents }
           Mail = @{ Mode = $MailMode; Host = "$SmtpServer.$ZoneName"; PickupFolder = $MailPickupFolder }
           Serilog = @{ WriteTo = @(@{ Name = 'File'; Args = @{ path = "$LogRoot\Documents\documents-.log"; rollingInterval = 'Day' } }) }
       } }
)

foreach ($svc in $services) {
    $path = Join-Path $ServiceRoot $svc.Folder
    $source = Join-Path $PublishRoot $svc.Folder
    if (-not (Test-Path $source)) { throw "Published files for $($svc.Name) not found in $source." }

    $existing = Get-Service $svc.Name -ErrorAction SilentlyContinue
    if ($existing -and $existing.Status -ne 'Stopped') { Stop-Service $svc.Name -Force }

    New-Item -ItemType Directory -Path $path -Force | Out-Null
    Copy-Item (Join-Path $source '*') $path -Recurse -Force
    Write-ProductionSettings -Path $path -Settings $svc.Settings
    Grant-FolderAccess -Path $path -Identity $ServiceAccount -Rights ReadAndExecute

    $binary = Join-Path $path $svc.Exe
    if (-not $existing) {
        New-Service -Name $svc.Name -BinaryPathName "`"$binary`"" -DisplayName $svc.DisplayName -Description $svc.Description `
            -StartupType Automatic -Credential $credential | Out-Null
    }
    else {
        sc.exe config $svc.Name binPath= "`"$binary`"" obj= $ServiceAccount password= ($credential.GetNetworkCredential().Password) | Out-Null
    }
    sc.exe config $svc.Name start= delayed-auto | Out-Null
    sc.exe failure $svc.Name reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
    Start-Service $svc.Name
    Write-Host "$($svc.Name) started from $path"
}

foreach ($folder in @($LogRoot, $BordereauFolder, $MailPickupFolder)) {
    Grant-FolderAccess -Path $folder -Identity $ServiceAccount -Rights Modify
}
if ($RatingProvider -eq 'RateCore') {
    Grant-FolderAccess -Path $RateCoreTablesPath -Identity $ServiceAccount -Rights ReadAndExecute
}
if (Test-Path $SftpKeyPath) {
    Grant-FolderAccess -Path (Split-Path $SftpKeyPath) -Identity $ServiceAccount -Rights ReadAndExecute
}
