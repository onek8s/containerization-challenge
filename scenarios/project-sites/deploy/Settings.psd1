# Environment settings used by the deploy scripts. Secrets are not stored here: the scripts prompt for them
# or read them from the parameters.
@{
    Domain                = 'corp.local'
    NetBiosDomain         = 'CORP'
    DnsServer             = 'dc01.corp.local'

    # SharePoint farm
    WebApplicationUrl     = 'https://projects.corp.local'
    HubUrl                = 'https://projects.corp.local/sites/hub'
    ProjectManagedPath    = 'projects'
    FarmAdmin             = 'CORP\spadmin'
    ContentDatabaseServer = 'spsql'
    HubContentDatabase    = 'WSS_Content_ProjectsHub'
    RecordsManagers       = 'CORP\GG-ProjectSites-Records'

    # High-trust (server-to-server) add-ins
    IssuerId              = 'a3f1c9e7-5b2d-4f60-8e1a-9c7b5d3e1f20'
    IssuerName            = 'Lucerne Project Sites issuer'
    SigningCertificate    = '\\fs01.corp.local\Install$\ProjectSites\Certificates\ProjectSitesS2S.cer'
    DashboardsClientId    = '7c1f5e2a-3b4d-4e6f-8a9b-0c1d2e3f4a5b'
    SyncClientId          = '2e9d4b71-6a3c-4f58-b0e2-7d1c9a5f3b84'

    # Add-in web site
    DashboardsHost        = 'dashboards.corp.local'
    DashboardsServer      = 'web03.corp.local'
    DashboardsPath        = 'D:\Sites\ProjectDashboards'
    DashboardsAppPool     = 'ProjectDashboards'
    DashboardsLogs        = 'D:\Logs\ProjectDashboards'

    # Sync service
    SyncServer            = 'sync01.corp.local'
    SyncPath              = 'D:\Services\LucerneProjectSync'
    SyncAccount           = 'CORP\svc-ps-sync'
    SyncLogs              = 'D:\Logs\ProjectSync'

    # Add-in database
    SqlServer             = 'sql01.corp.local'
    Database              = 'ProjectSitesHub'

    # Build drop shared by Publish.ps1 and the install scripts
    DropRoot              = '\\fs01.corp.local\Install$\ProjectSites\Drops'
}
