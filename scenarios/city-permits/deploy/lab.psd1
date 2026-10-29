# Lab server (single Windows Server 2025 host): every DNS name points at host01, development
# providers stand in for products that are not installed there.
@{
    ZoneName        = 'corp.local'
    TargetHost      = 'host01.corp.local'
    HostNames       = @('permits', 'permits-office', 'inspect', 'maps', 'opendata', 'permits-app01', 'permits-sql', 'gis-sql', 'records01')
    SqlServer       = 'host01'
    GisProvider     = 'Development'      # Geomatix Pro is not installed on the lab server
    GisDataPath     = 'C:\CityPermits\samples\gis'
    ArchiveProvider = 'Folder'           # no FILESTREAM on SQL Express
    ArchiveFolder   = 'D:\CityPermits\Archive'
    EidMode         = 'Development'
    BackOfficeAuth  = 'Development'
    InspectionsAuth = 'Development'
    PaymentsMode    = 'Simulator'
    RecordsMode     = 'Simulator'
    OcrEngine       = 'Windows'          # Windows OCR works on the lab server once en-GB OCR is added
    MailMode        = 'Pickup'
}
