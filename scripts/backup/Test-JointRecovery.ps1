[CmdletBinding()]
param(
    [Guid]$TenantId = '5e17b5c9-5862-4029-a627-d95261cbf8a7',
    [string]$OutputDirectory,
    [string]$DockerPath,
    [switch]$VerifyApplication,
    [string]$HostDll,
    [string]$WorkerDll,
    [switch]$VerifyRetry,
    [switch]$VerifyVolume
)
$ErrorActionPreference = 'Stop'
if ($VerifyRetry -and !$WorkerDll) { throw 'VerifyRetry exige WorkerDll.' }
if ($VerifyVolume -and (!$WorkerDll -or $VerifyRetry)) { throw 'VerifyVolume exige WorkerDll e cenário separado de VerifyRetry.' }
if ($WorkerDll -and (!$VerifyApplication -or !(Test-Path -LiteralPath $WorkerDll -PathType Leaf))) { throw 'Worker exige -VerifyApplication e uma publicação válida de -WorkerDll.' }
if ($VerifyApplication -and (!(Test-Path -LiteralPath $HostDll -PathType Leaf) -or !$env:EIP_RECOVERY_DEMO_PASSWORD)) {
    throw 'Informe -HostDll publicado e EIP_RECOVERY_DEMO_PASSWORD com a senha existente da conta Demo.'
}
if ($TenantId -eq [Guid]::Empty) { throw 'Informe a organização Demo a validar.' }
if (!$DockerPath) { $DockerPath = Join-Path $env:LOCALAPPDATA 'Programs/DockerDesktop/resources/bin/docker.exe' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) "eip-joint-recovery-$([Guid]::NewGuid().ToString('N'))" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use uma pasta nova para preservar evidências.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$sqlDirectory = Join-Path $OutputDirectory 'sql'
$sqlResult = $null
try {
    & (Join-Path $PSScriptRoot 'Test-Recovery.ps1') -DockerPath $DockerPath -OutputDirectory $sqlDirectory -KeepRestoredContainer -PublishRestoredPort:$VerifyApplication
    $sqlResult = Get-Content -Raw (Join-Path $sqlDirectory 'result.json') | ConvertFrom-Json
    if ($sqlResult.status -ne 'passed' -or !$sqlResult.keptRestoredContainer) { throw 'Restauração SQL não confirmada.' }
    $verificationArgs = @($DockerPath, $sqlResult.restoredContainer, $sqlResult.runId, $TenantId.ToString(), $OutputDirectory)
    if ($VerifyApplication) { $verificationArgs += (Resolve-Path -LiteralPath $HostDll).Path }
    if ($WorkerDll) { $verificationArgs += (Resolve-Path -LiteralPath $WorkerDll).Path }
    if ($VerifyRetry) { $verificationArgs += '--verify-retry' }
    if ($VerifyVolume) { $verificationArgs += '--verify-volume' }
    & node (Join-Path $PSScriptRoot 'verify-raw-recovery.mjs') @verificationArgs
    if ($LASTEXITCODE -ne 0) { throw 'Conferência conjunta falhou; evidências preservadas.' }
    $jointResult = Get-Content -Raw (Join-Path $OutputDirectory 'raw-result.json') | ConvertFrom-Json
}
finally {
    if ($sqlResult) {
        $container = $sqlResult.restoredContainer
        $label = & $DockerPath inspect --format '{{index .Config.Labels "eip.recovery.run"}}' $container
        if ($LASTEXITCODE -ne 0 -or $label.Trim() -ne $sqlResult.runId -or $container -notmatch '^eip-recovery-restored-[a-f0-9]{32}$') {
            throw 'Limpeza recusada: instância não corresponde ao ensaio.'
        }
        & $DockerPath rm -f -v $container | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao remover a instância descartável.' }
    }
}
$jointResult | Add-Member -NotePropertyName isolatedContainersRemoved -NotePropertyValue $true
$jointResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding utf8
Write-Host "Recuperação conjunta validada: $OutputDirectory"
