param([Parameter(Mandatory)] [string] $Output, [string] $Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$src = Join-Path $PSScriptRoot '..\src'
$apps = @{
    citizen = 'CityPermits.Citizen'; backoffice = 'CityPermits.BackOffice'; maps = 'CityPermits.MapService'; inspections = 'CityPermits.Inspections'
    opendata = 'CityPermits.OpenData'; jobs = 'CityPermits.Jobs'; intake = 'CityPermits.MailIntake'; admin = 'CityPermits.Admin'
}
foreach ($k in $apps.Keys) {
    dotnet publish (Join-Path $src "$($apps[$k])\$($apps[$k]).csproj") -c $Configuration -r win-x64 --self-contained false -o (Join-Path $Output $k)
    if ($LASTEXITCODE -ne 0) { throw "Publish of $($apps[$k]) failed." }
}
