[CmdletBinding()]
param(
    [string]$PreviousRef = '3c2de50',
    [string]$OutputDirectory,
    [string]$DockerPath
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$runId = [Guid]::NewGuid().ToString('N')
if (!$OutputDirectory) { $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) "eip-rollback-$runId" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use uma pasta de saída nova.' }
if (!$DockerPath) {
    $command = Get-Command docker -ErrorAction SilentlyContinue
    $DockerPath = if ($command) { $command.Source } else { Join-Path $env:LOCALAPPDATA 'Programs/DockerDesktop/resources/bin/docker.exe' }
}
if (!(Test-Path -LiteralPath $DockerPath -PathType Leaf)) { throw 'Docker não localizado.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Write-Host "Diretório de evidências: $OutputDirectory"
$containers = [Collections.Generic.List[string]]::new()
$previousConnection = $env:EIP_TENANT_DB_CONNECTION
$timer = [Diagnostics.Stopwatch]::StartNew()
$previousMigration = '20260801174154_AddRoleToMembership'
$candidateMigration = '20260802013236_AddCompanyCountryCodeAndTradeName'

function Invoke-Docker([string[]]$Arguments) {
    $output = & $DockerPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Docker falhou: $($output -join [Environment]::NewLine)" }
    return ($output -join [Environment]::NewLine)
}
function Assert-Owned([string]$Container) {
    $label = Invoke-Docker @('inspect','--format','{{index .Config.Labels "eip.rollback.run"}}',$Container)
    if ($label.Trim() -ne $runId -or $Container -notlike "eip-rollback-*-$runId") { throw 'Container fora do ensaio; operação recusada.' }
}
function Remove-Owned([string]$Container) {
    Assert-Owned $Container
    Invoke-Docker @('rm','-f','-v',$Container) | Out-Null
    $containers.Remove($Container) | Out-Null
}
function Invoke-Sql([string]$Sql, [string]$File) {
    Assert-Owned $dbContainer
    $arguments = @('exec',$dbContainer,'sh','-c',
        'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd "$@"',
        'sh','-S','localhost','-U','sa','-C','-N','-b','-r','1','-y','0','-w','65535','-d','EIP')
    if ($File) { $arguments += @('-i',$File) } else { $arguments += @('-Q',"SET NOCOUNT ON; $Sql") }
    return Invoke-Docker $arguments
}
function Invoke-Migration([string]$Target, [string]$LogName) {
    Assert-Owned $dbContainer
    $project = Join-Path $repo 'src/Platform/Tenant/Infrastructure'
    & dotnet ef database update $Target --no-build --project $project --startup-project $project *> (Join-Path $OutputDirectory $LogName)
    if ($LASTEXITCODE -ne 0) { throw "Migration falhou. Consulte $LogName na pasta de evidências." }
}
function Get-Manifest {
    $json = (Invoke-Sql '' '/tmp/manifest.sql').Trim()
    $json | ConvertFrom-Json | Out-Null
    Invoke-Sql '' '/tmp/validate-rls.sql' | Out-Null
    return $json
}
function Start-App([string]$Image, [string]$Suffix) {
    $name = "eip-rollback-$Suffix-$runId"
    Invoke-Docker @('run','-d','--name',$name,'--label',"eip.rollback.run=$runId",
        '-p','127.0.0.1::8080','--env','ASPNETCORE_ENVIRONMENT=Development',$Image) | Out-Null
    $containers.Add($name)
    $port = (Invoke-Docker @('port',$name,'8080/tcp')).Trim().Split(':')[-1]
    $baseUrl = "http://127.0.0.1:$port"
    $deadline = [DateTime]::UtcNow.AddMinutes(1)
    do {
        try {
            $health = Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 3
            if ($health.StatusCode -eq 200) { break }
        }
        catch { if ([DateTime]::UtcNow -gt $deadline) { throw }; Start-Sleep -Seconds 1 }
    } while ($true)
    $api = Invoke-WebRequest "$baseUrl/openapi/v1.json" -UseBasicParsing -TimeoutSec 15
    $document = $api.Content | ConvertFrom-Json -AsHashtable
    $document.Remove('servers') | Out-Null
    $document | ConvertTo-Json -Depth 100 -Compress | Set-Content -LiteralPath (Join-Path $OutputDirectory "$Suffix-openapi.json") -Encoding utf8
    $imageId = Invoke-Docker @('inspect','--format','{{.Image}}',$name)
    return [pscustomobject]@{ Name=$name; ImageId=$imageId.Trim(); OpenApiHash=(Get-FileHash (Join-Path $OutputDirectory "$Suffix-openapi.json")).Hash }
}

try {
    Invoke-Docker @('info','--format','{{.ServerVersion}}') | Out-Null
    $revision = (& git -C $repo rev-parse --verify "$PreviousRef^{commit}").Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Ref anterior inválida.' }
    $dbContainer = "eip-rollback-db-$runId"
    $password = "RollbackOnly_$([Guid]::NewGuid().ToString('N'))!"
    Invoke-Docker @('run','-d','--name',$dbContainer,'--label',"eip.rollback.run=$runId",
        '-p','127.0.0.1::1433','--env','ACCEPT_EULA=Y','--env','MSSQL_PID=Developer',
        '--env',"MSSQL_SA_PASSWORD=$password",'mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04') | Out-Null
    $containers.Add($dbContainer)
    $port = (Invoke-Docker @('port',$dbContainer,'1433/tcp')).Trim().Split(':')[-1]
    $env:EIP_TENANT_DB_CONNECTION = "Server=127.0.0.1,$port;Database=EIP;User Id=sa;Password=$password;TrustServerCertificate=True;"
    Write-Host 'Preparando banco isolado na migration anterior...'
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        try {
            Invoke-Docker @('exec',$dbContainer,'sh','-c',
                'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -N -b -Q "SELECT 1"') | Out-Null
            break
        }
        catch { if ([DateTime]::UtcNow -gt $deadline) { throw }; Start-Sleep -Seconds 2 }
    } while ($true)
    $project = Join-Path $repo 'src/Platform/Tenant/Infrastructure'
    & dotnet build $project --no-restore --verbosity quiet *> (Join-Path $OutputDirectory 'migration-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Build da migration falhou.' }
    Invoke-Migration $previousMigration 'migration-baseline.log'
    Invoke-Docker @('cp',(Join-Path $PSScriptRoot 'seed.sql'),"${dbContainer}:/tmp/seed.sql") | Out-Null
    Invoke-Docker @('cp',(Join-Path $PSScriptRoot 'validate-rls.sql'),"${dbContainer}:/tmp/validate-rls.sql") | Out-Null
    Invoke-Docker @('cp',(Join-Path $repo 'scripts/backup/manifest.sql'),"${dbContainer}:/tmp/manifest.sql") | Out-Null
    Invoke-Sql '' '/tmp/seed.sql' | Out-Null
    $baseline = Get-Manifest
    $baseline | Set-Content (Join-Path $OutputDirectory 'baseline.json') -Encoding utf8
    Write-Host 'Aplicando a migration candidata e revertendo para a anterior...'
    Invoke-Migration $candidateMigration 'migration-up.log'
    Invoke-Sql "IF COL_LENGTH('tenant.Companies','CountryCode') IS NULL THROW 51020,'Migration candidata ausente.',1;" | Out-Null
    Invoke-Sql '' '/tmp/validate-rls.sql' | Out-Null
    Invoke-Migration $previousMigration 'migration-down.log'
    Invoke-Sql "IF COL_LENGTH('tenant.Companies','CountryCode') IS NOT NULL THROW 51021,'Rollback nao removeu coluna candidata.',1; DBCC CHECKDB ([EIP]) WITH NO_INFOMSGS, ALL_ERRORMSGS;" | Out-Null
    $restored = Get-Manifest
    $restored | Set-Content (Join-Path $OutputDirectory 'rolled-back.json') -Encoding utf8
    if ($baseline -cne $restored) { throw 'Dados ou políticas divergiram após o rollback.' }

    Write-Host 'Construindo imagens de duas versões reais do Host...'
    $previousSource = Join-Path $OutputDirectory 'previous-source'
    New-Item -ItemType Directory -Path $previousSource | Out-Null
    $archivePath = Join-Path $OutputDirectory 'previous-source.tar'
    & git -C $repo archive --format=tar "--output=$archivePath" $revision
    if ($LASTEXITCODE -ne 0) { throw 'Falha no archive da versão anterior.' }
    & tar -xf (Join-Path $OutputDirectory 'previous-source.tar') -C $previousSource
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao extrair versão anterior.' }
    $images = @{}
    $publishHashes = @{}
    foreach ($version in 'previous','candidate') {
        $context = Join-Path $OutputDirectory $version
        New-Item -ItemType Directory -Path $context | Out-Null
        $source = if ($version -eq 'previous') { $previousSource } else { $repo }
        & dotnet publish (Join-Path $source 'src/Host/EIP.Host.csproj') -c Release -o (Join-Path $context 'publish') --artifacts-path (Join-Path $OutputDirectory "$version-artifacts") -p:NuGetAudit=false *> (Join-Path $OutputDirectory "$version-publish.log")
        if ($LASTEXITCODE -ne 0) { throw "Publicação local da versão $version falhou; consulte o log." }
        $publishHashes[$version] = (Get-FileHash (Join-Path $context 'publish/EIP.Host.dll')).Hash
        @'
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --chown=app:app publish/ .
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "EIP.Host.dll"]
'@ | Set-Content (Join-Path $context 'Dockerfile') -Encoding utf8
        $tag = "eip-rollback-host:${runId}-$version"
        Invoke-Docker @('build','--label',"eip.rollback.run=$runId",'--label',"org.opencontainers.image.revision=$(if ($version -eq 'previous') { $revision } else { 'working-tree' })",'-t',$tag,$context) | Out-Null
        $images[$version] = $tag
    }
    if ($publishHashes.previous -eq $publishHashes.candidate) { throw 'As versões publicadas são idênticas; escolha uma ref anterior diferente.' }
    Write-Host 'Executando versão anterior, candidata e rollback para o mesmo ID de imagem anterior...'
    $before = Start-App $images.previous 'before'
    Remove-Owned $before.Name
    $candidate = Start-App $images.candidate 'candidate'
    Remove-Owned $candidate.Name
    $after = Start-App $images.previous 'after'
    if ($before.ImageId -ne $after.ImageId -or $before.OpenApiHash -ne $after.OpenApiHash -or $candidate.ImageId -eq $before.ImageId) {
        throw 'Rollback não recuperou a mesma imagem/API anterior.'
    }
    $data = $restored | ConvertFrom-Json
    [ordered]@{
        status='passed'; completedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
        previousMigration=$previousMigration; candidateMigration=$candidateMigration
        preexistingTables=$data.tables.Count; preexistingRows=($data.tables | Measure-Object RowCount -Sum).Sum
        tableHashesMatch=$true; rlsProbePassed=$true; checkDbPassed=$true
        previousRevision=$revision; candidateSource='working-tree'; publishedHostHashes=$publishHashes
        previousImage=$before.ImageId; candidateImage=$candidate.ImageId; rolledBackImage=$after.ImageId
        previousSchemaSha256=$before.OpenApiHash; candidateSchemaSha256=$candidate.OpenApiHash; rolledBackSchemaSha256=$after.OpenApiHash
        imageRollbackPassed=$true; livenessPassed=$true; fullReadinessTested=$false
        elapsedSeconds=[Math]::Round($timer.Elapsed.TotalSeconds,2)
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'result.json') -Encoding utf8
    Write-Host "Rollback validado. Evidências: $OutputDirectory"
}
finally {
    $env:EIP_TENANT_DB_CONNECTION = $previousConnection
    foreach ($container in $containers.ToArray()) { Remove-Owned $container }
}
