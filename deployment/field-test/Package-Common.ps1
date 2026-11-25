$ErrorActionPreference = 'Stop'
$packageRoot = $PSScriptRoot
$settingsPath = Join-Path $packageRoot 'appsettings.json'
if (-not (Test-Path -LiteralPath $settingsPath)) { throw 'Missing root appsettings.json.' }
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$sqlConfig = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$sqlConfig.set_ConnectionString([string]$settings.ConnectionStrings.DefaultConnection)
if ($sqlConfig.get_InitialCatalog() -cne 'OeeMasan2' -or $settings.Deployment.ExpectedDatabase -cne 'OeeMasan2') {
    throw 'Package configuration must target the exact database OeeMasan2.'
}
$env:OEE_SETTINGS_FILE = $settingsPath
$env:ConnectionStrings__DefaultConnection = $sqlConfig.get_ConnectionString()
$env:Deployment__ExpectedDatabase = 'OeeMasan2'
$env:Deployment__MasterDataFile = Join-Path $packageRoot 'Database\master-data.json'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
# Test local trên máy đích; không thay đổi cookie policy hay bỏ authentication của ứng dụng.
$logRoot = Join-Path $packageRoot 'Logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

function Invoke-PackageMigrator([string]$Command) {
    Push-Location (Join-Path $packageRoot 'DatabaseMigrator')
    try {
        & '.\Oee.DatabaseMigrator.exe' $Command
        if ($LASTEXITCODE -ne 0) { throw "DatabaseMigrator failed (exit $LASTEXITCODE). See the message above." }
    } finally { Pop-Location }
}
