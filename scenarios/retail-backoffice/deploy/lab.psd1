# Environment description for the LAB environment (vanarsdel-lab.corp.local test hosts).
# Copy to test.psd1 / prod.psd1 and change the host names; the scripts take -Environment <file>.
@{
    Name          = 'LAB'
    DnsServer     = 'dc01.corp.local'
    DnsZone       = 'corp.local'
    SqlServer     = 'sql01.corp.local'
    RabbitMqHost  = 'mq01.corp.local'
    RabbitMqVHost = 'retail'
    PackageShare  = 'https://artifacts.corp.local/retail-backoffice'
    InstallRoot   = 'D:\VanArsdel'
    LogRoot       = 'D:\Logs\VanArsdel'

    # Secrets come from the SecretManagement vault of the deployment agent, named "<Vault>/<name>".
    Vault         = 'RetailLab'

    # Wildcard certificate in LocalMachine\My on every app host; IIS bindings and Kestrel use it.
    CertificateSubject = 'CN=*.corp.local'

    Hosts = @{
        App01 = @{ Name = 'app01.corp.local'; Address = '10.20.30.11'; Role = 'Front' }
        App02 = @{ Name = 'app02.corp.local'; Address = '10.20.30.12'; Role = 'Services' }
        App03 = @{ Name = 'app03.corp.local'; Address = '10.20.30.13'; Role = 'Workers' }
    }

    # IIS sites (in-process hosting, one application pool per site, "No Managed Code").
    WebSites = @(
        @{ Project = 'VanArsdel.Gateway';         Site = 'Retail API Gateway';   HostName = 'api.corp.local';        Host = 'App01'; Pool = 'RetailGateway' }
        @{ Project = 'VanArsdel.BackOffice';      Site = 'Retail Back Office';   HostName = 'backoffice.corp.local'; Host = 'App01'; Pool = 'RetailBackOffice'; HealthPath = '/health/live' }
        @{ Project = 'VanArsdel.StoreManager';    Site = 'Retail Store Manager'; HostName = 'stores.corp.local';     Host = 'App01'; Pool = 'RetailStoreManager' }
        @{ Project = 'VanArsdel.Catalog.Api';     Site = 'Retail Catalog';       HostName = 'catalog.corp.local';    Host = 'App02'; Pool = 'RetailCatalog';    Database = 'Catalog' }
        @{ Project = 'VanArsdel.Pricing.Api';     Site = 'Retail Pricing';       HostName = 'pricing.corp.local';    Host = 'App02'; Pool = 'RetailPricing';    Database = 'Pricing' }
        @{ Project = 'VanArsdel.Allocation.Api';  Site = 'Retail Allocation';    HostName = 'allocation.corp.local'; Host = 'App02'; Pool = 'RetailAllocation'; Database = 'Allocation' }
        @{ Project = 'VanArsdel.Orders.Api';      Site = 'Retail Orders';        HostName = 'orders.corp.local';     Host = 'App02'; Pool = 'RetailOrders';     Database = 'Orders' }
        @{ Project = 'VanArsdel.Customers.Api';   Site = 'Retail Customers';     HostName = 'customers.corp.local';  Host = 'App02'; Pool = 'RetailCustomers' }
        @{ Project = 'VanArsdel.Returns.Api';     Site = 'Retail Returns';       HostName = 'returns.corp.local';    Host = 'App02'; Pool = 'RetailReturns';    Database = 'Returns' }
        @{ Project = 'VanArsdel.StoreOps.Api';    Site = 'Retail Store Ops';     HostName = 'storeops.corp.local';   Host = 'App02'; Pool = 'RetailStoreOps';   Database = 'StoreOps' }
        @{ Project = 'VanArsdel.Reporting.Api';   Site = 'Retail Reporting';     HostName = 'reporting.corp.local';  Host = 'App02'; Pool = 'RetailReporting';  Database = 'Reporting' }
    )

    # Windows services (self-contained publish is not used; the .NET runtimes are installed on App03).
    Services = @(
        @{ Project = 'VanArsdel.Inventory.Api';   Name = 'VanArsdelInventory';     DisplayName = 'VanArsdel Inventory (gRPC + REST)'; Host = 'App03'; Account = 'NT SERVICE\VanArsdelInventory';     Database = 'Inventory'; HostName = 'inventory.corp.local'; Port = 5001 }
        @{ Project = 'VanArsdel.ErpConnector';    Name = 'VanArsdelErpConnector';  DisplayName = 'VanArsdel ERP Connector';           Host = 'App03'; Account = 'NT SERVICE\VanArsdelErpConnector';  Database = 'ErpConnector' }
        @{ Project = 'VanArsdel.Notifications';   Name = 'VanArsdelNotifications'; DisplayName = 'VanArsdel Notifications';           Host = 'App03'; Account = 'NT SERVICE\VanArsdelNotifications'; Database = 'Notifications' }
        @{ Project = 'VanArsdel.PosFeed';         Name = 'VanArsdelPosFeed';       DisplayName = 'VanArsdel POS Feed';                Host = 'App03'; Account = 'NT SERVICE\VanArsdelPosFeed' }
        @{ Project = 'VanArsdel.Scheduler';       Name = 'VanArsdelScheduler';     DisplayName = 'VanArsdel Scheduler';               Host = 'App03'; Account = 'NT SERVICE\VanArsdelScheduler';     Database = 'Scheduler'; ConnectionName = 'Quartz' }
        # Quartz runs clustered: a second scheduler node on App02 takes over when App03 is down.
        @{ Project = 'VanArsdel.Scheduler';       Name = 'VanArsdelScheduler';     DisplayName = 'VanArsdel Scheduler';               Host = 'App02'; Account = 'NT SERVICE\VanArsdelScheduler';     Database = 'Scheduler'; ConnectionName = 'Quartz' }
    )

    # Filled in automatically on install (see Common.ps1, Update-AppSettings):
    #   ConnectionStrings:*            password from vault "sql-<login>"
    #   Messaging:Password             vault "rabbitmq-<Messaging:Username>"
    #   Serilog:WriteTo:1:Args:apiKey  vault "seq-apikey"
    #   ServiceIdentity:ClientId       ClientIds below, secret from vault "entra-<project>"
    # Anything else per project; "vault:<name>" values are read from the vault.
    Settings = @{
        'VanArsdel.BackOffice'    = @{
            'Oidc:Providers:0:ClientId' = '3f6c1a52-8d0e-4b7a-a9e4-55c2d81f0b13'; 'Oidc:Providers:0:ClientSecret' = 'vault:entra-backoffice'
            'Oidc:Providers:1:ClientId' = 'b2c7e1a4-6f39-4d25-8c0e-91a3f4d27e66'; 'Oidc:Providers:1:ClientSecret' = 'vault:adfs-backoffice'
        }
        'VanArsdel.Catalog.Api'   = @{ 'Search:Elasticsearch:Password' = 'vault:elastic-catalog' }
        'VanArsdel.Orders.Api'    = @{ 'WebShopBus:ConnectionString' = 'vault:webshop-servicebus'; 'Psp:MerchantKey' = 'vault:psp-merchant-key'; 'Psp:WebhookSecret' = 'vault:psp-webhook'; 'Carrier:ApiKey' = 'vault:carrier-apikey'; 'Carrier:WebhookSecret' = 'vault:carrier-webhook' }
        'VanArsdel.Returns.Api'   = @{ 'Psp:MerchantKey' = 'vault:psp-merchant-key'; 'Psp:CertificatePassword' = 'vault:psp-pfx'; 'Returns:CarrierApiKey' = 'vault:carrier-apikey'; 'Returns:CarrierWebhookSecret' = 'vault:carrier-webhook' }
        'VanArsdel.Notifications' = @{ 'Notifications:Postiq:ApiKey' = 'vault:postiq-apikey' }
        'VanArsdel.PosFeed'       = @{ 'PosFeed:Sftp:PrivateKeyPassphrase' = 'vault:pos-sftp-key'; 'PosFeed:Sftp:HostKeyFingerprint' = 'ssh-ed25519 256 3b:7f:0c:91:5e:a2:44:d8:19:6b:c0:2e:8f:71:a5:d3' }
        'VanArsdel.Inventory.Api' = @{ 'Kestrel:Endpoints:Https:Certificate:Password' = 'vault:inventory-pfx' }
    }

    # Entra ID app registrations of the service identities (client credentials between the services).
    ClientIds = @{
        'VanArsdel.Catalog.Api'    = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e01'
        'VanArsdel.Pricing.Api'    = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e02'
        'VanArsdel.Inventory.Api'  = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e03'
        'VanArsdel.Allocation.Api' = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e04'
        'VanArsdel.Orders.Api'     = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e05'
        'VanArsdel.Customers.Api'  = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e06'
        'VanArsdel.Returns.Api'    = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e07'
        'VanArsdel.StoreOps.Api'   = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e08'
        'VanArsdel.Reporting.Api'  = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e09'
        'VanArsdel.Notifications'  = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e10'
        'VanArsdel.Scheduler'      = 'c0a8f1e2-1b2c-4d3e-8f90-0a1b2c3d4e11'
    }
}
