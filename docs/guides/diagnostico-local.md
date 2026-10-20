# Diagnóstico da operação local

Validação em 19/10/2026, para o laboratório com dados de demonstração escolhido
pelo usuário. Não homologa um piloto externo nem inicia monitoramento recorrente.

Na raiz, com Docker disponível, execute:

```powershell
./scripts/performance/Test-LocalOperation.ps1
```

A saída informa uma pasta nova em `%TEMP%/eip-local-operation-<id>` contendo:

- `resumo.md`: serviços, consumidor, filas e cargas em linguagem de operação;
- `result.json`: resultado estruturado e limitações;
- `tenant-snapshot.json`: contagens e último sucesso por organização, para acesso restrito do operador.

O script confere front, liveness, readiness e dados Demo via Gateway; saúde dos
quatro containers; contadores de mensagens/consumidores do RabbitMQ; e snapshot SQL
sob o contexto RLS de cada organização. Não lê payloads das mensagens, não faz
get/ack/purge/requeue, não dispara cargas e não apaga dados. As consultas geram
apenas o tráfego de leitura e os registros normais de acesso dos serviços.

Parâmetros: `-FrontendUrl`, `-HostUrl`, `-GatewayUrl`, `-DockerPath` e
`-OutputDirectory`. URLs aceitas são bases locais sem credenciais, caminho, query
ou fragmento. Redirecionamentos HTTP não são seguidos. A pasta de saída deve ser
nova; uma evidência anterior nunca é sobrescrita.

Saída 0 / `passed`: conferências pontuais aprovadas e sem filas/cargas a investigar.
Saída 2 / `attention`: falha de consulta/saúde, ausência de consumidor ou cargas/
mensagens existentes para análise. Pendência momentânea também gera atenção;
isso não afirma que ocorreu um incidente. Entrada inválida falha antes da coleta.
Uma consulta que falhou permanece desconhecida, sem virar contagem zero.

## Resultado observado

[Resumo da coleta](../operations/diagnostico-local-2026-10-19.md) e
[resultado estruturado](../operations/diagnostico-local-2026-10-19.json).

Front, liveness, readiness e Gateway: HTTP 200 com conteúdo esperado. SQL Server,
Redis, RabbitMQ e MinIO saudáveis. Um consumidor ativo; zero mensagens na fila
principal, em processamento, retry e DLQ. Snapshot: 13 organizações, zero cargas
pendentes/em andamento/com falha em 24 horas; três organizações com algum sucesso
no Warehouse. As demais não são classificadas como dados frescos.

Caso negativo: apenas o endereço do front no diagnóstico foi apontado para uma
porta local vazia. O relatório retornou `attention` e saída 2, preservando os
serviços reais e as demais conferências. URL externa e reutilização da pasta de
evidência também são recusadas.

É uma fotografia do estado local, não um histórico de disponibilidade nem um
ensaio de recuperação. Não valida login, ingestão ou reconciliação novamente.
Sem meta de frescor acordada, `freshnessEvaluated=false`. Snapshot e relatórios
privilegiados ficam restritos ao operador, sem exposição como API de tenant.

## Próximos ensaios locais

A [revisão consolidada de 19/10](../roadmap/revisao-laboratorio-2026-10-19.md)
confere os aceites L01–L05 e organiza as ampliações seguintes.

| Etapa | Situação | Evidência esperada |
|---|---|---|
| L01 — Diagnóstico pontual reutilizável | Concluído localmente | Execução positiva, detecção de indisponibilidade e proteção de entradas/evidências |
| L02 — Recuperação conjunta SQL/raw em cópias isoladas | Concluído no escopo Demo | [SQL íntegro e 3 objetos referenciados restaurados](recuperacao-conjunta-demo.md), metadados/checksums e corrupção na cópia conferidos; aplicação original preservada |
| L03 — Carga maior de demonstração | Concluído no escopo Warehouse/consultas | [20 mil fatos, 4 usuários, 120 consultas](volume-analytics.md); valores conferidos com gerador/SQL e latências medidas; ingestão/canônico fora deste ensaio |
| L04 — Retomada da aplicação contra cópias recuperadas | Concluído no escopo de leitura pela API | [Login, consulta, widgets, linhagem e reconciliação](retomada-aplicacao.md) com SQL/raw restaurados, Redis vazio e broker próprios; aplicação original preservada |
| L05 — Retomar ingestão nas cópias | Concluído no escopo incremental Demo | [Três sincronizações](retomada-ingestao.md), novos raws com checksum, canônico/Warehouse sem duplicação, marcadores/frescor avançados e filas isoladas vazias |

Os ensaios locais apoiam P04/P06/P07 do [plano do piloto](../roadmap/preparacao-piloto.md),
mas não substituem ambiente, metas, responsáveis e aceites próprios de um piloto real.
