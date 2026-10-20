[CmdletBinding()]
param([string]$OutputFile, [string]$DockerPath)
$ErrorActionPreference = 'Stop'
if (!$DockerPath) { $DockerPath=Join-Path $env:LOCALAPPDATA 'Programs/DockerDesktop/resources/bin/docker.exe' }
if (!$OutputFile) { $OutputFile=Join-Path ([IO.Path]::GetTempPath()) "eip-operation-$([Guid]::NewGuid().ToString('N')).json" }
if (Test-Path -LiteralPath $OutputFile) { throw 'Use um arquivo novo para preservar evidências.' }
$query=Get-Content (Join-Path $PSScriptRoot 'operational-snapshot.sql') -Raw
$output=& $DockerPath exec eip-sqlserver sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd "$@"' sh -S localhost -U sa -C -N -b -r 1 -y 0 -w 65535 -d EIP -Q $query
if ($LASTEXITCODE -ne 0) { throw 'Falha ao consultar snapshot operacional.' }
$snapshot=($output -join '').Trim() | ConvertFrom-Json
$snapshot | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputFile -Encoding utf8
Write-Host "Snapshot salvo: $OutputFile; tenants: $($snapshot.tenants.Count)."
