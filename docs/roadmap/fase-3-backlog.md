# Backlog de Execução — Fase 3 (Produto Operável e Expansão Controlada)

**Projeto:** Enterprise Intelligence Platform (EIP)
**Versão:** 1.0
**Status:** Oficial
**Última atualização:** 19/10/2026 — Fase 3 encerrada no escopo definido no §3

---

# 1. Objetivo

Este documento traduz `docs/15-Roadmap.md §7` (Fase 3) em tarefas concretas, ordenadas e verificáveis
— mesmo papel que `docs/roadmap/fase-0-backlog.md`, `docs/roadmap/fase-1-backlog.md` e
`docs/roadmap/fase-2-backlog.md` cumpriram para as fases anteriores (todas concluídas e formalmente
fechadas: 2026-08-01, 2026-08-04 e 2026-08-23).

Fase 3 prepara o MVP (Fases 0–2) para operação piloto: mais confiabilidade operacional, mais
segurança, e o fechamento de lacunas que já incomodaram todas as fases anteriores na prática (mais
notavelmente, nunca existiu uma forma de um usuário criar seu próprio tenant — toda validação
ao vivo até aqui precisou de SQL bruto). Isso segue `docs/15-Roadmap.md §7`: "preparar o MVP para
operação piloto com mais usuários, empresas, fontes e rotinas de suporte" — mas **este projeto ainda
não tem clientes/piloto reais**, então a fase é deliberadamente filtrada para o que dá para construir
e validar sozinho (§3 abaixo detalha cada redução).

Este documento **não substitui** nenhuma regra definida em `docs/00` a `docs/15` ou nas ADRs. Em caso
de conflito, os documentos de arquitetura/segurança/ADR prevalecem.

---

# 2. Como usar este backlog

- Cada tarefa tem um ID (`E<épico>.<sequência>`), descrição, arquivos/projetos afetados, dependências
  e critério de aceite. Numeração reinicia em `E1` neste arquivo — cada backlog de fase é
  autocontido.
- Marcar `- [x]` somente quando o critério de aceite estiver satisfeito e validado (build/teste
  passando contra infraestrutura real, ou validação ao vivo quando aplicável), nunca por "está quase
  pronto" — mesma disciplina das fases anteriores.
- Épicos são majoritariamente independentes entre si nesta fase (ao contrário das Fases 1/2, que eram
  fortemente sequenciais) — podem ser feitos em qualquer ordem, mas a ordem listada reflete prioridade
  (E1 fecha a lacuna mais sentida na prática; E6 fecha a fase).
- Toda tarefa que cria uma tabela com `TenantId` continua bloqueada até a política RLS correspondente
  existir e ter teste de acesso cruzado — sem exceção (ADR-007), mesma regra permanente desde a
  Fase 0.
- A Fase 3 só é considerada concluída quando a seção 4 (Critérios de Saída) estiver 100% satisfeita
  — mesma regra das fases anteriores, com as reduções de escopo do §3 aplicadas.

---

# 3. Decisões técnicas fixadas para a Fase 3

| Decisão | Valor | Origem |
|---|---|---|
| `Workspace` continua fora do escopo | Terceira vez que a decisão é revisitada (Fases 0, 1, agora 3) — sem caso de uso validado que exija segmentar dentro do tenant. Dashboards/Companies/tudo continua escopado só por `Tenant` | Decisão do usuário em 2026-08-23 — reavaliar em fase futura se houver demanda validada |
| Nenhum conector novo | `docs/15-Roadmap.md §3` já declara o princípio: "cada conector novo precisa justificar demanda comercial e datasets necessários" — sem cliente real ainda, não há essa demanda. A fase robustece (retry, DLQ visível, documentação) o único conector de referência que já existe (REST genérico, Fase 0 E7) em vez de adicionar uma origem nova especulativa | Decisão do usuário em 2026-08-23 |
| Itens que pressupõem piloto/cliente real ficam fora do escopo, um a um, documentados | Onboarding "de produto" (UX de ativação), planos/billing, modo shared/dedicated por tenant elegível, processo de suporte formal — todos pressupõem usuários reais que ainda não existem neste projeto solo. Cada um está listado no §6 com o motivo, mesmo padrão já usado para Workspace/exportação nas fases anteriores | Decisão do usuário em 2026-08-23 |
| "Onboarding" nesta fase = provisionamento próprio de tenant, não UX de ativação de produto | O gap que mais incomodou na prática: da Fase 0 à Fase 2, toda validação ao vivo precisou de um tenant criado via SQL bruto sob a sentinela `TenantContext.System` (nunca existiu `POST /api/v1/tenants` de autoatendimento). Fechar isso é puramente de engenharia, testável sozinho, e teria evitado retrabalho manual em quase toda sessão de validação até aqui | Achado real ao revisar o histórico das Fases 0–2 para planejar esta |
| Sem Grafana/Alertmanager nesta fase | `docs/03-Stack-Tecnologica.md` declara Grafana no stack, mas nunca foi de fato implantado localmente (só Prometheus scrape + exporter, desde a Fase 0 E3) — implantar o par completo de alerta é desproporcional sem operação real gerando alertas para reagir. SLOs desta fase são definidos e *medidos* via as métricas já expostas, não *alertados* automaticamente | Escopo desta fase, YAGNI sem operação real |
| Testes de carga usam k6 (ou ferramenta equivalente já disponível), contra a stack local via Docker Compose | Sem ambiente de staging/produção ainda — a prova de isolamento sob carga roda contra a mesma infraestrutura local já usada em todo o projeto, simulando múltiplos tenants concorrentes | Escopo desta fase |
| Quotas: só sincronização e uso mensurado, sem armazenamento/exportação | Quota de armazenamento no Data Lake e quota de exportação exigiriam dados de uso real para calibrar limites sensatos — sem eles, qualquer número seria arbitrário. Quota de sincronização (frequência por tenant) é o caso mais concreto e já tem precedente de abuso óbvio (tenant disparando sync em loop) | Escopo desta fase, YAGNI sem dado de uso real |

---

# 4. Critérios de Saída da Fase 3 (Definition of Done)

Copiados literalmente de `docs/15-Roadmap.md §7`. A Fase 3 só termina quando todos estiverem `[x]`,
com evidência real (não "os épicos estão codificados") — mesma disciplina aplicada no fechamento das
Fases 0, 1 e 2. Onde um critério pressupõe piloto/cliente real que este projeto ainda não tem, isso é
marcado explicitamente como N/A nesta fase (mesmo padrão já usado para "cache" no fechamento da
Fase 1, E8.3, e para "exportação" no fechamento da Fase 2):

- [x] Piloto atende múltiplos tenants sem incidente de isolamento.
      *(Sem piloto real — reinterpretado como: testes de carga/isolamento com múltiplos tenants
      concorrentes simulados, E6.3, provam ausência de incidente de isolamento sob carga.)*
- [x] Conectores possuem monitoramento, retry, DLQ e documentação operacional.
- [x] Recuperação de backup e rollback de release foram testados.
- [x] Métricas de adoção, frescor, falha de sincronização e satisfação são acompanhadas.
      *(Adoção/satisfação são N/A nesta fase — pressupõem usuários reais. Frescor e falha de
      sincronização já são medidos desde as Fases 1/2; esta fase garante que ficam visíveis também
      numa perspectiva operacional entre tenants, não só por tenant individual.)*
- [x] Custos de infraestrutura e consulta são mensurados por tenant/plano.
      *("Plano" é N/A nesta fase (sem billing) — reinterpretado como: uso mensurado por tenant
      (sincronizações, consultas analíticas), não custo em R$/USD, que exigiria dado real de
      infraestrutura de produção que este projeto não tem.)*

Critério adicional obrigatório por conta da ADR-007 (mesmo texto usado para fechar as Fases 0, 1 e 2):

- [x] Toda tabela nova com `TenantId` possui política RLS ativa, e o gate automatizado
      (`RlsCoverageTests`) continua passando sem exceções adicionadas.

Revisão formal em 19/10/2026: [matriz de evidências e limites](fechamento-fase-3-2026-10-19.md).
Os N/A acima permanecem; o fechamento não atesta produção ou pipeline completo na stack atual.
Gate RLS/acesso cruzado/uso reexecutado: 7/7 aprovados. Frescor/falhas entre tenants agora
também têm coleta operacional manual: [snapshot](../operations/operacao-tenants-2026-10-19.json).

---

# 5. Épicos e Tarefas

## E1 — Provisionamento Próprio de Tenant (o "onboarding" real desta fase)

Objetivo: fechar o gap que mais custou retrabalho manual em toda validação ao vivo desde a Fase 0 —
nunca existiu uma forma de um usuário criar seu próprio tenant sem intervenção via SQL bruto.

- [x] **E1.1** `POST /api/v1/tenants` — usuário autenticado (sem tenant ainda, ou com
      `requiresTenantSelection=true`) cria um novo Tenant + Company inicial + Membership `Owner`
      `Active` para si mesmo, tudo numa única transação atômica. Nunca usa a sentinela
      `TenantContext.System` diretamente do request do cliente — o endpoint em si roda sob o sistema
      internamente (mesmo padrão de `IMembershipDirectory`), o cliente só fornece nome/slug/país.
  - *Depende de:* nenhuma (usa entidades já existentes de `EIP.Platform.Tenant.Domain`).
    ✅ Concluído (2026-08-23) — `TenantsController.Create` (`EIP.Platform.Tenant.Api`), sem
    `[RequirePermission]` explícito (o `FallbackPolicy` global, E2.5/Fase 0, já exige só autenticação —
    o usuário ainda não tem nenhuma permissão de tenant nesse momento). `Tenant.Create` + `.Activate()`
    + `Company.Create` + `Membership.Create` + `.Activate()` construídos em memória primeiro (validação
    de domínio falha antes de qualquer I/O), só então persistidos numa única transação sob
    `TenantContext.System` (via `ITenantContextAccessor`, injetado no controller) — `Company`/
    `Membership` são RLS-protegidas e o tenant novo ainda não tem `SESSION_CONTEXT` nenhum definido
    para si mesmo antes de existir. Teste prova o fluxo completo: usuário se registra, cria tenant,
    faz login de novo e já cai direto (sem seleção manual, sem SQL).
  - *Aceite:* teste de integração prova que um usuário recém-registrado, sem nenhum tenant, consegue
    criar um e imediatamente fazer login nele sem qualquer etapa manual/SQL.
- [x] **E1.2** `Tenant.Slug` já valida unicidade no domínio (Fase 0) — confirmar que o novo endpoint
      devolve um erro claro (`409` ou `400` com `ProblemDetails`) num slug duplicado, nunca um erro
      de banco vazando pro cliente.
  - *Depende de:* E1.1. ✅ Concluído (2026-08-23) — checagem proativa de unicidade (`Tenants.AnyAsync`,
    tabela `tenant.Tenants` não é RLS-protegida — o próprio tenant não precisa de `SESSION_CONTEXT`
    para se enxergar) antes de qualquer tentativa de escrita, mais um `catch (DbUpdateException)` como
    backstop para a rara corrida entre a checagem e o `INSERT` (o índice único de `Slug`,
    `TenantConfiguration`, é a garantia real). Os dois caminhos retornam `409 Conflict` com
    `ProblemDetails`, nunca um 500. Teste prova: dois usuários diferentes tentando o mesmo slug — o
    segundo recebe 409.
  - *Aceite:* teste prova que criar um tenant com slug já existente retorna erro claro, nunca 500.
- [x] **E1.3** Frontend: tela "Criar organização" (Angular) — formulário mínimo (nome, slug,
      país), chamada ao novo endpoint, redireciona para o dashboard de boas-vindas já existente.
      Aparece automaticamente no fluxo de login quando `requiresTenantSelection=true` e
      `availableTenants` está vazio (hoje esse estado mostra "nenhuma organização disponível, faça
      login novamente" — um beco sem saída real).
  - *Depende de:* E1.1. ✅ Concluído (2026-08-23) — `features/create-tenant`, rota `/create-tenant`
    (`authGuard`, não `tenantGuard` — o usuário ainda não tem tenant). `select-tenant.html` trocou a
    mensagem de beco sem saída por um botão "Criar organização". Depois de criar, chama
    `auth.refresh()` (nunca um novo login) para o token já refletir a membership recém-criada — com
    exatamente 1 membership o tenant é selecionado automaticamente. **Ampliado durante a validação
    visual**: o usuário pediu um app shell de verdade (sidebar + topbar) inspirado no template
    Able Pro (Angular Material, mesma biblioteca já usada no projeto) — construído em
    `core/layout/shell.ts`, envolvendo toda rota autenticada+com-tenant via rota pai no
    `app.routes.ts` (`tenantGuard` aplicado uma vez no pai, não repetido em cada filha). Páginas que
    antes eram cada uma um card centralizado de página cheia (`dashboard`, `dashboard-list`,
    `dashboard-viewer`, `dashboard-form`) tiveram o wrapper de página removido e os botões de
    "Sair"/navegação duplicados retirados, já que o shell agora provê isso globalmente (sidebar
    "Início"/"Painéis", menu do usuário com e-mail/iniciais/"Sair" na topbar). **Bug real encontrado e
    corrigido durante a validação visual do usuário**: o botão do menu do usuário usava a diretiva
    `mat-button`, cujo wrapper interno do Material (`.mdc-button__label`) não respeita bem um layout
    flex customizado (avatar + e-mail + seta) — quebrava em duas linhas dentro de uma pílula estreita.
    Corrigido trocando para um `<button>` simples sem a diretiva `mat-button`, estilizado só com
    Tailwind. Validado visualmente pelo usuário depois da correção ("está correto agora").
  - *Aceite:* validado visualmente pelo usuário no navegador — um usuário novo consegue ir do
    registro até o dashboard sem nenhuma etapa fora da UI.

## E2 — Conector de Referência: Robustecimento Operacional

Objetivo: fechar o critério de saída "conectores possuem monitoramento, retry, DLQ e documentação
operacional" para o único conector que existe — em vez de adicionar uma origem nova sem demanda.

- [x] **E2.1** Retry com backoff limitado antes de ir para a DLQ (hoje: `BasicNack(requeue: false)`
      direto na primeira falha, decisão aceita na Fase 0 E7.2 por não haver ainda o plugin de
      mensagem atrasada do RabbitMQ). Usar `x-delayed-message` ou uma fila de retry intermediária
      com TTL crescente (ex. 3 tentativas, 10s/30s/90s) — só então DLQ. Nunca retry infinito.
  - *Depende de:* nenhuma.
  - *Aceite:* teste prova que uma falha transitória (ex. timeout simulado) é reprocessada
    automaticamente até N tentativas antes de cair na DLQ; uma falha persistente ainda cai na DLQ
    depois de esgotar as tentativas.
  - **Evidência:** sem o plugin `x-delayed-message` (mesma restrição de imagem da Fase 0), a escada de
    retry usa 3 filas RabbitMQ com `x-message-ttl` fixo (10s/30s/90s) + `x-dead-letter-*` apontando de
    volta para a fila principal (`ConnectorMessagingTopology.cs`) — os delays são configuráveis via
    `ConnectorRetryOptions` (novo, `src/Data/Connector/Infrastructure/ConnectorRetryOptions.cs`),
    produção usa os valores default, testes usam delays de centenas de ms. `SyncRequestedConsumerService`
    republica explicitamente na fila de retry correta a cada falha (incrementando o header
    `x-retry-attempt`), só indo para a DLQ de fato depois de esgotar as tentativas.
    Prova ao vivo contra RabbitMQ real (Testcontainers, nunca exercitado de ponta a ponta antes desta
    fase — `HostApiFixture` só usava uma connection string sintaticamente válida):
    `EIP.Data.Connector.IntegrationTests/ConnectorRetryAndDlqTests.cs`, 3 testes — falha transitória se
    recupera sem nunca chegar à DLQ; falha persistente esgota as tentativas e chega à DLQ; requeue de
    `SyncRunId` de outro tenant retorna `NotFound` (IDOR-safe).
    **Bug real encontrado e corrigido durante a validação ao vivo (E2.3):** a idempotência de
    `SyncRun` (`TryStartProcessing`) tratava `Failed` como terminal — herdado da Fase 0, onde uma
    falha de processamento sempre ia direto para a DLQ sem reentrega. Com o retry automático da E2.1,
    a 2ª entrega de uma mensagem (redelivery da fila de retry) chegava a um run já `Failed` e o método
    retornava sem fazer nada — o worker então confirmava (`Ack`) a entrega como se tivesse dado certo,
    e a mensagem nunca era reprocessada nem chegava à DLQ. Corrigido em
    `src/Data/Connector/Domain/SyncRun.cs`: `TryStartProcessing()` agora aceita `Pending` OU `Failed`
    (só bloqueia `Running`/`Succeeded`, que continuam protegidos contra duplicata); `Complete()` limpa
    `ErrorMessage` de uma tentativa anterior. Prova:
    `ConnectorSyncProcessorRetryIdempotencyTests.cs` (2 testes — redelivery de run `Failed` reprocessa
    de verdade; redelivery de run `Succeeded` continua ignorada). Validado ao vivo: retry ladder
    completa (`10s → 30s → 90s → DLQ`) reproduzida contra Host+Worker reais, logs mostrando as 3
    tentativas + DLQ, exatamente no timing esperado.
- [x] **E2.2** `GET /api/v1/connectors/dlq` — lista mensagens atualmente na fila DLQ (hoje só visível
      via RabbitMQ Management UI, fora da plataforma) com o `SyncRunId`/motivo da falha original.
      `POST /api/v1/connectors/dlq/{syncRunId}/requeue` — reprocessa manualmente uma mensagem da DLQ.
  - *Depende de:* E2.1.
  - *Aceite:* teste prova que uma mensagem colocada na DLQ aparece na listagem e pode ser
    reenfileirada com sucesso.
  - **Evidência:** `IConnectorDlqReader`/`RabbitMqConnectorDlqReader` (AMQP puro via `BasicGetAsync`
    não-destrutivo, sem depender da management API do RabbitMQ — mesma escolha de dependência do
    publisher). A DLQ é um recurso físico único compartilhado por todos os tenants (RabbitMQ não tem
    RLS) — o isolamento é garantido filtrando por `TenantId` dentro do próprio reader, nunca por uma
    permissão de "acesso global"; um `SyncRunId` de outro tenant é tratado como inexistente (mesmo
    padrão IDOR-safe de `GetSyncRun`). Endpoints em `ConnectorsController`
    (`ListDlq`/`RequeueDlq`, `connector.view`/`connector.manage`). Validado ao vivo: registrado um
    conector com URL inválida, disparada uma sincronização real, acompanhados os 3 retries + DLQ nos
    logs do Worker, listada a mensagem via `GET /api/v1/connectors/dlq` (com `failureReason: "rejected"`
    lido do header `x-death` nativo do RabbitMQ), reenfileirada via `POST .../requeue` (`204`), e
    confirmado que a fila de retry recomeçou do zero (nova tentativa 1) para a mensagem reenfileirada.
- [x] **E2.3** Runbook operacional do conector (`docs/guides/`): como diagnosticar uma sincronização
      travada, como interpretar a DLQ, como reprocessar, quais métricas observar.
  - *Depende de:* E2.1, E2.2.
  - *Aceite:* runbook existe e foi seguido manualmente ao vivo para diagnosticar um cenário de falha
    simulado, sem precisar de conhecimento fora do documento.
  - **Evidência:** `docs/guides/conector-operacoes.md` — fluxo da sincronização, diagnóstico de
    sincronização travada, interpretação/reprocessamento da DLQ, tabela de métricas/sinais a observar,
    e um cenário de falha simulado passo a passo. O próprio cenário simulado do documento foi seguido
    ao vivo para escrever esta evidência (não só para validar depois de pronto) — foi durante essa
    execução que o bug de idempotência da E2.1 foi encontrado, então o runbook já reflete o
    comportamento real e corrigido do sistema, não uma versão idealizada.

## E3 — Catálogo de Dados e Linhagem

Objetivo: formalizar como um endpoint o que até aqui só existia como prova manual em sessões de
validação (Fase 1, E8.2) — rastrear um dado do fato analítico até a origem bruta.

- [x] **E3.1** `GET /api/v1/data-catalog/lineage/{factSalesInvoiceItemId}` — retorna a cadeia completa
      de rastreabilidade de um item de fato: `RawObjectUri` (Data Lake), o registro canônico
      correspondente (`SalesInvoiceItem`), o `SyncRun`/`ConnectorInstance` de origem, e o
      `LoadBatch` que o carregou no Warehouse. Somente leitura, protegido por `dashboard.view` (a
      mesma permissão de consumo analítico — linhagem é parte de "confiar no dado", não uma
      permissão nova).
  - *Depende de:* nenhuma (todos os campos já existem desde as Fases 0/1, só nunca foram agregados
    num único endpoint).
  - *Aceite:* teste prova que a cadeia retornada bate com uma sincronização real de ponta a ponta
    (mesmo dado validado manualmente no fechamento da Fase 1, E8.2, agora via API).
  - **Evidência:** novo módulo `EIP.Data.Catalog.{Application,Api}` (sem persistência própria —
    `DataLineageService` só compõe interfaces de Application já existentes de outros módulos —
    `IWarehouseLoadStore`, `ICanonicalRecordStore`, `IConnectorSyncStore` — mesmo padrão de
    `WarehouseReconciliationService`, nunca acessando o `DbContext` de outro módulo diretamente,
    docs/02 §9.2). O `SyncRunId` é extraído diretamente do 7º segmento do `RawObjectUri`
    (`{tenantId}/{sourceSystemId}/{sourceEntity}/{yyyy}/{MM}/{dd}/{syncRunId}/{sequence}.json`,
    formato determinístico de `S3RawObjectStore.BuildKey`) — evita uma chamada ao Data Lake, que nem
    exporia isso (`IRawObjectStore` só devolve conteúdo, nunca metadados). O elo canônico é
    encontrado pela chave de negócio `(TenantId, SourceSystemId, SourceEntity, SourceRecordId)`,
    nunca por `SalesInvoiceItemId` (que fica obsoleto a cada reprocessamento da fatura, docs/04
    §4.1) — qualquer elo pode faltar (nulo) sem que a consulta inteira falhe. Novas leituras
    adicionadas às stores existentes: `IWarehouseLoadStore.FindFactSalesInvoiceItemAsync`/
    `FindLoadBatchAsync`, `ICanonicalRecordStore.FindSalesInvoiceItemByBusinessKeyAsync` — nenhuma
    tabela nova. Testado contra SQL Server real (Testcontainers):
    `EIP.Data.Catalog.IntegrationTests/DataCatalogServiceTests.cs` — cadeia completa
    (fato→lote→canônico→sync run→conector) construída por sincronização real via
    `IPipelineProcessor`/`IWarehouseLoadService`, e chave desconhecida devolvendo `NotFound`.
    **Validado ao vivo**, reproduzindo o mesmo rastreamento manual do fechamento da Fase 1 (E8.2, na
    época via `mc stat`): subido um servidor HTTP estático local servindo JSON de
    clientes/produtos/faturas, registrados 3 conectores reais (`customers`/`products`/
    `sales-invoices`) contra ele, disparadas as 3 sincronizações via Host+Worker reais — o
    `SyncRun` chegou a `Succeeded`, o Pipeline gravou o registro canônico, e a carga do Warehouse
    materializou o fato + `LoadBatch` automaticamente (mesmo fluxo de
    `ConnectorSyncProcessor.ProcessAsync`, sem intervenção manual). `GET
    /api/v1/data-catalog/lineage/{key}` devolveu a cadeia completa e correta: mesmo `RawObjectUri`
    no fato e no canônico, `SyncRunId` corretamente extraído da URI e batendo com o `SyncRun` real
    (`Status: Succeeded`), `ConnectorInstance` com nome/URL corretos.
- [x] **E3.2** `GET /api/v1/data-catalog/metrics` — dicionário de métricas mais completo que o
      `GET /api/v1/analytics/datasets` da Fase 2 (que só expõe nome/descrição): inclui a fórmula em
      texto (já documentada em `CertifiedMetrics`), a versão, o dono, e a última reconciliação
      conhecida (de `ICanonicalReconciliationService`/`IWarehouseReconciliationService`).
  - *Depende de:* nenhuma.
  - *Aceite:* teste prova que a resposta reflete os metadados reais de `CertifiedMetrics` sem
    hardcode duplicado.
  - **Evidência:** `MetricsCatalogService` combina os metadados estáticos de `CertifiedMetrics`
    (nome, fórmula em texto — o próprio campo `Description`, versão, dono — nenhum hardcode
    duplicado, lido direto da fonte) com uma reconciliação Canônico↔Fato calculada **ao vivo** a cada
    chamada via `IWarehouseReconciliationService.ReconcileSalesInvoiceItemsAsync` — decisão
    deliberada de não criar uma tabela de histórico de reconciliações nesta fase: "a última
    reconciliação conhecida" é sempre a mais fresca possível (computada na hora), nunca uma
    fotografia potencialmente desatualizada, e usa exatamente um dos dois serviços citados no
    backlog sem inventar persistência nova. Reconciliação sempre por
    `(TenantId, ConnectorInstanceId)` — nunca agregada às cegas entre origens diferentes — e só para
    instâncias cujo `SourceEntity` é `sales-invoices` (as 3 métricas certificadas desta fase são
    todas sobre `FactSalesInvoiceItem`; incluir outra origem daria um falso positivo "0 == 0"). Nova
    leitura em `IConnectorSyncStore.ListInstancesAsync(tenantId)`. Testado contra SQL Server real: 3
    métricas certificadas retornadas com reconciliação correta para uma origem com dado carregado
    (`IsWithinTolerance: true`, contagens/totais batendo) e reconciliação vazia para um tenant sem
    nenhum conector de `sales-invoices`. **Validado ao vivo** (mesma sessão da E3.1): `GET
    /api/v1/data-catalog/metrics` devolveu as 3 métricas certificadas com a reconciliação real da
    sincronização recém-executada — `canonicalNetAmountTotal`/`factNetAmountTotal` ambos `300.00`
    (3 unidades × R$100), `isWithinTolerance: true`.

## E4 — Quotas e Medição de Uso por Tenant

Objetivo: fechar "consultas respeitam...quotas" (já parcial, guardrails por consulta na Fase 2) e
"custos...mensurados por tenant" — como uso, não como custo em dinheiro (§3).

- [x] **E4.1** Quota de sincronização por tenant: no máximo N sincronizações por `ConnectorInstance`
      por período (ex. 1 por minuto) — protege contra um tenant disparando sync em loop. Retorna
      `429` com `Retry-After` quando excedida, nunca enfileira silenciosamente além do limite.
  - *Depende de:* nenhuma.
  - *Aceite:* teste prova que disparar sync repetidamente acima do limite retorna `429` a partir da
    N+1ª tentativa dentro da janela.
  - **Evidência (2026-10-17):** reserva atômica no Redis compartilhado, chave por tenant/instância,
    janela iniciada na primeira reserva, configurável no Host (`Connector:SyncQuota`, padrão
    1 solicitação/60s). `ConnectorSyncService` valida visibilidade/estado antes da reserva e só
    então cria o run/publica. Excesso retorna `429 ProblemDetails` e `Retry-After`, sem persistir
    nem publicar. `ConnectorSyncQuotaTests`: oito pedidos concorrentes com limite dois resultam
    em dois `202`, seis `429`, exatamente dois runs e duas publicações; outra instância segue
    disponível. Redis real prova compartilhamento entre clientes, isolamento por tenant e
    expiração. Suíte completa `EIP.Host.IntegrationTests`: **37/37 passando**, SQL Server/Redis
    efêmeros via Testcontainers. Runbook atualizado com configuração e semântica de falhas.
    Durante a validação, corrigido também `AuthService.RefreshAsync`: token anterior à criação
    da organização passa a reavaliar memberships, permitindo selecionar o tenant recém-criado
    e carregar suas permissões; o teste percorre registro → provisionamento → refresh → sync.
- [x] **E4.2** `GET /api/v1/admin/tenants/{tenantId}/usage` — contagem de sincronizações, consultas
      analíticas (`analytics.query`) e bytes armazenados no Data Lake, por período. Protegido por uma
      nova permissão de plataforma (não de tenant) — só quem administra a plataforma como um todo
      consulta uso de qualquer tenant, nunca um membro comum do próprio tenant.
  - *Depende de:* nenhuma.
  - *Aceite:* teste prova que os números batem com atividade real gerada no próprio teste (N syncs,
    M consultas → contadores refletem exatamente N e M).
  - **Evidência (2026-10-18):** endpoint com intervalo UTC `[from,to)` obrigatório (máximo 366
    dias). Runs contados na persistência existente; consultas bem-sucedidas, incluindo cache e
    widgets, registradas duravelmente em `administration.AnalyticsQueries`, com RLS na própria
    migration inicial; armazenamento lido por prefixo/paginação S3, somando objetos presentes com
    `LastModified` no período. Sem backfill de consultas antigas nem snapshots de objetos removidos.
    Nova permissão global `platform.tenant-usage.read`, verificada a cada request nas claims
    persistidas do Identity: Owner/Admin de tenant não herdam essa permissão; revogação imediata
    mesmo com token anterior. Leitura roda sob o tenant alvo, sem bypass RLS, e restaura o contexto.
    `TenantUsageTests` gera 2 syncs/3 queries/2 objetos de 3+5 bytes e confirma exatamente 2/3/8,
    além de período vazio, outro tenant, 401/403, revogação, tenant inválido/inexistente, RLS de
    leitura e INSERT. `RawStorageUsageReaderTests` cobre paginação e bordas do intervalo.
    **40/40 testes da API passando**, SQL Server/Redis reais e emulador S3Mock 5.1.0 em containers
    temporários (MinIO específico ainda não homologado: download da imagem indisponível nesta
    retomada; o emulador é só de teste e não substitui o Compose). Gate `RlsCoverageTests` passa
    sem exceções novas. Ver `docs/guides/medicao-de-uso.md` para semântica e operação.

## E5 — Confiabilidade Operacional

Objetivo: fechar "recuperação de backup e rollback de release foram testados" e estabelecer SLOs
iniciais medidos (não alertados, §3).

- [x] **E5.1** Backup e restauração do SQL Server testados de verdade: `runbook` (`docs/guides/`)
      documentando o processo, exercido ao vivo contra a stack local (backup → derrubar o container
      → restaurar → confirmar dado e RLS intactos).
  - *Depende de:* nenhuma.
  - *Aceite:* restauração executada ao vivo, dado íntegro confirmado depois (não só "o comando
    rodou sem erro").
  - **Evidência (2026-10-18):** `scripts/backup/Test-Recovery.ps1` exercitado sobre um backup
    `COPY_ONLY` + `CHECKSUM` do EIP local. Restaurado em container descartável, removido esse
    container com seu armazenamento e restaurado o backup externo em uma nova instância.
    `RESTORE VERIFYONLY`, `DBCC CHECKDB`, contagens e SHA-256 do conteúdo de **39 tabelas / 41 linhas**
    coincidiram nas duas cópias; **7 políticas RLS** preservadas. Gate estrutural e prova funcional
    (leitura sem contexto, isolamento A/B e bloqueio de INSERT cruzado) passaram antes/depois da
    conferência. Ensaio completo em 25,55s no ambiente local; não é RTO de produção. Containers
    temporários removidos; origem continua saudável, frontend HTTP 200 e API `Healthy`.
    Runbook: `docs/guides/backup-restauracao.md`. Resumo sem payloads versionado em
    `docs/operations/recuperacao-sql-2026-10-18.json`; `.bak` e manifestos mantidos fora do Git,
    na pasta local de evidências exibida pelo script. A perda foi simulada somente na cópia,
    preservando a instância usada pelo frontend. Data Lake/fila/cache não fazem parte deste ensaio.
- [x] **E5.2** Runbook de rollback de release: como reverter uma migration aplicada indevidamente e
      como reverter para uma imagem Docker anterior, exercido ao vivo contra a stack local.
  - *Depende de:* nenhuma.
  - *Aceite:* uma migration é aplicada e revertida (`dotnet ef database update <migration anterior>`)
    ao vivo, dado pré-existente confirmado intacto.
  - **Evidência (2026-10-18):** `scripts/rollback/Test-Rollback.ps1` executado em SQL Server
    descartável: baseline `AddRoleToMembership`, aplicação de `AddCompanyCountryCodeAndTradeName`
    e reversão via `dotnet ef database update` para a baseline. Conteúdo e políticas de 4 tabelas /
    8 linhas anteriores ao upgrade coincidiram por hash; RLS de leitura/INSERT e DBCC passaram.
    Duas versões reais do Host publicadas (commit anterior `3c2de50` e código atual), empacotadas
    em imagens locais distintas e executadas na sequência anterior → candidata → anterior.
    ID de imagem e hash OpenAPI anteriores recuperados; liveness HTTP 200 nas três instâncias.
    **Limite explícito:** ensaios de schema e imagem são independentes; readiness/fluxos completos
    não foram exercitados nos containers de imagem. Isso não homologa uma combinação release/schema
    para produção. Fonte de dados e frontend locais preservados. Runbook:
    `docs/guides/rollback-release.md`; resumo: `docs/operations/rollback-2026-10-18.json`.
- [x] **E5.3** SLOs iniciais definidos e documentados (`docs/guides/`) para os endpoints críticos
      (login, consulta analítica, health checks): latência alvo (p95/p99) e disponibilidade alvo.
      Medidos via as métricas OpenTelemetry/Prometheus já expostas desde a Fase 0 (E3) — sem alerta
      automatizado (§3).
  - *Depende de:* nenhuma.
  - *Aceite:* SLOs documentados, e uma consulta real contra `/metrics` mostra os números atuais
    comparáveis ao alvo definido (mesmo que ainda não formalmente monitorados 24/7).
  - **Evidência (2026-10-18):** metas em `scripts/performance/slo-targets.json` e runbook em
    `docs/guides/slos-iniciais.md`: login p95/p99 500/1.000ms, analytics 1.000/2.000ms, liveness
    50/100ms e readiness 250/500ms; disponibilidade alvo 99,9% em janela móvel de 30 dias.
    `Measure-Slo.ps1` executado com tenant sintético exclusivo, 5 aquecimentos e 100 amostras
    sequenciais por endpoint. **400/400 HTTP 200** na amostra; p95/p99 do cliente: login
    85,866/105,202ms, analytics 30,408/34,282ms, liveness 3,055/3,980ms e readiness
    66,609/69,127ms. Consulta real ao exporter antes/depois de cada bloco confirmou deltas de
    100 respostas por rota no histograma `http_server_request_duration_seconds`; limites
    superiores dos buckets p95/p99 também ficaram dentro dos alvos. Resumo versionado em
    `docs/operations/slo-2026-10-18.json`, snapshots `.prom` e amostras individuais fora do Git.
    **Limites:** execução de baixa carga, dataset vazio e consulta pré-aquecida; não substitui
    E6.3 nem comprova disponibilidade mensal (`rolling30DaySloEvaluated=false`). Sem servidor de
    histórico Prometheus/alertas implantado nesta atividade; PromQL documentado para coleta futura.

## E6 — Hardening de Segurança, LGPD e Isolamento sob Carga

Objetivo: fechar "piloto atende múltiplos tenants sem incidente de isolamento" (reinterpretado como
teste de carga simulado, §3) e o hardening de segurança geral da fase.

- [x] **E6.1** Revisão de segurança formal de toda a superfície da API (autenticação, autorização,
      RLS, exposição de erro, headers, CORS) — usar o skill `security-review` como ponto de partida
      e complementar manualmente onde ele cobre só o diff, não o sistema inteiro. Qualquer achado
      vira uma correção real, não uma exceção documentada (mesma disciplina de RLS já estabelecida).
  - *Depende de:* nenhuma.
  - *Aceite:* revisão concluída, achados (se houver) corrigidos e revalidados.
  - **Evidência (2026-10-18):** revisão manual de todos os controllers/composição, documentada em
    `docs/security/revisao-api-2026-10-18.md` (skill `security-review` indisponível no catálogo/disco).
    Corrigidos replay concorrente de refresh, emissão para usuário bloqueado, logout sem revogação,
    SSRF/destinos sem autorização/redirects/cookies/resposta ilimitada do conector, vínculo de
    empresa no cadastro, configuração padrão insegura fora de Development, erros públicos com
    detalhe interno, validação/cache de autenticação, claims obrigatórias/tenant reservado e
    segmento de chave do Data Lake. PKCS fixado em 10.0.13; auditoria NuGet de 51 projetos sem
    pacote vulnerável conhecido. MFA exigido no endpoint global fora de Development; como o login
    local não emite prova MFA, esse acesso permanece fechado nesse ambiente até integração confiável.
    **73/73 testes da API**, **9/9 do conector/worker** e **16/16 do frontend** passando; dois testes
    de erro/idempotência reexecutados com asserção de sanitização reforçada. Build Release da
    solução e build de produção do frontend passaram. No ambiente local atualizado: login 200,
    logout 204, replay de refresh 401, destino proibido 400, CORS permite o frontend e nega origem
    estrangeira, métricas não expostas pelo Gateway (404), headers de proteção presentes.
    Resumo: `docs/operations/seguranca-api-2026-10-18.json`. Não é certificação de produção:
    MFA/federação, infraestrutura/TLS/segredos reais, runtime/imagens e frontend exigem homologação
    no ambiente final; LGPD e carga continuam em E6.2/E6.3.
- [x] **E6.2** Revisão LGPD: documento (`docs/07-Seguranca.md` ou novo `docs/16-LGPD.md`) listando
      quais dados pessoais a plataforma coleta (e-mail, nome, dados de cliente/fornecedor ingeridos
      via conector), onde ficam armazenados, por quanto tempo, e como um titular exerceria direito de
      acesso/exclusão hoje (mesmo que o processo ainda seja manual nesta fase).
  - *Depende de:* nenhuma.
  - *Aceite:* documento existe e foi revisado contra o schema real do banco (nenhum dado pessoal
    óbvio ficou de fora do inventário).
  - *Evidência (18/10/2026):* [inventário e roteiro manual](../16-LGPD.md) confrontados com
    [metadados das 39 tabelas do banco local](../operations/lgpd-schema-2026-10-18.json), sem
    exportar valores pessoais. Inclui Identity, clientes, documentos comerciais, SCD2, campos
    livres, raw, cache, filas, navegador, logs e backups. Retenção sem prazo/expurgo e ausência
    de exclusão integral automatizada estão explicitadas como lacunas; nenhuma exclusão foi
    executada e não há declaração de conformidade integral.
- [x] **E6.3** Teste de carga/isolamento: múltiplos tenants concorrentes (k6 ou equivalente) batendo
      no Gateway simultaneamente (auth, consulta analítica, sync) — prova que a RLS e os guardrails
      seguram sob concorrência real, sem um tenant nunca ver dado de outro mesmo sob carga.
  - *Depende de:* nenhuma.
  - *Aceite:* execução real do teste de carga contra a stack local, relatório de resultados
    (throughput, latência, zero incidente de isolamento) documentado.
  - *Evidência (19/10/2026):* [relatório](../guides/isolamento-sob-carga.md) e
    [resultado estruturado](../operations/isolamento-carga-2026-10-19.json): quatro tenants,
    oito fluxos, 280 chamadas em 44,920 s (6,233 req/s com pausas), zero divergência;
    valores analíticos distintos, acessos cruzados negados, quotas e 40 trocas de contexto
    RLS sem filtro. Rajada adicional de 120 chamadas produziu 76 bloqueios por IP.
    Sync validado até aceite/publicação, sem atestar conclusão do Worker/raw indisponível.

## E7 — Fechamento da Fase

- [x] **E7.1** Revisão formal da seção 4 (Definition of Done) desta fase, com evidência por
      critério — mesmo processo aplicado às Fases 0, 1 e 2. Atualizar a tabela de rastreamento (§7)
      e a memória do projeto.
  - *Depende de:* todos os épicos anteriores.
  - *Evidência (19/10/2026):* [revisão formal](fechamento-fase-3-2026-10-19.md), seis critérios
    reconciliados com evidência e N/A preservados. Coleta operacional por tenant acrescentada
    e exercitada; 7 testes selecionados de RLS/acesso cruzado/uso passaram sem ignorados.
    [Memória versionada](memoria-projeto.md) atualizada. MinIO/Worker completo, requisitos
    de piloto e homologação final continuam explicitamente nas próximas atividades.

---

# 6. Fora do Escopo da Fase 3

Para não perder o foco (`docs/15-Roadmap.md §3`), os itens abaixo são explicitamente adiados:

- `Workspace` — mantido fora do escopo pela terceira vez (decisão já registrada nas Fases 0, 1 e 2).
- Conectores novos (SAP, Protheus, CSV, etc.) — sem demanda comercial validada ainda; a fase
  robustece o único conector de referência existente em vez de adicionar origem nova (§3).
- Onboarding como UX de ativação de produto (tour guiado, e-mails de boas-vindas, etc.) — só o
  provisionamento técnico de tenant (E1) está no escopo; polimento de ativação pressupõe usuário real
  observando a experiência.
- Planos/billing — não existe conceito de "plano" comercial na plataforma ainda; sem isso, "quotas
  por plano" (docs/15 §7) vira só "quotas por tenant" (E4).
- Modo shared/dedicated por tenant elegível — pressupõe tenants pagantes com necessidades
  diferenciadas de isolamento de infraestrutura, que não existem neste projeto solo.
- Processo de suporte e administração de tenant como processo de negócio — só o lado técnico
  (endpoint de uso por tenant, E4.2) está no escopo; processo humano de atendimento fica para quando
  houver equipe de suporte real.
- Grafana/Alertmanager (alerta automatizado) — SLOs desta fase são definidos e medidos, não
  alertados 24/7 (§3); Prometheus scrape já existe desde a Fase 0.
- Quota de armazenamento no Data Lake e quota de exportação — sem dado de uso real para calibrar um
  limite sensato (§3); só quota de sincronização (E4.1) tem um caso de abuso óbvio o suficiente para
  justificar um número arbitrário razoável.
- Métricas de adoção/satisfação de usuário — pressupõem usuários reais que este projeto ainda não
  tem (documentado como N/A no §4, mesmo padrão de "cache"/"exportação" nas fases anteriores).

---

# 7. Rastreamento

| Épico | Status |
|---|---|
| E1 — Provisionamento próprio de tenant | Concluído (E1.1–E1.3) |
| E2 — Conector de referência: robustecimento operacional | Concluído (E2.1–E2.3) |
| E3 — Catálogo de dados e linhagem | Concluído (E3.1–E3.2) |
| E4 — Quotas e medição de uso por tenant | Concluído (E4.1–E4.2) |
| E5 — Confiabilidade operacional | Concluído (E5.1–E5.3) |
| E6 — Hardening de segurança, LGPD e isolamento sob carga | Concluído (E6.1–E6.3; evidências e limites documentados) |
| E7 — Fechamento da fase | Concluído (E7.1, 19/10/2026; escopo reduzido e limites preservados) |

Atualizar esta tabela conforme os épicos avançam.
