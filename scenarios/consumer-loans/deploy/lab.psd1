# Lab settings: everything on one Windows Server (host01) with the core simulator and
# the lab stand-ins for the credit bureau, e-signature and payments hub.
@{
    DnsZone        = 'corp.local'
    DnsServer      = 'localhost'
    Hosts          = @{
        'apply'          = '10.0.0.10'
        'lending-api'    = '10.0.0.10'
        'lending-office' = '10.0.0.10'
        'lending-app01'  = '10.0.0.10'
        'lending-lab'    = '10.0.0.10'
    }
    InstallRoot    = 'D:\ConsumerLoans'
    SitesRoot      = 'C:\inetpub\sites'
    LogRoot        = 'D:\Logs\ConsumerLoans'
    InterfaceRoot  = 'D:\Interfaces'
    CertificateThumbprint = ''           # empty: a self-signed certificate is created for the lab names

    Sql = @{
        Server            = 'localhost\SQLEXPRESS'
        LendingDatabase   = 'ConsumerLending'
        PortalDatabase    = 'ConsumerLendingPortal'
    }

    Core = @{
        Provider          = 'Simulator'
        SimulatorDatabase = 'D:\ConsumerLoans\Simulator\core-simulator.db'
    }

    RiskDw = @{
        Target            = 'Folder'
    }

    Services = @{
        Account           = 'LocalSystem'
        Documents         = 'LucerneLendingDocuments'
        Collections       = 'LucerneLendingCollections'
        LoanTapeTask      = 'Lucerne Lending Loan Tape'
    }

    AppPools = @{
        'apply'          = 'ApplicationPoolIdentity'
        'lending-api'    = 'ApplicationPoolIdentity'
        'lending-office' = 'ApplicationPoolIdentity'
        'lending-lab'    = 'ApplicationPoolIdentity'
    }

    Urls = @{
        Bureau      = 'https://lending-lab.corp.local/bureau/ConsumerReportService.svc'
        ESign       = 'https://lending-lab.corp.local/esign/'
        PaymentsHub = 'https://lending-lab.corp.local/payhub/'
        Smtp        = ''                 # empty: mails go to D:\ConsumerLoans\Mail as .eml
    }

    OfficeAuthMode = 'Development'       # cookie sign-in with role selection instead of ADFS
    PortalRequireTwoFactor = $false
    LabStubs = $true
}
