. (Join-Path $PSScriptRoot 'Package-Common.ps1')
Write-Host 'Create NEW OeeMasan2 and import the packaged master data.'
Write-Host 'Existing OeeMasan2 will be refused without modification.'
Write-Host 'Password must contain uppercase, lowercase, a digit and a special character.'
$adminSecret = Read-Host 'Choose the new OEE admin password (at least 12 characters)' -AsSecureString
$secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminSecret)
try {
    $env:Seed__AdminPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
    if ($env:Seed__AdminPassword.Length -lt 12) { throw 'Use an administrator password of at least 12 characters.' }
    Invoke-PackageMigrator 'initialize-fieldtest'
    Invoke-PackageMigrator 'verify'
    Write-Host 'Initialization succeeded. Login username: admin; password: the value you just entered.'
} finally {
    Remove-Item -LiteralPath Env:Seed__AdminPassword -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer)
    $adminSecret.Dispose()
}
