# Copy outside source control, fill in and pass with -SecretsFile. Every value is required for a production deployment.
@{
    SqlAdminPassword        = ''
    WebDbPassword           = ''   # clinic_web
    ServicesDbPassword      = ''   # clinic_services
    InterfaceDbPassword     = ''   # clinic_interface
    BatchDbPassword         = ''   # clinic_batch
    PortalDbPassword        = ''   # clinic_portal
    ReportsDbPassword       = ''   # clinic_reports
    PortalApiKey            = ''   # X-Clinic-Key the patient site sends to the ASMX booking service
    InternalServiceKey      = ''   # X-Clinic-Key between the staff site and the WCF services
    ServiceCertificatePfx   = ''   # path to clinic-svc.pfx (WCF message security)
    ServiceCertificatePassword = ''
    MachineValidationKey    = ''   # patient site machineKey (web farm)
    MachineDecryptionKey    = ''
    SftpPassword            = ''
    SmsToken                = ''
    ReportServerUser        = ''   # local account on rpt01 with Content Manager on /Southridge
    ReportServerPassword    = ''
}
