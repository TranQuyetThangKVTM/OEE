#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$SiteName,
    [ValidateRange(1, 65535)][int]$Port = 8098,
    [ValidateRange(1, 1440)][int]$ObserveMinutes = 30,
    [ValidateRange(15, 300)][int]$IntervalSeconds = 30
)

$ErrorActionPreference = 'Stop'
$logRoot = Join-Path $PSScriptRoot 'Logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$logFile = Join-Path $logRoot ('Background-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.jsonl')
$selector = @{ Port = $Port }
if ($SiteName) { $selector.SiteName = $SiteName }
$previous = $null
$until = [DateTimeOffset]::UtcNow.AddMinutes($ObserveMinutes)
Write-Host ('Read-only IIS/SQL observation on port ' + $Port + '. No HTTP requests, no IIS recycle, no database writes.')
Write-Host 'Close all OEE browser tabs. Keep this console running across the shift boundary. Ctrl+C stops observation.'
Write-Host ('Log: ' + $logFile)
try {
    do {
        $text = & (Join-Path $PSScriptRoot 'Test-IisBackground.ps1') @selector | Out-String
        $current = $text | ConvertFrom-Json
        $current | ConvertTo-Json -Depth 10 -Compress | Add-Content -LiteralPath $logFile -Encoding UTF8
        Write-Host ('UTC ' + $current.CheckedAtUtc + ' | Pool=' + $current.PoolState + ' | PID=' + (($current.IisWorkers | ForEach-Object { $_.PID }) -join ',') + ' | IdleMinutes=' + $current.IdleTimeoutMinutes)
        if (@($current.IisWorkers).Count -eq 0) {
            Write-Warning 'No IIS worker process exists for this App Pool. A running IIS service alone does not prove OEE is running.'
        }
        if ($current.SqlProbe) { Write-Warning $current.SqlProbe }
        foreach ($device in $current.DeviceDataActivityUtc) {
            $prior = @($previous.DeviceDataActivityUtc | Where-Object { $_.ExternalDeviceId -eq $device.ExternalDeviceId }) | Select-Object -First 1
            $status = 'First observation'
            if ($prior) {
                $receiving = $device.LastDataAt -and $prior.LastDataAt -and [DateTimeOffset]::Parse($device.LastDataAt) -gt [DateTimeOffset]::Parse($prior.LastDataAt)
                if (!$receiving) { $status = 'OPC reception has not advanced' }
                elseif ($device.CounterPending -eq 1) { $status = 'OPC receiving; counter recovery pending' }
                elseif ($device.LastTotalCount -eq $prior.LastTotalCount) { $status = 'OPC receiving; accepted counter unchanged' }
                elseif (!$device.CurrentPlan) { $status = 'Counter advanced; no active plan' }
                elseif ($device.CurrentPlan -ne $prior.CurrentPlan) { $status = 'Plan changed; inspect recorded quantity for new plan' }
                elseif ($device.RecordedTotal -gt $prior.RecordedTotal) { $status = 'Counter and recorded production advanced' }
                else { $status = 'Counter advanced; recorded production unchanged' }
            }
            Write-Host ('  ' + $device.WorkUnit + ' | Counter=' + $device.LastTotalCount + ' | Plan=' + $device.CurrentPlan + ' | Recorded=' + $device.RecordedTotal + ' | ' + $status)
        }
        $previous = $current
        $remaining = ($until - [DateTimeOffset]::UtcNow).TotalSeconds
        if ($remaining -le 0) { break }
        Start-Sleep -Seconds ([int][Math]::Min($IntervalSeconds, [Math]::Ceiling($remaining)))
    } while ([DateTimeOffset]::UtcNow -lt $until)
    Write-Host ('Observation complete. Send this log for diagnosis: ' + $logFile)
} catch {
    Write-Host ('FAILED: ' + $_.Exception.GetType().Name + '. No settings were changed. Verify Administrator and port/site selection.') -ForegroundColor Red
    Write-Host ('Partial log: ' + $logFile)
    exit 1
}
