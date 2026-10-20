# Guia — Operação do Conector de Referência (Retry, DLQ, Diagnóstico)

**Projeto:** Enterprise Intelligence Platform (EIP)
**Status:** Oficial
**Última atualização:** Outubro/2026

---

# 1. Objetivo

Runbook operacional do conector REST genérico (`docs/05-Connector-Framework.md`) para o critério de
saída da Fase 3 E2 ("conectores possuem monitoramento, retry, DLQ e documentação operacional") —
como diagnosticar uma sincronização travada, interpretar a DLQ, reprocessar uma mensagem e quais
métricas/logs observar. Não repete `docs/guides/ambiente-local.md` (como subir o ambiente) nem
`docs/05-Connector-Framework.md` (arquitetura) — só o dia a dia de operar o conector já rodando.

---

# 2. Como uma sincronização flui (resumo operacional)

Desde a revisão E6.1, o Host e o Worker exigem uma lista operacional de URLs exatas em
`ReferenceRest:AllowedEndpoints`. A configuração local autoriza apenas os três endpoints
`http://localhost:5080/api/v1/sample/{customers|products|sales-invoices}`. Sem lista, tudo é negado;
não existem wildcards nem liberação por tenant. URLs com credenciais, query ou fragmento não são
aceitas. O watermark é acrescentado internamente após validar a URL base.

Redirects e cookies estão desabilitados; o corpo tem limite padrão de 8 MiB, configurável por
`ReferenceRest:MaxResponseBytes` (até 64 MiB), e timeout de leitura. Um erro no relatório público
de sync agora é genérico: diagnosticar os detalhes nos logs pelo CorrelationId. Fora de Development,
destinos/conexões devem usar TLS e as credenciais locais padrão são recusadas.

O disparo em `POST /api/v1/connectors/{id}/sync` tem quota por tenant e instância:
por padrão, **1 solicitação por 60 segundos**. A primeira reserva aceita inicia a janela;
tentativas excedentes retornam `429 ProblemDetails` com `Retry-After` em segundos inteiros
(arredondados para cima), sem criar outro `SyncRun` nem publicar outra mensagem. Aguarde esse
tempo antes de repetir. Rejeições não prolongam a janela. `reprocessFrom` usa a mesma quota.

Configuração no Host: `Connector:SyncQuota:PermitLimit` e
`Connector:SyncQuota:WindowSeconds`, ambos positivos. Equivalentes em variáveis de ambiente:
`Connector__SyncQuota__PermitLimit` e `Connector__SyncQuota__WindowSeconds`.
A reserva é atômica no Redis compartilhado entre processos da API e isolada por tenant/conector;
reiniciar só a API não reinicia a janela. Perda/limpeza dos dados do Redis reinicia as quotas.
Se o Redis estiver indisponível, a solicitação falha antes de persistir/publicar; não há liberação
automática sem quota. A reserva permanece até expirar mesmo se a persistência/publicação falhar:
ela limita solicitações admitidas, não apenas sincronizações concluídas.

Retries automáticos e reenvio pela DLQ mantêm o fluxo operacional existente e não criam novas
solicitações neste endpoint; esta quota não limita esses caminhos.

```
POST /api/v1/connectors/{id}/sync
        │
        ▼
  SyncRun (Pending) + mensagem publicada em connector.sync.requested
        │
        ▼
  Worker consome ──sucesso──► SyncRun (Completed), watermark avançado
        │
        └─falha──► retry com backoff (até 3 tentativas: 10s / 30s / 90s)
                       │
                       └─ainda falha após a 3ª──► DLQ (connector.sync.requested.dlq)
```

Falha de **deserialização ou versão de contrato incompatível** vai direto para a DLQ, sem retry —
reprocessar não ajudaria, a mensagem nunca vai ficar válida. Só falhas de **processamento** (timeout
de origem, erro transitório na extração/pipeline) entram na escada de retry.

O `SyncRun` correspondente é atualizado para `Failed` com `ErrorMessage` preenchido assim que a
última tentativa (a que efetivamente vai para a DLQ) falha — consultável a qualquer momento via
`GET /api/v1/connectors/{connectorInstanceId}/sync-runs/{syncRunId}`, sem precisar olhar a fila.

---

# 3. Diagnosticando uma sincronização "travada"

"Travada" normalmente significa: o `SyncRun` ainda está `Pending` ou `Running` muito depois do
esperado, ou o chamador nunca viu o status mudar para `Completed`/`Failed`.

1. **Consulte o `SyncRun`:**
   ```bash
   curl -H "Authorization: Bearer $TOKEN" \
     http://localhost:5000/api/v1/connectors/{connectorInstanceId}/sync-runs/{syncRunId}
   ```
   - `Status: "Pending"` por mais de alguns segundos com o Worker rodando → suspeite que o Worker não
     está consumindo (veja passo 2).
   - `Status: "Failed"` com `ErrorMessage` preenchido → a sincronização já terminou (com erro); não
     está mais "travada", está na DLQ ou já esgotou os retries. Vá para a seção 4.
   - `Status: "Running"` por muito tempo → provavelmente uma chamada à origem (REST externo) sem
     retorno; verifique o `QueryTimeoutMilliseconds`/timeout HTTP do conector e os logs do Worker
     (passo 3) para essa `SyncRunId`.

2. **Confirme que o Worker está de pé e consumindo:**
   ```bash
   docker compose -f deploy/docker-compose/docker-compose.yml ps rabbitmq
   ```
   No RabbitMQ Management UI (`http://localhost:15672`, credenciais do `.env`), abra a fila
   `connector.sync.requested` — se **Consumers** estiver em `0`, o processo `EIP.Worker.Sync` não
   está rodando ou perdeu a conexão. Reinicie-o (`dotnet run --project src/Worker`).

3. **Logs estruturados** (Serilog, `CorrelationId`/`SyncRunId` sempre presentes nos eventos do
   Worker): procure por `SyncRunId` nos logs do processo `EIP.Worker.Sync` —
   - `"Falha ao processar SyncRun {SyncRunId} — agendando tentativa {NextAttempt} com backoff."`
     (nível Warning) → está retentando, ainda dentro do orçamento de 3 tentativas.
   - `"Falha ao processar SyncRun {SyncRunId} após esgotar as tentativas — enviado para a DLQ."`
     (nível Error) → esgotou os retries, foi para a DLQ (seção 4).

---

# 4. Interpretando e reprocessando a DLQ

A DLQ (`connector.sync.requested.dlq`) é global ao broker (RabbitMQ não tem RLS), mas os endpoints
abaixo filtram automaticamente pelo tenant do usuário autenticado — nunca é possível ver ou
reenfileirar uma mensagem de outro tenant através da API.

**Listar as mensagens na DLQ do seu tenant:**
```bash
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/v1/connectors/dlq
```
Retorna, para cada mensagem: `syncRunId`, `connectorInstanceId`, `correlationId`, `contractVersion`,
`failureReason` (melhor esforço — lido do header `x-death` que o próprio RabbitMQ anexa ao
dead-letrar) e `deadLetteredAtUtc`.

**Reprocessar manualmente uma mensagem específica** (depois de corrigir a causa raiz — ex.: origem
externa estava fora do ar e já voltou):
```bash
curl -X POST -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/v1/connectors/dlq/{syncRunId}/requeue
```
`204 No Content` → a mensagem saiu da DLQ e voltou para o início da fila principal (sem o header de
tentativa — é tratada como uma sincronização nova, com o orçamento completo de 3 retries de novo se
falhar outra vez). `404 Not Found` → não existe mensagem com esse `syncRunId` na DLQ do seu tenant
(já foi reprocessada, nunca existiu, ou pertence a outro tenant).

**Antes de reenfileirar, sempre confirme a causa raiz** — reenfileirar sem corrigir nada só
reproduz a mesma falha depois de gastar os 3 retries de novo (10s + 30s + 90s ≈ 2 minutos). O
`failureReason` da listagem e os logs do Worker (seção 3.3) devem indicar se foi um problema
transitório (rede, timeout — seguro reenfileirar) ou um problema de dado/configuração (schema
mudou na origem, credencial expirada — corrija antes).

---

# 5. Métricas e sinais a observar

A interface agora oferece cadastro, disparo e histórico em **Integrações**:
[guia de uso e validação](integracoes-interface.md). Os endpoints e ferramentas deste runbook
continuam disponíveis para operação e diagnóstico.

Continuidade da interface: [quarentena e fila de falhas](falhas-interface.md) agora podem
ser consultadas e reenviadas em **Integrações → Diagnóstico de falhas**. O reprocessamento
de quarentena é integral, preserva watermark e registra solicitação, não confirmação de correção.

## Visão operacional entre tenants (19/10/2026)

Para a revisão diária local, o operador autorizado pode executar:

```powershell
./scripts/performance/Get-OperationalSnapshot.ps1
```

A coleta é somente de leitura no container `eip-sqlserver`, banco `EIP`, com credencial
administrativa local resolvida dentro do container. Não é endpoint para usuários de tenant.
Enumera os tenants e consulta cada um sob seu próprio `SESSION_CONTEXT`, sem desabilitar RLS
nem presumir bypass pela sentinela de sistema. O JSON contém apenas IDs, contagens e horários;
tratar esses metadados como operacionais restritos. O arquivo de saída deve ser novo.

Campos: syncs criados nas últimas 24 horas; quantos desses estão atualmente `Failed`;
quantidade atual de `Pending`/`Running`; último sync bem-sucedido; última carga Warehouse
bem-sucedida e idade em segundos dessa carga. `failedRunsLast24Hours` mede estado atual de
runs criados na janela, não tentativas históricas nem todos os incidentes ocorridos nela.
Timestamp/idade `null` significa **nenhum sucesso registrado**, nunca frescor zero. Não é
o atraso desde a atualização original do ERP. Sucesso de sync não equivale automaticamente
a sucesso da carga Warehouse: os dois horários são separados.

Coleta real de 19/10: [snapshot](../operations/operacao-tenants-2026-10-19.json), 11 tenants,
8 runs pendentes, nenhum em execução/falha e nenhuma carga bem-sucedida registrada. Os
pendentes vêm dos ensaios sintéticos; são um sinal de trabalho não concluído, não de saúde
integral do pipeline. MinIO indisponível impede atestar ingestão completa nesta retomada.

Após esse snapshot, no mesmo dia, o [MinIO foi recuperado](recuperacao-minio-ingestao.md)
e o Worker processou os pedidos sintéticos. O [snapshot posterior](../operations/operacao-apos-recuperacao-storage-2026-10-19.json)
mostra zero pendências e dois tenants com sucesso Warehouse. A evidência anterior não foi sobrescrita.

Rotina manual: executar/conferir snapshot durante a operação; investigar pendências/idade
segundo os passos deste runbook, correlacionar retries/DLQ no RabbitMQ e logs do Worker;
registrar providência e repetir após correção. Não foi instalado agendamento, histórico
central de snapshots nem alerta. IDs/contagens não substituem análise do tenant e da origem.

## Sinais por componente

Sem um dashboard operacional dedicado nesta fase (fora de escopo — ver
`docs/roadmap/fase-3-backlog.md §3`), os sinais abaixo bastam para operar o conector no dia a dia:

| Sinal | Onde | O que indica |
|---|---|---|
| Profundidade de `connector.sync.requested` | RabbitMQ Management UI | Fila crescendo sem consumo → Worker parado ou lento demais para o volume. |
| Profundidade de `connector.sync.requested.dlq` | RabbitMQ Management UI ou `GET /api/v1/connectors/dlq` | Qualquer valor > 0 sustentado merece investigação — não é esperado no fluxo normal. |
| Profundidade das filas `connector.sync.retry.*` | RabbitMQ Management UI | Mensagens acumulando aqui por muito tempo (mais que o TTL da própria fila) indicam origem externa instável, não um bug pontual. |
| Logs `LogProcessingFailedRetrying` (Warning) | stdout/Serilog do Worker | Volume alto e sustentado → mesma origem falhando repetidamente; considere revisar o `SourceEntity`/URL do conector antes que esgote os retries. |
| Logs `LogProcessingFailedExhausted` (Error) | stdout/Serilog do Worker | Cada ocorrência corresponde a uma mensagem nova na DLQ — correlacione pelo `SyncRunId`. |
| `SyncRun.Status == "Failed"` acumulando | `GET /api/v1/connectors/{id}/sync-runs/{syncRunId}` por tenant | Reflexo do lado "negócio" da DLQ — útil quando quem investiga não tem acesso ao RabbitMQ Management UI. |

---

# 6. Cenário de falha simulado (para treinar/validar este runbook)

1. Registre um conector apontando para uma URL que devolve erro/timeout (ex.: `https://example.invalid/customers`).
2. Dispare uma sincronização: `POST /api/v1/connectors/{id}/sync`.
3. Acompanhe os logs do Worker — deve aparecer `LogProcessingFailedRetrying` até 3 vezes (10s/30s/90s
   de intervalo), depois `LogProcessingFailedExhausted`.
4. Confirme na DLQ: `GET /api/v1/connectors/dlq` deve listar o `syncRunId` do passo 2.
5. Corrija a URL do conector (ou simule a correção) e reenfileire:
   `POST /api/v1/connectors/dlq/{syncRunId}/requeue`.
6. Confirme que a mensagem saiu da DLQ (`GET /api/v1/connectors/dlq` não lista mais o `syncRunId`) e
   que o `SyncRun` avança para `Completed` no próximo processamento bem-sucedido.
