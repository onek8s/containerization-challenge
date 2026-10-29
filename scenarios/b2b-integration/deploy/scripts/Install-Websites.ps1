<#
    Order gateway (public WCF service behind the reverse proxy, /partners/) and operations portal (intranet,
    Windows authentication) on the internal web server. App pool passwords come from the secrets file; the
    gateway pool runs as the account the BizTalk receive web.config allows (B2BReceive).
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [Parameter(Mandatory)] [hashtable] $Secrets
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

$src = Join-Path (Get-B2BDeployRoot) '..\src'
$https = Test-B2BTrue $Settings.UseHttps
$share = '\\{0}\{1}' -f $Settings.FileServer, $Settings.FileShare
$firstBizTalk = (Get-B2BServerList $Settings.BizTalkServers)[0]
$domainAccount = { param($user) $user -and $user -notlike 'IIS APPPOOL\*' }

function Set-AppSetting([xml] $xml, [string] $key, [string] $value) {
    $node = $xml.configuration.appSettings.add | Where-Object { $_.key -eq $key }
    if (-not $node) {
        $node = $xml.CreateElement('add')
        $node.SetAttribute('key', $key)
        [void]$xml.configuration.appSettings.AppendChild($node)
    }
    $node.SetAttribute('value', $value)
}

function Set-ConnectionString([xml] $xml, [string] $name, [string] $value) {
    $node = $xml.configuration.connectionStrings.add | Where-Object { $_.name -eq $name }
    if ($node) { $node.SetAttribute('connectionString', $value) }
}

$gatewayUser = $Settings.GatewayAppPoolUser
Publish-B2BWebApplication -Server $Settings.WebServer -ProjectFolder (Join-Path $src 'VanArsdel.Integration.Gateway') -Site 'B2BGateway' `
    -HostHeader $Settings.B2BPublicHost -Environment $Environment -UseHttps $https -CertificateThumbprint $Settings.CertificateThumbprint `
    -AppPoolUser $(if (& $domainAccount $gatewayUser) { $gatewayUser }) -AppPoolPassword (Get-B2BSecret $Secrets 'GatewayAppPoolPassword' -Optional) `
    -ConfigureWebConfig {
        param($xml)
        $db = 'Data Source={0};Initial Catalog={1};Integrated Security=SSPI;Application Name=B2B.Gateway' -f $Settings.SqlServer, $Settings.IntegrationDatabase
        Set-ConnectionString $xml 'VanArsdelIntegration' $db
        Set-ConnectionString $xml 'BAMPrimaryImport' ('Data Source={0};Initial Catalog={1};Integrated Security=SSPI;Application Name=B2B.Gateway' -f $Settings.SqlServer, $Settings.BamDatabase)
        $client = $xml.configuration.'system.serviceModel'.client.endpoint | Where-Object { $_.name -eq 'BizTalkPartnerOrders' }
        $client.SetAttribute('address', ('http://{0}{1}' -f $firstBizTalk, $Settings.GatewayReceivePath))
    }

$portalUser = $Settings.PortalAppPoolUser
Publish-B2BWebApplication -Server $Settings.WebServer -ProjectFolder (Join-Path $src 'VanArsdel.Integration.OpsPortal') -Site 'B2BOpsPortal' `
    -HostHeader $Settings.OpsPortalHost -Environment $Environment -UseHttps $https -CertificateThumbprint $Settings.CertificateThumbprint `
    -AppPoolUser $(if (& $domainAccount $portalUser) { $portalUser }) -AppPoolPassword (Get-B2BSecret $Secrets 'PortalAppPoolPassword' -Optional) `
    -Authentication Windows -ConfigureWebConfig {
        param($xml)
        Set-AppSetting $xml 'Portal.Environment' $Environment
        Set-AppSetting $xml 'Portal.Group.Viewers' $Settings.PortalViewerGroup
        Set-AppSetting $xml 'Portal.Group.Operators' $Settings.OperatorsGroup
        Set-AppSetting $xml 'Portal.Group.Admins' $Settings.PortalAdminGroup
        Set-AppSetting $xml 'BizTalk.MgmtDbServer' $Settings.SqlServer
        Set-AppSetting $xml 'BizTalk.MgmtDbName' $Settings.ManagementDatabase
        Set-AppSetting $xml 'BizTalk.WmiServer' $firstBizTalk
        Set-AppSetting $xml 'Resubmit.DefaultFolder' "$share\resubmit\default"
        foreach ($p in @{ rp_Customer_EDI_AS2 = 'customer-edi'; rp_Customer_EDI_FILE = 'customer-edi'; rp_Supplier_EDI = 'supplier-edi'; rp_Gateway_Orders = 'gateway'; rp_Erp_IDoc = 'idoc' }.GetEnumerator()) {
            Set-AppSetting $xml ('Resubmit.Folder.' + $p.Key) ('{0}\resubmit\{1}' -f $share, $p.Value)
        }
        Set-ConnectionString $xml 'VanArsdelIntegration' ('Data Source={0};Initial Catalog={1};Integrated Security=SSPI;Application Name=B2B.OpsPortal' -f $Settings.SqlServer, $Settings.IntegrationDatabase)
        Set-ConnectionString $xml 'BizTalkMsgBoxDb' ('Data Source={0};Initial Catalog=BizTalkMsgBoxDb;Integrated Security=SSPI;Application Name=B2B.OpsPortal;Connect Timeout=10' -f $Settings.SqlServer)
    }

if (& $domainAccount $portalUser) {
    # Kerberos for the portal host name (useAppPoolCredentials), registered once by the domain admins
    Write-B2BLog ('Check the SPN: setspn -S HTTP/{0} {1}' -f $Settings.OpsPortalHost, $portalUser)
}
