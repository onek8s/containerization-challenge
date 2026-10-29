# Southridge Clinic Systems - production settings for the deployment scripts.
# Passwords and keys are not kept here: pass -SecretsFile with a copy of secrets.example.ps1 kept outside source control.
@{
    Name          = 'Production'
    Configuration = 'Release'
    DnsZone       = 'corp.local'
    DnsServer     = 'dc01.corp.local'
    Hosts         = @{
        'clinic'      = '10.40.2.21'   # web01: staff site
        'patients'    = '10.40.2.22'   # web02 (DMZ reverse proxy in front): patient site
        'clinic-ws'   = '10.40.2.21'   # web01: ASMX services
        'clinic-svc'  = '10.40.2.23'   # app01: WCF services and referral workflow
        'clinic-int'  = '10.40.2.24'   # app02: interface engine, billing batch, reminders
        'clinic-sql'  = '10.40.2.30'   # sql01
        'reports'     = '10.40.2.31'   # rpt01: Reporting Services 2008 R2
    }

    # Relative to the system drive of each server.
    SitesRoot     = 'inetpub\southridge'
    AppsRoot      = 'Southridge\Apps'
    DataRoot      = 'Southridge\Data'

    Sql = @{
        Server        = 'clinic-sql.corp.local'
        AdminUser     = 'sa'
        Database      = 'SouthridgeClinic'
        Workflow      = 'SouthridgeWorkflow'
        DataPath      = 'DEFAULT'   # or the instance data folder, ending with a backslash
        LogPath       = 'DEFAULT'
        DemoData      = $false
    }

    Sites = @(
        @{ Name = 'Southridge Staff';    Project = 'Southridge.Clinic.StaffWeb';    Host = 'clinic';     Pool = 'SouthridgeStaff';    Runtime = 'v4.0' }
        @{ Name = 'Southridge Patients'; Project = 'Southridge.Clinic.PatientWeb';  Host = 'patients';   Pool = 'SouthridgePatients'; Runtime = 'v4.0' }
        @{ Name = 'Southridge WS';       Project = 'Southridge.Clinic.WebServices'; Host = 'clinic-ws';  Pool = 'SouthridgeWS';       Runtime = 'v4.0' }
        @{ Name = 'Southridge Services'; Project = 'Southridge.Clinic.Services';    Host = 'clinic-svc'; Pool = 'SouthridgeServices'; Runtime = 'v4.0' }
    )
    CertificateThumbprint = ''      # LocalMachine\My certificate for the https bindings; empty = http only

    InterfaceEngine = @{
        ServiceName   = 'SouthridgeInterfaceEngine'
        HospitalHost  = 'stc-hl7.stcatherine.example'
        LabHost       = 'mllp.cascadelab.example'
    }

    Clearinghouse = @{
        Transport     = 'Sftp'
        SftpHost      = 'sftp.cascadeclaims.example'
        SftpPort      = 22
        SftpUser      = 'smgclinic'
    }

    Sms = @{
        GatewayUrl    = 'https://api.textlinkpro.example/v1/sms/send'
        Account       = 'SMG-4471'
    }

    ReportServer = @{
        Url           = 'http://reports.corp.local/ReportServer'
        Folder        = '/Southridge/Clinic'
        DataSource    = 'Data Source=clinic-sql.corp.local;Initial Catalog=SouthridgeClinic'
    }

    # Scheduled tasks on clinic-int (Task Scheduler 2.0, run whether user is logged on or not).
    Tasks = @(
        @{ Name = 'Southridge\Billing - create and send claims'; Program = 'BillingBatch\SouthridgeBilling.exe'; Arguments = 'claims send';          Schedule = 'DAILY';  At = '22:30' }
        @{ Name = 'Southridge\Billing - responses';              Program = 'BillingBatch\SouthridgeBilling.exe'; Arguments = 'responses';           Schedule = 'HOURLY'; At = '06:15' }
        @{ Name = 'Southridge\Billing - statements and aging';   Program = 'BillingBatch\SouthridgeBilling.exe'; Arguments = 'statements aging';    Schedule = 'WEEKLY'; At = '02:00'; Day = 'MON' }
        @{ Name = 'Southridge\Reminders - appointment reminders'; Program = 'Reminders\SouthridgeReminders.exe';      Arguments = 'reminders';           Schedule = 'DAILY';  At = '17:00' }
        @{ Name = 'Southridge\Reminders - no-shows';             Program = 'Reminders\SouthridgeReminders.exe';      Arguments = 'noshows';             Schedule = 'HOURLY'; At = '08:45' }
        @{ Name = 'Southridge\Reminders - waitlist offers';      Program = 'Reminders\SouthridgeReminders.exe';      Arguments = 'waitlist';            Schedule = 'HOURLY'; At = '08:50' }
    )
}
