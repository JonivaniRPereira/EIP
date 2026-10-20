[CmdletBinding()]
param(
    [Uri]$FrontendUrl = 'http://127.0.0.1:4200',
    [Uri]$HostUrl = 'http://127.0.0.1:5080',
    [Uri]$GatewayUrl = 'http://127.0.0.1:5000',
    [string]$OutputDirectory,
    [string]$DockerPath
)
$ErrorActionPreference = 'Stop'
foreach ($url in @($FrontendUrl, $HostUrl, $GatewayUrl)) {
    if (!$url.IsLoopback -or $url.Scheme -notin 'http','https' -or $url.UserInfo -or $url.Query -or $url.Fragment -or $url.AbsolutePath -ne '/') {
        throw 'Use apenas URLs base locais, sem credenciais, caminho, query ou fragmento.'
    }
}
if (!$DockerPath) { $DockerPath = Join-Path $env:LOCALAPPDATA 'Programs/DockerDesktop/resources/bin/docker.exe' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) "eip-local-operation-$([Guid]::NewGuid().ToString('N'))" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use uma pasta nova para preservar as evidências anteriores.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$probes = [Collections.Generic.List[object]]::new()
$containers = [Collections.Generic.List[object]]::new()
$issues = [Collections.Generic.List[string]]::new()
$client = [Net.Http.HttpClient]::new([Net.Http.HttpClientHandler]@{ AllowAutoRedirect = $false })
$client.Timeout = [TimeSpan]::FromSeconds(10)
try {
    $checks = @(
        @{ Name = 'Frontend'; Url = $FrontendUrl.AbsoluteUri; Kind = 'frontend' },
        @{ Name = 'API viva'; Url = [Uri]::new($HostUrl, '/health/live').AbsoluteUri; Kind = 'health' },
        @{ Name = 'Dependências da API'; Url = [Uri]::new($HostUrl, '/health/ready').AbsoluteUri; Kind = 'ready' },
        @{ Name = 'Gateway e origem Demo'; Url = [Uri]::new($GatewayUrl, '/api/v1/sample/customers').AbsoluteUri; Kind = 'sample' }
    )
    foreach ($check in $checks) {
        $statusCode = 0
        $ok = $false
        try {
            $response = $client.GetAsync($check.Url).GetAwaiter().GetResult()
            try {
                $statusCode = [int]$response.StatusCode
                $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                $ok = $statusCode -eq 200
                if ($check.Kind -eq 'health') { $ok = $ok -and $body.Trim() -eq 'Healthy' }
                elseif ($check.Kind -eq 'ready') {
                    $health = $body | ConvertFrom-Json
                    $ok = $ok -and $health.status -eq 'Healthy' -and @($health.checks).Count -gt 0 -and @($health.checks | Where-Object status -ne 'Healthy').Count -eq 0
                }
                elseif ($check.Kind -eq 'frontend') { $ok = $ok -and $body.Contains('<app-root') }
                elseif ($check.Kind -eq 'sample') {
                    $sample = ConvertFrom-Json -InputObject $body -NoEnumerate
                    $ok = $ok -and $sample -is [Array] -and $sample.Count -gt 0 -and !!$sample[0].code
                }
            }
            finally { $response.Dispose() }
        }
        catch { $ok = $false }
        $probes.Add([pscustomobject]@{ name = $check.Name; url = $check.Url; statusCode = $statusCode; passed = $ok })
        if (!$ok) { $issues.Add("Falha na conferência: $($check.Name).") }
    }
}
finally { $client.Dispose() }
foreach ($name in @('eip-sqlserver','eip-redis','eip-rabbitmq','eip-minio')) {
    $state = 'unknown'
    try {
        $inspection = & $DockerPath inspect --format '{{.State.Status}}|{{.State.Health.Status}}' $name 2>$null
        if ($LASTEXITCODE -eq 0) { $state = ($inspection -join '').Trim() }
    }
    catch { $state = 'unavailable' }
    $ok = $state -eq 'running|healthy'
    $containers.Add([pscustomobject]@{ name = $name; state = $state; passed = $ok })
    if (!$ok) { $issues.Add("Dependência indisponível ou sem saúde confirmada: $name.") }
}
$queueSummary = $null
try {
    # Metadata only: no message get/ack/purge/requeue.
    $raw = & $DockerPath exec eip-rabbitmq rabbitmqctl -q --timeout 10 list_queues name messages_ready messages_unacknowledged consumers --formatter=json 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Consulta de filas falhou.' }
    $queues = @((($raw -join '') | ConvertFrom-Json) | Where-Object { $_.name -like 'connector.sync.requested*' -or $_.name -like 'connector.sync.retry.*' })
    $main = $queues | Where-Object name -eq 'connector.sync.requested'
    $dead = $queues | Where-Object name -eq 'connector.sync.requested.dlq'
    if (!$main -or !$dead) { throw 'Filas esperadas ausentes.' }
    foreach ($retryName in @('connector.sync.retry.10s', 'connector.sync.retry.30s', 'connector.sync.retry.90s')) {
        if (!($queues | Where-Object name -eq $retryName)) { throw 'Fila de retry esperada ausente.' }
    }
    $queueSummary = [pscustomobject]@{
        activeConsumers = [int]$main.consumers
        ready = [long]$main.messages_ready
        processing = [long]$main.messages_unacknowledged
        deadLetters = [long]$dead.messages_ready + [long]$dead.messages_unacknowledged
        retryMessages = [long](($queues | Where-Object name -like 'connector.sync.retry.*' | Measure-Object messages_ready -Sum).Sum) + [long](($queues | Where-Object name -like 'connector.sync.retry.*' | Measure-Object messages_unacknowledged -Sum).Sum)
    }
    if ($queueSummary.activeConsumers -lt 1) { $issues.Add('Nenhum consumidor ativo na fila de sincronização.') }
    if ($queueSummary.ready -or $queueSummary.processing -or $queueSummary.deadLetters -or $queueSummary.retryMessages) { $issues.Add('Há mensagens em espera, processamento, retry ou fila de falhas; consultar o diagnóstico.') }
}
catch { $issues.Add('Não foi possível conferir as filas de sincronização.') }
$usage = $null
try {
    $snapshotFile = Join-Path $OutputDirectory 'tenant-snapshot.json'
    & (Join-Path $PSScriptRoot 'Get-OperationalSnapshot.ps1') -OutputFile $snapshotFile -DockerPath $DockerPath
    $snapshot = Get-Content -Raw -LiteralPath $snapshotFile | ConvertFrom-Json
    $usage = [pscustomobject]@{
        observedAtUtc = $snapshot.observedAtUtc
        tenants = $snapshot.tenants.Count
        pending = [long](($snapshot.tenants | Measure-Object pendingRuns -Sum).Sum)
        running = [long](($snapshot.tenants | Measure-Object runningRuns -Sum).Sum)
        failedLast24Hours = [long](($snapshot.tenants | Measure-Object failedRunsLast24Hours -Sum).Sum)
        tenantsWithWarehouseSuccess = @($snapshot.tenants | Where-Object lastSuccessfulWarehouseLoadAt).Count
        freshnessEvaluated = $false
    }
    if ($usage.pending -or $usage.running -or $usage.failedLast24Hours) { $issues.Add('Há cargas pendentes, em andamento ou com falha no banco; conferir por organização.') }
}
catch { $issues.Add('Não foi possível coletar o estado das cargas no banco.') }
$result = [pscustomobject]@{
    observedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    scope = 'local-demo-read-only'
    status = $(if ($issues.Count) { 'attention' } else { 'passed' })
    probes = $probes.ToArray()
    containers = $containers.ToArray()
    queues = $queueSummary
    loads = $usage
    issues = $issues.ToArray()
    limits = @('Verificação pontual; não é SLO nem monitoramento contínuo.', 'Não executa login, ingestão ou reconciliação.', 'Sem prazo de frescor acordado; ausência de carga não indica dados frescos.')
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding utf8
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# Diagnóstico local EIP')
$lines.Add('')
$lines.Add("Resultado: **$($result.status)**. Coleta: $($result.observedAtUtc).")
$lines.Add('')
$lines.Add('| Conferência | Resultado |')
$lines.Add('|---|---|')
foreach ($probe in $probes) { $lines.Add("| $($probe.name) | $(if ($probe.passed) { 'OK' } else { 'Verificar' }) (HTTP $($probe.statusCode)) |") }
foreach ($container in $containers) { $lines.Add("| $($container.name) | $($container.state.Replace('|', ' / ')) |") }
if ($queueSummary) {
    $lines.Add("| Consumidores de sincronização | $($queueSummary.activeConsumers) |")
    $lines.Add("| Mensagens em espera / processamento | $($queueSummary.ready) / $($queueSummary.processing) |")
    $lines.Add("| Mensagens em retry / fila de falhas | $($queueSummary.retryMessages) / $($queueSummary.deadLetters) |")
}
if ($usage) {
    $lines.Add("| Cargas pendentes / em andamento / com falha em 24 h | $($usage.pending) / $($usage.running) / $($usage.failedLast24Hours) |")
    $lines.Add("| Organizações com algum sucesso no Warehouse / total | $($usage.tenantsWithWarehouseSuccess) / $($usage.tenants) |")
}
$lines.Add('')
foreach ($issue in $issues) { $lines.Add("- $issue") }
foreach ($limit in $result.limits) { $lines.Add("- $limit") }
$lines | Set-Content -LiteralPath (Join-Path $OutputDirectory 'resumo.md') -Encoding utf8
Write-Host "Diagnóstico: $($result.status). Relatório: $(Join-Path $OutputDirectory 'resumo.md')"
if ($issues.Count) { exit 2 }
exit 0
