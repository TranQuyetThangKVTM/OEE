. (Join-Path $PSScriptRoot 'Package-Common.ps1')
$probeConfig = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$probeConfig.set_ConnectionString($sqlConfig.get_ConnectionString())
$probeConfig.set_InitialCatalog('master')
$probeConfig.set_ConnectTimeout(10)
$connection = New-Object System.Data.SqlClient.SqlConnection($probeConfig.get_ConnectionString())
try { $connection.Open(); Write-Host 'SQL Server login: OK (master; no DDL or data writes).' }
finally { $connection.Dispose() }
$endpoint = [Uri]$settings.OpcUa.EndpointUrl
$test = Test-NetConnection -ComputerName $endpoint.Host -Port $endpoint.Port -InformationLevel Quiet -WarningAction SilentlyContinue
if (-not $test) { throw 'OPC UA TCP endpoint is unreachable. Check server, OPC service, routing and firewall.' }
Write-Host 'OPC UA TCP port: OK. This verifies reachability, not OPC session/tag permissions.'
