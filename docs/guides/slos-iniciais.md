# SLOs iniciais e medição local

Fase 3 E5.3. Definidos em 18/10/2026; objetivos de engenharia sujeitos a revisão após carga
representativa e operação real. Responsável nesta fase: mantenedor do projeto.

## Metas e indicadores

| Endpoint | p95 de processamento | p99 de processamento | Disponibilidade alvo |
|---|---:|---:|---:|
| Login (`POST /api/v1/auth/login`) | 500 ms | 1.000 ms | 99,9% |
| Analytics (`POST /api/v1/analytics/query`) | 1.000 ms | 2.000 ms | 99,9% |
| Liveness (`GET /health/live`) | 50 ms | 100 ms | 99,9% |
| Readiness (`GET /health/ready`) | 250 ms | 500 ms | 99,9% |

As metas são fixadas antes da medição, em `scripts/performance/slo-targets.json`. A janela formal
é móvel de 30 dias. A latência usa o histograma de requisições HTTP bem-sucedidas do **Host**,
em segundos; o relatório converte para milissegundos. O tempo total medido no cliente também é
comparado às metas, mas inclui Gateway/transporte e não é o mesmo indicador do histograma.

Disponibilidade HTTP observável no Host: requisições 2xx divididas pelas requisições 2xx, 5xx e
429 da rota. 4xx esperados por entrada/credenciais inválidas não entram nesse indicador. Isso não
classifica sozinho se uma negativa 401/403 foi correta: fluxos sintéticos válidos complementam a
observação e devem tratar qualquer resposta diferente da esperada como falha. No ensaio, todos
os quatro endpoints devem retornar exatamente 200; timeout/conexão recusada também contam como
falha no cliente.

O orçamento de erro de 99,9% é 0,1% das requisições elegíveis da janela; não é automaticamente um
orçamento de minutos, pois este indicador é baseado em requisições. Sem tráfego/histórico, a
disponibilidade é **não avaliada**, nunca presumida como 100%. Antes de julgar uma janela mensal,
conferir cobertura de coleta e quantidade de amostras (ao menos 1.000 requisições por rota para
ter resolução de uma falha em 0,1%; isso não garante confiança estatística).

`/health/live` indica processo vivo; não verifica banco/cache/fila. `/health/ready` verifica as
dependências configuradas, e respostas 503 devem reduzir a disponibilidade observada. Sucesso
em liveness não substitui readiness ou login/consulta funcionando.

## Executar a medição

Com Host `:5080`, Gateway `:5000`, SQL Server, Redis e RabbitMQ disponíveis:

```powershell
./scripts/performance/Measure-Slo.ps1
```

O script cria uma conta e organização sintéticas exclusivas e executa cinco aquecimentos e 100
requisições sequenciais por endpoint, com pausa mínima de 150 ms entre pedidos. Login e analytics
passam pelo Gateway; probes e `/metrics` usam o Host diretamente, conforme a arquitetura.
Esse ritmo fica abaixo do limite nominal do Gateway de 100 pedidos/10 segundos por IP, desde
que outro tráfego do mesmo IP não consuma o orçamento. Um 429 continua sendo registrado como falha.

Parâmetros: `-Samples` (100–1.000), `-DelayMilliseconds` (150–2.000), `-GatewayUrl`, `-HostUrl`
(apenas loopback) e `-OutputDirectory` (pasta nova). O padrão de saída é
`$env:TEMP/eip-slo-<id>`. Não são salvos tokens, senha ou corpos das respostas de autenticação.
Conta/tenant de teste, auditoria, refresh tokens e registros de uso ficam no banco local; o script
não executa exclusões por SQL. Cada nova execução cria seu próprio tenant.

O dataset `sales` desse tenant está vazio. A consulta é repetida e pré-aquecida, sujeita ao TTL
normal do cache. Portanto, a medição é uma baseline funcional de baixa carga; não representa
latência de cargas grandes, cache frio, concorrência ou piloto. E6.3 continua necessário.

## Conferência via OpenTelemetry/Prometheus

O exporter real está em `http://localhost:5080/metrics`. A série observada é
`http_server_request_duration_seconds`, tipo `histogram`, com labels `http_route`,
`http_request_method`, `http_response_status_code` e `le` nos buckets.

As rotas dos controllers aparecem como `api/v1/auth/login` e `api/v1/analytics/query`, **sem**
barra inicial; os health checks aparecem como `/health/live` e `/health/ready`.

O script salva snapshots antes/depois de cada bloco de medição e usa diferenças dos buckets e
do contador de respostas 200. Isso exclui os aquecimentos e as requisições históricas do processo.
Reinício com contador decrescente invalida a medição. O histograma agrega o tráfego da rota;
requisições concorrentes de outros usuários podem aumentar suas contagens, mesmo que o cliente
sintético tenha emitido exatamente 100 pedidos. Não há label de tenant nesses histogramas.

O p95/p99 do servidor é estimado por interpolação linear dentro dos buckets, como em
`histogram_quantile`; também se registra o limite superior do bucket que contém o percentil.
O campo `serverLatencyTargetsObserved` exige que esse limite esteja dentro da meta, evitando
declarar atendimento apenas por uma interpolação otimista. Percentil na cauda `+Inf` não é
tratado como latência finita dentro do alvo. Os percentis do cliente usam ordenação e posto
`ceil(q*N)`, apenas nas respostas 200.

Com um servidor Prometheus coletando continuamente, a expressão abaixo calcula p95 em ms por rota:

```promql
1000 * histogram_quantile(0.95,
  sum by (le, http_route) (
    increase(http_server_request_duration_seconds_bucket{
      http_route=~"api/v1/auth/login|api/v1/analytics/query|/health/live|/health/ready",
      http_response_status_code="200"
    }[30d])
  )
)
```

Para p99, trocar `0.95` por `0.99`; para diagnóstico recente, usar `[5m]` em vez de `[30d]`.
A janela de 5 minutos é um diagnóstico e não comprova a meta mensal.

Disponibilidade HTTP observável por rota:

```promql
100 *
sum by (http_route) (
  increase(http_server_request_duration_seconds_count{
    http_route=~"api/v1/auth/login|api/v1/analytics/query|/health/live|/health/ready",
    http_response_status_code=~"2.."
  }[30d])
)
/
sum by (http_route) (
  increase(http_server_request_duration_seconds_count{
    http_route=~"api/v1/auth/login|api/v1/analytics/query|/health/live|/health/ready",
    http_response_status_code=~"2..|5..|429"
  }[30d])
)
```

As consultas são instruções para um coletor; não foram executadas contra um servidor Prometheus
nesta atividade. A prova local consulta o exporter e calcula deltas dos dados realmente expostos.
O Compose atual não provisiona um servidor de histórico Prometheus. Uma coleta externa deve
persistir as séries e observar falhas de scrape (`up`) e probes válidos via Gateway: um Host ou
Gateway totalmente inacessível não emite uma resposta 5xx que seu próprio histograma consiga contar.

## Resultado e resposta a desvios

Resumo da baseline: `docs/operations/slo-2026-10-18.json`. Os snapshots `.prom` e amostras
individuais permanecem na pasta local indicada na execução. `status=measured` indica medição
concluída, não aprovação de um mês de disponibilidade; `rolling30DaySloEvaluated=false` é explícito.
O relatório conserva os valores observados mesmo se excederem as metas, sem ajustar os alvos.

Ao observar desvio: confirmar janela/cobertura de coleta, volume/dataset/cache, tráfego concorrente,
429 do Gateway e disponibilidade de SQL/Redis/RabbitMQ; consultar logs pelo CorrelationId e repetir
o cenário em condições conhecidas. Persistindo, priorizar investigação antes de ampliar carga.
Não há alerta automático, Grafana ou Alertmanager introduzido nesta fase. Executar manualmente
após mudanças relevantes e reavaliar as metas quando houver dados de carga/uso representativos.
