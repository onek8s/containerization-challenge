@{
    RootModule        = 'LucerneProjectSites.psm1'
    ModuleVersion     = '5.3.1'
    GUID              = '1b7e4c2d-9a3f-4e58-b6d1-0c2e4f6a8b93'
    Author            = 'Lucerne Engineering - Collaboration Services'
    CompanyName       = 'Lucerne Engineering'
    Copyright         = '(c) 2014-2019 Lucerne Engineering'
    Description       = 'Build and install helpers for Lucerne Project Sites (farm solutions, add-in site, sync service, database).'
    PowerShellVersion = '4.0'
    FunctionsToExport = @(
        'Get-ProjectSitesSettings', 'Get-ProjectSitesDrop', 'Assert-Elevated', 'Assert-SharePointShell',
        'Wait-SolutionJob', 'Enable-ProjectSitesFeature', 'Set-ProjectSitesAppPool', 'Invoke-SqlScript',
        'Write-Step'
    )
}
