$ErrorActionPreference = 'Stop'
# Chỉ dừng EXE thuộc đúng thư mục gói này, không dừng toàn bộ dotnet hoặc phần mềm khác.
foreach ($relativePath in @('Web\Oee.Web.exe')) {
    $exactPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $relativePath))
    $exeName = [IO.Path]::GetFileName($exactPath)
    Get-CimInstance Win32_Process -Filter "Name='$exeName'" | Where-Object { $_.ExecutablePath -eq $exactPath } | ForEach-Object {
        Stop-Process -Id $_.ProcessId
        Write-Host "Stopped $exeName (PID $($_.ProcessId))"
    }
}
