#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$SiteName,
    [ValidateRange(1, 65535)][int]$Port = 8010
)
$ErrorActionPreference = 'Stop'
$assemblyPath = Join-Path $env:windir 'System32\inetsrv\Microsoft.Web.Administration.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) { throw 'IIS is not installed.' }
Add-Type -Path $assemblyPath
$manager = [Microsoft.Web.Administration.ServerManager]::new()
try {
    if ([string]::IsNullOrWhiteSpace($SiteName)) {
        $candidates = @($manager.Sites | Where-Object { @($_.Bindings | Where-Object { $_.EndPoint.Port -eq $Port }).Count -gt 0 })
        if ($candidates.Count -ne 1) { throw 'Cannot identify one IIS site by port. Pass -SiteName explicitly.' }
        $SiteName = $candidates[0].Name
    }
    $site = $manager.Sites[$SiteName]
    if ($null -eq $site) { throw 'IIS site not found.' }
    $application = $site.Applications['/']
    $pool = $manager.ApplicationPools[$application.ApplicationPoolName]
    if ($null -eq $pool) { throw 'Application Pool not found.' }
    $webRoot = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($application.VirtualDirectories['/'].PhysicalPath))
    $settingsPath = Join-Path $webRoot 'appsettings.json'
    if (-not (Test-Path -LiteralPath $settingsPath)) { throw 'appsettings.json not found in IIS physical path.' }
    $settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    [xml]$webConfig = Get-Content -LiteralPath (Join-Path $webRoot 'web.config') -Raw
    $aspNetCore = $webConfig.SelectSingleNode('/configuration/location/system.webServer/aspNetCore')
    $initialization = $manager.GetWebConfiguration($SiteName).GetSection('system.webServer/applicationInitialization')
    $modulePresent = @($manager.GetApplicationHostConfiguration().GetSection('system.webServer/globalModules').GetCollection() |
        Where-Object { $_.GetAttributeValue('name') -eq 'ApplicationInitializationModule' }).Count -gt 0
    $report = [ordered]@{
        Computer = $env:COMPUTERNAME
        CheckedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        Site = $SiteName
        SiteState = $site.State.ToString()
        PhysicalPath = $webRoot
        Pool = $pool.Name
        PoolState = $pool.State.ToString()
        StartMode = $pool.StartMode.ToString()
        IdleTimeoutMinutes = $pool.ProcessModel.IdleTimeout.TotalMinutes
        IdleTimeoutAction = $pool.ProcessModel.IdleTimeoutAction.ToString()
        PeriodicRecycleMinutes = $pool.Recycling.PeriodicRestart.Time.TotalMinutes
        RecycleSchedule = @($pool.Recycling.PeriodicRestart.Schedule | ForEach-Object { $_.Value.ToString() })
        RecycleMemoryKilobytes = $pool.Recycling.PeriodicRestart.Memory
        RecyclePrivateMemoryKilobytes = $pool.Recycling.PeriodicRestart.PrivateMemory
        RecycleRequests = $pool.Recycling.PeriodicRestart.Requests
        DisallowOverlappingRotation = $pool.Recycling.DisallowOverlappingRotation
        MaximumWorkerProcesses = $pool.ProcessModel.MaxProcesses
        PreloadEnabled = $application.GetAttributeValue('preloadEnabled')
        ApplicationInitializationInstalled = $modulePresent
        InitializeAfterRestart = $initialization.GetAttributeValue('doAppInitAfterRestart')
        InitializationPages = @($initialization.GetCollection() | ForEach-Object { $_.GetAttributeValue('initializationPage') })
        HostingModel = $aspNetCore.GetAttribute('hostingModel')
        ProcessPath = $aspNetCore.GetAttribute('processPath')
        BackgroundProcessingConfigured = $settings.BackgroundProcessing.Enabled
        OpcUaConfigured = $settings.OpcUa.Enabled
        SettingsNote = 'Configuration flags and SQL probe use appsettings.json; also check environment-specific JSON and environment variable overrides.'
        IisWorkers = @($manager.WorkerProcesses | Where-Object { $_.AppPoolName -eq $pool.Name } | ForEach-Object {
            $worker = Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue
            [ordered]@{ PID = $_.ProcessId; SessionId = $worker.SessionId; State = $_.State.ToString(); StartedAtUtc = if ($worker) { $worker.StartTime.ToUniversalTime().ToString('o') } else { $null } }
        })
        StandaloneWebProcesses = @(Get-Process -Name Oee.Web -ErrorAction SilentlyContinue | Where-Object {
            $_.Path -eq (Join-Path $webRoot 'Oee.Web.exe')
        } | ForEach-Object { [ordered]@{PID=$_.Id; SessionId=$_.SessionId} })
        OtherOeeProcesses = @(Get-Process -Name 'Oee*' -ErrorAction SilentlyContinue | Where-Object {
            $_.ProcessName -ne 'Oee.Web'
        } | ForEach-Object { [ordered]@{PID=$_.Id; Name=$_.ProcessName; SessionId=$_.SessionId} })
    }
    # Read timestamps only; never print connection strings, credentials, payloads or user data.
    $sql = $null
    try {
        if (-not [string]::IsNullOrWhiteSpace($settings.ConnectionStrings.DefaultConnection)) {
            $sql = [System.Data.SqlClient.SqlConnection]::new($settings.ConnectionStrings.DefaultConnection)
            $sql.Open()
            $command = $sql.CreateCommand()
            $command.CommandTimeout = 10
            $command.CommandText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Read-BackgroundActivity.sql') -Raw -Encoding UTF8
            $reader = $command.ExecuteReader()
            if ($reader.Read()) {
                $report['SqlActivityUtc'] = [ordered]@{
                    LastPlcData = if ($reader.IsDBNull(0)) { $null } else { $reader.GetValue(0).ToUniversalTime().ToString('o') }
                    LastCounterRead = if ($reader.IsDBNull(1)) { $null } else { $reader.GetValue(1).ToUniversalTime().ToString('o') }
                    LastOeeCalculation = if ($reader.IsDBNull(2)) { $null } else { $reader.GetValue(2).ToUniversalTime().ToString('o') }
                }
            }
            foreach ($section in @('DeviceDataActivityUtc', 'SuspectedOutputsCrossingPlanBoundary')) {
                $rows = @()
                if ($reader.NextResult()) {
                    while ($reader.Read()) {
                        $row = [ordered]@{}
                        for ($column = 0; $column -lt $reader.FieldCount; $column++) {
                            $value = if ($reader.IsDBNull($column)) { $null } else { $reader.GetValue($column) }
                            if ($value -is [DateTimeOffset]) { $value = $value.ToUniversalTime().ToString('o') }
                            $row[$reader.GetName($column)] = $value
                        }
                        $rows += [pscustomobject]$row
                    }
                }
                $report[$section] = $rows
            }
            $report['OutputDiagnosticNote'] = 'Candidate rows only; timestamps spanning plans do not prove how much quantity belongs to each shift. No data is changed.'
            $reader.Close()
        }
    } catch { $report['SqlProbe'] = 'Unavailable using appsettings.json; check effective SQL configuration on this server.'; $report['SqlProbeErrorType'] = $_.Exception.GetType().Name }
    finally { if ($sql) { $sql.Dispose() } }
    $report | ConvertTo-Json -Depth 8
    # Deliberately do not request /health or open a browser: those requests could wake an idle app.
} finally { $manager.Dispose() }
