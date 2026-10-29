<#
    Business Rules policies (rules\*.xml, exported with the Rules Engine Deployment Wizard). Imports each file
    into the rule store, publishes and deploys the policy version it contains; a version that is already
    deployed is left alone, so a policy change needs a new minor version in the export.
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [hashtable] $Secrets
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

Add-Type -AssemblyName 'Microsoft.RuleEngine, Version=3.0.1.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35'
Add-Type -AssemblyName 'Microsoft.BizTalk.RuleEngineExtensions, Version=3.0.1.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35'

$driver = New-Object Microsoft.BizTalk.RuleEngineExtensions.RuleSetDeploymentDriver
$store = $driver.GetRuleStore()
$rulesFolder = Join-Path (Get-B2BDeployRoot) '..\rules'

foreach ($file in Get-ChildItem -Path $rulesFolder -Filter '*.xml' | Sort-Object Name) {
    $export = [xml](Get-Content -Path $file.FullName -Raw)
    $ruleset = $export.brl.ruleset
    $name = $ruleset.name
    $version = $ruleset.version
    $info = New-Object Microsoft.RuleEngine.RuleSetInfo($name, [int]$version.major, [int]$version.minor)

    $deployed = $driver.GetDeployedRuleSets() | Where-Object { $_.Name -eq $name -and $_.MajorRevision -eq $info.MajorRevision -and $_.MinorRevision -eq $info.MinorRevision }
    if ($deployed) {
        Write-B2BLog ("{0} {1}.{2} already deployed" -f $name, $info.MajorRevision, $info.MinorRevision)
        continue
    }

    $fileStore = New-Object Microsoft.RuleEngine.FileRuleStore($file.FullName)
    foreach ($vocabulary in $fileStore.GetVocabularies([Microsoft.RuleEngine.RuleStore+Filter]::All)) {
        if (-not $store.GetVocabularies($vocabulary.Name, [Microsoft.RuleEngine.RuleStore+Filter]::All) | Where-Object { $_.CurrentVersion.MajorRevision -eq $vocabulary.CurrentVersion.MajorRevision -and $_.CurrentVersion.MinorRevision -eq $vocabulary.CurrentVersion.MinorRevision }) {
            $store.Add($vocabulary, $true)
            Write-B2BLog "Vocabulary $($vocabulary.Name) published"
        }
    }
    $store.Add($fileStore.GetRuleSet($info), $true)
    $driver.Deploy($info)
    Write-B2BLog ("{0} {1}.{2} published and deployed" -f $name, $info.MajorRevision, $info.MinorRevision)
}
