[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SettingsFile,
    [Parameter(Mandatory = $true)][string]$Destination
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$publishRoot = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $publishRoot) { throw 'Use a new destination directory; existing deployments are not overwritten.' }
$settings = Get-Content -LiteralPath $SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($settings.ConnectionStrings.DefaultConnection)) { throw 'SQL connection string is missing.' }
& dotnet publish (Join-Path $repoRoot 'src\Oee.Web\Oee.Web.csproj') -c Release -r win-x64 --self-contained true -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'Web publish failed.' }
# Only remove the development override produced in this new publish directory.
$developmentSettings = Join-Path $publishRoot 'appsettings.Development.json'
if (Test-Path -LiteralPath $developmentSettings) { Remove-Item -LiteralPath $developmentSettings }
$webSettingsPath = Join-Path $publishRoot 'appsettings.json'
$webSettings = Get-Content -LiteralPath $webSettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$webSettings.ConnectionStrings.DefaultConnection = $settings.ConnectionStrings.DefaultConnection
$webSettings | Add-Member -MemberType NoteProperty -Name OpcUa -Value $settings.OpcUa -Force
if ($null -ne $settings.OpcUaWorker) {
    foreach ($property in $settings.OpcUaWorker.PSObject.Properties) {
        $webSettings.OpcUaWorker | Add-Member -MemberType NoteProperty -Name $property.Name -Value $property.Value -Force
    }
}
$webSettings.BackgroundProcessing.Enabled = $true
$webSettings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $webSettingsPath -Encoding UTF8
[xml]$iis = Get-Content -LiteralPath (Join-Path $publishRoot 'web.config') -Raw
$aspNetCore = $iis.SelectSingleNode('/configuration/location/system.webServer/aspNetCore')
$aspNetCore.SetAttribute('stdoutLogEnabled', 'true')
$aspNetCore.SetAttribute('stdoutLogFile', '.\logs\stdout')
$environmentVariables = $iis.CreateElement('environmentVariables')
$environment = $iis.CreateElement('environmentVariable')
$environment.SetAttribute('name', 'ASPNETCORE_ENVIRONMENT')
$environment.SetAttribute('value', 'Development')
$environmentVariables.AppendChild($environment) | Out-Null
$aspNetCore.AppendChild($environmentVariables) | Out-Null
$iis.Save((Join-Path $publishRoot 'web.config'))
foreach ($directory in @('logs', 'pki')) { New-Item -ItemType Directory -Path (Join-Path $publishRoot $directory) -Force | Out-Null }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Configure-Iis.ps1') -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README-IIS.md') -Destination $publishRoot
$verifiedSettings = Get-Content -LiteralPath $webSettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($verifiedSettings.ConnectionStrings.DefaultConnection -cne $settings.ConnectionStrings.DefaultConnection) { throw 'Published SQL configuration mismatch.' }
if (-not $verifiedSettings.BackgroundProcessing.Enabled) { throw 'Web background processing must be enabled.' }
if ($verifiedSettings.OpcUa.EndpointUrl -cne $settings.OpcUa.EndpointUrl) { throw 'Published OPC UA endpoint mismatch.' }
if (Get-ChildItem -LiteralPath $publishRoot -Filter 'Oee.Worker*' -File) { throw 'A separate Worker executable must not be included.' }
Write-Host ('Published one IIS Web host: ' + $publishRoot)
