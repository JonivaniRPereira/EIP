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
Fases 0 e 1:

- [ ] Gestor autorizado visualiza dashboard de produção com filtros e dados consistentes.
- [ ] Métricas possuem dono, definição, versão e reconciliação aprovadas.
- [ ] Dashboards não acessam ERP/Raw/DW diretamente.
- [ ] Consultas respeitam orçamento de desempenho e quotas.
- [ ] Publicação, exportação e acesso por escopo são auditados.

Critério adicional obrigatório por conta da ADR-007 (mesmo texto usado para fechar as Fases 0 e 1):

- [ ] Toda tabela com `TenantId` (agora incluindo o novo schema `dashboard`) possui política RLS
      ativa, e o gate automatizado (`RlsCoverageTests`) continua passando sem exceções adicionadas.

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
- [ ] **E1.3** `GET /api/v1/analytics/datasets`/`GET /api/v1/analytics/datasets/{id}` — expõe o
      catálogo (métricas/dimensões/dono/versão) para o frontend descobrir o que pode consultar, sem
      hardcode duplicado no cliente.
  - *Depende de:* E1.2.
  - *Aceite:* resposta reflete exatamente o catálogo em código (mudar uma definição no backend muda a
    resposta, sem exigir alteração no frontend).
- [ ] **E1.4** Cache real no Redis (`ADR-006`, primeiro uso funcional de fato — gap documentado no
      fechamento da Fase 1, E8.3): chave `tenant + dataset + versão semântica + consulta normalizada +
      versão de dado`, TTL curto configurável. Cache nunca serve dado de um tenant para outro (a
      chave inclui o tenant sempre derivado do claim, nunca do payload).
  - *Depende de:* E1.2.
  - *Aceite:* teste prova cache hit (segunda consulta idêntica não gera nova query no Warehouse,
    validado por contagem de chamadas) e cache miss corretamente isolado por tenant (mesma consulta,
    tenants diferentes, entradas de cache diferentes).
- [ ] **E1.5** Guardrails mínimos: limite de linhas de resposta, período máximo de filtro de data,
      timeout de execução. Consulta que excede qualquer limite retorna erro claro (`docs/06 §10`
      Problem Details), nunca trunca silenciosamente.
  - *Depende de:* E1.2.
  - *Aceite:* teste prova que exceder o limite de período retorna 400 com mensagem explicando qual
    limite foi violado.
- [ ] **E1.6** Observabilidade da consulta (`docs/10 §11`): log estruturado (Serilog, já usado desde a
      Fase 0) com dataset/métricas/dimensões/tenant/cache hit-ou-miss/duração/`CorrelationId`, sem
      expor valores de dado sensível no log.
  - *Depende de:* E1.2, E1.4.
  - *Aceite:* validado ao vivo — uma consulta real produz uma linha de log com todos os campos
    esperados.

## E2 — Dashboard Builder: domínio e persistência

Objetivo: modelo mínimo de `docs/12-Dashboard-Builder.md §3/§4` para o novo domínio `Intelligence`,
com RLS obrigatória desde a primeira migration (ADR-007).

- [ ] **E2.1** Novo módulo `EIP.Intelligence.Dashboard.{Domain,Infrastructure}`, schema `dashboard`.
      Entidades mínimas: `Dashboard` (`Id`, `TenantId`, `Name`, `OwnerId`, `Status`
      `Draft`/`Published`/`Archived`) e `DashboardVersion` (imutável, `DashboardId`, `Widgets` — lista
      de `Widget` como Value Object serializado, não tabela própria, já que nunca é consultado
      isoladamente fora do próprio dashboard). `Widget`: `Type` (`Kpi`/`LineChart`/`Table`), `Title`,
      `QueryDefinition` (dataset/métricas/dimensões/filtros — mesmo contrato do E1.2), `Visualization`
      (config mínima de exibição por tipo).
  - *Depende de:* E1.2 (o contrato de `QueryDefinition` precisa existir para o Widget referenciá-lo).
  - *Aceite:* migration inicial já nasce com `TenantId` + política RLS (mesmo padrão de sempre) —
    sem exceção.
- [ ] **E2.2** Publicação: `Dashboard.Publish()` cria/promove uma `DashboardVersion` imutável de forma
      atômica; editar um dashboard publicado sempre cria um novo rascunho, nunca sobrescreve a versão
      publicada (`docs/12 §4`).
  - *Depende de:* E2.1.
  - *Aceite:* teste prova que publicar duas vezes gera duas `DashboardVersion` distintas, e a versão
    anterior continua íntegra e recuperável (histórico).
- [ ] **E2.3** Validação da `QueryDefinition` de cada widget contra o catálogo do Analytics Engine
      (E1.3) no momento de salvar/publicar — nunca aceitar uma consulta inválida num rascunho que será
      publicado.
  - *Depende de:* E1.3, E2.1.
  - *Aceite:* teste prova que salvar um widget com uma métrica inexistente é rejeitado antes de
    persistir.

## E3 — Dashboard Builder: API

Objetivo: fechar `docs/12 §8/§12` reduzido ao escopo desta fase — CRUD, publicação e execução de
consulta, sempre delegando ao Analytics Engine (E1), nunca acessando o Warehouse diretamente.

- [ ] **E3.1** Novo `EIP.Intelligence.Dashboard.Api`: `POST /api/v1/dashboards` (criar rascunho),
      `GET /api/v1/dashboards` (listar do tenant), `GET /api/v1/dashboards/{id}`,
      `PATCH /api/v1/dashboards/{id}` (editar rascunho — publicado sempre cria novo rascunho, E2.2),
      `POST /api/v1/dashboards/{id}/publish`, `POST /api/v1/dashboards/{id}/archive`.
  - *Depende de:* E2.1, E2.2.
  - *Aceite:* teste de isolamento cross-tenant (tenant A não lê/edita/publica dashboard de tenant B,
    mesmo adulterando o Id na rota — mesmo padrão de `ConnectorCrossTenantIsolationTests`).
- [ ] **E3.2** `POST /api/v1/dashboards/{id}/query` (ou por widget) — executa a `QueryDefinition` de
      cada widget do dashboard publicado (ou do rascunho, se o solicitante for o dono) via o Analytics
      Engine (E1.2), nunca lendo `warehouse.*` diretamente deste módulo.
  - *Depende de:* E1.2, E3.1.
  - *Aceite:* teste prova que o resultado bate com o mesmo `QueryDefinition` executado direto via
    `POST /api/v1/analytics/query` — mesmo caminho de código, só orquestrado por dashboard/widget.
- [ ] **E3.3** Novas permissões `dashboard.view`/`dashboard.manage` e `analytics.query`
      (`EIP.Shared.Contracts`, mesmo padrão de `MetricsPermissions`/`ConnectorPermissions`) — Owner/
      Admin ganham manage+view, Member ganha só view, todos ganham `analytics.query` (mesma politica
      de leitura agregada já usada para `metrics.view` na Fase 1).
  - *Depende de:* E3.1.
  - *Aceite:* teste prova que um Member recebe 403 ao tentar publicar/arquivar um dashboard, mas
    consegue visualizá-lo.

## E4 — Frontend: visualização e criação de dashboard

Objetivo: fechar o critério de saída "gestor autorizado visualiza dashboard de produção com filtros e
dados consistentes" — a parte do MVP que precisa ser genuinamente real, não só a API.

- [ ] **E4.1** Página de listagem de dashboards do tenant autenticado (Angular, reaproveitando
      `AuthService`/roteamento já existentes da Fase 0).
  - *Depende de:* E3.1.
- [ ] **E4.2** Página de visualização de um dashboard publicado: renderiza KPI (valor + rótulo),
      Line chart (ECharts, já declarado no stack — `docs/03`) e Table com dado real vindo de E3.2;
      exibe frescor dos dados e filtros ativos (`docs/12 §10`, mínimo de acessibilidade: título,
      descrição, alternativa textual do valor principal de cada widget).
  - *Depende de:* E3.2, E4.1.
  - *Aceite:* validado visualmente pelo usuário no navegador (mesmo padrão pendente desde o E6 da
    Fase 0) — não basta a chamada HTTP funcionar, precisa aparecer certo na tela.
- [ ] **E4.3** Criação/edição mínima: formulário para criar um dashboard e adicionar widgets um a um
      (dataset/métricas/dimensões/tipo — sem editor visual de posicionamento livre, ver §3) + botão de
      publicar.
  - *Depende de:* E3.1, E1.3 (para popular os campos disponíveis a partir do catálogo real).

## E5 — Isolamento e Fechamento da Fase

Objetivo: fechar o critério de saída "toda tabela com `TenantId` possui RLS" para o novo schema
`dashboard`, e validar a Fase 2 inteira de ponta a ponta, mesmo rigor das revisões que fecharam as
Fases 0 e 1.

- [ ] **E5.1** Testes cross-tenant automatizados cobrindo `dashboard.*` (RLS real via EF Core,
      Testcontainers — mesmo padrão de `CanonicalCrossTenantIsolationTests`/
      `WarehouseCrossTenantIsolationTests` do fechamento da Fase 1) e o cache do Analytics Engine
      (E1.4 — mesma chave nunca vaza entre tenants).
  - *Depende de:* E2.1, E1.4.
- [ ] **E5.2** Validação end-to-end real, com infraestrutura de verdade rodando (Host, Gateway,
      Worker, SQL Server, Redis, RabbitMQ, MinIO, frontend): criar um dashboard com os 3 tipos de
      widget sobre dado real da Fase 1, publicar, visualizar no navegador, confirmar cache hit numa
      segunda consulta idêntica.
- [ ] **E5.3** Revisão formal da seção 4 (Definition of Done) desta fase, com evidência por critério —
      mesmo processo aplicado às Fases 0 e 1. Atualizar a tabela de rastreamento (§7) e a memória do
      projeto.

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
| E1 — Analytics Engine | Em andamento (E1.1, E1.2 concluídos) |
| E2 — Dashboard Builder: domínio e persistência | Não iniciado |
| E3 — Dashboard Builder: API | Não iniciado |
| E4 — Frontend: visualização e criação de dashboard | Não iniciado |
| E5 — Isolamento e Fechamento da Fase | Não iniciado |

Atualizar esta tabela conforme os épicos avançam.
