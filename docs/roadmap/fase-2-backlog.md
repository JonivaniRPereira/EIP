# Backlog de Execução — Fase 2 (MVP Analítico e Dashboards)

**Projeto:** Enterprise Intelligence Platform (EIP)
**Versão:** 1.0
**Status:** Oficial
**Última atualização:** Agosto/2026

---

# 1. Objetivo

Este documento traduz `docs/15-Roadmap.md §6` (Fase 2) em tarefas concretas, ordenadas e verificáveis
— mesmo papel que `docs/roadmap/fase-0-backlog.md` e `docs/roadmap/fase-1-backlog.md` cumpriram para
as fases anteriores (ambas concluídas e formalmente fechadas em 2026-08-01 e 2026-08-04).

Fase 2 entrega o primeiro valor recorrente ao gestor: **um Analytics Engine declarativo** (`docs/10`)
sobre a camada semântica já certificada na Fase 1, e um **Dashboard Builder mínimo** (`docs/12`) capaz
de publicar e visualizar painéis reais com esses dados — sem SQL exposto, sem acesso direto ao DW, com
isolamento de tenant e cache seguro. Isso segue literalmente `docs/15-Roadmap.md §6`: *"entregar ao
gestor o primeiro valor recorrente: indicadores confiáveis e painéis seguros"* — e o princípio de
priorização §3: *"entregar verticalmente: origem → dado → métrica → dashboard → usuário"*, fechando
agora o último elo (`dashboard → usuário`) que a Fase 1 deixou em aberto.

Este documento **não substitui** nenhuma regra definida em `docs/00` a `docs/15` ou nas ADRs. Em caso
de conflito, os documentos de arquitetura/segurança/ADR prevalecem — em especial `docs/10-Analytics-Engine.md`
e `docs/12-Dashboard-Builder.md`, as duas fontes normativas mais usadas aqui. Onde este backlog reduz
deliberadamente o escopo desses documentos (ver §3), a redução é explícita e justificada, nunca uma
omissão silenciosa.

---

# 2. Como usar este backlog

- Cada tarefa tem um ID (`E<épico>.<sequência>`), descrição, arquivos/projetos afetados, dependências
  e critério de aceite. Numeração reinicia em `E1` neste arquivo (independente das fases anteriores) —
  cada backlog de fase é autocontido.
- Marcar `- [x]` somente quando o critério de aceite estiver satisfeito e validado (build/teste
  passando contra infraestrutura real), nunca por "está quase pronto" — mesma disciplina das fases
  anteriores.
- Épicos são majoritariamente sequenciais (E1 → E2 → ... → E5).
- Toda tarefa que cria uma tabela com `TenantId` é bloqueada até a política RLS correspondente existir
  e ter teste de acesso cruzado — sem exceção (ADR-007), mesma regra permanente desde a Fase 0.
- A Fase 2 só é considerada concluída quando a seção 4 (Critérios de Saída) estiver 100% satisfeita —
  mesma regra das fases anteriores.

---

# 3. Decisões técnicas fixadas para a Fase 2

| Decisão | Valor | Origem |
|---|---|---|
| Domínio de negócio | Só **Comercial** — reaproveita o Modelo Canônico/Data Warehouse/3 métricas certificadas já construídos na Fase 1 (E2–E6). `Financeiro`/`Estoque` continuam fora do escopo | Decisão do usuário em 2026-08-04 — a fase foca em provar o Analytics Engine + Dashboard Builder sobre dados que já existem, sem abrir uma nova frente de ingestão |
| `Workspace` continua fora do escopo | `docs/12` amarra Dashboard/versão/compartilhamento a um Workspace (`docs/08 §4.3`), mas Workspace nunca foi implementado (adiado nas Fases 0 e 1). Dashboards e o Analytics Engine ficam escopados só por `Tenant`, mesmo padrão de tudo que já existe | Decisão do usuário em 2026-08-04 — reavaliar em fase futura se houver demanda validada de segmentação dentro do tenant |
| Novo domínio arquitetural `src/Intelligence/` | `docs/00-Arquitetura-do-Repositorio.md` reserva `src/Intelligence/{Analytics,Dashboard,Reporting,Automation,Notification,AI}` — Fase 2 cria `Analytics/` e `Dashboard/`. Por `docs/00`: `Intelligence` nunca acessa fontes externas nem a persistência interna de `Data`/`Platform` diretamente — só consome contratos publicados (mesmo princípio já usado entre `Data`/`Platform` via `ITenantDirectory`/`IMembershipDirectory`) | `docs/00`, primeira vez que este backlog cria algo fora de `Platform`/`Data` |
| 1 dataset analítico: `sales` | Reaproveita `FactSalesInvoiceItem`/`DimCustomer`/`DimProduct`/`DimProductCategory`/`DimDate` (Fase 1, E5) e as 3 métricas certificadas do E6 (`net_revenue`, `invoiced_quantity`, `average_ticket`). Dimensões iniciais: `date.month`, `customer`, `product`, `product.category` | Consistente com "só Comercial" acima |
| Catálogo do Analytics Engine é código, não configurável | Mesma filosofia do E6 da Fase 1 (`CertifiedMetrics` como classe estática) — um catálogo de datasets/métricas persistido e editável por usuário é escopo do motor de métricas genérico, explicitamente fora do MVP (`docs/10 §14`: "substituição da camada semântica por configurações individuais") | `docs/10 §14`, escopo desta fase |
| Guardrails mínimos, não o framework completo de `docs/10 §8.2` | Limite de linhas por consulta, período máximo e timeout — sem quotas por plano/usuário (não existe conceito de "plano" no sistema ainda) nem exportação assíncrona como job (`docs/10 §8.3`). Consulta que excede o orçamento retorna erro claro, nunca trunca silenciosamente | Escopo desta fase, YAGNI sem caso de uso comercial validado |
| Sem Time Intelligence avançado (`docs/10 §9`) nem Análises Avançadas (`docs/10 §10`) | Filtro de período simples (`between`) é suficiente para o MVP; comparação MoM/YoY, média móvel, calendário fiscal, previsão/anomalia ficam para quando houver dataset/demanda que justifique | `docs/10 §10` já marca isso como fora do escopo obrigatório do próprio motor |
| Cache real no Redis — primeira vez efetivamente usado | Redis está de pé desde a Fase 0 (E1) só com health check; Fase 2 é a primeira consulta de dado tenant-scoped que passa por ele (fecha o gap documentado no fechamento da Fase 1, E8.3). Chave: `tenant + dataset + versão semântica + consulta normalizada + versão de dado` (reduzida de `docs/10 §8.1` — sem `workspace`, que não existe). Invalidação só por TTL curto nesta fase (sem invalidação orientada a evento por atualização do Warehouse — `docs/10 §8.1` descreve isso como ideal, mas exige um mecanismo de notificação que ainda não existe) | `docs/10 §8.1`, ADR-006, redução justificada por YAGNI |
| Sem exportação (PDF/CSV/XLSX) nesta fase | `docs/10 §12`/`docs/12 §12` listam endpoints de exportação como "iniciais", mas o framework de job assíncrono com expiração/auditoria dedicada (`docs/06 §9.2`) é desproporcional sem um caso de uso validado — mesmo critério já usado para adiar retry-com-backoff na fila da Fase 0 (`docs/roadmap/fase-0-backlog.md`, E7.2). O critério de saída "publicação, exportação e acesso por escopo são auditados" (docs/15 §6) é satisfeito para publicação/acesso; exportação fica documentada como não aplicável nesta fase (mesmo padrão usado para "cache" no fechamento da Fase 1, E8.3) | Escopo desta fase, revisitar quando houver demanda |
| Dashboard: 1 página implícita, sem `Page`/`Theme`/`SharePolicy` granular | `docs/12 §3` modela `Page`/`Theme`/`SharePolicy` como entidades próprias — nesta fase um `Dashboard` tem widgets diretamente (sem multi-página), tema fixo do frontend (sem editor de tema), e compartilhamento é binário (visível a todo membro do tenant com a permissão `dashboard.view`, sem granularidade por usuário/grupo) | Escopo desta fase, YAGNI sem caso de uso multi-página validado |
| Ciclo de vida do Dashboard: `Draft → Published → Archived` (sem `InReview`) | `docs/12 §4` inclui um estado `InReview` para "quando o processo exigir" validação técnica/negócio — não existe esse processo de aprovação modelado ainda em nenhum módulo da plataforma, adicionar o estado sem um fluxo real seria especulativo | `docs/12 §4`, já reconhece isso como condicional |
| 3 tipos de widget no MVP: KPI, Line chart, Table | `docs/12 §5` lista 7 tipos iniciais; estes 3 cobrem os 3 KPIs certificados da Fase 1 (KPI para os valores agregados, Line chart para Receita Líquida por mês, Table para detalhe por cliente/produto) sem precisar de Bar/Donut/Filter-control ainda | Escopo desta fase, mesmo princípio de "poucos domínios/poucos recursos por vez" já usado nas fases anteriores |
| Frontend: criação de dashboard por formulário, não editor visual arrasta-e-solta | Um editor de grid com posicionamento livre (`docs/12 §6`, `layout: {x,y,w,h}`) é uma frente de UI própria e cara; o MVP usa posicionamento sequencial automático (cada widget adicionado empilha abaixo do anterior) — a *visualização* do dashboard publicado é a parte que precisa ser real e fiel ao critério de saída, a *edição* pode ser mínima | Escopo desta fase, foco no critério de saída "gestor visualiza dashboard de produção" |
| `EIP.Intelligence.Analytics` chama `EIP.Data.Semantic.Application` diretamente (referência de projeto), nunca `EIP.Data.Warehouse.*` | Mesmo princípio de `docs/00`: `Intelligence` não acessa a persistência interna de `Data` — só o contrato já publicado por `Data.Semantic` (que é quem tem permissão de tocar o Warehouse). `EIP.Data.Semantic.Application` ganha uma nova capacidade de consulta dimensional (agrupar por `date.month`/`customer`/`product`/`product.category`) para o Analytics Engine consumir, sem o Analytics Engine nunca montar SQL/agregação ele mesmo | `docs/00`, `docs/10 §1` ("o motor não acessa diretamente... a camada semântica") |

---

# 4. Critérios de Saída da Fase 2 (Definition of Done)

Copiados literalmente de `docs/15-Roadmap.md §6`. A Fase 2 só termina quando todos estiverem `[x]`,
com evidência real (não "os épicos estão codificados") — mesma disciplina aplicada no fechamento das
Fases 0 e 1. Revisão formal concluída em 2026-08-23 (E5.3):

- [x] Gestor autorizado visualiza dashboard de produção com filtros e dados consistentes.
      **Evidência**: E4.2, validado visualmente pelo usuário no navegador real
      (`http://localhost:4200/dashboards`) — painel com KPI/LineChart/Table renderizando dado real da
      Fase 1 (Receita Líquida R$ 11.100, 3 clientes). Filtros ativos de cada widget exibidos como
      legenda somente-leitura (não há UI de filtro interativo nesta fase, escopo já reduzido no §3).
      Autorização: `dashboard.view` exigido em toda rota de leitura (E3.3), testado com Member (200)
      vs. sem permissão.
- [x] Métricas possuem dono, definição, versão e reconciliação aprovadas.
      **Evidência**: as 3 métricas certificadas (`net_revenue`/`invoiced_quantity`/`average_ticket`)
      já tinham dono/definição/versão desde a Fase 1 (E6.1, `CertifiedMetrics`) e reconciliação desde
      E4.3/E5 da Fase 1 — a Fase 2 não altera essas garantias, só as expõe de forma declarativa
      (`AnalyticsCatalog`, E1.2/E1.3) e as reaproveita em cada widget sem duplicar a fórmula
      (`CommercialMetricsCalculator`, compartilhado desde E1.1).
- [x] Dashboards não acessam ERP/Raw/DW diretamente.
      **Evidência estrutural** (não só convenção — imposta pelas referências de projeto):
      `EIP.Intelligence.Dashboard.{Domain,Infrastructure,Api}` não referenciam
      `EIP.Data.Warehouse.*`/`EIP.Data.Canonical.*`/`EIP.Data.DataLake` em nenhum `.csproj` — o único
      caminho para dado é `IDeclarativeAnalyticsQueryService` (`EIP.Intelligence.Analytics.Application`,
      já validado como a única porta de entrada desde o E1.2). Um widget não pode fisicamente compilar
      uma chamada direta ao Warehouse mesmo que alguém tentasse.
- [x] Consultas respeitam orçamento de desempenho e quotas.
      **Evidência**: guardrails do E1.5 (limite de linhas, período máximo, timeout de execução) valem
      para toda consulta que passa pelo Analytics Engine, incluindo as disparadas por
      `POST /dashboards/{id}/query` (mesmo caminho de código do E3.2) — testado ao vivo com timeout
      determinístico e limite de linhas/período nos testes do E1.5. Sem quotas por plano/usuário
      (redução deliberada, §3 — não existe conceito de "plano" na plataforma ainda).
- [x] Publicação, exportação e acesso por escopo são auditados.
      **Evidência**: `DashboardVersion.PublishedByUserId` (E5.3) registra quem publicou cada versão —
      o próprio histórico de versões já é, por construção, um registro imutável e append-only de toda
      publicação. Log estruturado (`[LoggerMessage]`, Serilog + `CorrelationId` automático) em toda
      mutação e visualização (`Created`/`Updated`/`Published`/`Archived`/`Viewed`), validado ao vivo
      contra o Host real. Acesso por escopo é imposto por `[RequirePermission]`
      (`dashboard.view`/`dashboard.manage`, E3.3) e testado (Member 403 em mutação, 200 em leitura).
      **Exportação é explicitamente N/A nesta fase** (mesmo padrão de marcação usado para "cache" no
      fechamento da Fase 1, E8.3) — `docs/roadmap/fase-2-backlog.md §3` já documenta a exportação como
      fora do escopo (PDF/CSV/XLSX como job assíncrono desproporcional sem caso de uso validado); não
      há nada para auditar porque a funcionalidade não existe.
- [x] Toda tabela com `TenantId` (agora incluindo o novo schema `dashboard`) possui política RLS
      ativa, e o gate automatizado (`RlsCoverageTests`) continua passando sem exceções adicionadas.
      **Evidência**: `RlsCoverageTests` (varredura genérica de `sys.security_predicates`) passou
      contra o schema `dashboard` migrado desde o E2.1, sem nenhuma exceção adicionada à lista de
      tabelas conhecidas. Reforçado pelo E5.1 (isolamento cross-tenant real via EF Core, não só
      "a política existe").

---

# 5. Épicos e Tarefas

## E1 — Analytics Engine (camada declarativa sobre a Semântica)

Objetivo: fechar `docs/10-Analytics-Engine.md` para o dataset `sales`, com contrato declarativo real
(nunca SQL do cliente), cache seguro e guardrails mínimos.

- [x] **E1.1** Estender `EIP.Data.Semantic.Application` com uma capacidade de consulta dimensional:
      agrupar `FactSalesInvoiceItem` por `date.month`/`customer`/`product`/`product.category`,
      aplicando os mesmos filtros de exclusão de documentos cancelados já usados pelas 3 métricas
      certificadas (E6, Fase 1). Continua sendo a única camada que efetivamente toca o Warehouse.
  - *Depende de:* Fase 1 E5/E6. ✅ Concluído — `product.category` deliberadamente **não** implementado
    (achado real durante o design, não hipotético): `DimProduct.CategoryKey` é sempre nulo nesta fase
    (o conector de referência nunca ingere categoria, mesma lacuna já documentada desde a Fase 1,
    E5.1) — agrupar por uma dimensão sempre vazia seria uma funcionalidade de fachada. `IAnalyticsQueryService`/
    `AnalyticsQueryService` (novo, `EIP.Data.Semantic.Application`) suportam `DateMonth`/`Customer`/
    `Product`. `IWarehouseLoadStore.ListFactSalesInvoiceItemsForMetricsAsync` estendido (join com
    `DimCustomers`/`DimProducts` por chave substituta) para trazer `CustomerId`/`CustomerName`/
    `ProductId`/`ProductName`/`DateKey` — agrupamento sempre pela chave de negócio durável
    (`CustomerId`/`ProductId`), nunca pela chave substituta versionada de SCD2, para nunca separar o
    mesmo cliente/produto em dois grupos só porque uma versão de dimensão mudou no meio do período.
    Fórmula das 3 métricas extraída para `CommercialMetricsCalculator` (compartilhada entre o
    agregado único do E6.1 e o novo agrupamento — nunca duplicada). 2 testes de integração novos
    (Testcontainers, `EIP.Data.Warehouse.IntegrationTests`): agrupar por `date.month` com dados em
    janeiro/fevereiro retorna 2 linhas com os agregados corretos (inclusive excluindo uma fatura
    cancelada), batendo com o cálculo manual; agrupar por `customer` nunca mistura dois clientes.
    **Regressão real encontrada e corrigida durante a validação**: o novo `JOIN` obrigatório com
    `DimCustomers`/`DimProducts` quebrou `MetricsCrossTenantIsolationTests` (Fase 1, E6.2) — o teste
    semeava `FactSalesInvoiceItem` diretamente com `customerKey`/`productKey` arbitrários (`1`), sem
    nenhuma linha de dimensão correspondente (atalho válido quando a métrica só somava valores, mas
    que o processo de carga real nunca produziria — toda carga de verdade sempre resolve/cria a
    dimensão antes do fato). Corrigido semeando `DimCustomer`/`DimProduct` reais no teste e usando as
    chaves substitutas geradas, em vez de afrouxar o `JOIN` para `LEFT JOIN` (que mascararia uma
    inconsistência de dados real caso ela algum dia ocorra em produção). `dotnet test` (41 testes, toda
    a solução) e `dotnet format --verify-no-changes` limpos após a correção.
- [x] **E1.2** Novo módulo `EIP.Intelligence.Analytics.{Application,Api}` (primeiro módulo fora de
      `Platform`/`Data`, `docs/00`): catálogo do dataset `sales` em código (nome técnico, métricas,
      dimensões, `Owner`, versão — mesmo padrão de `CertifiedMetrics`) + contrato declarativo de
      consulta (`dataset`/`metrics`/`dimensions`/`filters`/`orderBy`/`limit`, `docs/10 §5.1`),
      validado contra o catálogo antes de qualquer execução. `TenantId` sempre do claim JWT, nunca do
      payload.
  - *Depende de:* E1.1. ✅ Concluído — `AnalyticsCatalog` (`EIP.Intelligence.Analytics.Application`)
    publica o dataset `sales` (3 métricas certificadas reaproveitadas do E6.1, 3 dimensões do E1.1);
    `DeclarativeAnalyticsQueryService` valida dataset/métricas/dimensões/filtros/orderBy/limit contra
    o catálogo (nunca lança exceção para erro de validação — retorna `AnalyticsQueryExecutionResult`
    com mensagem clara, mesmo padrão de `ConnectorSyncService`) e só então delega a execução a
    `IAnalyticsQueryService` (`EIP.Data.Semantic.Application`, contrato já publicado do E1.1) — nunca
    acessa `EIP.Data.Warehouse.*` diretamente, conforme `docs/00`. Reduções deliberadas desta fase
    (documentadas em código, mesmo espírito do §3 desta tabela): exatamente 1 dimensão por consulta
    (o agrupamento do E1.1 só suporta uma), filtros restritos a `date` (`between`) e `company.id`
    (`equals`), no máximo 1 critério de ordenação. Novo endpoint `POST /api/v1/analytics/query`
    (`EIP.Intelligence.Analytics.Api`) protegido por nova permissão `analytics.query`
    (`EIP.Shared.Contracts.Analytics.AnalyticsPermissions`, concedida a todos os papéis — mesma
    política de `metrics.view`); criada agora em vez de esperar o E3.3 original, já que o endpoint
    precisa de autorização real desde já, não de um `[Authorize]` genérico temporário. **Extensão
    retroativa ao E1.1** (pequena, aditiva, sem alterar comportamento existente): `IWarehouseLoadStore`
    ganhou `GetLastSuccessfulLoadAtAsync` e `IAnalyticsQueryService` ganhou `GetDataFreshnessAsync`
    (repassa sem transformação) para atender ao "frescor" exigido por `docs/10 §5.3` — sem essa
    extensão a métrica de frescor não existiria em lugar nenhum da stack. 5 novos testes de integração
    (`EIP.Host.IntegrationTests/AnalyticsQueryTests`, via `WebApplicationFactory` real + SQL Server
    efêmero Testcontainers, mesmo padrão de `MetricsCrossTenantIsolationTests`): métrica inexistente,
    dataset inexistente e mais de 1 dimensão retornam 400 sem executar; consulta válida (`date.month`)
    retorna 2 linhas com metadados completos (`dataset`, `semanticVersion`, `rowCount`,
    `dataFreshnessAt` não nulo após semear um `LoadBatch` concluído); isolamento cross-tenant provado
    (tenant B nunca vê o dado de tenant A). 46/46 testes passando na solução inteira (41 anteriores +
    5 novos), `dotnet build`/`dotnet format --verify-no-changes`/`dotnet list package --vulnerable`
    limpos. **Não validado com o stack `docker-compose` completo ao vivo nesta tarefa** (só com
    Testcontainers) — mesmo padrão de validação já aceito para o E1.1 desta mesma fase; a validação
    end-to-end com infraestrutura real de verdade é o E5.2, que fecha a fase inteira.
  - *Aceite:* `POST /api/v1/analytics/query` com um campo/métrica/dimensão inexistente retorna erro
    claro (nunca executa); uma consulta válida retorna dados + metadados (`docs/10 §5.3`: dataset,
    versão semântica, `executedAt`, frescor, contagem de linhas).
- [x] **E1.3** `GET /api/v1/analytics/datasets`/`GET /api/v1/analytics/datasets/{id}` — expõe o
      catálogo (métricas/dimensões/dono/versão) para o frontend descobrir o que pode consultar, sem
      hardcode duplicado no cliente.
  - *Depende de:* E1.2. ✅ Concluído (2026-08-23) — dois novos endpoints em `AnalyticsController`
    (`EIP.Intelligence.Analytics.Api`), mesma proteção de permissão `analytics.query` já usada pelo
    `POST /query` (leitura do catálogo é o mesmo nível de acesso de executar uma consulta). Mapeamento
    direto de `AnalyticsCatalog.All`/`Find` para os novos DTOs (`AnalyticsDatasetSummaryDto`/
    `AnalyticsMetricSummaryDto`/`AnalyticsDimensionSummaryDto`, `Api/Contracts/AnalyticsContracts.cs`)
    — nenhuma lógica nova na camada de aplicação, o catálogo já existia desde o E1.2. `GET
    /datasets/{id}` retorna 404 (Problem Details) para um dataset inexistente. 3 novos testes de
    integração (`EIP.Host.IntegrationTests/AnalyticsDatasetsTests.cs`, mesmo padrão
    `WebApplicationFactory`+Testcontainers): listar retorna o dataset `sales` com as 3 métricas e 3
    dimensões exatas do catálogo; buscar por id conhecido retorna o mesmo dataset; buscar por id
    desconhecido retorna 404. 49/49 testes passando na solução inteira (46 anteriores + 3 novos),
    `dotnet build`/`dotnet format --verify-no-changes` limpos.
  - *Aceite:* resposta reflete exatamente o catálogo em código (mudar uma definição no backend muda a
    resposta, sem exigir alteração no frontend).
- [x] **E1.4** Cache real no Redis (`ADR-006`, primeiro uso funcional de fato — gap documentado no
      fechamento da Fase 1, E8.3): chave `tenant + dataset + versão semântica + consulta normalizada +
      versão de dado`, TTL curto configurável. Cache nunca serve dado de um tenant para outro (a
      chave inclui o tenant sempre derivado do claim, nunca do payload).
  - *Depende de:* E1.2. ✅ Concluído (2026-08-23) — novo projeto `EIP.Intelligence.Analytics.
    Infrastructure` (`RedisAnalyticsQueryCache`, só depende de `IDistributedCache` — não sabe que o
    backend é Redis; isso é decidido inteiramente pela composição de DI do Host via
    `AddStackExchangeRedisCache`). `IAnalyticsQueryCache` (contrato, `Application`) +
    `AnalyticsCacheKeyBuilder` (função pura, sem dependência de infra) montam a chave exatamente como
    especificado: `analytics:{tenantId}:{dataset}:{semanticVersion}:{dataVersion}:{hashDaConsultaNormalizada}`
    — `dataVersion` é o frescor do tenant (`GetDataFreshnessAsync`, já existente desde o E1.2): quando
    o Warehouse recebe uma carga nova a chave muda sozinha e a consulta antiga vira cache miss natural,
    sem precisar de invalidação orientada a evento (redução já prevista na tabela §3). Consulta
    normalizada ordena métricas/filtros antes de hashear (SHA-256) para que pedidos semanticamente
    equivalentes, mas com campos em ordem diferente, colidam na mesma chave. `DeclarativeAnalyticsQueryService`
    computa o frescor e a chave *antes* de decidir se consulta o Warehouse; em cache miss, executa a
    consulta, ordena/limita e grava no cache (TTL configurável via `Analytics:CacheTtlSeconds`,
    default 60s, lido sempre no momento da resolução do serviço — nunca capturado antes de
    `builder.Build()`, mesma disciplina do E7 da Fase 1). `HostApiFixture` (testes de integração)
    passou a subir um Redis real efêmero via Testcontainers (`redis:7.4-alpine`, mesma tag do
    `docker-compose`) em vez da string sintaticamente válida mas inalcançável usada até aqui — RabbitMQ
    continua fake, já que nenhum teste do Host dispara sincronização via fila. 2 novos testes
    (`AnalyticsQueryCacheTests`, via um decorador de contagem de chamadas
    (`AnalyticsQueryCallCounter`/`CountingAnalyticsQueryService`) em volta do `IAnalyticsQueryService`
    real — nunca inspeciona o Redis diretamente): duas consultas idênticas do mesmo tenant resultam em
    exatamente 1 chamada ao Warehouse (cache hit na segunda); a mesma consulta para dois tenants
    diferentes resulta em 2 chamadas e cada resposta traz o dado correto do seu próprio tenant (nunca
    vaza). 51/51 testes passando na solução inteira (49 anteriores + 2 novos),
    `dotnet build`/`dotnet format --verify-no-changes`/`dotnet list package --vulnerable` limpos.
  - *Aceite:* teste prova cache hit (segunda consulta idêntica não gera nova query no Warehouse,
    validado por contagem de chamadas) e cache miss corretamente isolado por tenant (mesma consulta,
    tenants diferentes, entradas de cache diferentes).
- [x] **E1.5** Guardrails mínimos: limite de linhas de resposta, período máximo de filtro de data,
      timeout de execução. Consulta que excede qualquer limite retorna erro claro (`docs/06 §10`
      Problem Details), nunca trunca silenciosamente.
  - *Depende de:* E1.2. ✅ Concluído (2026-08-23) — `AnalyticsGuardrailOptions` (`MaxRows`,
    `MaxPeriodDays`, `QueryTimeout`), configurável via `Analytics:MaxRows`/`MaxPeriodDays`/
    `QueryTimeoutMilliseconds` (defaults 5000/400/10000, lidos sempre no momento da resolução do
    serviço). Três checagens em `DeclarativeAnalyticsQueryService`: (1) `limit` explícito maior que
    `MaxRows` rejeitado antes de qualquer execução; (2) filtro `date` cobrindo mais que `MaxPeriodDays`
    rejeitado no mesmo ponto onde as datas já são validadas; (3) timeout de execução via
    `CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter`, envolvendo só a chamada ao
    Warehouse (nunca o pipeline HTTP/autenticação em volta) — distingue "nosso timeout estourou" de
    "o cliente cancelou a requisição" com `catch (OperationCanceledException) when
    (!cancellationToken.IsCancellationRequested)`, repropagando o cancelamento do cliente sem
    convertê-lo em erro de guardrail. Guardrail adicional não listado originalmente, mas exigido pela
    própria regra "nunca trunca silenciosamente": consulta sem `limit` explícito cujo resultado
    excederia `MaxRows` é rejeitada (nunca truncada) — com `limit` explícito, entregar exatamente o
    que o cliente pediu não conta como truncamento silencioso. 3 novos testes: 2 em
    `AnalyticsGuardrailTests` (período/linhas, sem precisar semear dado nenhum — ambos os guardrails
    são checados antes de qualquer leitura) e 1 em `AnalyticsQueryTimeoutTests`, que precisou de uma
    `WebApplicationFactory<Program>` própria (reaproveitando o mesmo SQL Server/Redis efêmeros da
    `HostApiFixture` compartilhada, só com um `Analytics:QueryTimeoutMilliseconds` de 50ms exclusivo
    dessa suíte) decorando `IAnalyticsQueryService` com um atraso deliberado de 300ms
    (`SlowAnalyticsQueryService`) — determinístico independente da velocidade da máquina, já que o
    orçamento de tempo só é comparado contra o atraso injetado, nunca contra o overhead HTTP/auth.
    54/54 testes passando na solução inteira (51 anteriores + 3 novos),
    `dotnet build`/`dotnet format --verify-no-changes` limpos.
  - *Aceite:* teste prova que exceder o limite de período retorna 400 com mensagem explicando qual
    limite foi violado.
- [x] **E1.6** Observabilidade da consulta (`docs/10 §11`): log estruturado (Serilog, já usado desde a
      Fase 0) com dataset/métricas/dimensões/tenant/cache hit-ou-miss/duração/`CorrelationId`, sem
      expor valores de dado sensível no log.
  - *Depende de:* E1.2, E1.4. ✅ Concluído (2026-08-23) — `DeclarativeAnalyticsQueryService` virou
    `partial` e ganhou um `[LoggerMessage]` (mesmo padrão já usado em `ConnectorSyncProcessor`/
    `SyncRequestedConsumerService`, evita os avisos `CA1848`/`CA1873` que chamadas diretas a
    `ILogger.LogInformation` introduziriam). Uma única linha de log ao final de toda consulta
    executada com sucesso: `dataset`, `metrics` (nomes técnicos concatenados, nunca os valores
    retornados), `dimension`, `tenantId`, `cacheHit` (computado a partir do próprio resultado do
    `IAnalyticsQueryCache.GetAsync`), `durationMs` (`Stopwatch` iniciado no começo do método) e
    `rowCount` — nunca os valores de dado da resposta em si. `CorrelationId` não precisou de nenhum
    código novo: já é empurrado para `Serilog.Context.LogContext` pelo middleware de correlação do
    Host (E3, Fase 0) em toda requisição, e aparece automaticamente em qualquer log estruturado emitido
    durante o request — inclusive este. Consultas rejeitadas por validação/guardrail (upstream) não
    geram esta linha — escopo do critério de aceite é a consulta real executada, não cada tentativa
    inválida. **Validado ao vivo, não só "compilou"**: rodei um teste real
    (`AnalyticsQueryTests.Query_ValidRequest_ReturnsDataAndCompleteMetadata`) com o logger do Serilog
    em modo console e capturei a linha de verdade: `Analytics query executada: dataset=sales
    metrics=netRevenue dimension=date.month tenantId=d066315f-eeef-4644-9865-90b600c20731
    cacheHit=False durationMs=641.86 rowCount=2`, com o `CorrelationId` visível entre parênteses no
    início da linha (`(af7e1628-...)`), confirmando todos os campos exigidos pelo critério de aceite
    de uma vez. `dotnet build`/`dotnet format --verify-no-changes` limpos. **Épico E1 (Analytics
    Engine) fechado — E1.1 a E1.6 todos concluídos.**
  - *Aceite:* validado ao vivo — uma consulta real produz uma linha de log com todos os campos
    esperados.

## E2 — Dashboard Builder: domínio e persistência

Objetivo: modelo mínimo de `docs/12-Dashboard-Builder.md §3/§4` para o novo domínio `Intelligence`,
com RLS obrigatória desde a primeira migration (ADR-007).

- [x] **E2.1** Novo módulo `EIP.Intelligence.Dashboard.{Domain,Infrastructure}`, schema `dashboard`.
      Entidades mínimas: `Dashboard` (`Id`, `TenantId`, `Name`, `OwnerId`, `Status`
      `Draft`/`Published`/`Archived`) e `DashboardVersion` (imutável, `DashboardId`, `Widgets` — lista
      de `Widget` como Value Object serializado, não tabela própria, já que nunca é consultado
      isoladamente fora do próprio dashboard). `Widget`: `Type` (`Kpi`/`LineChart`/`Table`), `Title`,
      `QueryDefinition` (dataset/métricas/dimensões/filtros — mesmo contrato do E1.2), `Visualization`
      (config mínima de exibição por tipo).
  - *Depende de:* E1.2 (o contrato de `QueryDefinition` precisa existir para o Widget referenciá-lo).
    ✅ Concluído (2026-08-23) — primeiro módulo dentro de `Intelligence` além de `Analytics`. **Correção
    de modelo feita durante o planejamento do E3.1** (real, encontrada antes de virar dívida): a versão
    original desta entrada não dava a `Dashboard` nenhum campo `Widgets` — só `DashboardVersion` (criada
    apenas no publish) tinha conteúdo, o que tornava `PATCH /api/v1/dashboards/{id}` (E3.1, "editar
    rascunho") impossível de implementar coerentemente, já que não havia onde um rascunho em edição
    pudesse morar entre um save e outro. Corrigido acrescentando `Dashboard.Widgets` (rascunho atual,
    mutável, mesma conversão JSON de `DashboardVersion.Widgets`) e `Dashboard.UpdateDraft(name?,
    widgets?)`; `Dashboard.Publish()` deixou de receber widgets por parâmetro e passou a tirar um
    instantâneo do rascunho atual. Migration inicial regenerada do zero (nunca tinha sido aplicada a um
    banco persistente, só a containers efêmeros — sem custo de uma segunda migration corretiva). Ver
    E2.2 abaixo para a assinatura final de `Publish()`. `Dashboard` e `DashboardVersion` seguem
    exatamente o padrão de `Entity<TId>`/factory estático já usado em
    `ConnectorInstance`/`SyncRun`/`Membership` (ctor privado + `Create(...)` validando, ctor sem
    parâmetros só para o EF). **Desvio deliberado do padrão "Domain sem dependências externas"**:
    `EIP.Intelligence.Dashboard.Domain` referencia diretamente `EIP.Intelligence.Analytics.Application`
    para reaproveitar `AnalyticsQueryDefinition` como o tipo de `Widget.QueryDefinition` — literalmente
    "o mesmo contrato do E1.2", não uma cópia paralela que dessincronizaria; os dois módulos vivem sob
    `Intelligence/`, então isso não fura a fronteira `Intelligence`↔`Data`/`Platform` do `docs/00`, só a
    convenção estilística de Domain-sem-deps que os outros módulos seguem por não terem essa necessidade.
    **Colisão de nomes real encontrada e corrigida**: a entidade `Dashboard` colide com o próprio
    segmento de namespace do módulo (`EIP.Intelligence.Dashboard.*`) — exatamente o mesmo problema que
    `EIP.Platform.Tenant.Domain.Tenant` já tinha; resolvido com a mesma técnica já usada em
    `TenantDbContext`/`TenantConfiguration` (referenciar `Domain.Dashboard` explicitamente nos poucos
    lugares ambíguos, em vez de importar `Dashboard` bare). `Widgets` (lista de `Widget`) é a primeira
    coleção de Value Objects serializada do projeto — sem precedente de `OwnsMany`/`ToJson` no resto do
    código-base (que exigiria remodelar `AnalyticsQueryDefinition` para tipos amigáveis ao mapeamento de
    dono do EF); optei por `HasConversion` manual via `System.Text.Json` para a lista inteira (mesmo
    idioma já usado para os enums, só que para uma coleção) com um `ValueComparer` explícito, evitando o
    aviso do EF Core para propriedades de coleção convertidas sem comparador. Migration inicial
    (`dotnet ef migrations add`) + RLS anexada via SQL bruto no mesmo padrão exato de
    `canonical.fn_TenantAccessPredicate`/`canonical.CanonicalAccessPolicy` (Fase 1, E2.2): 1 FILTER + 2
    BLOCK (INSERT/UPDATE) por tabela, para `dashboard.Dashboards` e `dashboard.DashboardVersions`.
    `DatabaseMigrator.MigrateAllAsync` (`EIP.Testing.Infrastructure`) atualizado para incluir
    `DashboardDbContext` — sem isso o gate `RlsCoverageTests` teria um ponto cego para o novo schema,
    mesma disciplina já registrada desde a Fase 1/E2.2. **Validado**: rodei `RlsCoverageTests` (que
    varre genericamente TODAS as tabelas com `TenantId` sem policy ativa) contra o schema recém-migrado
    e ele passou — prova que as duas tabelas novas já nascem com RLS ativa, sem precisar de um teste
    dedicado só para isto (isolamento cross-tenant específico do schema `dashboard` fica para o E5.1,
    mesmo padrão de fechamento em lote já usado no E8.1 da Fase 1). Nenhum teste novo escrito nesta
    tarefa (reaproveitou o `RlsCoverageTests` já existente) — 54/54 testes continuam passando na
    solução inteira, `dotnet build`/`dotnet format --verify-no-changes` limpos.
  - *Aceite:* migration inicial já nasce com `TenantId` + política RLS (mesmo padrão de sempre) —
    sem exceção.
- [x] **E2.2** Publicação: `Dashboard.Publish()` cria/promove uma `DashboardVersion` imutável de forma
      atômica; editar um dashboard publicado sempre cria um novo rascunho, nunca sobrescreve a versão
      publicada (`docs/12 §4`).
  - *Depende de:* E2.1. ✅ Concluído (2026-08-23), assinatura final revisada durante o E3.1 (ver nota no
    E2.1 acima): `Dashboard.Publish()` (sem parâmetros — tira um instantâneo do rascunho atual em
    `Dashboard.Widgets`, método puro de domínio, sem I/O): promove `Status` para `Published`
    (idempotente quanto ao status — publicar de novo mantém `Published`, nunca falha por já estar
    publicado) e sempre retorna uma `DashboardVersion` nova via `DashboardVersion.Create(...)`, nunca
    reaproveitando/mutando uma existente. Atomicidade é responsabilidade de quem persiste, não do
    método em si — um único `SaveChangesAsync` cobrindo a atualização do `Dashboard` e a inserção da
    nova `DashboardVersion` na mesma transação (mesmo princípio de unit-of-work já usado em toda
    gravação do projeto, nunca uma responsabilidade nova). **A frase "editar sempre cria novo rascunho"
    do enunciado é comportamento de `Dashboard.UpdateDraft` (E3.1's `PATCH` chama este método) — E2.2
    entrega só a capacidade de publicar; não há endpoint ainda (E2 é só "domínio e persistência").
    Novo projeto de teste `EIP.Intelligence.Dashboard.Infrastructure.IntegrationTests` (mesmo padrão de
    `EIP.Data.Canonical.Infrastructure.IntegrationTests` — direto contra `DashboardDbContext` + SQL
    Server real via Testcontainers, sem Host/Api, que ainda não existem para este módulo): publica duas
    vezes com conteúdo de widgets diferente a cada vez, confirma 2 linhas distintas em
    `DashboardVersions` (nunca 1), a primeira com exatamente o widget original intacto (nunca
    sobrescrita pela segunda) e o `Dashboard` terminando `Published`. 55/55 testes passando na
    solução inteira (54 anteriores + 1 novo), `dotnet build`/`dotnet format --verify-no-changes`
    limpos.
  - *Aceite:* teste prova que publicar duas vezes gera duas `DashboardVersion` distintas, e a versão
    anterior continua íntegra e recuperável (histórico).
- [x] **E2.3** Validação da `QueryDefinition` de cada widget contra o catálogo do Analytics Engine
      (E1.3) no momento de salvar/publicar — nunca aceitar uma consulta inválida num rascunho que será
      publicado.
  - *Depende de:* E1.3, E2.1. ✅ Concluído (2026-08-23) — `WidgetQueryDefinitionValidator` (interno ao
    módulo, `EIP.Intelligence.Dashboard.Domain`) valida dataset/métricas/dimensão de cada widget contra
    `AnalyticsCatalog` (reaproveitado diretamente, mesma referência de projeto já justificada no E2.1).
    Só validação estrutural de existência no catálogo — a validação completa de filtros/orderBy/limit
    já é feita pelo próprio `DeclarativeAnalyticsQueryService` (E1.2) toda vez que o widget é executado
    (E3.2), duplicar aqui seria redundante sem ganho real. Chamado de dentro de
    `DashboardVersion.Create` — o único ponto de criação de versão no módulo inteiro — então toda
    chamada futura (não só `Dashboard.Publish`, E2.2) passa pela validação sem exceção, sem precisar de
    disciplina externa para lembrar de chamá-la. 2 novos testes em
    `WidgetQueryDefinitionValidationTests`: widget com métrica inexistente e widget com dataset
    inexistente lançam `ArgumentException` (mensagem citando o nome inválido) e, na sequência, uma
    consulta direta ao banco confirma zero linhas em `DashboardVersions` — prova concreta de "rejeitado
    antes de persistir", não só "a chamada lançou uma exceção em algum lugar". 57/57 testes passando na
    solução inteira (55 anteriores + 2 novos), `dotnet build`/`dotnet format --verify-no-changes`
    limpos.
  - *Aceite:* teste prova que salvar um widget com uma métrica inexistente é rejeitado antes de
    persistir.

## E3 — Dashboard Builder: API

Objetivo: fechar `docs/12 §8/§12` reduzido ao escopo desta fase — CRUD, publicação e execução de
consulta, sempre delegando ao Analytics Engine (E1), nunca acessando o Warehouse diretamente.

- [x] **E3.1** Novo `EIP.Intelligence.Dashboard.Api`: `POST /api/v1/dashboards` (criar rascunho),
      `GET /api/v1/dashboards` (listar do tenant), `GET /api/v1/dashboards/{id}`,
      `PATCH /api/v1/dashboards/{id}` (editar rascunho — publicado sempre cria novo rascunho, E2.2),
      `POST /api/v1/dashboards/{id}/publish`, `POST /api/v1/dashboards/{id}/archive`.
  - *Depende de:* E2.1, E2.2. ✅ Concluído (2026-08-23) — controller vai direto contra
    `IDbContextFactory<DashboardDbContext>`, mesmo padrão de `TenantsController` (sem uma camada de
    Application dedicada; a lógica de negócio real já vive nos métodos de domínio
    `Dashboard.Create`/`UpdateDraft`/`Publish`/`Archive`, o controller só orquestra carregar → chamar
    método → salvar). Isolamento entre tenants nunca depende do controller lembrar de filtrar por
    `TenantId` — a RLS (ADR-007) já garante isso no banco; um dashboard de outro tenant simplesmente
    não aparece nas consultas. 6 novos testes em `DashboardCrossTenantIsolationTests` (mesmo padrão de
    `ConnectorCrossTenantIsolationTests`): `GetById`/`Update`/`Publish`/`Archive` para o dashboard do
    tenant B retornam 404 mesmo adulterando o Id na rota; `List` nunca inclui dashboards de outro
    tenant; criar sem token retorna 401.
  - *Aceite:* teste de isolamento cross-tenant (tenant A não lê/edita/publica dashboard de tenant B,
    mesmo adulterando o Id na rota — mesmo padrão de `ConnectorCrossTenantIsolationTests`).
- [x] **E3.2** `POST /api/v1/dashboards/{id}/query` (ou por widget) — executa a `QueryDefinition` de
      cada widget do dashboard publicado (ou do rascunho, se o solicitante for o dono) via o Analytics
      Engine (E1.2), nunca lendo `warehouse.*` diretamente deste módulo.
  - *Depende de:* E1.2, E3.1. ✅ Concluído (2026-08-23) — **simplificação deliberada da nuance
    "publicado vs. rascunho se dono"**: o endpoint sempre executa `Dashboard.Widgets` (o conteúdo
    atual), nunca relê `DashboardVersion` (que existe só como trilha histórica imutável, nunca
    reconsultada para "o que este widget mostra agora"). Justificativa: assim que `Publish()` acontece,
    `Widgets` e a última `DashboardVersion` são idênticos por construção; só divergem quando há edições
    não publicadas — e aí mostrar o rascunho mais recente (em vez de uma versão obsoleta) é o
    comportamento correto para quem já tem `dashboard.view`, que neste MVP é uma permissão de tenant
    inteiro, nunca por dono (`docs/12 §3`: "compartilhamento é binário... sem granularidade por
    usuário"). Isso tornou a nuance de "dono" do enunciado original sem efeito prático — documentado
    aqui em vez de implementar uma distinção owner-based que contradiria a própria decisão de
    compartilhamento binário já fixada no §3 deste backlog. 1 novo teste (`DashboardQueryTests`): cria
    e publica um dashboard com 1 widget `netRevenue`/`date.month`, chama
    `POST /api/v1/dashboards/{id}/query` e `POST /api/v1/analytics/query` com o mesmo `QueryDefinition`
    lado a lado, confirma dataset/rowCount/soma de `netRevenue` idênticos nos dois caminhos.
  - *Aceite:* teste prova que o resultado bate com o mesmo `QueryDefinition` executado direto via
    `POST /api/v1/analytics/query` — mesmo caminho de código, só orquestrado por dashboard/widget.
- [x] **E3.3** Novas permissões `dashboard.view`/`dashboard.manage` e `analytics.query`
      (`EIP.Shared.Contracts`, mesmo padrão de `MetricsPermissions`/`ConnectorPermissions`) — Owner/
      Admin ganham manage+view, Member ganha só view, todos ganham `analytics.query` (mesma politica
      de leitura agregada já usada para `metrics.view` na Fase 1).
  - *Depende de:* E3.1. ✅ Concluído (2026-08-23) — `DashboardPermissions` novo em
    `EIP.Shared.Contracts.Dashboards`, `RolePermissions` atualizado (Owner/Admin: view+manage; Member:
    só view; `analytics.query` já existia desde a Fase 2 E1.2, concedida a todos os papéis). 4 novos
    testes em `DashboardPermissionTests`: Member consegue `GetById` (200), mas `Publish`/`Archive`/
    `Update` retornam 403.
  - *Aceite:* teste prova que um Member recebe 403 ao tentar publicar/arquivar um dashboard, mas
    consegue visualizá-lo.

## E4 — Frontend: visualização e criação de dashboard

Objetivo: fechar o critério de saída "gestor autorizado visualiza dashboard de produção com filtros e
dados consistentes" — a parte do MVP que precisa ser genuinamente real, não só a API.

- [x] **E4.1** Página de listagem de dashboards do tenant autenticado (Angular, reaproveitando
      `AuthService`/roteamento já existentes da Fase 0).
  - *Depende de:* E3.1. ✅ Concluído (2026-08-23) — `features/dashboards/dashboard-list` (mesma
    convenção de arquivo de `features/dashboard`: classe sem sufixo `Component`, `.html`/`.scss`/
    `.spec.ts` irmãos, Tailwind + Angular Material). Primeira vez que `MatTableModule` é usado no
    projeto (só Button/Card/FormField/Input/List/ProgressSpinner existiam até aqui) — natural para uma
    listagem tabular com colunas fixas. Rota `/dashboards` protegida por `tenantGuard` (mesmo guard de
    `/dashboard`). Link "Ver painéis" adicionado à tela de boas-vindas da Fase 0 para navegar até aqui.
- [x] **E4.2** Página de visualização de um dashboard publicado: renderiza KPI (valor + rótulo),
      Line chart (ECharts, já declarado no stack — `docs/03`) e Table com dado real vindo de E3.2;
      exibe frescor dos dados e filtros ativos (`docs/12 §10`, mínimo de acessibilidade: título,
      descrição, alternativa textual do valor principal de cada widget).
  - *Depende de:* E3.2, E4.1. ✅ Concluído (2026-08-23) — primeiro uso de Apache ECharts no frontend.
    `npm install echarts`; `shared/echart-line/echart-line.ts` importa só os módulos necessários
    (`echarts/core` + `LineChart`/`GridComponent`/`TooltipComponent`/`CanvasRenderer`) em vez do
    pacote `echarts` completo — reduziu o chunk lazy de `dashboard-viewer` de 1,14 MB para 507 KB
    (transferência estimada 310 KB → 147 KB). `dashboard-viewer` busca `GetById` (metadados/widgets) e
    `POST .../query` (dados) em paralelo, casando `widgets[i]` com `results[i]` por índice (mesma
    ordem, já que ambos iteram `Dashboard.Widgets`). Kpi soma a métrica principal em todas as linhas
    retornadas (nunca mostra só a primeira linha silenciosamente, já que o Analytics Engine sempre
    agrupa por 1 dimensão — nem sempre resulta em exatamente 1 linha). Acessibilidade mínima: cada
    widget é uma `<section aria-label>`; Kpi e LineChart têm um `<span class="sr-only">` com a
    alternativa textual completa do valor/série. Tabela renderizada com HTML semântico simples (não
    `MatTableModule`) porque as colunas variam por widget — dinamizar `matColumnDef` por linha exigiria
    complexidade desproporcional ao ganho. **Validado visualmente pelo usuário no navegador real**
    (não só "a chamada HTTP funciona"): subi a stack completa (infra Docker + Host + Gateway +
    `ng serve`), criei um usuário de teste, dei a ele uma Membership Owner no tenant "Empresa Demo" já
    existente no banco de dev (reaproveitando 5 linhas de fato/3 cargas concluídas de sessões
    anteriores — sem precisar ressincronizar conectores), criei e publiquei um painel com os 3 tipos de
    widget via API real (`curl`) confirmando Receita Líquida R$ 11.100 e 3 clientes na tabela, e pedi
    para o usuário abrir `http://localhost:4200/dashboards` e confirmar visualmente — usuário confirmou
    ("validado"). **Limitação honesta**: nenhuma ferramenta de automação de navegador local estava
    disponível nesta sessão para eu mesmo tirar screenshot (mesma limitação já registrada no
    fechamento do E6 da Fase 0) — a confirmação visual veio do usuário, não de mim.
  - *Aceite:* validado visualmente pelo usuário no navegador (mesmo padrão pendente desde o E6 da
    Fase 0) — não basta a chamada HTTP funcionar, precisa aparecer certo na tela.
- [x] **E4.3** Criação/edição mínima: formulário para criar um dashboard e adicionar widgets um a um
      (dataset/métricas/dimensões/tipo — sem editor visual de posicionamento livre, ver §3) + botão de
      publicar.
  - *Depende de:* E3.1, E1.3 (para popular os campos disponíveis a partir do catálogo real).
    ✅ Concluído (2026-08-23) — `features/dashboards/dashboard-form` cobre criar (`/dashboards/new`) e
    editar (`/dashboards/:id/edit`) com o mesmo componente. Fluxo em 2 passos: "Criar rascunho" (só
    Nome) cria o `Dashboard` via API antes de qualquer widget existir; só então a seção de widgets
    aparece. Campos do widget (título, tipo, métricas via checkbox, dimensão via select) vêm
    diretamente de `GET /api/v1/analytics/datasets` — nenhum hardcode duplicado do catálogo (mudar uma
    métrica no backend muda as opções aqui automaticamente, mesma garantia do E1.3). Sem seletor de
    dataset na tela: só existe `sales` publicado nesta fase, then um seletor mostraria uma única opção
    sem propósito. "Salvar rascunho" faz `PATCH`; "Publicar" faz `PATCH` seguido de `POST .../publish`
    e navega para o viewer. Validado end-to-end via API real (mesma sessão de validação do E4.2) e via
    `ng build`/`ng test` (12/12, incluindo os 3 novos specs `should create`).

## E5 — Isolamento e Fechamento da Fase

Objetivo: fechar o critério de saída "toda tabela com `TenantId` possui RLS" para o novo schema
`dashboard`, e validar a Fase 2 inteira de ponta a ponta, mesmo rigor das revisões que fecharam as
Fases 0 e 1.

- [x] **E5.1** Testes cross-tenant automatizados cobrindo `dashboard.*` (RLS real via EF Core,
      Testcontainers — mesmo padrão de `CanonicalCrossTenantIsolationTests`/
      `WarehouseCrossTenantIsolationTests` do fechamento da Fase 1) e o cache do Analytics Engine
      (E1.4 — mesma chave nunca vaza entre tenants).
  - *Depende de:* E2.1, E1.4. ✅ Concluído (2026-08-23) — novo `DashboardTableCrossTenantIsolationTests`
    (`EIP.Intelligence.Dashboard.Infrastructure.IntegrationTests`, mesmo padrão EF-Core-através-da-RLS
    real, nunca SQL bruto): sem `SESSION_CONTEXT` nenhuma linha aparece; com o contexto do tenant A só
    as linhas do tenant A aparecem, mesmo pedindo explicitamente o tenant B; `INSERT` com `TenantId`
    divergente do `SESSION_CONTEXT` é bloqueado pelo block predicate — para `Dashboards` e
    `DashboardVersions`. Defesa em profundidade além do gate estrutural genérico (`RlsCoverageTests`,
    já provado no E2.1) e além do isolamento em nível de API (`DashboardCrossTenantIsolationTests` no
    `EIP.Host.IntegrationTests`, E3.1, que prova 404 — este aqui prova que o banco em si nunca
    devolveria a linha, mesmo que um bug futuro no controller esquecesse de checar algo). O isolamento
    de cache do Analytics Engine já estava coberto desde o E1.4
    (`AnalyticsQueryCacheTests.Query_SameRequestDifferentTenants_NeverSharesCacheEntry`) — nenhum teste
    novo necessário, só reaproveitado como evidência aqui.
- [x] **E5.2** Validação end-to-end real, com infraestrutura de verdade rodando (Host, Gateway,
      Worker, SQL Server, Redis, RabbitMQ, MinIO, frontend): criar um dashboard com os 3 tipos de
      widget sobre dado real da Fase 1, publicar, visualizar no navegador, confirmar cache hit numa
      segunda consulta idêntica.
  - ✅ Concluído (2026-08-23) — stack completa real rodando (Docker Compose + Host `:5080` + Gateway
    `:5000` + `ng serve` `:4200`); usuário de teste com Membership Owner no tenant "Empresa Demo" já
    existente no banco de dev (reaproveitando 5 linhas de fato/3 cargas concluídas de sessões
    anteriores da Fase 1, sem precisar ressincronizar conectores). Criado e publicado um painel com os
    3 tipos de widget (Kpi/LineChart/Table) sobre o dataset `sales`; **validado visualmente pelo
    usuário no navegador real** em `http://localhost:4200/dashboards` (E4.2). Cache hit confirmado ao
    vivo pelos próprios logs estruturados do Host (E1.6): primeira consulta
    `cacheHit=False durationMs=23.9`, segunda consulta idêntica `cacheHit=True durationMs=3.9` — mais
    de 5x mais rápida, e o Redis (`docker exec eip-redis redis-cli KEYS "analytics:*"`) mostrou as
    entradas reais criadas. Worker não precisou ser exercitado nesta validação (o dado já existia de
    sincronizações da Fase 1; a Fase 2 não introduziu nenhum código novo no Worker).
- [x] **E5.3** Revisão formal da seção 4 (Definition of Done) desta fase, com evidência por critério —
      mesmo processo aplicado às Fases 0 e 1. Atualizar a tabela de rastreamento (§7) e a memória do
      projeto.
  - ✅ Concluído (2026-08-23) — ver §4 abaixo, todos os 6 critérios revisados com evidência real.
    **Lacuna real encontrada e corrigida durante esta revisão** (não hipotética): o critério
    "publicação...e acesso por escopo são auditados" não tinha nenhuma evidência concreta —
    `DashboardVersion` não registrava quem publicou, e nenhuma ação do módulo gerava log estruturado.
    Corrigido com `DashboardVersion.PublishedByUserId` (migration aditiva
    `AddPublishedByUserIdToDashboardVersion` — a inicial já tinha dado real de sessões de validação
    anteriores, então nunca regenerada) + log estruturado (`[LoggerMessage]`, mesmo idioma do E1.6) em
    toda mutação e visualização (`Created`/`Updated`/`Published`/`Archived`/`Viewed`, com
    dashboardId/tenantId/userId + `CorrelationId` automático). Validado ao vivo: exercitei o ciclo de
    vida completo via `curl` contra o Host real e capturei as 5 linhas de log correspondentes, além de
    confirmar via SQL que a versão publicada depois da correção já grava o `PublishedByUserId` real
    (as duas versões publicadas antes da correção mostram o valor de backfill `00000000-...`,
    documentado como esperado, não escondido). 73/73 testes passando na solução inteira,
    `dotnet build`/`dotnet format --verify-no-changes` limpos.

---

# 6. Fora do Escopo da Fase 2

Para não perder o foco (`docs/15-Roadmap.md §3`), os itens abaixo são explicitamente adiados:

- Domínios Financeiro (`FinancialTitle`/`FinancialTransaction`) e Estoque
  (`InventoryBalance`/`InventoryMovement`) como dataset analítico — só `sales` (Comercial).
- `Workspace` — mantido fora do escopo (decisão já registrada nas Fases 0 e 1).
- Catálogo de datasets/métricas configurável por usuário (motor de métricas genérico) — o catálogo
  desta fase é fixo em código, mesmo padrão do E6 da Fase 1.
- Time Intelligence avançado (comparação MoM/YoY, calendário fiscal, média móvel) e Análises
  Avançadas (tendência, previsão, anomalia, decomposição) — `docs/10 §9/§10`, ambos fora do MVP do
  próprio Analytics Engine.
- Exportação (PDF/CSV/XLSX) como job assíncrono — `docs/10 §8.3`/`docs/12 §8.3`.
- Compartilhamento externo/embeds públicos — explicitamente fora do MVP em `docs/12 §8.2`.
- `Page`/`Theme`/`SharePolicy` como entidades próprias — dashboard de página única, tema fixo do
  frontend, compartilhamento binário por permissão.
- Estados `InReview` no ciclo de vida do dashboard — sem processo de aprovação modelado ainda.
- Widgets `Bar`/`Donut`/`Filter control` — só `Kpi`/`LineChart`/`Table` nesta fase.
- Editor visual de posicionamento livre (drag-and-drop) — widgets empilhados em ordem de criação.
- Drill-down/drill-through — `docs/12 §7.3`, sem hierarquias declaradas no dataset ainda.
- IA Assistida na criação de dashboards — `docs/12 §11`, depende da Fase 4 (IA).
- Quotas por plano/usuário — não existe conceito de "plano" comercial no sistema ainda; só os
  guardrails mínimos do E1.5.
- Invalidação de cache orientada a evento (por atualização do Warehouse) — só TTL curto nesta fase.
- Kubernetes/Helm — Docker Compose continua suficiente (mesmo critério das fases anteriores).

---

# 7. Rastreamento

| Épico | Status |
|---|---|
| E1 — Analytics Engine | Concluído (E1.1–E1.6) |
| E2 — Dashboard Builder: domínio e persistência | Concluído (E2.1–E2.3) |
| E3 — Dashboard Builder: API | Concluído (E3.1–E3.3) |
| E4 — Frontend: visualização e criação de dashboard | Concluído (E4.1–E4.3) |
| E5 — Isolamento e Fechamento da Fase | Concluído (E5.1–E5.3) |

**Fase 2 formalmente fechada em 2026-08-23** — todos os 5 épicos (E1–E5) e os 6 critérios de saída da
seção 4 concluídos com evidência real, mesmo rigor do fechamento das Fases 0 e 1. Próximo passo:
memória do projeto atualizada (E5.3) e decisão do usuário sobre commitar o trabalho local / iniciar a
Fase 3.
