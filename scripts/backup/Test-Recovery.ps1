[CmdletBinding()]
param(
    [string]$SourceContainer = 'eip-sqlserver',
    [string]$Database = 'EIP',
    [string]$OutputDirectory,
    [string]$DockerPath,
    [switch]$KeepRestoredContainer,
    [switch]$PublishRestoredPort
)

$ErrorActionPreference = 'Stop'
if ($SourceContainer -notmatch '^[a-zA-Z0-9][a-zA-Z0-9_.-]+$' -or $Database -notmatch '^[a-zA-Z][a-zA-Z0-9_]{0,63}$') {
    throw 'Nome de container ou banco inválido.'
}
if (!$DockerPath) {
    $dockerCommand = Get-Command docker -ErrorAction SilentlyContinue
    $DockerPath = if ($dockerCommand) { $dockerCommand.Source } else { Join-Path $env:LOCALAPPDATA 'Programs/DockerDesktop/resources/bin/docker.exe' }
}
if (!(Test-Path -LiteralPath $DockerPath -PathType Leaf)) { throw 'Docker não localizado. Informe -DockerPath.' }
$runId = [Guid]::NewGuid().ToString('N')
if (!$OutputDirectory) { $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) "eip-recovery-$runId" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use um diretório de saída novo para preservar evidências anteriores.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Write-Host "Diretório de evidências: $OutputDirectory"
$image = 'mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04'
$backupPath = "/var/opt/mssql/data/eip-recovery-$runId.bak"
$ownedContainers = [Collections.Generic.List[string]]::new()
$timer = [Diagnostics.Stopwatch]::StartNew()

function Invoke-Docker([string[]]$Arguments) {
    $output = & $DockerPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Docker falhou: $($output -join [Environment]::NewLine)" }
    return ($output -join [Environment]::NewLine)
}

function Invoke-Sql([string]$Container, [string]$Db, [string]$Sql, [string]$File) {
    $arguments = @('exec', $Container, 'sh', '-c',
        'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd "$@"',
        'sh', '-S', 'localhost', '-U', 'sa', '-C', '-N', '-b', '-r', '1', '-y', '0', '-w', '65535', '-d', $Db)
    if ($File) { $arguments += @('-i', $File) } else { $arguments += @('-Q', "SET NOCOUNT ON; $Sql") }
    return Invoke-Docker $arguments
}

function Assert-Owned([string]$Container) {
    $label = Invoke-Docker @('inspect', '--format', '{{index .Config.Labels "eip.recovery.run"}}', $Container)
    if ($label.Trim() -ne $runId -or $Container -eq $SourceContainer) { throw 'Container não pertence a este ensaio; operação recusada.' }
}

function New-RecoveryContainer([string]$Suffix) {
    $container = "eip-recovery-$Suffix-$runId"
    $password = "RecoveryOnly_$([Guid]::NewGuid().ToString('N'))!"
    $arguments = @('run', '-d', '--name', $container, '--label', "eip.recovery.run=$runId",
        '--env', 'ACCEPT_EULA=Y', '--env', 'MSSQL_PID=Developer', '--env', "MSSQL_SA_PASSWORD=$password")
    if ($PublishRestoredPort -and $Suffix -eq 'restored') { $arguments += @('-p', '127.0.0.1::1433') }
    Invoke-Docker ($arguments + @($image)) | Out-Null
    $ownedContainers.Add($container)
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        try { Invoke-Sql $container 'master' 'SELECT 1;' | Out-Null; return $container }
        catch { if ([DateTime]::UtcNow -gt $deadline) { throw }; Start-Sleep -Seconds 2 }
    } while ($true)
}

function Restore-Copy([string]$Container, $Files) {
    Assert-Owned $Container
    Invoke-Docker @('cp', (Join-Path $OutputDirectory 'database.bak'), "${Container}:/var/opt/mssql/data/recovery.bak") | Out-Null
    $moves = [Collections.Generic.List[string]]::new()
    $index = 0
    foreach ($file in $Files) {
        if ($file.type -notin 0,1) { throw 'Ensaio suporta arquivos ROWS/LOG; FILESTREAM exige runbook específico.' }
        $logical = $file.name.Replace("'", "''")
        $extension = if ($file.type -eq 1) { 'ldf' } else { 'mdf' }
        $moves.Add("MOVE N'$logical' TO N'/var/opt/mssql/data/recovery_$index.$extension'")
        $index++
    }
    Invoke-Sql $Container 'master' "RESTORE VERIFYONLY FROM DISK=N'/var/opt/mssql/data/recovery.bak' WITH CHECKSUM; RESTORE DATABASE [$Database] FROM DISK=N'/var/opt/mssql/data/recovery.bak' WITH $($moves -join ', '), CHECKSUM, RECOVERY;" | Out-Null
    foreach ($name in 'manifest.sql','validate-rls.sql') {
        Invoke-Docker @('cp', (Join-Path $PSScriptRoot $name), "${Container}:/tmp/$name") | Out-Null
    }
    Invoke-Sql $Container $Database "DBCC CHECKDB ([$Database]) WITH NO_INFOMSGS, ALL_ERRORMSGS;" | Out-Null
    Invoke-Sql $Container $Database '' '/tmp/validate-rls.sql' | Out-Null
    $manifest = (Invoke-Sql $Container $Database '' '/tmp/manifest.sql').Trim()
    $manifest | ConvertFrom-Json | Out-Null
    # Conferir novamente após a transação de leitura que desativa RLS só na cópia.
    Invoke-Sql $Container $Database '' '/tmp/validate-rls.sql' | Out-Null
    return $manifest
}

try {
    Invoke-Docker @('info', '--format', '{{.ServerVersion}}') | Out-Null
    Write-Host 'Criando backup COPY_ONLY com CHECKSUM do banco local...'
    $files = (Invoke-Sql $SourceContainer 'master' "SELECT name,type FROM sys.master_files WHERE database_id=DB_ID(N'$Database') ORDER BY file_id FOR JSON PATH;") | ConvertFrom-Json
    if (!$files) { throw 'Banco de origem não encontrado.' }
    Invoke-Sql $SourceContainer 'master' "BACKUP DATABASE [$Database] TO DISK=N'$backupPath' WITH COPY_ONLY, CHECKSUM, INIT;" | Out-Null
    Invoke-Docker @('cp', "${SourceContainer}:$backupPath", (Join-Path $OutputDirectory 'database.bak')) | Out-Null
    $backupHash = (Get-FileHash -LiteralPath (Join-Path $OutputDirectory 'database.bak') -Algorithm SHA256).Hash
    Write-Host 'Restaurando cópia inicial e verificando integridade e RLS...'
    $baselineContainer = New-RecoveryContainer 'baseline'
    $baseline = Restore-Copy $baselineContainer $files
    $baseline | Set-Content -LiteralPath (Join-Path $OutputDirectory 'baseline.json') -Encoding utf8
    Assert-Owned $baselineContainer
    $removedId = Invoke-Docker @('inspect', '--format', '{{.Id}}', $baselineContainer)
    Invoke-Docker @('rm', '-f', '-v', $baselineContainer) | Out-Null
    $ownedContainers.Remove($baselineContainer) | Out-Null
    Write-Host 'Container descartável removido. Recuperando em uma nova instância...'
    $restoredContainer = New-RecoveryContainer 'restored'
    $restored = Restore-Copy $restoredContainer $files
    $restored | Set-Content -LiteralPath (Join-Path $OutputDirectory 'restored.json') -Encoding utf8
    if ($baseline -cne $restored) { throw 'Dados ou políticas divergiram após a recuperação. Consulte os manifestos.' }
    $restoredId = Invoke-Docker @('inspect', '--format', '{{.Id}}', $restoredContainer)
    if ($restoredId -eq $removedId) { throw 'A recuperação precisa usar uma instância nova.' }
    $data = $restored | ConvertFrom-Json
    [ordered]@{
        status='passed'; runId=$runId; restoredContainer=$restoredContainer; keptRestoredContainer=[bool]$KeepRestoredContainer; completedAtUtc=[DateTimeOffset]::UtcNow.ToString('O'); sourceContainer=$SourceContainer
        database=$Database; image=$image; backupSha256=$backupHash
        tables=$data.tables.Count; rows=($data.tables | Measure-Object RowCount -Sum).Sum
        policies=$data.policies.Count; baselineContainerRemoved=$true; restoredToNewInstance=$true
        backupVerified=$true; checkDbPassed=$true; tableHashesMatch=$true; rlsProbePassed=$true
        elapsedSeconds=[Math]::Round($timer.Elapsed.TotalSeconds,2)
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding utf8
    Write-Host "Recuperação validada. Evidências e backup: $OutputDirectory"
}
finally {
    foreach ($container in $ownedContainers) {
        if ($KeepRestoredContainer -and $container -eq $restoredContainer -and (Test-Path (Join-Path $OutputDirectory 'result.json'))) { continue }
        Assert-Owned $container
        Invoke-Docker @('rm', '-f', '-v', $container) | Out-Null
    }
    # Só remove o arquivo temporário que este ensaio criou na origem, nunca dados do banco.
    if ($backupPath -match '^/var/opt/mssql/data/eip-recovery-[a-f0-9]{32}\.bak$') {
        & $DockerPath exec $SourceContainer rm -f -- $backupPath | Out-Null
    }
}
