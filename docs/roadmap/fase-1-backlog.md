# Backlog de Execução — Fase 1 (Primeiro Fluxo de Dados Confiável)

**Projeto:** Enterprise Intelligence Platform (EIP)
**Versão:** 1.0
**Status:** Oficial
**Última atualização:** Agosto/2026

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

- [ ] Uma sincronização é executada de ponta a ponta, reprocessável e auditada.
- [ ] Dado bruto, registro canônico e fato analítico podem ser rastreados entre si.
- [ ] Falhas de qualidade ficam em quarentena, sem corromper o DW.
- [ ] Totais/contagens de dados críticos são reconciliados com a origem dentro do limite definido.
- [ ] Tenant, empresa, cache, fila e Object Storage preservam isolamento em testes.

Critério adicional obrigatório por conta da ADR-007 (mesmo texto usado para fechar a Fase 0):

- [ ] Toda tabela com `TenantId` (agora incluindo `canonical.*` e `warehouse.*`) possui política RLS
      ativa, e o gate automatizado (`RlsCoverageTests`, criado na revisão de fechamento da Fase 0)
      continua passando sem exceções adicionadas.

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

- [ ] **E3.1** Estender a fonte de dados de referência: novos endpoints estáticos no Host
      (`GET /api/v1/sample/products`, `GET /api/v1/sample/sales-invoices`) retornando um pequeno
      conjunto realista de faturas com itens, referenciando clientes/produtos de exemplo — mesma
      filosofia do `/api/v1/sample/customers` do E7 (stand-in local para sistema externo, não
      feature de produto).
  - *Aceite:* payload documentado, contém campos suficientes para mapear todos os campos
    obrigatórios de `Customer`/`Product`/`SalesInvoice`/`SalesInvoiceItem`.
- [ ] **E3.2** `ConnectorSyncProcessor` (E7) passa a gravar cada registro bruto extraído no Data
      Lake (E1.2) — com linhagem completa — **antes** de qualquer transformação, nunca depois
      (`docs/04 §7`: conector só extrai/preserva/mapeia; nunca escreve direto no canônico).
  - *Depende de:* E1.2, E3.1.
  - *Aceite:* após um `SyncRun`, os objetos brutos existem no MinIO e são auditáveis via `RawObjectUri`.
- [ ] **E3.3** Novo módulo `EIP.Data.Pipeline` (Application, consome `Data.Connector` +
      `Data.Canonical` + `Data.DataLake`, nunca o inverso): orquestra, por `SyncRun`, a leitura dos
      objetos brutos gravados em E3.2, aplica o mapeamento fixo origem→canônico (E7 REST genérico →
      `Customer`/`Product`/`SalesInvoice`/`SalesInvoiceItem`), valida (`docs/04 §8.1`), resolve
      referências (`Customer`/`Product` por chave de negócio — não resolvido → quarentena, nunca uma
      relação arbitrária, `docs/04 §6.3`), e persiste no Modelo Canônico.
  - *Depende de:* E2.2, E2.4, E3.2.
  - *Aceite:* teste de integração ponta a ponta (Testcontainers) com um lote misto de registros
    válidos/inválidos: válidos viram canônico, inválidos viram quarentena — nunca os dois ao
    mesmo tempo para o mesmo registro.
- [ ] **E3.4** Idempotência do pipeline (`docs/04 §4.1`, `docs/05 §10.1`): reprocessar o mesmo
      `RawObjectUri`/`SyncRun` não duplica registros canônicos — usa a chave de negócio única
      (E2.3) como upsert, não insert cego.
  - *Depende de:* E2.3, E3.3.
  - *Aceite:* teste republica a mesma mensagem/objeto bruto (mesmo padrão de teste manual usado no
    E7 para provar idempotência do worker) e confirma que a contagem de registros canônicos não muda.

## E4 — Qualidade e Reconciliação

Objetivo: fechar `docs/04 §8` e o critério de saída "falhas de qualidade ficam em quarentena, sem
corromper o DW".

- [ ] **E4.1** Relatório de execução por `SyncRun` (`docs/04 §8.3`): contagens de extraído, aceito,
      atualizado, rejeitado, processado. Estender `SyncRun` (E7) ou nova entidade associada — decidir
      durante a implementação conforme o que fica mais simples sem violar o grão do `SyncRun` já
      existente.
  - *Depende de:* E3.3.
  - *Aceite:* `GET /api/v1/connectors/{id}/sync-runs/{runId}` (endpoint já existente do E7) passa a
    incluir essas contagens na resposta.
- [ ] **E4.2** Endpoint(s) de consulta e reprocessamento de quarentena (`docs/04 §8.2`: "operador
      pode corrigir o mapeamento e reprocessar a carga, mantendo trilha de auditoria"): listar
      entradas de quarentena por tenant/conector/período, e disparar reprocessamento de uma entrada
      específica (reaproveitando a fila assíncrona do E7/E3).
  - *Depende de:* E2.4, E3.3.
- [ ] **E4.3** Reconciliação Canônico↔Origem (`docs/04 §8.3`): para a fatia Comercial, verificação de
      totais (contagem de faturas, soma de `NetAmount`) comparando o relatório do `SyncRun` com o
      que foi de fato persistido no Modelo Canônico.
  - *Depende de:* E4.1, E3.3.
  - *Aceite:* teste prova que uma divergência acima do limite configurado é detectável (mesmo que o
    bloqueio automático de publicação fique para E5/E6 — aqui o requisito mínimo é a verificação
    existir e ser testável).

## E5 — Data Warehouse Inicial (fatia Comercial)

Objetivo: fechar `docs/09-Data-Warehouse.md` para o fato `FactSalesInvoiceItem` e suas dimensões
mínimas, com RLS obrigatória (`docs/09 §4.1`).

- [ ] **E5.1** Novo módulo `EIP.Data.Warehouse` (Domain/Infrastructure), schema `warehouse`.
      Dimensões conformadas mínimas (`docs/09 §5.2`): `DimTenant`, `DimCompany` (com `CountryCode`
      de E2.1), `DimDate` (pré-gerada, calendário), `DimCustomer` (SCD Type 2), `DimProduct` (SCD
      Type 2), `DimProductCategory`, `DimCurrency` (mínima — só código + nome). Convenções
      obrigatórias de `docs/09 §5.1`: chaves substitutas com sufixo `Key`, `TenantKey` obrigatório
      em toda fato/dimensão tenant-scoped, nomes em inglês no modelo físico.
  - *Depende de:* E2.1, E2.2.
  - *Aceite:* migration inicial já nasce com RLS (mesmo padrão de sempre) nas dimensões/fato
    tenant-scoped; `DimDate` não precisa de RLS (dado de referência, não tenant-scoped).
- [ ] **E5.2** Fato `FactSalesInvoiceItem` (`docs/09 §5.3`): grão de uma linha por item de fatura
      emitida; métricas mínimas (quantidade, bruto, desconto, imposto, líquido — custo/margem só se
      disponível na origem, o que não é o caso do conector de referência nesta fase, então ficam de
      fora por enquanto).
  - *Depende de:* E5.1.
- [ ] **E5.3** Processo de carga (`docs/09 §7.1`): staging → resolução de dimensões (aplicando SCD 2
      em `DimCustomer`/`DimProduct` quando o atributo relevante muda) → materialização do fato →
      contagens de reconciliação. Disparado a partir do Modelo Canônico já validado (E3/E4), nunca
      direto da origem.
  - *Depende de:* E5.1, E5.2, E3.3.
  - *Aceite:* teste ponta a ponta: um `SyncRun` reflete em linhas de `FactSalesInvoiceItem`
    rastreáveis até o registro canônico e o objeto bruto correspondentes (critério de saída
    "dado bruto, registro canônico e fato analítico podem ser rastreados entre si").
- [ ] **E5.4** Reconciliação Canônico↔Fato (`docs/09 §8.2`): comparação de contagens/somas entre
      `canonical.SalesInvoiceItem` e `warehouse.FactSalesInvoiceItem` por lote de carga.
  - *Depende de:* E5.3, E4.3.

## E6 — Camada Semântica Mínima

Objetivo: expor as métricas certificadas de exemplo de `docs/09-Data-Warehouse.md §9` para a fatia
Comercial, sem construir um motor de métricas genérico (isso é Fase 2).

- [ ] **E6.1** Definição versionada e testada das 3 métricas certificadas: Receita Líquida
      (soma de `NetAmount` em `FactSalesInvoiceItem`, excluindo documentos cancelados), Quantidade
      Faturada (soma de quantidade dos itens válidos), Ticket Médio (Receita Líquida / contagem
      distinta de faturas válidas). Cada métrica tem dono, versão e teste de reconciliação
      (`docs/09 §9`: "nenhuma métrica é oficial sem definição, dono, versão e teste de
      reconciliação").
- [ ] **E6.2** Endpoint versionado de consulta (`GET /api/v1/metrics/...` ou equivalente, a decidir
      o path exato durante a implementação seguindo `docs/06-API-Design.md`), filtrável por empresa
      e período, respeitando tenant/permissões (nova permissão `metrics.view`?, a definir).
  - *Depende de:* E5.3, E6.1.
  - *Aceite:* teste de isolamento cross-tenant nas métricas (tenant A não vê números de tenant B).

## E7 — Carga Incremental e Reprocessamento

Objetivo: fechar `docs/09 §7.2`/`docs/04 §11` — sincronização incremental, não só carga completa
repetida.

- [ ] **E7.1** Estratégia de watermark por `ConnectorInstance` (`docs/04 §11`: "frequência de
      sincronização, estratégia incremental"): guardar o último `SourceUpdatedAt`/checkpoint
      processado com sucesso, e usá-lo para limitar a próxima extração. Para o conector de
      referência REST genérico (dado estático), isso pode ser validado com um dataset de exemplo
      que varia entre execuções (ex.: parâmetro de "atualizado desde" no endpoint de amostra do E3.1).
  - *Depende de:* E3.1, E3.3.
- [ ] **E7.2** Reprocessamento por período/entidade (`docs/09 §7.2`): capacidade de reconstruir um
      intervalo específico sem apagar dados não relacionados — reaproveita o mecanismo de
      reprocessamento de quarentena do E4.2, generalizado para "reprocessar este período".

## E8 — Testes de Isolamento e Fechamento da Fase

Objetivo: fechar o critério de saída "tenant, empresa, cache, fila e Object Storage preservam
isolamento em testes" e validar a Fase 1 inteira de ponta a ponta, mesmo rigor da revisão que fechou
a Fase 0.

- [ ] **E8.1** Testes cross-tenant automatizados cobrindo Canonical (`Customer`/`SalesInvoice`) e
      Warehouse (`FactSalesInvoiceItem`) — mesmo padrão de
      `ConnectorCrossTenantIsolationTests`/`RlsCoverageTests` criados no fechamento da Fase 0.
  - *Depende de:* E2.2, E5.1.
- [ ] **E8.2** Validação end-to-end real, com infraestrutura de verdade rodando (Host, Gateway,
      Worker(s), SQL Server, RabbitMQ, MinIO): disparar uma sincronização completa e confirmar,
      registro a registro, o rastro `RawObjectUri → registro canônico → linha de fato`, mais as
      contagens de reconciliação batendo em cada etapa.
- [ ] **E8.3** Revisão formal da seção 4 (Definition of Done) desta fase, com evidência por
      critério — mesmo processo aplicado à Fase 0 em 2026-08-01. Atualizar a tabela de rastreamento
      (§7) e a memória do projeto.

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
| E3 — Pipeline de Ingestão e Transformação | Não iniciado |
| E4 — Qualidade e Reconciliação | Não iniciado |
| E5 — Data Warehouse Inicial | Não iniciado |
| E6 — Camada Semântica Mínima | Não iniciado |
| E7 — Carga Incremental e Reprocessamento | Não iniciado |
| E8 — Testes de Isolamento e Fechamento da Fase | Não iniciado |

Atualizar esta tabela conforme os épicos avançam.
