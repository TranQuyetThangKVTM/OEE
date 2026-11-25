[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WebRoot,
    [Parameter(Mandatory = $true)][ValidateSet('OeeDb', 'OeeMasan2')][string]$ExpectedDatabase,
    [string]$SettingsFile,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$installedRoot = (Resolve-Path -LiteralPath $WebRoot).Path
$settingsPath = if ([string]::IsNullOrWhiteSpace($SettingsFile)) { Join-Path $installedRoot 'appsettings.json' } else { (Resolve-Path -LiteralPath $SettingsFile).Path }
if (-not (Test-Path -LiteralPath (Join-Path $installedRoot 'Oee.Web.exe'))) {
    throw 'WebRoot must be the installed OEE Web directory containing Oee.Web.exe.'
}
if (-not (Test-Path -LiteralPath $settingsPath)) { throw 'Installed Web appsettings.json was not found.' }
$settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($settings.ConnectionStrings.DefaultConnection)) { throw 'Installed SQL connection string is missing.' }
$sql = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$sql.set_ConnectionString([string]$settings.ConnectionStrings.DefaultConnection)
if ($sql.get_InitialCatalog() -cne $ExpectedDatabase) { throw ('Database mismatch. Expected ' + $ExpectedDatabase + '; settings target ' + $sql.get_InitialCatalog() + '. No database changed.') }

$migratorRoot = Join-Path $PSScriptRoot 'DatabaseMigrator'
$executable = Join-Path $migratorRoot 'Oee.DatabaseMigrator.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Packaged DatabaseMigrator executable was not found.' }
$values = @{
    OEE_SETTINGS_FILE = $settingsPath
    ConnectionStrings__DefaultConnection = $sql.get_ConnectionString()
    Deployment__ExpectedDatabase = $ExpectedDatabase
    DOTNET_ENVIRONMENT = 'Production'
    ASPNETCORE_ENVIRONMENT = 'Production'
}
$previousEnvironment = @{}
foreach ($name in $values.Keys) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $values[$name], 'Process')
}
try {
    Push-Location -LiteralPath $migratorRoot
    try {
        Write-Host ('SQL Server: ' + $sql.get_DataSource() + '; database: ' + $ExpectedDatabase)
        & $executable verify
        if ($LASTEXITCODE -ne 0) { throw 'Existing database verification failed. Update stopped without creating or seeding a database.' }
        if (-not $VerifyOnly) {
            & $executable migrate
            if ($LASTEXITCODE -ne 0) { throw "Database migration failed (exit $LASTEXITCODE). Do not start the new Web yet." }
            & $executable verify
            if ($LASTEXITCODE -ne 0) { throw "Database verification failed (exit $LASTEXITCODE)." }
        }
    } finally { Pop-Location }
} finally {
    foreach ($name in $values.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}
