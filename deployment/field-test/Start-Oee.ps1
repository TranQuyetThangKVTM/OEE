. (Join-Path $PSScriptRoot 'Package-Common.ps1')
Invoke-PackageMigrator 'verify'
$webExe = Join-Path $packageRoot 'Web\Oee.Web.exe'
$ownedWeb = @(Get-CimInstance Win32_Process -Filter "Name='Oee.Web.exe'" | Where-Object { $_.ExecutablePath -eq $webExe })
if ($ownedWeb.Count -eq 0 -and (Get-NetTCPConnection -LocalPort 5052 -State Listen -ErrorAction SilentlyContinue)) {
    throw 'Port 5052 is occupied by another application. Stop that application before launching this package.'
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ($ownedWeb.Count -eq 0) {
    $web = Start-Process -FilePath $webExe -ArgumentList @('--urls','http://localhost:5052','--environment','Development') -WorkingDirectory (Join-Path $packageRoot 'Web') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logRoot "web-$stamp.stdout.log") -RedirectStandardError (Join-Path $logRoot "web-$stamp.stderr.log") -PassThru
    Write-Host "Web started: PID $($web.Id)"
}
$ready = $false
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    try { $response = Invoke-WebRequest -Uri 'http://localhost:5052/Account/Login' -UseBasicParsing -TimeoutSec 2; if ($response.StatusCode -eq 200) { $ready = $true; break } } catch { }
    Start-Sleep -Seconds 1
}
if (-not $ready) { throw 'Web did not become ready. Check Logs and run Stop-Oee.ps1 before retrying.' }
Write-Host "Open http://localhost:5052 . Web hosts OPC UA and OEE background processing; WriteBackEnabled=$($settings.OpcUaWorker.WriteBackEnabled)."
