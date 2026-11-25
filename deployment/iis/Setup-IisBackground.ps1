[CmdletBinding()]
param(
    [string]$SiteName,
    [ValidateRange(1, 65535)][int]$Port = 8098,
    [ValidateRange(15, 300)][int]$ObserveSeconds = 30
)

$ErrorActionPreference = 'Stop'
$logRoot = Join-Path $PSScriptRoot 'Logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logFile = Join-Path $logRoot ('OEE-IIS-' + $stamp + '.log')
$selector = @{ Port = $Port }
if (-not [string]::IsNullOrWhiteSpace($SiteName)) { $selector.SiteName = $SiteName }
$exitCode = 1
Start-Transcript -LiteralPath $logFile | Out-Null
try {
    $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Right-click CAU-HINH-CHAY-NEN-IIS.cmd and choose Run as administrator.'
    }
    Write-Host ('OEE IIS background setup - port ' + $Port)
    Write-Host 'The OEE App Pool will recycle once. Keep browser pages closed during verification.'
    & (Join-Path $PSScriptRoot 'Configure-Iis.ps1') @selector

    $beforeText = & (Join-Path $PSScriptRoot 'Test-IisBackground.ps1') @selector | Out-String
    $before = $beforeText | ConvertFrom-Json
    $beforeText | Set-Content -LiteralPath (Join-Path $logRoot ($stamp + '-before.json')) -Encoding UTF8
    Write-Host ('Configuration applied. Observing SQL timestamps for ' + $ObserveSeconds + ' seconds without HTTP requests...')
    Start-Sleep -Seconds $ObserveSeconds
    $afterText = & (Join-Path $PSScriptRoot 'Test-IisBackground.ps1') @selector | Out-String
    $after = $afterText | ConvertFrom-Json
    $afterText | Set-Content -LiteralPath (Join-Path $logRoot ($stamp + '-after.json')) -Encoding UTF8
    if ($after.StartMode -ne 'AlwaysRunning' -or $after.IdleTimeoutMinutes -ne 0 -or
        $after.SiteState -ne 'Started' -or $after.PoolState -ne 'Started' -or @($after.IisWorkers).Count -ne 1 -or
        -not $after.PreloadEnabled -or -not $after.ApplicationInitializationInstalled -or
        -not $after.InitializeAfterRestart -or $after.MaximumWorkerProcesses -ne 1 -or
        $after.PeriodicRecycleMinutes -ne 0 -or @($after.RecycleSchedule).Count -ne 0 -or
        $after.InitializationPages -notcontains '/health/live') {
        throw 'IIS configuration verification failed. See the saved before/after reports.'
    }
    $advancedDevices = @($after.DeviceDataActivityUtc | Where-Object {
        $current = $_
        $previous = @($before.DeviceDataActivityUtc | Where-Object { $_.WorkUnit -eq $current.WorkUnit }) | Select-Object -First 1
        $previous -and $previous.LastDataAt -and $current.LastDataAt -and
            [DateTimeOffset]::Parse($current.LastDataAt) -gt [DateTimeOffset]::Parse($previous.LastDataAt)
    })
    if ($advancedDevices.Count -gt 0) {
        Write-Host ('SUCCESS: SQL reception timestamps advanced for ' + $advancedDevices.Count + ' device(s) without any browser requests.') -ForegroundColor Green
        Write-Host 'This confirms reception during this short observation only; it does not prove production was saved or the pool stays alive overnight.'
        $after.DeviceDataActivityUtc | Select-Object WorkUnit, LastTotalCount, CounterPending, CurrentPlan, RecordedTotal, LastOutputAt | Format-Table -AutoSize | Out-Host
        Write-Host 'An unchanged product counter is normal if the device produced nothing during this observation.'
        $exitCode = 0
    } else {
        Write-Warning 'IIS settings are verified, but advancing PLC timestamps were not confirmed. Check the JSON reports, effective SQL configuration, OPC tags and application logs.'
        $exitCode = 2
    }
    Write-Host ($afterText)
    Write-Host ('Saved reports: ' + $logRoot)
    Write-Host 'For a cross-shift/idle investigation, run KIEM-TRA-CHAY-NEN.cmd (read-only; no HTTP or recycle).'
}
catch {
    Write-Host ('FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'If Application Initialization is missing, enable it under IIS > World Wide Web Services > Application Development Features (or Server Manager), then run this file again.'
}
finally {
    Write-Host ('Log: ' + $logFile)
    Stop-Transcript | Out-Null
}
exit $exitCode
