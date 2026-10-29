#Requires -Version 7.2
<#
.SYNOPSIS
  Full deployment of the retail back office to one environment, in dependency order.
.EXAMPLE
  .\Deploy-All.ps1 -Environment .\lab.psd1
  .\Deploy-All.ps1 -Environment .\prod.psd1 -SkipInfrastructure
#>
param(
    [Parameter(Mandatory)][string] $Environment,
    [switch] $SkipInfrastructure,
    [switch] $SkipPublish
)
. "$PSScriptRoot\Common.ps1"

if (-not $SkipPublish) { & "$PSScriptRoot\Publish.ps1" }

if (-not $SkipInfrastructure) {
    & "$PSScriptRoot\Install-DnsRecords.ps1" -Environment $Environment
    & "$PSScriptRoot\Initialize-Database.ps1" -Environment $Environment
    & "$PSScriptRoot\Install-RabbitMq.ps1" -Environment $Environment
}

# Services that own data first, then the ones that call them, the front end last.
& "$PSScriptRoot\Install-Service.ps1" -Environment $Environment -Project VanArsdel.Inventory.Api
& "$PSScriptRoot\Install-WebSite.ps1" -Environment $Environment -Project VanArsdel.Catalog.Api, VanArsdel.Pricing.Api, VanArsdel.Customers.Api
& "$PSScriptRoot\Install-WebSite.ps1" -Environment $Environment -Project VanArsdel.Allocation.Api, VanArsdel.Orders.Api, VanArsdel.Returns.Api, VanArsdel.StoreOps.Api, VanArsdel.Reporting.Api
& "$PSScriptRoot\Install-Service.ps1" -Environment $Environment -Project VanArsdel.ErpConnector, VanArsdel.Notifications, VanArsdel.PosFeed, VanArsdel.Scheduler
& "$PSScriptRoot\Install-WebSite.ps1" -Environment $Environment -Project VanArsdel.Gateway, VanArsdel.BackOffice, VanArsdel.StoreManager

Write-Host 'Deployment finished. Health overview: https://backoffice.corp.local/health-ui' -ForegroundColor Green
