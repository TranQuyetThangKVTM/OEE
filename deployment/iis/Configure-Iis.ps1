#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$SiteName,
    [ValidateRange(1, 65535)][int]$Port = 8010
)

$ErrorActionPreference = 'Stop'
# Run in Windows PowerShell 5.1 on the IIS machine, after creating a dedicated OEE site/App Pool.
$adminAssembly = Join-Path $env:windir 'System32\inetsrv\Microsoft.Web.Administration.dll'
if (-not (Test-Path -LiteralPath $adminAssembly)) { throw 'IIS is not installed.' }
Add-Type -Path $adminAssembly
$manager = [Microsoft.Web.Administration.ServerManager]::new()
try {
    if ([string]::IsNullOrWhiteSpace($SiteName)) {
        $candidates = @($manager.Sites | Where-Object { @($_.Bindings | Where-Object { $_.EndPoint.Port -eq $Port }).Count -gt 0 })
        if ($candidates.Count -ne 1) { throw 'Cannot identify one IIS site by port. Pass -SiteName explicitly.' }
        $SiteName = $candidates[0].Name
    }
    $site = $manager.Sites[$SiteName]
    if ($null -eq $site) { throw 'Site not found. Create the OEE IIS site first.' }
    $application = $site.Applications['/']
    $pool = $manager.ApplicationPools[$application.ApplicationPoolName]
    if ($null -eq $pool) { throw 'Application Pool not found.' }
    $otherApplications = @($manager.Sites | ForEach-Object {
        $currentSite = $_
        $_.Applications | Where-Object {
            $_.ApplicationPoolName -eq $pool.Name -and ($currentSite.Name -ne $SiteName -or $_.Path -ne '/')
        }
    })
    if ($otherApplications.Count -gt 0) { throw 'Use a dedicated OEE App Pool; this pool is shared with another application.' }
    # Giữ nguyên tài khoản đang chạy để không làm hỏng đăng nhập SQL/credential OPC.
    $storageIdentity = switch ($pool.ProcessModel.IdentityType.ToString()) {
        'ApplicationPoolIdentity' { 'IIS AppPool\' + $pool.Name }
        'NetworkService' { 'NT AUTHORITY\NETWORK SERVICE' }
        'LocalService' { 'NT AUTHORITY\LOCAL SERVICE' }
        'LocalSystem' { 'NT AUTHORITY\SYSTEM' }
        'SpecificUser' { $pool.ProcessModel.UserName }
    }
    if ([string]::IsNullOrWhiteSpace($storageIdentity)) { throw 'Cannot resolve the existing App Pool identity.' }
    $webRoot = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($application.VirtualDirectories['/'].PhysicalPath))
    if (-not (Test-Path -LiteralPath (Join-Path $webRoot 'Oee.Web.exe'))) { throw 'The IIS site physical path does not contain Oee.Web.exe.' }
    $globalModules = $manager.GetApplicationHostConfiguration().GetSection('system.webServer/globalModules').GetCollection()
    if (-not @($globalModules | Where-Object { $_.GetAttributeValue('name') -eq 'ApplicationInitializationModule' }).Count) {
        throw 'Enable IIS Application Initialization before running this script (Windows Features / Server Manager).' }
    $backupName = 'OEE-Before-Background-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
    & (Join-Path $env:windir 'System32\inetsrv\appcmd.exe') add backup $backupName | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Cannot back up IIS configuration.' }
    $pool.ManagedRuntimeVersion = ''
    $pool.Enable32BitAppOnWin64 = $false
    $pool.AutoStart = $true
    $site.ServerAutoStart = $true
    $pool.StartMode = [Microsoft.Web.Administration.StartMode]::AlwaysRunning
    $pool.ProcessModel.IdleTimeout = [TimeSpan]::Zero
    $pool.ProcessModel.MaxProcesses = 1
    $pool.ProcessModel.LoadUserProfile = $true
    $pool.Recycling.DisallowOverlappingRotation = $true
    $pool.Recycling.PeriodicRestart.Time = [TimeSpan]::Zero
    $pool.Recycling.PeriodicRestart.Schedule.Clear()
    $application.SetAttributeValue('preloadEnabled', $true)
    $initialization = $manager.GetWebConfiguration($SiteName).GetSection('system.webServer/applicationInitialization')
    $initialization.SetAttributeValue('doAppInitAfterRestart', $true)
    $pages = $initialization.GetCollection()
    foreach ($oldWarmup in @($pages | Where-Object { $_.GetAttributeValue('initializationPage') -eq '/health' })) {
        $pages.Remove($oldWarmup)
    }
    if (-not @($pages | Where-Object { $_.GetAttributeValue('initializationPage') -eq '/health/live' }).Count) {
        $page = $pages.CreateElement('add')
        $page.SetAttributeValue('initializationPage', '/health/live')
        $pages.Add($page) | Out-Null
    }
    $manager.CommitChanges()
    foreach ($directory in @('logs', 'pki', 'App_Data')) {
        $directoryPath = Join-Path $webRoot $directory
        New-Item -ItemType Directory -Path $directoryPath -Force | Out-Null
        & icacls.exe $directoryPath /grant ($storageIdentity + ':(OI)(CI)M') | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Cannot grant App Pool Modify access.' }
    }
    if ($pool.State -eq [Microsoft.Web.Administration.ObjectState]::Started) { $pool.Recycle() | Out-Null }
    else { $pool.Start() | Out-Null }
    if ($site.State -ne [Microsoft.Web.Administration.ObjectState]::Started) { $site.Start() | Out-Null }
    $verification = [Microsoft.Web.Administration.ServerManager]::new()
    try {
        $verifiedApplication = $verification.Sites[$SiteName].Applications['/']
        $verifiedPool = $verification.ApplicationPools[$verifiedApplication.ApplicationPoolName]
        $verifiedInitialization = $verification.GetWebConfiguration($SiteName).GetSection('system.webServer/applicationInitialization')
        $hasWarmup = @($verifiedInitialization.GetCollection() | Where-Object {
            $_.GetAttributeValue('initializationPage') -eq '/health/live'
        }).Count -gt 0
        if ($verifiedPool.StartMode.ToString() -ne 'AlwaysRunning' -or
            $verifiedPool.ProcessModel.IdleTimeout -ne [TimeSpan]::Zero -or
            $verifiedPool.ProcessModel.MaxProcesses -ne 1 -or
            $verifiedPool.Recycling.PeriodicRestart.Time -ne [TimeSpan]::Zero -or
            $verifiedPool.Recycling.PeriodicRestart.Schedule.Count -ne 0 -or
            -not $verifiedApplication.GetAttributeValue('preloadEnabled') -or
            -not $verifiedInitialization.GetAttributeValue('doAppInitAfterRestart') -or
            -not $hasWarmup) {
            throw 'IIS configuration did not persist as expected. Inspect the saved log.'
        }
    } finally { $verification.Dispose() }
    Write-Host ('Verified OEE site ' + $SiteName + ': AlwaysRunning, preload /health/live, idle timeout 0, one process, no scheduled recycle. IIS backup: ' + $backupName)
} finally { $manager.Dispose() }
