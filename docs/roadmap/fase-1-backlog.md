# Backlog de Execução — Fase 1 (Primeiro Fluxo de Dados Confiável)

**Projeto:** Enterprise Intelligence Platform (EIP)
**Versão:** 1.0
**Status:** Oficial
**Última atualização:** Agosto/2026 — Fase 1 concluída e formalmente fechada em 2026-08-04

---

# 1. Objetivo

Este documento traduz `docs/15-Roadmap.md §5` (Fase 1) em tarefas concretas, ordenadas e
verificáveis — mesmo papel que `docs/roadmap/fase-0-backlog.md` cumpriu para a Fase 0 (concluída e
formalmente fechada em 2026-08-01).

Fase 1 comprova a cadeia completa **origem → dado bruto → Modelo Canônico → Data Warehouse →
consumo analítico mínimo**, com uma única origem (o conector de referência REST genérico do E7) e
**uma única fatia vertical de negócio** — não o Modelo Canônico inteiro. Isso segue literalmente
`docs/15-Roadmap.md §5`: *"comprovar a cadeia completa de ingestão até consumo analítico com uma
única origem e poucos domínios prioritários"* — e o princípio de priorização §3: *"entregar
verticalmente: origem → dado → métrica → dashboard → usuário"*.

Este documento **não substitui** nenhuma regra definida em `docs/00` a `docs/15` ou nas ADRs. Em
caso de conflito, os documentos de arquitetura/segurança/ADR prevalecem — em especial
`docs/04-Modelo-Canonico.md` e `docs/09-Data-Warehouse.md`, as duas fontes normativas mais usadas
aqui.

---

# 2. Como usar este backlog

- Cada tarefa tem um ID (`E<épico>.<sequência>`), descrição, arquivos/projetos afetados,
  dependências e critério de aceite. Numeração reinicia em `E1` neste arquivo (independente da
  Fase 0) — cada backlog de fase é autocontido.
- Marcar `- [x]` somente quando o critério de aceite estiver satisfeito e validado (build/teste
  passando contra infraestrutura real), nunca por "está quase pronto" — mesma disciplina da Fase 0.
- Épicos são majoritariamente sequenciais (E1 → E2 → ... → E8).
- Toda tarefa que cria uma tabela com `TenantId` é bloqueada até a política RLS correspondente
  existir e ter teste de acesso cruzado — sem exceção (ADR-007). Isso agora também vale para as
  tabelas do Data Warehouse (`docs/09-Data-Warehouse.md §4.1`: modo Shared exige RLS obrigatória,
  sem exceção).
- A Fase 1 só é considerada concluída quando a seção 4 (Critérios de Saída) estiver 100%
  satisfeita — mesma regra da Fase 0.

---

# 3. Decisões técnicas fixadas para a Fase 1

| Decisão | Valor | Origem |
|---|---|---|
| Fatia vertical priorizada | **Comercial/Faturamento** (`SalesInvoice`/`SalesInvoiceItem`), com `Customer` e `Product`/`ProductCategory` como cadastros de apoio | Decisão do usuário em 2026-08 — `docs/09-Data-Warehouse.md §9` já tem métricas certificadas de exemplo prontas para este domínio (Receita Líquida, Quantidade Faturada, Ticket Médio), o que reduz ambiguidade ao definir o Data Mart/semântica mínima |
| `SalesOrder`/`SalesOrderItem` fora desta fase | O CDM define pedido comercial como entidade distinta da fatura (`docs/04 §5.3`), mas o roadmap pede "poucos domínios" — carteira/pedido fica para quando houver demanda validada de um dashboard que precise dela | Escopo desta fase |
| `Currency`/`ExchangeRate`/`PaymentTerm` não viram entidades próprias ainda | CDM os lista como "Referência" (`docs/04 §3`), mas nesta fatia o código de moeda (`CurrencyCode`, ISO 4217) é armazenado como string nas entidades que precisam — sem tabela de câmbio nem condições de pagamento | Escopo desta fase |
| **Correção estrutural: `Connector` muda de `src/Platform/` para `src/Data/`** | `docs/00-Arquitetura-do-Repositorio.md` reserva `src/Data/{Connector,Pipeline,Canonical,DataLake,Warehouse,Semantic,Catalog}` como a Data Platform — o módulo Connector criado no E7 foi colocado em `src/Platform/Connector` por engano (nunca comparado contra docs/00 naquele momento). Como esta fase cria `Pipeline`/`Canonical`/`DataLake`/`Warehouse` — que **têm** que ficar em `Data/` — corrigir `Connector` agora evita uma inconsistência estrutural permanente. Namespace muda de `EIP.Platform.Connector.*` para `EIP.Data.Connector.*` | Achado desta revisão, corrigido em E1.1 |
| `Catalog` (`docs/00`) fica fora desta fase | É o componente de metadados/qualidade/proprietário de datasets (`docs/04 §10`, `docs/05 §4.1` "Connector Registry") — prematuro com um único conector hardcoded e sem Connector Registry completo (esse é explicitamente escopo de Fase 1 "completo" no `docs/15 §5`, mas o MVP desta fatia não precisa de um registry genérico ainda) | Escopo desta fase |
| Mapeamento origem→canônico é fixo no código, não configurável | Não existe Connector Registry completo (Draft/Configuring/Validating, múltiplos Connector Types) — só o conector de referência REST genérico do E7. Um mapeamento configurável por usuário é prematuro com um único tipo de conector | Escopo desta fase, consistente com E7 |
| Data Lake usa MinIO (já disponível desde E1 da Fase 0, nunca usado até agora) | `docs/03-Stack-Tecnologica.md §6.2`, `docs/14-DevOps.md §4` — S3-compatible, já sobe via `docker-compose` | Fase 0, E1.1 |
| RLS obrigatória também no Data Warehouse | `docs/09-Data-Warehouse.md §4.1`: modo Shared exige RLS "sem exceção" — mesmo mecanismo `SESSION_CONTEXT`/`SECURITY POLICY` já usado em `tenant`/`connector`/`identity` (ADR-007) | `docs/09 §4.1`, ADR-007 |
| Isolamento de tenant no Data Lake não é RLS (Object Storage não é SQL) | A garantia de isolamento é aplicada em código (prefixo de chave obrigatório por tenant, nunca aceito de input não validado) + teste automatizado dedicado — mesmo princípio do ADR-007, mecanismo diferente porque o MinIO não tem `SECURITY POLICY` | `docs/07-Seguranca.md §6`, `docs/09 §11` |
| SCD Type 2 limitado a `DimCustomer`/`DimProduct` nesta fase | `docs/09-Data-Warehouse.md §6.1` pede SCD 2 quando o atributo afeta leitura histórica — nesta fatia os candidatos são segmento/categoria de cliente e categoria de produto; `DimCompany`/`DimDate`/`DimCurrency` não precisam de SCD 2 ainda | `docs/09 §6.1` |
| Camada semântica mínima: 3 métricas certificadas, sem motor de métricas genérico | Analytics Engine (consultas declarativas, cache, motor de métricas reutilizável) é entrega de Fase 2 (`docs/15 §6`) — aqui basta um endpoint versionado consultando `FactSalesInvoiceItem` com a definição de cada métrica documentada e testada, não um mecanismo configurável | `docs/15 §5` vs `§6`, escopo desta fase |
| `Workspace` continua fora do escopo | Já era decisão da Fase 0 (`fase-0-backlog.md §6`) e `docs/15 §5` não exige Workspace para a Fase 1 — Data Mart/Semântica desta fase pertencem diretamente ao Tenant, sem camada de Workspace | Fase 0 §6, mantido |
| `Company` precisa de `CountryCode` (novo campo obrigatório do CDM) | `docs/04-Modelo-Canonico.md §5.1` exige `CountryCode` em `Company`; a entidade `Tenant.Domain.Company` de hoje não tem esse campo — precisa de uma migration de extensão antes do primeiro registro canônico referenciar uma empresa | Achado desta revisão, E2.1 |
| `EIP.Data.Canonical` só com `Domain`/`Infrastructure` no E2 | `Application`/`Api` ficam para quando o Pipeline (E3) ou os endpoints de quarentena (E4.2) precisarem de fato de uma abstração/rota — nenhum consumidor existe ainda dentro do próprio E2, então criar esses projetos agora seria especulativo (YAGNI) | E2.2 |
| Sem FK/navegação EF Core entre entidades canônicas do mesmo schema | `Customer`/`Product` referenciados por `SalesInvoice`/`SalesInvoiceItem` via `Guid` simples, sem `HasOne/WithMany` — evita qualquer interação entre constraints de FK e block predicates de RLS (não testado/documentado pela Microsoft para este cenário específico), e seque o mesmo padrão já usado em `Membership.TenantId`/`Company.TenantId` no módulo Tenant | E2.2 |
| `DatabaseMigrator.MigrateAllAsync` (Fase 0, E5.1) agora também migra `CanonicalDbContext` | Sem isso, o gate `RlsCoverageTests` (criado no fechamento da Fase 0) não veria as tabelas de `canonical.*` no banco efêmero de teste — a cobertura de RLS ficaria correta em produção/dev mas não seria verificada automaticamente no CI. Qualquer módulo novo com tabelas precisa entrar nesta lista | E2.2, mantém o gate da Fase 0 válido |

---

# 4. Critérios de Saída da Fase 1 (Definition of Done)

Copiados literalmente de `docs/15-Roadmap.md §5`. A Fase 1 só termina quando todos estiverem `[x]`,
com evidência real (não "os épicos estão codificados") — mesma disciplina aplicada na revisão de
fechamento da Fase 0:

- [x] Uma sincronização é executada de ponta a ponta, reprocessável e auditada.
  - Ponta a ponta: E3.3 (Extração → Data Lake → Validação/Mapeamento → Canônico), E5.3 (→ Warehouse),
    revalidado ao vivo no E8.2. Reprocessável: E7.1 (watermark incremental) + E7.2 (`reprocessFrom`
    por período, nunca move o watermark automático) + E4.2 (reprocessamento de quarentena). Auditada:
    `SyncRun` com contagens completas (`recordsProcessed`/`acceptedCount`/`updatedCount`/
    `rejectedCount`/`deletedCount`, E4.1), `LoadBatch` para cada carga do Warehouse (E5.3),
    idempotência provada (E3.4, E7 — reprocessar não duplica nem faz nada além do necessário).
- [x] Dado bruto, registro canônico e fato analítico podem ser rastreados entre si.
  - Teste automatizado: `WarehouseLoadServiceTests.LoadSalesInvoiceItemsAsync_TraceableAndIdempotent_...`
    (E5.3). Validado ao vivo no E8.2: um registro específico (`NF-0003-2`) rastreado
    `RawObjectUri` (objeto real no MinIO, `mc stat` com metadados de linhagem completos) →
    `canonical.SalesInvoiceItems` (mesmo `RawObjectUri`) → `warehouse.FactSalesInvoiceItem` (mesmo
    `SalesInvoiceItemId`/`RawObjectUri`) — valores (`NetAmount=900`) idênticos nas três camadas.
- [x] Falhas de qualidade ficam em quarentena, sem corromper o DW.
  - `CanonicalQuarantineEntry` (E2.4), `PipelineProcessor` roteia cada registro para canônico OU
    quarentena, nunca os dois (E3.3, testado em `EIP.Data.Pipeline.IntegrationTests`), endpoints de
    consulta/reprocessamento (E4.2, validado ao vivo com um mapeamento inválido de propósito — 5/5
    registros em quarentena, zero no canônico).
- [x] Totais/contagens de dados críticos são reconciliados com a origem dentro do limite definido.
  - `CanonicalReconciliationService` (Canônico↔Origem, E4.3) e `WarehouseReconciliationService`
    (Canônico↔Fato, E5.4), tolerância de 1% configurável por fração, ambos testados (Testcontainers,
    caso dentro da tolerância e caso de divergência detectável) e confirmados ao vivo no E8.2
    (contagem/soma exatas nos dois lados, sem nenhum aviso de divergência).
- [x] Tenant, empresa, cache, fila e Object Storage preservam isolamento em testes.
  - Tenant/empresa: `TenantIsolationTests` (Fase 0) + `CanonicalCrossTenantIsolationTests`/
    `WarehouseCrossTenantIsolationTests` (E8.1, novos) + `ConnectorCrossTenantIsolationTests`/
    `MetricsCrossTenantIsolationTests` (Fase 0/E6) — todos via RLS real (Testcontainers), nunca só a
    nível de aplicação. Fila: `ConnectorSyncProcessorQueueTenantIsolationTests` (novo, E8.3 — gap
    real encontrado e corrigido durante esta revisão, ver evidência do E8.3). Object Storage:
    `EIP.Data.DataLake.Infrastructure.IntegrationTests` (E1.3, MinIO real via Testcontainers, prefixo
    de tenant obrigatório, prova negativa de acesso cross-tenant). Cache: **não aplicável nesta
    fase** — `Redis` está de pé (`docker-compose`) e com health check (`/health/ready`), mas nenhum
    caminho de código desta fase lê/grava dado tenant-scoped nele ainda (Analytics Engine com cache é
    Fase 2, `docs/15 §6`); não há o que isolar. Documentado aqui em vez de marcar `[x]` sem
    evidência — revisitar quando a Fase 2 introduzir uso real de cache.

Critério adicional obrigatório por conta da ADR-007 (mesmo texto usado para fechar a Fase 0):

- [x] Toda tabela com `TenantId` (agora incluindo `canonical.*` e `warehouse.*`) possui política RLS
      ativa, e o gate automatizado (`RlsCoverageTests`, criado na revisão de fechamento da Fase 0)
      continua passando sem exceções adicionadas.
  - `RlsCoverageTests` varre o catálogo do sistema (não depende de conhecer cada módulo) e continua
    verde com `canonical.*`/`warehouse.*` incluídos automaticamente desde que `DatabaseMigrator.
    MigrateAllAsync` passou a migrar `CanonicalDbContext`/`WarehouseDbContext` (E2.2/E5.1) — sem
    exceções adicionadas nesta fase.

---

# 5. Épicos e Tarefas

## E1 — Correção Estrutural + Fundações de Dados

Objetivo: alinhar a estrutura de pastas com `docs/00` antes de acrescentar mais módulos em `src/Data/`,
e ter uma forma segura de gravar/ler dado bruto (Data Lake).

- [x] **E1.1** Mover `EIP.Platform.Connector.*` → `EIP.Data.Connector.*` (`src/Platform/Connector` →
      `src/Data/Connector`, mesma estrutura Domain/Application/Infrastructure/Api). Atualizar
      namespaces, `EIP.slnx`, referências em `Host`, `Worker`, `EIP.Testing.Infrastructure` e nos
      testes de integração. Nenhuma mudança de comportamento — só localização/namespace.
  - *Aceite:* `dotnet build`/`dotnet test` continuam 100% verdes; `git mv` preserva histórico dos
    arquivos quando possível. ✅ Concluído — `git mv` usado para o diretório e para os 4 `.csproj`
    renomeados (`EIP.Platform.Connector.*.csproj` → `EIP.Data.Connector.*.csproj`); 40 referências
    de namespace/caminho corrigidas em `EIP.slnx`, `Host`, `Worker`, `EIP.Testing.Infrastructure` e
    3 projetos de teste. **Gotcha real encontrado**: `git mv` do diretório falhou com "Permission
    denied" — causado por `bin`/`obj` da build anterior com handles abertos; resolvido apagando
    `bin`/`obj` antes de mover (nada a ver com o próprio `git mv`). **Segundo gotcha**: como
    `src/Data` já existia (criado por `mkdir -p` antes do `git mv`), o comando moveu o conteúdo para
    dentro de `src/Data/Connector/Connector/...` (aninhado) em vez de `src/Data/Connector/...` —
    corrigido movendo os 4 subdiretórios um nível acima. Validado com a suíte completa (13/13),
    `dotnet format --verify-no-changes` limpo, rebuild real da imagem Docker do Host, e um teste de
    fumaça ponta a ponta via API (registrar conector → sincronizar → `Succeeded` com
    `recordsProcessed: 5`) confirmando que a mudança de namespace não quebrou nada em runtime.
- [x] **E1.2** Novo módulo `EIP.Data.DataLake` (Infrastructure): abstração `IRawObjectStore`
      (Application-side interface, sem depender de S3/MinIO) + implementação via cliente
      S3-compatible apontando para o MinIO do E1 (Fase 0). Convenção de chave obrigatória:
      `{tenantId}/{sourceSystemId}/{sourceEntity}/{yyyy}/{MM}/{dd}/{syncRunId}/{sequencial}.json`
      — nunca aceitar um prefixo vindo de input de cliente. Cada objeto grava metadados de
      linhagem (`TenantId`, `ConnectorInstanceId`, `SyncRunId`, `SourceEntity`, `IngestedAt`) e um
      checksum SHA-256.
  - *Depende de:* nenhuma (usa o MinIO já disponível desde a Fase 0).
  - *Aceite:* gravar e ler um objeto de verdade contra o MinIO real do `docker-compose`; checksum
    verificado na leitura. ✅ Concluído. Dois projetos, mesmo padrão `BuildingBlocks`/
    `BuildingBlocks.Web` (abstração livre de dependências + implementação separada):
    `EIP.Data.DataLake` (`IRawObjectStore`, `RawObjectMetadata`, `StoredRawObject`, zero pacotes) e
    `EIP.Data.DataLake.Infrastructure` (`S3RawObjectStore` via `AWSSDK.S3` 4.0.101.6, apontando para
    qualquer endpoint S3-compatible com `ForcePathStyle = true`, obrigatório para MinIO). A chave é
    **sempre** construída dentro de `PutAsync` a partir do `TenantId` de `RawObjectMetadata` — nunca
    aceita do chamador. "Sequencial" simplificado como um GUID curto (Object Storage não tem
    sequência transacional nativa; documentado no código). **Mesmo gotcha de aninhamento do
    BuildingBlocks/BuildingBlocks.Web reapareceu aqui**: coloquei `Infrastructure` como subpasta de
    `DataLake` por engano — MSBuild incluiu os `.cs` dos dois projetos no mesmo assembly (erro
    `CS0579` de atributo duplicado); corrigido movendo `Infrastructure` para `src/Data/DataLake.Infrastructure`
    (irmã, não aninhada) — a lição já registrada na memória do projeto desde a Fase 0 se confirmou
    na prática de novo.
- [x] **E1.3** Teste automatizado de isolamento de tenant no Data Lake: tenant A não lista nem lê
      objetos do prefixo de tenant B via `IRawObjectStore`, mesmo adulterando o `TenantId` num
      parâmetro de chamada da API interna (não existe endpoint HTTP direto para o Data Lake — o
      teste chama a abstração diretamente, análogo ao nível "banco" do E2.3 da Fase 0).
  - *Depende de:* E1.2.
  - *Aceite:* teste roda contra MinIO real (ou Testcontainers com uma imagem S3-compatible),
    prova negativa explícita, não apenas ausência de erro. ✅ Concluído — novo
    `tests/Integration/EIP.Data.DataLake.Infrastructure.IntegrationTests`, seguindo o mesmo padrão
    Testcontainers do `SqlServerContainerFixture` (E5.1 da Fase 0): novo `MinioContainerFixture`
    (`EIP.Testing.Infrastructure`, pacote `Testcontainers.Minio` 4.13.0, mesma imagem do MinIO da
    Fase 0) sobe um MinIO efêmero real por execução. 3 testes: round-trip put→get com checksum
    SHA-256 batendo; `ListKeysAsync` de um tenant nunca retorna chaves de outro (o prefixo é filtro
    nativo do Object Storage, não checagem posterior); `GetAsync` com uma chave de outro tenant
    lança `UnauthorizedAccessException` antes de qualquer chamada real ao storage. 3/3 aprovados,
    validado com tempo de parede (~5,5s) consistente com a criação real de um container (não apenas
    "não deu erro").

## E2 — Modelo Canônico (fatia Comercial)

Objetivo: entidades canônicas mínimas da fatia Comercial, com RLS obrigatória desde a primeira
migration (ADR-007), consistentes com `docs/04-Modelo-Canonico.md §4/§5`.

- [x] **E2.1** Estender `Tenant.Domain.Company` com `CountryCode` (obrigatório, ISO 3166-1 alpha-2)
      e `TradeName` (opcional) para satisfazer `docs/04 §5.1`. Nova migration no módulo Tenant.
  - *Aceite:* migration aplicada; `CountryCode` obrigatório na criação, `TaxId` continua opcional.
    ✅ Concluído — migration `AddCompanyCountryCodeAndTradeName` aplicada (0 linhas existentes em
    `tenant.Companies`, sem necessidade de backfill). `Company.Create` agora exige `countryCode`;
    os 3 usos em `TenantIsolationTests` atualizados.
- [x] **E2.2** Novo módulo `EIP.Data.Canonical` (Domain/Application/Infrastructure/Api), schema
      `canonical`. Campos comuns de `docs/04 §4` como um objeto de valor/base compartilhado
      (`Id, TenantId, CompanyId, SourceSystemId, SourceEntity, SourceRecordId, SourceUpdatedAt,
      IngestedAt, ProcessedAt, IsDeleted, SchemaVersion, CorrelationId, RawObjectUri`). Entidades
      desta fatia: `Customer`, `Product`, `ProductCategory` (mínima, sem hierarquia obrigatória),
      `SalesInvoice`, `SalesInvoiceItem` — campos exatamente conforme `docs/04 §5.2/§5.3`.
  - *Depende de:* E2.1 (referência a `CompanyId` já com `CountryCode` disponível para o DW depois).
  - *Aceite:* migration inicial já nasce com `TenantId` + política RLS (mesmo padrão de
    `tenant`/`connector`/`identity`) — sem exceção. ✅ Concluído — só `Domain` + `Infrastructure`
    criados (`Application`/`Api` ficam para quando o Pipeline/E4 precisarem de fato de uma
    abstração ou endpoint; YAGNI por ora). Campos comuns centralizados em `CanonicalEntity`
    (classe base abstrata) + `CanonicalLineage` (record com a linhagem, passado a cada `Create`).
    `BranchId`/`SalesOrder`/`Currency`-como-entidade deliberadamente fora (ver §3). Referências
    entre entidades do próprio schema (`Customer`/`Product` a partir de `SalesInvoice`/`Item`) são
    `Guid` simples, sem FK/navegação EF Core — mesmo padrão já usado em `Membership.TenantId`, e
    evita qualquer interação estranha entre constraints de FK e block predicates de RLS.
- [x] **E2.3** Índice de unicidade da chave de negócio (`docs/04 §4.1`):
      `TenantId + SourceSystemId + SourceEntity + SourceRecordId` único por entidade canônica.
  - *Depende de:* E2.2.
  - *Aceite:* teste prova que inserir duas vezes o mesmo registro de origem falha/idempotentemente
    não duplica (depende de como E3 resolve isso — ver E3.4). ✅ Concluído — índice único criado via
    `CanonicalEntityConfigurationExtensions.ConfigureCanonicalFields<T>()` (evita repetir/divergir a
    configuração entre as 5 entidades). Validado com SQL bruto real contra o `canonical.Customers`:
    inserir a mesma chave de negócio duas vezes falha com erro de índice único
    (`IX_Customers_TenantId_SourceSystemId_SourceEntity_SourceRecordId`).
- [x] **E2.4** Entidade de quarentena (`docs/04 §8.2`): `CanonicalQuarantineEntry` (schema
      `canonical`, RLS obrigatória) — motivo da rejeição, referência ao `RawObjectUri`,
      `ConnectorInstanceId`, `SyncRunId`, `CorrelationId`, a regra de validação que falhou, e o
      payload bruto rejeitado (ou referência a ele) para permitir correção manual e reprocessamento.
  - *Depende de:* E2.2. ✅ Concluído — `CanonicalQuarantineEntry` não herda de `CanonicalEntity`
    (um registro rejeitado pode não ter conseguido resolver nem os campos comuns mínimos, docs/04
    §6.3); guarda o payload bruto por referência (`RawObjectUri`, já grava no Data Lake via E1.2),
    não uma cópia inline. `MarkResolved` preserva o histórico em vez de apagar a entrada.

**Validação de fechamento do E2** (todas as 4 tarefas, 2026-08-02): `dotnet build`/`dotnet test`
(16/16, incluindo `RlsCoverageTests` — `DatabaseMigrator.MigrateAllAsync` em
`EIP.Testing.Infrastructure` passou a migrar também `CanonicalDbContext`, então o gate de RLS da
Fase 0 já cobre o novo schema automaticamente) e `dotnet format --verify-no-changes` limpos.
Migration aplicada no SQL Server local e RLS validada com SQL bruto: catálogo do sistema confirma
zero tabelas com `TenantId` desprotegidas nas 6 novas tabelas; isolamento cross-tenant e unicidade
de chave de negócio provados com `INSERT`s reais (sem contexto → 0 linhas; contexto do tenant B →
0 linhas; duplicidade de chave de negócio → bloqueada pelo índice único).

## E3 — Pipeline de Ingestão e Transformação

Objetivo: fechar o fluxo `docs/04 §7` (Extração → Data Lake → Validação/Mapeamento → Canônico) para
a fatia Comercial, estendendo a infraestrutura assíncrona já provada no E7 da Fase 0.

- [x] **E3.1** Estender a fonte de dados de referência: novos endpoints estáticos no Host
      (`GET /api/v1/sample/products`, `GET /api/v1/sample/sales-invoices`) retornando um pequeno
      conjunto realista de faturas com itens, referenciando clientes/produtos de exemplo — mesma
      filosofia do `/api/v1/sample/customers` do E7 (stand-in local para sistema externo, não
      feature de produto).
  - *Aceite:* payload documentado, contém campos suficientes para mapear todos os campos
    obrigatórios de `Customer`/`Product`/`SalesInvoice`/`SalesInvoiceItem`. ✅ Concluído —
    `/api/v1/sample/customers` redesenhado (`code, name, email, city, stateOrRegion, countryCode,
    isActive`) e os dois novos endpoints adicionados; 3 faturas de exemplo (`NF-0001..0003`)
    referenciando clientes/produtos existentes por código. `ConnectorInstance` ganhou `CompanyId`
    (obrigatório, `docs/05 §3`) e `SourceEntity` (declara qual entidade canônica a instância
    sincroniza — `Id` da instância também serve como `SourceSystemId` do Modelo Canônico).
- [x] **E3.2** `ConnectorSyncProcessor` (E7) passa a gravar cada registro bruto extraído no Data
      Lake (E1.2) — com linhagem completa — **antes** de qualquer transformação, nunca depois
      (`docs/04 §7`: conector só extrai/preserva/mapeia; nunca escreve direto no canônico).
  - *Depende de:* E1.2, E3.1.
  - *Aceite:* após um `SyncRun`, os objetos brutos existem no MinIO e são auditáveis via `RawObjectUri`.
    ✅ Concluído — `IReferenceRestClient` passou de contar registros para retornar o conteúdo bruto
    (`FetchRawContentAsync`); `ConnectorSyncProcessor` grava via `IRawObjectStore.PutAsync` (chave
    `{tenantId}/{sourceSystemId}/{sourceEntity}/{yyyy}/{MM}/{dd}/{syncRunId}/{sequencial}.json`)
    antes de chamar o Pipeline. Validado com infraestrutura real: os 3 objetos das sincronizações de
    teste (customers/products/sales-invoices) apareceram no bucket `eip-datalake` do MinIO
    (`mc ls --recursive`), com o prefixo de tenant correto.
- [x] **E3.3** Novo módulo `EIP.Data.Pipeline` (Application, consome `Data.Connector` +
      `Data.Canonical` + `Data.DataLake`, nunca o inverso): orquestra, por `SyncRun`, a leitura dos
      objetos brutos gravados em E3.2, aplica o mapeamento fixo origem→canônico (E7 REST genérico →
      `Customer`/`Product`/`SalesInvoice`/`SalesInvoiceItem`), valida (`docs/04 §8.1`), resolve
      referências (`Customer`/`Product` por chave de negócio — não resolvido → quarentena, nunca uma
      relação arbitrária, `docs/04 §6.3`), e persiste no Modelo Canônico.
  - *Depende de:* E2.2, E2.4, E3.2.
  - *Aceite:* teste de integração ponta a ponta (Testcontainers) com um lote misto de registros
    válidos/inválidos: válidos viram canônico, inválidos viram quarentena — nunca os dois ao
    mesmo tempo para o mesmo registro. ✅ Concluído — `EIP.Data.Pipeline` (só `IPipelineProcessor`/
    `PipelineProcessor`, sem Infrastructure própria: nenhum SDK externo, só EF Core via
    `ICanonicalRecordStore`). Diferença deliberada em relação ao texto original desta tarefa: o
    conteúdo bruto é passado em memória do `ConnectorSyncProcessor` direto para o Pipeline na mesma
    chamada — nunca relido do Data Lake na mesma sincronização (o objeto já gravado em E3.2 continua
    servindo para auditoria/reprocessamento futuro, não como intermediário obrigatório de toda
    sincronização). Validado com infraestrutura real (Host + Gateway + Worker + SQL Server + MinIO):
    sincronizações de customers/products/sales-invoices via `/api/v1/connectors/{id}/sync`
    resultaram em 5 clientes + 4 produtos + 3 faturas + 5 itens no `canonical.*`, com
    `CustomerId`/`ProductId` corretamente resolvidos e valores de cabeçalho (`GrossAmount`/
    `DiscountAmount`/`NetAmount`) batendo com a soma dos itens. Caminho de quarentena (referência não
    resolvida) coberto por teste de integração automatizado (ver E3.4) — nunca lança exceção que
    aborta o lote.
- [x] **E3.4** Idempotência do pipeline (`docs/04 §4.1`, `docs/05 §10.1`): reprocessar o mesmo
      `RawObjectUri`/`SyncRun` não duplica registros canônicos — usa a chave de negócio única
      (E2.3) como upsert, não insert cego.
  - *Depende de:* E2.3, E3.3.
  - *Aceite:* teste republica a mesma mensagem/objeto bruto (mesmo padrão de teste manual usado no
    E7 para provar idempotência do worker) e confirma que a contagem de registros canônicos não muda.
    ✅ Concluído — novo projeto `tests/Integration/EIP.Data.Pipeline.IntegrationTests`
    (Testcontainers, SQL Server real): `ProcessAsync_ReprocessingTheSameCustomer_...` reprocessa o
    mesmo lote duas vezes e confirma exatamente 1 linha em `canonical.Customers`;
    `ProcessAsync_SalesInvoiceWithUnresolvableCustomerCode_IsQuarantined_...` prova que uma
    referência não resolvida vira 1 entrada de quarentena, zero faturas, e o `ProcessAsync` retorna
    normalmente (`RejectedCount = 1`), nunca lança. Reforçado manualmente com infraestrutura real: o
    `SyncRun` de customers foi disparado uma segunda vez (mesmo endpoint de origem) e
    `canonical.Customers` continuou com exatamente 5 linhas.

**Validação de fechamento do E3** (todas as 4 tarefas, 2026-08-02): `dotnet build`/`dotnet test`
limpos na solução inteira (18 testes, incluindo os 2 novos de `EIP.Data.Pipeline.IntegrationTests`
e o gate `RlsCoverageTests` da Fase 0, que continua cobrindo `canonical.*`/`connector.*` sem gaps
após as novas colunas `ConnectorInstance.CompanyId`/`SourceEntity`). Ponta a ponta real (Host na
porta `5080`, Gateway na `5000`, Worker consumindo RabbitMQ, SQL Server e MinIO via Docker):
registrados 3 `ConnectorInstance` (customers/products/sales-invoices) para uma empresa de
demonstração, sincronizados na ordem de dependência correta, e os 3 `SyncRun` terminaram
`Succeeded` com as contagens esperadas. Durante essa validação, corrigido um bug pré-existente (não
introduzido nesta sessão): `src/Host/Properties/launchSettings.json` e
`src/Gateway/Properties/launchSettings.json` tinham portas geradas por scaffold (`5299`/`5176`) que
nunca bateram com a convenção documentada em `docs/guides/ambiente-local.md` (`5080`/`5000`) nem com
o cluster do YARP (`src/Gateway/appsettings.json`) — o Gateway respondia 502 para qualquer rota.
Ajustado para `5080`/`5000` nos dois `launchSettings.json` (e a URL de exemplo em
`src/Host/EIP.Host.http`), alinhando com o que já era esperado pelo resto do repositório.

## E4 — Qualidade e Reconciliação

Objetivo: fechar `docs/04 §8` e o critério de saída "falhas de qualidade ficam em quarentena, sem
corromper o DW".

- [x] **E4.1** Relatório de execução por `SyncRun` (`docs/04 §8.3`): contagens de extraído, aceito,
      atualizado, rejeitado, processado. Estender `SyncRun` (E7) ou nova entidade associada — decidir
      durante a implementação conforme o que fica mais simples sem violar o grão do `SyncRun` já
      existente.
  - *Depende de:* E3.3.
  - *Aceite:* `GET /api/v1/connectors/{id}/sync-runs/{runId}` (endpoint já existente do E7) passa a
    incluir essas contagens na resposta. ✅ Concluído — `SyncRun` ganhou `AcceptedCount`/
    `UpdatedCount`/`RejectedCount`/`DeletedCount` (nullable, mesma migration `AddSyncRunReportCounts`);
    `RecordsProcessed` (já existente do E7) continua servindo como "extraídas"/"processadas" — as duas
    coincidem sempre neste conector de referência (extração completa a cada sincronização, sem
    watermark/incremental ainda). `ICanonicalRecordStore.Upsert*Async` passou a retornar
    `bool` (existia e foi atualizado) para o Pipeline poder contar "atualizadas" separado de
    "aceitas". `DeletedCount` sempre 0 — o conector de referência não emite sinal de exclusão de
    origem. Validado ao vivo: primeira sincronização de customers → `acceptedCount=5, updatedCount=0`;
    reexecutando a mesma sincronização → `acceptedCount=5, updatedCount=5` (as 5 já existiam).
- [x] **E4.2** Endpoint(s) de consulta e reprocessamento de quarentena (`docs/04 §8.2`: "operador
      pode corrigir o mapeamento e reprocessar a carga, mantendo trilha de auditoria"): listar
      entradas de quarentena por tenant/conector/período, e disparar reprocessamento de uma entrada
      específica (reaproveitando a fila assíncrona do E7/E3).
  - *Depende de:* E2.4, E3.3. ✅ Concluído — novo `QuarantineController` (`EIP.Data.Connector.Api`):
    `GET /api/v1/connectors/quarantine` (filtros opcionais `connectorInstanceId`/`createdFrom`/
    `createdTo`) e `POST /api/v1/connectors/quarantine/{id}/reprocess`. Reprocessar aqui dispara uma
    nova sincronização completa da instância dona da entrada (via `IConnectorSyncService.
    RequestSyncAsync`, reaproveitando a fila assíncrona do E7) e marca a entrada como resolvida
    (`MarkResolved`, nunca apaga — docs/04 §8.2 "mantendo auditoria"); não existe reprocessamento
    cirúrgico de um único registro nesta fase (mapeamento fixo no código, conteúdo bruto armazenado
    só por lote/SyncRun, não por registro individual) — decisão deliberada, documentada no próprio
    controller. `ICanonicalRecordStore` ganhou `ListQuarantineEntriesAsync`/`FindQuarantineEntryAsync`/
    `MarkQuarantineEntryResolvedAsync`. Validado ao vivo: registrado um conector com mapeamento
    inválido de propósito, sincronizado (5 registros → 5 rejeitados), a listagem retornou as 5
    entradas, o filtro por `connectorInstanceId` funcionou, e `reprocess` disparou um novo `SyncRun`
    (`Succeeded`, mesma quarentena reaparecendo — dado de origem não mudou, comportamento esperado) e
    marcou a entrada original com `resolvedAt` preenchido.
- [x] **E4.3** Reconciliação Canônico↔Origem (`docs/04 §8.3`): para a fatia Comercial, verificação de
      totais (contagem de faturas, soma de `NetAmount`) comparando o relatório do `SyncRun` com o
      que foi de fato persistido no Modelo Canônico.
  - *Depende de:* E4.1, E3.3.
  - *Aceite:* teste prova que uma divergência acima do limite configurado é detectável (mesmo que o
    bloqueio automático de publicação fique para E5/E6 — aqui o requisito mínimo é a verificação
    existir e ser testável). ✅ Concluído — `ICanonicalReconciliationService`/
    `CanonicalReconciliationService` (`EIP.Data.Canonical.Application`, sem projeto próprio de
    Infrastructure — só usa `ICanonicalRecordStore`): compara a contagem/soma de `NetAmount`
    reportada pelo Pipeline nesta execução contra `GetSalesInvoiceTotalsAsync` (o que está de fato
    persistido para aquele `SourceSystemId`). Tolerância configurável por fração (fixa em 1% nesta
    fase — parametrização por tenant/conector fica para E5/E6). `ConnectorSyncProcessor` chama a
    reconciliação só para `sales-invoices`, logando um aviso (nunca falhando o `SyncRun`) quando fora
    da tolerância. 2 testes de integração novos (Testcontainers) provam o caso dentro da tolerância e
    o caso de divergência detectável. Validado ao vivo: sincronização real de sales-invoices não
    gerou nenhum aviso de reconciliação (dados consistentes, como esperado).

**Validação de fechamento do E4** (todas as 3 tarefas, 2026-08-02): `dotnet build`/`dotnet test`
limpos na solução inteira (20 testes — 2 novos de reconciliação além dos 18 já existentes),
`dotnet format --verify-no-changes` limpo. **Bug real encontrado e corrigido durante a validação ao
vivo** (não introduzido antes desta sessão de trabalho, mas exposto por ela): o `EIP.Host` nunca
registrava `CanonicalDbContext`/`ICanonicalRecordStore` no container de DI — só o `EIP.Worker.Sync`
tinha essa dependência, porque até o E3 nenhum endpoint do Host precisava do Modelo Canônico
diretamente. O novo `QuarantineController` (E4.2) quebrou essa suposição; sem o registro, toda
chamada a `/api/v1/connectors/quarantine` retornava 500 (`Unable to resolve service for type
ICanonicalRecordStore`). Corrigido adicionando `CanonicalDbContext`/`ICanonicalRecordStore` ao
`Program.cs` do Host (mesmo padrão do Worker) e a connection string `CanonicalDb` a
`appsettings.json`/`HostApiFixture` (testes de integração). Ponta a ponta real (Host+Gateway+Worker+
SQL Server+RabbitMQ+MinIO): validados os três itens do épico com o fluxo completo de sincronização,
listagem/reprocessamento de quarentena, e reconciliação silenciosa quando os dados batem.

## E5 — Data Warehouse Inicial (fatia Comercial)

Objetivo: fechar `docs/09-Data-Warehouse.md` para o fato `FactSalesInvoiceItem` e suas dimensões
mínimas, com RLS obrigatória (`docs/09 §4.1`).

- [x] **E5.1** Novo módulo `EIP.Data.Warehouse` (Domain/Infrastructure), schema `warehouse`.
      Dimensões conformadas mínimas (`docs/09 §5.2`): `DimTenant`, `DimCompany` (com `CountryCode`
      de E2.1), `DimDate` (pré-gerada, calendário), `DimCustomer` (SCD Type 2), `DimProduct` (SCD
      Type 2), `DimProductCategory`, `DimCurrency` (mínima — só código + nome). Convenções
      obrigatórias de `docs/09 §5.1`: chaves substitutas com sufixo `Key`, `TenantKey` obrigatório
      em toda fato/dimensão tenant-scoped, nomes em inglês no modelo físico.
  - *Depende de:* E2.1, E2.2.
  - *Aceite:* migration inicial já nasce com RLS (mesmo padrão de sempre) nas dimensões/fato
    tenant-scoped; `DimDate` não precisa de RLS (dado de referência, não tenant-scoped). ✅
    Concluído — `EIP.Data.Warehouse.{Domain,Application,Infrastructure}` (Application desde já,
    diferente do Canonical em E2.2: o processo de carga precisava de uma abstração de acesso a dados
    própria desde o início). Toda tabela com `TenantId` carrega **tanto** o `TenantKey` (surrogate
    int, convenção dimensional de docs/09 §5.1) **quanto** o `TenantId` (Guid, discriminador real da
    RLS via `SESSION_CONTEXT`) — os dois convivem porque a RLS deste projeto sempre compara contra o
    Guid, nunca contra uma chave substituta. `DimDate`/`DimCurrency` são as únicas sem `TenantId`
    (dado de referência compartilhado). `DimProductCategory` existe no esquema mas nunca é populada
    nesta fase — mesma lacuna já aceita em `Canonical.Product.CategoryId` (conector de referência não
    ingere categorias). Migration `InitialCreate` aplicada com RLS validada via SQL bruto real:
    catálogo do sistema confirma zero tabelas com `TenantId` desprotegidas no schema `warehouse`;
    isolamento cross-tenant provado com `INSERT`/`SELECT` reais (tenant B não vê linha do tenant A,
    sem contexto não vê nenhuma). `DatabaseMigrator`/`RlsCoverageTests` atualizados para cobrir o
    novo schema automaticamente (mesma regra permanente desde E2).
- [x] **E5.2** Fato `FactSalesInvoiceItem` (`docs/09 §5.3`): grão de uma linha por item de fatura
      emitida; métricas mínimas (quantidade, bruto, desconto, imposto, líquido — custo/margem só se
      disponível na origem, o que não é o caso do conector de referência nesta fase, então ficam de
      fora por enquanto).
  - *Depende de:* E5.1. ✅ Concluído — chave de negócio para upsert idempotente é
    `(TenantId, SourceSystemId, SourceEntity, SourceRecordId)`, a mesma linhagem do CDM (docs/04
    §4.1) — deliberadamente **não** `SalesInvoiceItemId`: o Canônico substitui (delete+insert) os
    itens de uma fatura a cada reprocessamento (E3.4), então esse Guid não é estável entre cargas,
    só o `SourceRecordId` (`"{invoiceNumber}-{lineNumber}"`) é. Campos de linhagem completos
    (`SalesInvoiceId`, `SalesInvoiceItemId` da versão atual, `RawObjectUri`, `LoadBatchId`) para
    rastreabilidade Fonte→Raw→Canonical→Fact.
- [x] **E5.3** Processo de carga (`docs/09 §7.1`): staging → resolução de dimensões (aplicando SCD 2
      em `DimCustomer`/`DimProduct` quando o atributo relevante muda) → materialização do fato →
      contagens de reconciliação. Disparado a partir do Modelo Canônico já validado (E3/E4), nunca
      direto da origem.
  - *Depende de:* E5.1, E5.2, E3.3.
  - *Aceite:* teste ponta a ponta: um `SyncRun` reflete em linhas de `FactSalesInvoiceItem`
    rastreáveis até o registro canônico e o objeto bruto correspondentes (critério de saída
    "dado bruto, registro canônico e fato analítico podem ser rastreados entre si"). ✅ Concluído —
    `IWarehouseLoadService`/`WarehouseLoadService`, chamado por `ConnectorSyncProcessor` logo após a
    reconciliação Canônico↔Origem (E4.3), só para `sales-invoices`. Sem staging físico separado
    nesta fase (YAGNI — o volume de referência não justifica); a "validação de tipos/duplicidade"
    do passo 3 já foi feita pelo Pipeline (E3). Nova projeção `SalesInvoiceItemForLoad`
    (`EIP.Data.Canonical.Application`) e `ICanonicalRecordStore.ListSalesInvoiceItemsForLoadAsync`:
    o Warehouse nunca acessa o schema `canonical` diretamente, só essa abstração — mesmo princípio
    já usado entre Pipeline e Canonical. **Nova abstração cross-domain**: `ITenantDirectory`
    (`EIP.Shared.Contracts.Tenancy`, implementada por `EIP.Platform.Tenant.Infrastructure.
    TenantDirectory`) — o Warehouse precisa do nome do tenant/empresa para `DimTenant`/`DimCompany`,
    que só existem no módulo Tenant; mesmo padrão já estabelecido por `IMembershipDirectory` (E2.4).
    SCD Tipo 2 em `DimCustomer`/`DimProduct`: fecha a versão atual (`EffectiveTo`/`IsCurrent=false`)
    e abre uma nova só quando um atributo descritivo muda de fato
    (`HasDescriptiveChangeComparedTo`) — nunca sobrescreve. **Decisão documentada**: a origem ainda
    não fornece `SourceUpdatedAt` (sempre nulo no Pipeline), então toda versão nasce datada do
    momento da carga, não do negócio; resolver a versão "válida para a data de negócio" (docs/09
    §6.1) quando a fatura é anterior à primeira carga cai de volta para a versão mais antiga
    conhecida (`ResolveDim{Customer,Product}KeyAsOfAsync`) — nunca falha por "não encontrado" para
    uma entidade já carregada nesta execução. `LoadBatch` (novo, mirando `SyncRun`) registra
    tenant/origem/correlação/contagens de cada carga. Validado com Testcontainers (3 testes novos:
    rastreabilidade+idempotência, versionamento SCD2, reconciliação) e ao vivo (Host+Gateway+Worker+
    SQL Server real): sincronização de sales-invoices gerou 5 linhas de `FactSalesInvoiceItem`
    rastreáveis até `canonical.SalesInvoiceItems` e o `RawObjectUri` do MinIO; resincronizar manteve
    exatamente 5 linhas (idempotente) com um novo `LoadBatch` de auditoria.
- [x] **E5.4** Reconciliação Canônico↔Fato (`docs/09 §8.2`): comparação de contagens/somas entre
      `canonical.SalesInvoiceItem` e `warehouse.FactSalesInvoiceItem` por lote de carga.
  - *Depende de:* E5.3, E4.3. ✅ Concluído — `IWarehouseReconciliationService`/
    `WarehouseReconciliationService` (mesmo desenho de `CanonicalReconciliationService`, E4.3):
    compara contagem/soma de `NetAmount` entre `canonical.SalesInvoiceItems` e
    `warehouse.FactSalesInvoiceItem` para o mesmo conector, com tolerância configurável (1% fixo
    nesta fase). Chamado por `ConnectorSyncProcessor` logo após a carga; só loga um aviso quando
    fora da tolerância, nunca bloqueia (bloqueio automático fica para fase futura, mesmo critério de
    E4.3). Novo `ICanonicalRecordStore.GetSalesInvoiceItemTotalsAsync` (grão de item, distinto do
    `GetSalesInvoiceTotalsAsync` de cabeçalho usado pelo E4.3). Testado (Testcontainers): caso dentro
    da tolerância e caso de divergência detectável (linha de fato removida manualmente após a carga).

**Validação de fechamento do E5** (todas as 4 tarefas, 2026-08-02): `dotnet build`/`dotnet test`
limpos na solução inteira (23 testes — 3 novos de `EIP.Data.Warehouse.IntegrationTests` além dos 20
já existentes), `dotnet format --verify-no-changes` limpo. RLS validada no novo schema `warehouse`
via SQL bruto real (zero tabelas com `TenantId` desprotegidas; isolamento cross-tenant provado com
`INSERT`/`SELECT`). Ponta a ponta real (Host+Gateway+Worker+SQL Server+RabbitMQ+MinIO): sincronizar
sales-invoices carregou `DimTenant`(1)/`DimCompany`(1)/`DimCustomer`(3)/`DimProduct`(4)/`DimDate`(3)/
`DimCurrency`(1) e 5 linhas de `FactSalesInvoiceItem` corretamente rastreáveis; resincronizar sem
mudança de origem manteve exatamente 5 linhas (upsert idempotente) com um segundo `LoadBatch` de
auditoria; nenhum aviso de reconciliação Canônico↔Fato (dados consistentes, como esperado). Módulo
novo `EIP.Data.Warehouse.{Domain,Application,Infrastructure}` e nova abstração cross-domain
`ITenantDirectory` (Shared→Tenant) wireados em `EIP.Worker.Sync` (novas connection strings
`WarehouseDb`/`TenantDb` no `appsettings.json`, mesmo valor "dev only" das demais).

## E6 — Camada Semântica Mínima

Objetivo: expor as métricas certificadas de exemplo de `docs/09-Data-Warehouse.md §9` para a fatia
Comercial, sem construir um motor de métricas genérico (isso é Fase 2).

- [x] **E6.1** Definição versionada e testada das 3 métricas certificadas: Receita Líquida
      (soma de `NetAmount` em `FactSalesInvoiceItem`, excluindo documentos cancelados), Quantidade
      Faturada (soma de quantidade dos itens válidos), Ticket Médio (Receita Líquida / contagem
      distinta de faturas válidas). Cada métrica tem dono, versão e teste de reconciliação
      (`docs/09 §9`: "nenhuma métrica é oficial sem definição, dono, versão e teste de
      reconciliação"). ✅ Concluído — `CertifiedMetricDefinition`/`CertifiedMetrics`
      (`EIP.Data.Semantic.Application`): 3 constantes estáticas (`net_revenue`, `invoiced_quantity`,
      `average_ticket`), cada uma com nome, descrição, dono (`Comercial`) e versão (`1.0`) — definição
      em código nesta fase, não um motor configurável/persistido (isso é Fase 2, conforme o objetivo
      do épico). **Gap real encontrado e corrigido durante o design**: `FactSalesInvoiceItem` (E5)
      não carregava o `Status` da fatura — sem isso, "excluindo documentos cancelados" exigiria voltar
      ao Canônico a cada consulta de métrica, violando a separação de camadas (docs/09 §2). Adicionada
      coluna `Status` (migration `AddStatusToFactSalesInvoiceItem`, aplicada sem backfill manual — a
      recarga idempotente do E5.3 já resincroniza o valor correto na próxima sincronização, mesmo
      padrão já aceito para colunas novas não nulas nesta fase). `IWarehouseLoadStore` ganhou
      `ListFactSalesInvoiceItemsForMetricsAsync` (nova projeção `FactSalesInvoiceItemForMetrics`,
      filtra por tenant/empresa/período comparando `DateKey` diretamente — sem precisar de junção com
      `DimDate`). `IMetricsQueryService`/`MetricsQueryService` calculam as 3 métricas a partir dessa
      projeção; Ticket Médio retorna `null` (nunca `0`) quando não há fatura válida, para o consumidor
      nunca confundir "sem dado" com "zero". 2 testes de reconciliação novos (Testcontainers):
      múltiplas faturas válidas + uma cancelada com valor bem maior (prova que a cancelada é
      excluída e os agregados batem com o cálculo manual), e o caso "nenhuma fatura válida" (prova o
      `null` do Ticket Médio).
- [x] **E6.2** Endpoint versionado de consulta (`GET /api/v1/metrics/...` ou equivalente, a decidir
      o path exato durante a implementação seguindo `docs/06-API-Design.md`), filtrável por empresa
      e período, respeitando tenant/permissões (nova permissão `metrics.view`?, a definir).
  - *Depende de:* E5.3, E6.1.
  - *Aceite:* teste de isolamento cross-tenant nas métricas (tenant A não vê números de tenant B).
    ✅ Concluído — novo módulo `EIP.Data.Semantic.Api`: `GET /api/v1/metrics/commercial` (filtros
    opcionais `companyId`/`periodStart`/`periodEnd`), nova permissão `metrics.view`
    (`EIP.Shared.Contracts.Metrics.MetricsPermissions`, concedida a Owner/Admin/Member — leitura
    agregada, sem fluxo de gestão próprio). `TenantId` do filtro vem sempre do claim JWT autenticado,
    nunca de input do cliente. Resposta sempre inclui a definição/dono/versão junto do valor
    (docs/09 §11: "tabelas de DW não são expostas diretamente" — só valores agregados com
    proveniência). Teste de isolamento cross-tenant novo (`EIP.Host.IntegrationTests`): semeia
    `FactSalesInvoiceItem` diretamente para dois tenants com valores bem diferentes e prova que cada
    tenant só vê o seu próprio `netRevenue` via HTTP real. Validado ao vivo (Host+Gateway+Worker+SQL
    Server real): `GET /api/v1/metrics/commercial` sem filtro retornou os valores corretos batendo
    com as 3 faturas de exemplo já sincronizadas (Receita Líquida 11100, Quantidade Faturada 8,
    Ticket Médio 3700 — conferido à mão); filtro de período (`periodStart=periodEnd=2026-07-01`)
    retornou corretamente só a NF-0001 (5500/2/5500).

**Validação de fechamento do E6** (ambas as tarefas, 2026-08-03): `dotnet build`/`dotnet test`
limpos na solução inteira (26 testes — 3 novos: 2 de reconciliação de métrica em
`EIP.Data.Warehouse.IntegrationTests` e 1 de isolamento cross-tenant em `EIP.Host.IntegrationTests`),
`dotnet format --verify-no-changes` limpo. Novos módulos `EIP.Data.Semantic.{Application,Api}`
wireados no Host (`WarehouseDb` já usado pelo Worker desde E5, agora também pelo Host — precisa do
Core Dimensional para responder consultas de métrica). Ponta a ponta real (Host+Gateway+Worker+
SQL Server+RabbitMQ+MinIO): confirmados os valores exatos das 3 métricas certificadas e o filtro de
período, ambos batendo com o cálculo manual esperado a partir dos dados de exemplo já conhecidos.

## E7 — Carga Incremental e Reprocessamento

Objetivo: fechar `docs/09 §7.2`/`docs/04 §11` — sincronização incremental, não só carga completa
repetida.

- [x] **E7.1** Estratégia de watermark por `ConnectorInstance` (`docs/04 §11`: "frequência de
      sincronização, estratégia incremental"): guardar o último `SourceUpdatedAt`/checkpoint
      processado com sucesso, e usá-lo para limitar a próxima extração. Para o conector de
      referência REST genérico (dado estático), isso pode ser validado com um dataset de exemplo
      que varia entre execuções (ex.: parâmetro de "atualizado desde" no endpoint de amostra do E3.1).
  - *Depende de:* E3.1, E3.3. ✅ Concluído — `ConnectorInstance.LastWatermark` (nulo = "nunca
    sincronizado", nunca retrocede via `AdvanceWatermark`), capturado **antes** da extração (não
    depois) para nunca perder um registro atualizado durante o próprio processamento da
    sincronização. `IReferenceRestClient.FetchRawContentAsync` ganhou `updatedSince`, repassado como
    query string `?updatedSince=` ao endpoint de origem (o filtro é decisão da origem, nunca aplicado
    no cliente — um filtro local exigiria buscar tudo de qualquer forma). Endpoints de amostra
    (`/api/v1/sample/{customers,products,sales-invoices}`) ganharam `updatedAt` por registro e um
    registro extra "sempre atualizado" (`C099`/`P099`/`NF-E7-ALWAYS`, com `updatedAt =
    DateTimeOffset.UtcNow` a cada chamada) para provar que o filtro varia de fato entre execuções, não
    só na primeira. `ConnectorSyncProcessor` só avança o watermark em sincronizações automáticas
    (`ReprocessFromUtc is null`) — ver E7.2. 3 testes de integração novos (Testcontainers,
    `EIP.Data.Connector.IntegrationTests`, novo projeto): primeira sincronização automática busca sem
    watermark e o avança; segunda sincronização usa o watermark salvo da primeira; reprocessamento
    manual usa a data explícita e nunca avança o watermark. Validado ao vivo (Host+Gateway+Worker+SQL
    Server+RabbitMQ real): 1ª sincronização de customers aceitou 6 registros (5 fixos + `C099`) e
    avançou o watermark; 2ª sincronização (sem reprocessamento) processou só 1 registro (`C099`,
    `updatedCount=1`) — os 5 fixos não reapareceram, confirmando o filtro `updatedSince` funcionando
    fim a fim contra o endpoint real.
  - **Bug real encontrado e corrigido durante a validação ao vivo** (pré-existente, não introduzido
    nesta sessão, mas exposto por ela): `src/Host/Program.cs` lia todas as `ConnectionStrings:*` em
    variáveis locais logo após `WebApplication.CreateBuilder(args)`, **antes** de `builder.Build()`.
    `WebApplicationFactory<Program>` (usado por `HostApiFixture`, `EIP.Host.IntegrationTests`)
    sobrescreve `ConnectionStrings:*` via `ConfigureAppConfiguration`, mas essa sobrescrita só existe
    em `IConfiguration` a partir do momento em que o host é de fato construído — ler antes sempre
    devolvia o valor "dev only" de `appsettings.json` (`Server=localhost,1433;...`), ignorando por
    completo a connection string do SQL Server efêmero do teste. Ficou mascarado até agora porque o
    SQL Server do `docker-compose` local costumava estar de pé na mesma porta/credenciais — os testes
    "passavam" acidentalmente contra o banco de desenvolvimento persistente, não contra o container
    efêmero isolado que a suíte pretende usar (uma falha real de isolamento de teste, inclusive para
    os testes de isolamento cross-tenant). Exposto porque o `docker-compose` estava parado no início
    desta sessão: as 8 chamadas de `/api/v1/auth/register` em `EIP.Host.IntegrationTests` falhavam
    com 500 (timeout de conexão para `localhost,1433`). Corrigido movendo toda leitura de
    `ConnectionStrings:*` para dentro dos delegates de registro do DI (`sp.GetRequiredService<
    IConfiguration>().GetConnectionString(...)`, incluindo os health checks via os overloads
    `Func<IServiceProvider, string>` de `AddSqlServer`/`AddRedis`/`AddRabbitMQ`), nunca mais em uma
    variável capturada antes do `Build()`. `dotnet test` (29 testes, toda a solução) e `dotnet format
    --verify-no-changes` limpos após a correção.
- [x] **E7.2** Reprocessamento por período/entidade (`docs/09 §7.2`): capacidade de reconstruir um
      intervalo específico sem apagar dados não relacionados — reaproveita o mecanismo de
      reprocessamento de quarentena do E4.2, generalizado para "reprocessar este período". ✅
      Concluído junto com E7.1 (mesmo mecanismo) — `POST /api/v1/connectors/{id}/sync?reprocessFrom=`
      (query param opcional, `DateTimeOffset`) ignora o `LastWatermark` salvo só para aquela execução
      e nunca o avança depois (`ConnectorSyncProcessor` só chama `AdvanceWatermark` quando
      `ReprocessFromUtc is null`) — nunca apaga dados não relacionados porque o upsert idempotente do
      Pipeline/Warehouse (E3.4/E5.3) já garante isso por chave de negócio, sem depender de uma janela
      de exclusão. `SyncRequestedMessage.ReprocessFromUtc` propaga a data pela fila assíncrona (E7).
      Validado ao vivo: reprocessamento manual com `reprocessFrom=2026-01-01` reprocessou os 6
      registros de novo (`acceptedCount=6, updatedCount=6`, todos já existiam) mesmo com um watermark
      mais recente salvo (da 2ª sincronização automática); consultado o banco depois, `LastWatermark`
      continuava exatamente no valor da 2ª sincronização automática — confirmando que o
      reprocessamento manual nunca o move.

**Validação de fechamento do E7** (ambas as tarefas, 2026-08-04): `dotnet build`/`dotnet test` limpos
na solução inteira (29 testes — 3 novos de `EIP.Data.Connector.IntegrationTests`, novo projeto, além
dos 26 já existentes), `dotnet format --verify-no-changes` limpo. Ponta a ponta real (Host+Gateway+
Worker+SQL Server+RabbitMQ real via `docker-compose`, tenant/empresa/conector provisionados ao vivo):
1ª sincronização de customers aceitou 6 registros e avançou o watermark; 2ª sincronização automática
processou só o registro "sempre atualizado" (`updatedCount=1`); reprocessamento manual por período
(`reprocessFrom=2026-01-01`) reprocessou os 6 registros de novo sem mover o watermark salvo. Bug de
isolamento de teste em `EIP.Host.IntegrationTests` (connection strings lidas antes de `Build()`,
mascarando a sobrescrita do `WebApplicationFactory`) encontrado e corrigido durante esta validação —
ver evidência no E7.1.

## E8 — Testes de Isolamento e Fechamento da Fase

Objetivo: fechar o critério de saída "tenant, empresa, cache, fila e Object Storage preservam
isolamento em testes" e validar a Fase 1 inteira de ponta a ponta, mesmo rigor da revisão que fechou
a Fase 0.

- [x] **E8.1** Testes cross-tenant automatizados cobrindo Canonical (`Customer`/`SalesInvoice`) e
      Warehouse (`FactSalesInvoiceItem`) — mesmo padrão de
      `ConnectorCrossTenantIsolationTests`/`RlsCoverageTests` criados no fechamento da Fase 0.
  - *Depende de:* E2.2, E5.1. ✅ Concluído — mesmo padrão de `TenantIsolationTests` (Fase 0, módulo
    Tenant): via EF Core real contra SQL Server (Testcontainers), nunca SQL bruto. Novo projeto
    `tests/Integration/EIP.Data.Canonical.Infrastructure.IntegrationTests`
    (`CanonicalCrossTenantIsolationTests`, 5 testes: sem contexto → vazio; tenant A só vê o próprio;
    filtro explícito pelo tenant B bloqueado pela RLS mesmo pedindo explicitamente; `INSERT` de
    `Customer`/`SalesInvoice` com `TenantId` divergente do `SESSION_CONTEXT` rejeitado pelo block
    predicate). Novo `WarehouseCrossTenantIsolationTests` no projeto já existente
    `EIP.Data.Warehouse.IntegrationTests` (4 testes, mesmo padrão para `FactSalesInvoiceItem`) — as
    chaves substitutas de dimensão (`TenantKey`/`CompanyKey`/`DateKey`/`CustomerKey`/`ProductKey`/
    `CurrencyKey`) usam valores fixos arbitrários porque não há FK/navegação EF Core entre fato e
    dimensões (mesma decisão deliberada de `docs/09 §5.1`), então não precisam de linhas de dimensão
    reais para provar isolamento por `TenantId`. `Customer`/`Warehouse` (ao contrário do módulo
    Tenant) não têm predicado de bypass de sistema — decisão deliberada, nenhum caso de uso desta
    fase precisa de consulta cross-tenant nesses schemas, então nenhum teste de "contexto de sistema
    vê tudo" foi escrito para eles (escreveria uma capacidade que não existe).
- [x] **E8.2** Validação end-to-end real, com infraestrutura de verdade rodando (Host, Gateway,
      Worker(s), SQL Server, RabbitMQ, MinIO): disparar uma sincronização completa e confirmar,
      registro a registro, o rastro `RawObjectUri → registro canônico → linha de fato`, mais as
      contagens de reconciliação batendo em cada etapa. ✅ Concluído — sincronizados customers →
      products → sales-invoices (ordem de dependência) contra o tenant/empresa de teste já
      provisionado (E7); sales-invoices: `recordsProcessed=4, acceptedCount=4, rejectedCount=0`.
      Rastreado um item específico (`SourceRecordId=NF-0003-2`) ponta a ponta: objeto bruto confirmado
      no bucket `eip-datalake` do MinIO real (`mc stat`, metadados de linhagem — `TenantId`,
      `ConnectorInstanceId`, `SourceSystemId`, `SyncRunId`, checksum SHA-256 — todos batendo) →
      `canonical.SalesInvoiceItems` (mesmo `RawObjectUri`, `NetAmount=900`) →
      `warehouse.FactSalesInvoiceItem` (mesmo `SalesInvoiceItemId`, mesmo `RawObjectUri`,
      `NetAmount=900`, `Status=Issued`) — os três elos da cadeia batendo exatamente. Reconciliação
      Canônico↔Fato (E5.4) conferida à mão além do "sem aviso" no log: `COUNT(*)`/`SUM(NetAmount)`
      idênticos dos dois lados (6 itens, 11110.0000) para o `SourceSystemId` desta sincronização — log
      do Worker sem nenhuma linha de aviso de reconciliação, confirmando que ambas as reconciliações
      (E4.3 Canônico↔Origem e E5.4 Canônico↔Fato) rodaram dentro da tolerância.
- [x] **E8.3** Revisão formal da seção 4 (Definition of Done) desta fase, com evidência por
      critério — mesmo processo aplicado à Fase 0 em 2026-08-01. Atualizar a tabela de rastreamento
      (§7) e a memória do projeto. ✅ Concluído — revisão item a item da seção 4 (ver abaixo). **Gap
      real encontrado durante a revisão**: o critério "tenant, empresa, cache, fila e Object Storage
      preservam isolamento em testes" citava "fila" explicitamente, mas não havia nenhum teste
      automatizado exercitando o isolamento na própria camada de fila/worker — só indiretamente via
      HTTP (`ConnectorCrossTenantIsolationTests`). `ConnectorSyncProcessor` já tinha a defesa em
      profundidade no código (`instance.TenantId != message.TenantId`, docs/05 §12), mas sem teste
      cobrindo esse caminho especificamente pela fila. Corrigido com um teste novo,
      `ConnectorSyncProcessorQueueTenantIsolationTests` (mesmo projeto do E7.1): um `SyncRun`
      pertencente ao Tenant B referencia (por Guid adivinhado/enumerado) um `ConnectorInstance` real
      do Tenant A — mesmo padrão de ataque IDOR já coberto na camada HTTP, aqui reproduzido na camada
      de fila, onde não há claim JWT, só o `TenantId` da própria mensagem. Prova que o `SyncRun` falha
      (`Instância de conector não encontrada para o tenant informado na mensagem`), a origem nunca é
      extraída, e o watermark da instância real do Tenant A nunca é tocado.

**Validação de fechamento do E8** (todas as 3 tarefas, 2026-08-04): `dotnet build`/`dotnet test`
limpos na solução inteira (39 testes — 10 novos: 5 de `EIP.Data.Canonical.Infrastructure.
IntegrationTests`, novo projeto, 4 de `WarehouseCrossTenantIsolationTests` e 1 de
`ConnectorSyncProcessorQueueTenantIsolationTests`, além dos 29 já existentes ao fim do E7),
`dotnet format --verify-no-changes` limpo. Ponta a ponta real (Host+Gateway+Worker+SQL Server+
RabbitMQ+MinIO via `docker-compose`): rastro completo `RawObjectUri → canônico → fato` confirmado
para um registro específico, com reconciliação Canônico↔Fato batendo exatamente (6 itens,
NetAmount 11110.0000 dos dois lados). **Fase 1 — Definition of Done (§4): ✅ revisada e satisfeita em
2026-08-04**, evidência por critério na seção 4.

---

# 6. Fora do Escopo da Fase 1

Para não perder o foco (`docs/15-Roadmap.md §3`), os itens abaixo são explicitamente adiados:

- `SalesOrder`/`SalesOrderItem` (carteira/pedido comercial) — só `SalesInvoice`/`SalesInvoiceItem`.
- Domínios Financeiro (`FinancialTitle`/`FinancialTransaction`) e Estoque
  (`InventoryBalance`/`InventoryMovement`) — próxima fatia vertical, por demanda validada.
- `Branch`, `CostCenter`, `Currency`/`ExchangeRate`/`PaymentTerm` como entidades próprias.
- Connector Registry completo (ciclo de vida Draft/Configuring/Validating/Active/Paused/Error,
  múltiplos Connector Types, Secret Provider) — só o conector de referência REST genérico do E7,
  com mapeamento fixo.
- `Catalog` (metadados/qualidade/proprietário de datasets) — prematuro com um único conector.
- Analytics Engine completo, Dashboard Builder, motor de métricas configurável — Fase 2.
- Modo `Dedicated` de banco por tenant — só `Shared` (mantido da Fase 0).
- `Workspace` — mantido fora do escopo (decisão já registrada na Fase 0).
- Retry com backoff exponencial na fila de canônico — mesmo critério do E7: idempotência + DLQ
  bastam para esta fase.
- Kubernetes/Helm — Docker Compose continua suficiente.

---

# 7. Rastreamento

| Épico | Status |
|---|---|
| E1 — Correção Estrutural + Fundações de Dados | ✅ Concluído (2026-08) |
| E2 — Modelo Canônico (fatia Comercial) | ✅ Concluído (2026-08) |
| E3 — Pipeline de Ingestão e Transformação | ✅ Concluído (2026-08) |
| E4 — Qualidade e Reconciliação | ✅ Concluído (2026-08) |
| E5 — Data Warehouse Inicial | ✅ Concluído (2026-08) |
| E6 — Camada Semântica Mínima | ✅ Concluído (2026-08) |
| E7 — Carga Incremental e Reprocessamento | ✅ Concluído (2026-08) |
| E8 — Testes de Isolamento e Fechamento da Fase | ✅ Concluído (2026-08) |

Atualizar esta tabela conforme os épicos avançam.
