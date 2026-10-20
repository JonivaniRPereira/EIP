[CmdletBinding()]
param(
    [ValidateRange(100,1000)][int]$Samples = 100,
    [ValidateRange(150,2000)][int]$DelayMilliseconds = 150,
    [Uri]$GatewayUrl = 'http://localhost:5000',
    [Uri]$HostUrl = 'http://localhost:5080',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
foreach ($url in @($GatewayUrl,$HostUrl)) {
    if (!$url.IsLoopback -or $url.Scheme -notin 'http','https' -or $url.UserInfo) { throw 'Use apenas endpoints locais sem credenciais na URL.' }
}
$targets = Get-Content (Join-Path $PSScriptRoot 'slo-targets.json') -Raw | ConvertFrom-Json
$runId = [Guid]::NewGuid().ToString('N')
if (!$OutputDirectory) { $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) "eip-slo-$runId" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use um diretório de saída novo.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Write-Host "Diretório de evidências: $OutputDirectory"
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(10)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('EIP-Local-SLO/1.0')
$invariant = [Globalization.CultureInfo]::InvariantCulture
$results = [Collections.Generic.List[object]]::new()
$startedAt = [DateTimeOffset]::UtcNow

function Send-Request([string]$Method, [string]$Url, [string]$Body, [string]$Token) {
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method),$Url)
    if ($Body) { $request.Content = [Net.Http.StringContent]::new($Body,[Text.Encoding]::UTF8,'application/json') }
    if ($Token) { $request.Headers.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$Token) }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            return [pscustomobject]@{ Status=[int]$response.StatusCode; ElapsedMs=$timer.Elapsed.TotalMilliseconds; Content=$content; Error=$null }
        }
        finally { $response.Dispose() }
    }
    catch { return [pscustomobject]@{ Status=0; ElapsedMs=$timer.Elapsed.TotalMilliseconds; Content=''; Error=$_.Exception.GetType().Name } }
    finally { $request.Dispose() }
}
function Invoke-Setup([string]$Path, $Body, [string]$Token) {
    $response = Send-Request 'POST' "$($GatewayUrl.AbsoluteUri.TrimEnd('/'))$Path" ($Body | ConvertTo-Json -Compress) $Token
    if ($response.Status -notin 200,201) { throw "Preparação falhou em $Path (HTTP $($response.Status)). Resposta não registrada para preservar tokens." }
    return $response.Content | ConvertFrom-Json
}
function Get-Metrics([string]$Name) {
    $response = Send-Request 'GET' "$($HostUrl.AbsoluteUri.TrimEnd('/'))/metrics" '' ''
    if ($response.Status -ne 200) { throw 'Exporter /metrics indisponível.' }
    $response.Content | Set-Content (Join-Path $OutputDirectory $Name) -Encoding utf8
    return $response.Content
}
function Read-Histogram([string]$Text, [string]$Route, [string]$Method) {
    $counts = @{}
    $buckets = @{}
    foreach ($line in ($Text -split "`n")) {
        if ($line -notmatch '^http_server_request_duration_seconds_(count|bucket)\{(.+)\}\s+([^\s]+)\s*$') { continue }
        $kind=$Matches[1]; $labels=$Matches[2]; $value=[double]::Parse($Matches[3],$invariant)
        if ($labels -notmatch 'http_route="([^"]*)"' -or $Matches[1] -cne $Route) { continue }
        if ($labels -notmatch 'http_request_method="([^"]*)"' -or $Matches[1] -cne $Method) { continue }
        if ($labels -notmatch 'http_response_status_code="([0-9]+)"') { continue }
        $status=$Matches[1]
        if ($kind -eq 'count') { $counts[$status] += $value }
        elseif ($status -eq '200' -and $labels -match 'le="([^"]+)"') { $buckets[$Matches[1]] += $value }
    }
    return [pscustomobject]@{ Counts=$counts; Buckets=$buckets }
}
function Get-Quantile($Buckets, [double]$Count, [double]$Quantile) {
    if ($Count -le 0) { return $null }
    $rank=$Quantile*$Count; $previousUpper=0.0; $previousCount=0.0
    $bounds=@($Buckets.Keys | Where-Object { $_ -ne '+Inf' } | ForEach-Object { [double]::Parse($_,$invariant) } | Sort-Object)
    foreach ($bound in $bounds) {
        $key = $Buckets.Keys | Where-Object { $_ -ne '+Inf' -and [double]::Parse($_,$invariant) -eq $bound } | Select-Object -First 1
        $cumulative=$Buckets[$key]
        if ($cumulative -ge $rank) {
            $estimate = if ($cumulative -gt $previousCount) { $previousUpper+($bound-$previousUpper)*($rank-$previousCount)/($cumulative-$previousCount) } else { $bound }
            return [pscustomobject]@{ EstimateMs=[Math]::Round($estimate*1000,3); UpperBoundMs=$bound*1000 }
        }
        $previousUpper=$bound; $previousCount=$cumulative
    }
    # Não tratar uma cauda no bucket +Inf como latência finita dentro da meta.
    return [pscustomobject]@{ EstimateMs=$null; UpperBoundMs=$null }
}

try {
    # Conta sintética exclusiva; nunca registra senha, token ou corpo de respostas de autenticação.
    Write-Host 'Criando organização sintética para medir login e analytics...'
    $email="slo-$runId@test.local"
    $password="SloOnly_$([Guid]::NewGuid().ToString('N'))!"
    $loginBody=@{email=$email;password=$password} | ConvertTo-Json -Compress
    $registered = Invoke-Setup '/api/v1/auth/register' @{email=$email;password=$password;displayName='SLO local'} ''
    $tenant = Invoke-Setup '/api/v1/tenants' @{name="SLO local $runId";slug="slo-$runId";countryCode='BR'} $registered.accessToken
    $auth = Invoke-Setup '/api/v1/auth/refresh' @{refreshToken=$registered.refreshToken} ''
    $analyticsBody='{"dataset":"sales","metrics":["netRevenue"],"dimensions":["date.month"]}'
    foreach ($endpoint in $targets.endpoints) {
        $baseUrl = if ($endpoint.viaGateway) { $GatewayUrl.AbsoluteUri.TrimEnd('/') } else { $HostUrl.AbsoluteUri.TrimEnd('/') }
        $body = if ($endpoint.name -eq 'login') { $loginBody } elseif ($endpoint.name -eq 'analytics') { $analyticsBody } else { '' }
        $token = if ($endpoint.name -eq 'analytics') { $auth.accessToken } else { '' }
        $url="$baseUrl$($endpoint.path)"
        Write-Host "Medindo $($endpoint.name): 5 aquecimentos e $Samples amostras sequenciais..."
        for ($i=0;$i -lt 5;$i++) {
            $warmup = Send-Request $endpoint.method $url $body $token
            if ($warmup.Status -ne 200) { throw "Aquecimento de $($endpoint.name) falhou (HTTP $($warmup.Status))." }
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
        $before=Read-Histogram (Get-Metrics "$($endpoint.name)-before.prom") $endpoint.route $endpoint.method
        $samplesForEndpoint=[Collections.Generic.List[object]]::new()
        for ($i=0;$i -lt $Samples;$i++) {
            $response=Send-Request $endpoint.method $url $body $token
            $samplesForEndpoint.Add([pscustomobject]@{status=$response.Status;elapsedMs=$response.ElapsedMs;error=$response.Error})
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
        $after=Read-Histogram (Get-Metrics "$($endpoint.name)-after.prom") $endpoint.route $endpoint.method
        $deltaBuckets=@{}
        foreach ($key in $after.Buckets.Keys) {
            $delta=$after.Buckets[$key]-$before.Buckets[$key]
            if ($delta -lt 0) { throw 'Contador reiniciou durante a medição; repetir sem reiniciar o Host.' }
            $deltaBuckets[$key]=$delta
        }
        $serverCount=$after.Counts['200']-$before.Counts['200']
        if ($serverCount -le 0) { throw "Nenhuma amostra de $($endpoint.route) observada no histograma da API." }
        $serverP95=Get-Quantile $deltaBuckets $serverCount 0.95
        $serverP99=Get-Quantile $deltaBuckets $serverCount 0.99
        $sorted=@($samplesForEndpoint | Where-Object status -eq 200 | ForEach-Object elapsedMs | Sort-Object)
        $p95 = if ($sorted.Count) { [Math]::Round($sorted[[Math]::Ceiling(0.95*$sorted.Count)-1],3) } else { $null }
        $p99 = if ($sorted.Count) { [Math]::Round($sorted[[Math]::Ceiling(0.99*$sorted.Count)-1],3) } else { $null }
        $record=[ordered]@{
            name=$endpoint.name; route=$endpoint.route; viaGateway=$endpoint.viaGateway
            samples=$Samples; successful=$sorted.Count; sampleAvailabilityPercent=100.0*$sorted.Count/$Samples
            targetP95Ms=$endpoint.p95Ms; targetP99Ms=$endpoint.p99Ms
            clientP95Ms=$p95; clientP99Ms=$p99; serverSuccessfulSamples=$serverCount
            serverP95EstimateMs=$serverP95.EstimateMs; serverP99EstimateMs=$serverP99.EstimateMs
            serverP95UpperBoundMs=$serverP95.UpperBoundMs; serverP99UpperBoundMs=$serverP99.UpperBoundMs
            serverLatencyTargetsObserved=($null -ne $serverP95.UpperBoundMs -and $null -ne $serverP99.UpperBoundMs -and $serverP95.UpperBoundMs -le $endpoint.p95Ms+0.000001 -and $serverP99.UpperBoundMs -le $endpoint.p99Ms+0.000001)
            clientLatencyTargetsObserved=($null -ne $p95 -and $null -ne $p99 -and $p95 -le $endpoint.p95Ms -and $p99 -le $endpoint.p99Ms)
            histogramBucketDeltas=$deltaBuckets
        }
        $results.Add([pscustomobject]$record)
        $samplesForEndpoint | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory "$($endpoint.name)-samples.json") -Encoding utf8
        Write-Host "$($endpoint.name): $($sorted.Count)/$Samples HTTP 200; cliente p95=${p95}ms p99=${p99}ms."
    }
    [ordered]@{
        status='measured'; startedAtUtc=$startedAt.ToString('O'); completedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
        samplesPerEndpoint=$Samples; delayMilliseconds=$DelayMilliseconds; tenantId=$tenant.tenantId
        workload='single client, sequential, empty sales dataset, repeated prewarmed query'
        serverSource='before/after deltas of http_server_request_duration_seconds at /metrics'
        availabilityTargetPercent=$targets.availabilityTargetPercent; rollingWindowDays=$targets.rollingWindowDays
        rolling30DaySloEvaluated=$false; syntheticAccountRetained=$true; endpoints=$results.ToArray()
    } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'result.json') -Encoding utf8
    Write-Host "Medição concluída. Evidências: $OutputDirectory"
}
finally { $client.Dispose() }
