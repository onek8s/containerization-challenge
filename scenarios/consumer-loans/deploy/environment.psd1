# Production settings for the Consumer Loans deployment scripts.
# Secrets are left empty here and passed with -Secrets (a .psd1 kept outside source control).
@{
    DnsZone        = 'corp.local'
    DnsServer      = 'dc01.corp.local'
    Hosts          = @{
        'apply'          = '10.20.4.21'   # web01, IIS site apply
        'lending-api'    = '10.20.4.21'   # web01, IIS site lending-api
        'lending-office' = '10.20.4.22'   # web02, IIS site lending-office
        'lending-app01'  = '10.20.4.31'   # documents + collections services, loan tape task
    }
    InstallRoot    = 'D:\ConsumerLoans'
    SitesRoot      = 'C:\inetpub\sites'
    LogRoot        = 'D:\Logs\ConsumerLoans'
    InterfaceRoot  = 'D:\Interfaces'
    CertificateThumbprint = ''           # TLS certificate in LocalMachine\My for the IIS bindings

    Sql = @{
        Server            = 'sql01.corp.local'
        LendingDatabase   = 'ConsumerLending'
        PortalDatabase    = 'ConsumerLendingPortal'
    }

    Core = @{
        Provider          = 'IbmI'
        SystemName        = 'core.corp.local'
        UseKerberos       = $true
        AcsPackage        = '\\files01.corp.local\Software\IBM\ACS\IBMiAccess_v1r1\Windows_Application\x64\IBM i Access Client Solutions 64-bit.msi'
    }

    RiskDw = @{
        Server            = 'riskdw.corp.local:50001'
        Database          = 'RISKDW'
        Schema            = 'LOANTAPE'
        User              = 'lnd_tape'
    }

    Services = @{
        Account           = 'CORP\svc-lending-app'
        Documents         = 'LucerneLendingDocuments'
        Collections       = 'LucerneLendingCollections'
        LoanTapeTask      = 'Lucerne Lending Loan Tape'
    }

    AppPools = @{
        'apply'          = 'CORP\svc-lending-portal'
        'lending-api'    = 'CORP\svc-lending-api'
        'lending-office' = 'CORP\svc-lending-office'
    }

    Urls = @{
        Bureau      = 'https://ws.nordiccredit.example/consumer/v3/ConsumerReportService.svc'
        ESign       = 'https://api.signhub.example'
        PaymentsHub = 'https://payhub.corp.local/'
        Adfs        = 'https://adfs.corp.local/FederationMetadata/2007-06/FederationMetadata.xml'
        Smtp        = 'smtp.corp.local'
    }

    LabStubs = $false
}
