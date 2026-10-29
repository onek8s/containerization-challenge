# Copy to a location outside the repository (e.g. D:\Deploy\consumer-loans-secrets.psd1) and fill in.
# Pass with -Secrets to Install-Database.ps1, Install-WebSites.ps1 and Install-AppServices.ps1.
@{
    'Sql.lending_api'    = ''
    'Sql.lending_office' = ''
    'Sql.lending_portal' = ''
    'Sql.lending_docs'   = ''
    'Sql.lending_coll'   = ''
    'Sql.lending_tape'   = ''
    'ApiKey.Portal'      = ''   # X-Api-Key of the customer portal towards lending-api
    'ApiKey.Office'      = ''   # X-Api-Key of the back office (Swagger testing, partner tooling)
    'Bureau.Password'    = ''
    'ESign.ApiKey'       = ''
    'ESign.CallbackSecret' = ''
    'PaymentsHub.ClientSecret' = ''
    'RiskDw.Password'    = ''
    'Service.Password'   = ''   # password of the service account (empty for gMSA / LocalSystem)
}
