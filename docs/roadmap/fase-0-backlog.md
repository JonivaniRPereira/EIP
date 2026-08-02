# Backlog de Execução — Fase 0 (Fundação de Engenharia)

**Projeto:** Enterprise Intelligence Platform (EIP)
**Versão:** 1.0
**Status:** Oficial
**Última atualização:** Agosto/2026

---

# 1. Objetivo

Este documento traduz `docs/15-Roadmap.md §4` (Fase 0) e `docs/03-Stack-Tecnologica.md §13` (Primeiro Incremento Executável) em tarefas concretas, ordenadas e verificáveis. Ele é a fonte usada para executar a Fase 0 — cada entrega futura deve pegar a próxima tarefa pendente daqui, implementá-la e marcar como concluída.

Este documento **não substitui** nenhuma regra definida em `docs/00` a `docs/15` ou nas ADRs — ele apenas organiza a ordem de execução. Em caso de conflito, os documentos de arquitetura/segurança/ADR prevalecem.

---

# 2. Como usar este backlog

- Cada tarefa tem um ID (`E<épico>.<sequência>`), descrição, arquivos/projetos afetados, dependências e critério de aceite.
- Marcar `- [x]` somente quando o critério de aceite estiver satisfeito e validado (build/teste passando), nunca por "está quase pronto".
- Épicos são majoritariamente sequenciais (E0 → E1 → E2 → E3 → E4 → E5 → E6 → E7), mas E1 (Docker Compose) pode ser feito em paralelo a E0.
- Toda tarefa que cria uma tabela com `TenantId` é bloqueada até a política RLS correspondente existir e ter teste de acesso cruzado — sem exceção (ADR-007).
- A Fase 0 só é considerada concluída quando a seção 4 (Critérios de Saída) estiver 100% satisfeita, não quando os épicos estiverem "codificados".

---

# 3. Decisões técnicas fixadas para a Fase 0

| Decisão | Valor | Origem |
|---|---|---|
| SDK .NET alvo | .NET 10 (SDK 10.0.302 já instalado na máquina de desenvolvimento) | Confirmado com o usuário em 2026-08 |
| Arquivo de solução | `EIP.slnx` (formato XML novo do `dotnet new sln` no SDK .NET 10; substitui o `.sln` clássico) | Gerado em E0.1 |
| Composition root | `src/Host` (`EIP.Host`, ASP.NET Core com controllers) referencia `.Api`/`.Infrastructure` de cada módulo; é o único processo executável do monólito modular | Criado em E0.2 |
| Frontend | Angular via `npx @angular/cli` (Node 24 / npm 11 instalados; sem CLI global) | `docs/03-Stack-Tecnologica.md §4` |
| Estrutura de código | Monólito modular, Clean Architecture (`Api/Application/Domain/Infrastructure`) por domínio | `docs/00`, `docs/02 §9.2` |
| Mecanismo de RLS | SQL Server `SECURITY POLICY` + função de filtro por `TenantId`, alimentada por `SESSION_CONTEXT('TenantId')`; um `DbCommandInterceptor`/`SaveChangesInterceptor` do EF Core define o `SESSION_CONTEXT` no início de cada unidade de trabalho, a partir do contexto de tenant autenticado (nunca de input do cliente) | `docs/adr/ADR-007`, `docs/08-Multi-Tenant.md §5-6` |
| Bypass controlado de RLS | Sentinela reservada `TenantContext.System` (`00000000-0000-0000-0000-000000000001`) tratada pela função de predicado como "acesso de sistema"; só atribuível por código interno de confiança (ex. `MembershipDirectory` no login), nunca a partir de input de cliente. Usada quando uma operação é legitimamente cross-tenant (`docs/07-Seguranca.md §6.1`) | Migration `AllowSystemTenantBypass`, E2.3/E2.4 |
| CQRS / Mediator | **Sem MediatR** — services de aplicação simples (`IAuthService`, etc.), sem dependência externa. Decisão do usuário: MediatR v13+ exige aceite de licença comercial paga acima de certo faturamento (mesmo modelo do AutoMapper, do mesmo autor), risco legal/financeiro real para SaaS comercial. Revisitar como ADR se um dia fizer sentido pagar pela licença ou adotar alternativa OSS (ex. pacote "Mediator", MIT) | Decisão do usuário em 2026-08, E2.4 |
| Autenticação | ASP.NET Core Identity + emissão própria de JWT (federação OIDC/SAML fica para fase posterior) | `docs/07-Seguranca.md §5.1`, `docs/03 §9.1` |
| Object Storage local | MinIO (S3-compatible) | `docs/03-Stack-Tecnologica.md §6.2` |
| Ambiente local | Docker Compose (confirmado: Docker 29.6.1 / Compose v5.2.0 instalados) | `docs/14-DevOps.md §4` |
| BuildingBlocks x BuildingBlocks.Web | `EIP.BuildingBlocks` fica 100% livre de ASP.NET Core (usável por Domain/Application). Qualquer building block que precise de tipos de ASP.NET Core (ex. `Microsoft.AspNetCore.Authorization`) vai em `EIP.BuildingBlocks.Web` (novo projeto, `FrameworkReference` a `Microsoft.AspNetCore.App`), referenciado só por `.Api`/Host. Misturar as duas coisas no mesmo projeto quebra silenciosamente a descoberta de controllers (ver E2.5) | Descoberto e corrigido em E2.5 |
| RabbitMQ.Client v7 é assíncrono | `AddRabbitMQ(...)` do `AspNetCore.HealthChecks.Rabbitmq` precisa de uma factory `Func<IServiceProvider, Task<IConnection>>` (`new ConnectionFactory{Uri=...}.CreateConnectionAsync()`), não mais uma string de conexão direta — `CreateConnection()` síncrono foi removido no client 7.x | E3.3 |
| Local do frontend no repositório | `frontend/` na raiz (não `src/Frontend`) — `docs/00-Arquitetura-do-Repositorio.md` não define isso (só cobre os domínios de backend em `src/`); segue a estrutura do `README.md` original do produto, que já mostrava `frontend/` como pasta irmã de `src/`/`docs/` | E6.1 |
| CORS vive no Gateway, nunca no Host | O navegador só fala com o Gateway (docs/02 §Gateway) — é ali que `Access-Control-Allow-Origin` precisa existir, com `UseCors` posicionado antes do proxy YARP para responder o preflight OPTIONS sem repassar pro Host (que rejeitaria por falta de auth). Configurável via `Cors:AllowedOrigins` no `appsettings.json` do Gateway | E6.2 |
| Testes de integração usam Testcontainers, não a stack persistente | `tests/Support/EIP.Testing.Infrastructure` sobe SQL Server efêmero (Testcontainers) por execução de teste — local e CI usam o mesmo mecanismo, sem depender do `docker compose` do E1 estar de pé. Padrão: `SqlServerContainerFixture`/`HostApiFixture` + `[Collection]`; `[CollectionDefinition]` precisa ser redeclarado em cada projeto de teste (xUnit resolve isso por assembly) | E5.1 |
| Portas locais | `EIP.Host` em `http://localhost:5080` (interno/direto — health/metrics), `EIP.Gateway` em `http://localhost:5000` (ponto de entrada externo — `/api/**`). Cliente "de fora" deve sempre usar `:5000` | E4.1 |
| Pacotes OpenTelemetry ainda beta | `OpenTelemetry.Exporter.Prometheus.AspNetCore` não tem release GA no ecossistema .NET (situação de anos, não peculiaridade deste projeto) — usado mesmo assim, pinado em versão beta exata, por ser a única opção para expor métricas em formato Prometheus hoje. `OpenTelemetry.Instrumentation.EntityFrameworkCore` (também só beta) foi deixado de fora nesta passada para não acumular mais dependências beta do que o estritamente necessário | E3.5 |
| `EIP.BuildingBlocks.Data` (novo projeto) | O `TenantSessionContextInterceptor` (mecanismo de RLS via `SESSION_CONTEXT`) deixou de viver só em `EIP.Platform.Tenant.Infrastructure` e passou para um projeto compartilhado (sibling de `BuildingBlocks`/`BuildingBlocks.Web`, com `Microsoft.EntityFrameworkCore.Relational`) — qualquer módulo novo com tabelas `TenantId` (Connector, e os que vierem depois) reusa a mesma implementação em vez de copiar o código de segurança mais crítico do projeto | E7.1 |
| Conector de referência é REST genérico, não CSV | Prioridade "Alta" para ambos em `docs/05-Connector-Framework.md §14`, mas REST genérico valida o framework com um contrato mais previsível (sem parsing de arquivo/encoding/layout) — decisão tomada por mim dado que o backlog delegava "decidir por demanda validada" e não há demanda de cliente real ainda nesta fase | E7.1 |
| Connector Registry completo (Draft/Configuring/Validating, Secret Provider, Data Lake) é Fase 1, não Fase 0 | `docs/15-Roadmap.md §5` (Fase 1) lista Connector Framework/Registry/Data Lake como entrega daquela fase; `docs/03-Stack-Tecnologica.md §13` só exige "um conector de referência e uma execução assíncrona ponta a ponta" no primeiro incremento. `ConnectorInstance` do E7 é deliberadamente mínimo (só Active/Paused, sem ciclo de vida completo) | E7.1 |
| `EIP.Worker.Sync` (novo composition root) | Terceiro processo executável do monólito modular (sibling de `Host`/`Gateway`), `src/Worker`, `Microsoft.NET.Sdk.Worker` (Generic Host, não ASP.NET Core — não expõe HTTP). Consome RabbitMQ e define o `SESSION_CONTEXT` a partir do `TenantId` da própria mensagem (nunca de claim JWT, já que não há request HTTP) | E7.2 |
| Fila de sincronização sem retry com backoff, só DLQ direto | E7.2 exige explicitamente "idempotente com DLQ", não retry — implementar backoff exponencial sem o plugin de mensagens atrasadas do RabbitMQ (não instalado) adicionaria complexidade desproporcional ao mínimo da Fase 0. Falha → `BasicNack(requeue: false)` → DLQ imediata, via `x-dead-letter-exchange`/`x-dead-letter-routing-key` na fila principal | E7.2 |
| Sem outbox transacional na publicação do `SyncRun` | Se `IConnectorSyncPublisher.PublishAsync` falhar depois do `SyncRun` já persistido em `Pending`, o run é marcado `Failed` explicitamente (nunca fica `Pending` para sempre sem mensagem alguma publicada) — um outbox transacional de verdade (tabela de outbox + processo relay) é hardening de fase posterior, não bloqueante para provar o fluxo ponta a ponta | E7.2 |
| `identity.RefreshTokens` tem RLS + bypass de sistema, não é exceção | Revisão da §4 (Definition of Done) encontrou, via um teste automatizado novo que varre o catálogo do SQL Server, que `identity.RefreshTokens` tinha uma coluna `TenantId` sem política RLS — violação da ADR-007 não percebida antes por não haver esse gate. Decisão do usuário: aplicar RLS de verdade (não documentar como exceção) — `RefreshTokenStore` agora roda toda operação sob `TenantContext.System` (mesmo bypass do `MembershipDirectory`), já que a busca por hash acontece antes de qualquer tenant estar em contexto. A função de predicado do schema `identity` também deixa passar linhas com `TenantId IS NULL` (tokens ainda sem tenant selecionado) — única diferença em relação aos predicados de `tenant`/`connector`. Precisou de duas migrations (`AddRlsToRefreshTokens` + `AllowSystemBypassOnRefreshTokens`, o segundo corrigindo um esquecimento do bypass no primeiro — mesmo padrão de `AllowSystemTenantBypass` no módulo Tenant) | Revisão da Fase 0, 2026-08-01 |
| Gate automatizado de cobertura de RLS (`RlsCoverageTests`) | Consulta direta ao catálogo do SQL Server (`sys.security_predicates`/`sys.security_policies`/`sys.columns`) via `HostApiFixture`, listando toda tabela com coluna `TenantId` sem uma `SECURITY POLICY` habilitada com FILTER predicate — falha o teste (e portanto o CI) se existir alguma. Fecha o critério da ADR-007 que estava marcado como dívida técnica desde o E5.2 ("não existe hoje um analisador estático..."); não precisa entender migrations C#, só o estado real do banco depois de todas aplicadas | Revisão da Fase 0, 2026-08-01 |
| `docker/worker/Dockerfile` usa `dotnet/runtime`, não `dotnet/aspnet` | `EIP.Worker.Sync` é um Generic Host puro (`Microsoft.NET.Sdk.Worker`), sem Kestrel/HTTP — a imagem final não precisa do runtime ASP.NET Core, só do runtime base do .NET | Revisão da Fase 0, 2026-08-01 |
| `docs/guides/ambiente-local.md` (novo) | Runbook único cobrindo infraestrutura (Compose) + migrations dos 3 módulos + os três processos executáveis (Host/Gateway/Worker) + frontend — fecha o critério "comando/documentação única para iniciar e validar o ambiente" (`docs/14-DevOps.md §4`), que antes só existia implicitamente via `scripts/dev-up.sh` (que cobre só a infraestrutura, não a aplicação) | Revisão da Fase 0, 2026-08-01 |

---

# 4. Critérios de Saída da Fase 0 (Definition of Done)

Copiados literalmente de `docs/15-Roadmap.md §4`. Revisados formalmente em 2026-08-01, ao final do
E7, com evidência para cada item — não apenas "os épicos estão codificados":

- [x] Ambiente local pode ser iniciado e validado de forma documentada.
  - `docs/guides/ambiente-local.md` (novo, ver §3 acima): infraestrutura (`scripts/dev-up.sh`) +
    migrations dos 3 módulos + os três processos executáveis (Host/Gateway/Worker) + frontend, com
    seção de validação (`/health/live`, `/health/ready`, login) e troubleshooting. Seguido do zero
    durante esta revisão (matando e resubindo os três processos várias vezes) — funciona como
    documentado.
- [x] Pipeline bloqueia build/teste/segurança críticos.
  - `.github/workflows/ci.yml`: lint (`dotnet format --verify-no-changes`) → build Release → testes
    (agora 13/13, incluindo o gate de RLS e os dois conjuntos de isolamento cross-tenant) → scan de
    dependências vulneráveis → scan de segredos (gitleaks). Qualquer falha nessas etapas bloqueia o
    job `build-and-test`; `docker-build` só roda se ele passar. Ainda não validado com um push real
    ao GitHub (ver ressalva no rastreamento).
- [x] Usuário de tenant A não acessa recursos de tenant B em testes automatizados.
  - `CrossTenantApiIsolationTests` (Tenant, E2.6) + `ConnectorCrossTenantIsolationTests` (Connector,
    novo nesta revisão) — cobre os dois domínios com recursos tenant-scoped hoje. Ambos rodam contra
    Host real + SQL Server efêmero (Testcontainers), não só a nível de banco.
- [x] Uma API autenticada possui autorização, auditoria, logs e health checks.
  - Autorização por permissão (E2.5), auditoria mínima de autenticação (E2.7 — login/registro/
    lockout; auditoria de ações de negócio como sync fica registrada no próprio `SyncRun`, não numa
    tabela de auditoria separada), Serilog estruturado + OpenTelemetry (E3.4/E3.5), `/health/live` e
    `/health/ready` cobrindo as 3 dependências críticas (E3.3).
- [x] Deploy de ambiente não produtivo é reproduzível a partir de artefatos versionados.
  - As três imagens (`docker/platform`, `docker/gateway`, `docker/worker`) buildam a partir de
    artefatos 100% versionados e foram validadas rodando de verdade contra a rede do
    `docker compose` local nesta revisão (o Worker conectou no RabbitMQ/SQL Server reais do E1).
    Nenhuma publica em registry ainda (aceito — não há registry configurado nesta fase).

Critério adicional obrigatório por conta da ADR-007 (não estava explícito no roadmap original, mas é vinculante):

- [x] Toda tabela com `TenantId` possui política RLS ativa, e o CI falha se alguma migration criar uma tabela de tenant sem RLS correspondente.
  - `RlsCoverageTests` (novo, ver §3 acima) consulta o catálogo do SQL Server depois de todas as
    migrations aplicadas e falha se qualquer tabela com `TenantId` não tiver uma `SECURITY POLICY`
    habilitada — roda no mesmo `dotnet test` do CI. Esta própria revisão encontrou e corrigiu uma
    violação real (`identity.RefreshTokens`, sem RLS) antes de marcar este critério como satisfeito;
    hoje a consulta retorna zero tabelas desprotegidas.

---

# 5. Épicos e Tarefas

## E0 — Scaffolding da Solução

Objetivo: solução .NET compilável, vazia, com a estrutura de pastas/projetos definida em `docs/00-Arquitetura-do-Repositorio.md`.

- [x] **E0.1** Criar `EIP.slnx` na raiz (o `dotnet new sln` do SDK .NET 10 gera o novo formato `.slnx`, não `.sln`) e estrutura `src/BuildingBlocks`, `src/Shared` com projetos de classe mínimos (sem lógica ainda), namespaces `EIP.BuildingBlocks.*` / `EIP.Shared.*`.
  - *Aceite:* `dotnet build` da solução passa. ✅ Concluído.
- [x] **E0.2** Criar projetos `EIP.Platform.Identity` e `EIP.Platform.Tenant` em Clean Architecture (`Api`, `Application`, `Domain`, `Infrastructure` por módulo), únicos domínios exigidos no primeiro incremento (`docs/03 §13`). Inclui `src/Host` (`EIP.Host`, ASP.NET Core com controllers) como processo executável único que referencia os projetos `.Api`/`.Infrastructure` de cada módulo — é o composition root do monólito modular.
  - *Depende de:* E0.1.
  - *Aceite:* build limpo; `Domain` não referencia `Infrastructure`; `Api` não referencia `Domain` diretamente (passa por `Application`). ✅ Concluído — `dotnet build EIP.slnx` compila os 11 projetos sem erros.
- [x] **E0.3** Configurar `Directory.Build.props`/`.editorconfig` com nullable enable, warnings como erro para regras críticas, e análise estática básica (ex.: analisadores do .NET).
  - *Aceite:* `dotnet build` reporta avisos de estilo/nulidade quando violado. ✅ Concluído — `Directory.Build.props` centraliza `TargetFramework`/`Nullable`/`AnalysisLevel`; `WarningsAsErrors=Nullable`; `.editorconfig` define nomenclatura e trata `CS86xx` (nulidade) como erro.
- [x] **E0.4** Criar `.gitignore` apropriado para .NET + Angular (bin/obj/node_modules/.env) — hoje o repositório não tem nenhum `.gitignore`.
  - *Aceite:* `git status` não lista artefatos de build depois de compilar. ✅ Concluído e verificado com `git status`.

## E1 — Infraestrutura Local (Docker Compose)

Objetivo: dependências locais sobem com um único comando, com healthchecks (`docs/14-DevOps.md §4`).

- [x] **E1.1** `deploy/docker-compose/docker-compose.yml` (local definido pela estrutura oficial de `docs/00`) subindo SQL Server (`2022-CU14-ubuntu-22.04`), Redis (`7.4-alpine`), RabbitMQ (`3.13-management-alpine`) e MinIO (`RELEASE.2025-04-08T15-41-24Z`), todas as imagens com tag pinada (não `latest`) e healthcheck próprio.
  - *Aceite:* `docker compose up -d` sobe os 4 serviços e todos reportam `healthy`. ✅ Validado em 2026-08: `eip-sqlserver`, `eip-redis`, `eip-rabbitmq`, `eip-minio` todos `Up (healthy)`.
- [x] **E1.2** `deploy/docker-compose/.env.example` com todas as variáveis necessárias, valores claramente marcados como "dev only", sem nenhum segredo real (`docs/07-Seguranca.md §8`).
- [x] **E1.3** `scripts/dev-up.sh`: copia `.env.example` → `.env` se faltar, sobe o Compose e faz polling de `docker inspect --format='{{.State.Health.Status}}'` até todos os serviços ficarem `healthy` (com timeout), antes de considerar o ambiente pronto para a API subir.
  - *Aceite:* segue exatamente os passos documentados, do zero, e funciona.

## E2 — Identity & Tenant + RLS Obrigatória

Objetivo: fluxo de autenticação + isolamento de tenant realmente aplicado no banco, não só na aplicação. Este é o épico de maior risco arquitetural.

- [x] **E2.1** Modelar entidades mínimas de `docs/08-Multi-Tenant.md §4`: `Tenant`, `Membership`, `Company` (sem `Branch`/`Workspace` ainda — fora do primeiro incremento).
  - *Aceite:* migration inicial gerada e revisada. ✅ Concluído — `EIP.Platform.Tenant.Domain` (`Tenant`, `Company`, `Membership`, enums de status), `EIP.BuildingBlocks.DDD.Entity<TId>`.
- [x] **E2.2** Migration inicial já nasce com `TenantId` em toda tabela de escopo de tenant **e** `CREATE SECURITY POLICY` com função de filtro correspondente — nunca uma migration "só de schema" seguida depois por outra "de RLS".
  - *Depende de:* E2.1, E1.1 (banco disponível).
  - *Aceite:* rodar a migration cria a tabela **e** a policy na mesma transação/script; consultar a tabela sem `SESSION_CONTEXT` definido não retorna linhas de nenhum tenant. ✅ Concluído e validado com SQL bruto direto no `eip-sqlserver`: sem contexto → 0 linhas; com contexto do Tenant A → só a empresa do Tenant A; `INSERT` com `TenantId` divergente do contexto → rejeitado pelo block predicate (erro 33504). Migration: `src/Platform/Tenant/Infrastructure/Migrations/20260801163802_InitialCreate.cs`. Tenant (schema `tenant`, tabelas `Companies`/`Memberships`) tem `ADD FILTER PREDICATE` + `ADD BLOCK PREDICATE ... AFTER INSERT/UPDATE`; `Tenants` não é protegida por RLS (ela É o tenant).
- [x] **E2.3** Implementar o interceptor EF Core que injeta `SESSION_CONTEXT('TenantId', @tenantId)` a partir do contexto autenticado — nunca de `TenantId` vindo do body/query/header não validado (`docs/08 §5.1`). *(Tenant/Connection Resolver completo, escolhendo Shared vs Dedicated, fica para quando o modo Dedicated for implementado — fora do escopo da Fase 0 per §6 deste documento.)*
  - *Depende de:* E2.2.
  - *Aceite:* teste de integração comprova que uma query sem contexto de tenant autenticado não executa. ✅ Concluído — `TenantSessionContextInterceptor` (`DbConnectionInterceptor`) + `ITenantContextAccessor`/`AsyncLocalTenantContextAccessor` em `EIP.BuildingBlocks.Security`. 4 testes reais em `tests/Integration/EIP.Platform.Tenant.Infrastructure.IntegrationTests` (via EF Core contra o SQL Server do E1, não SQL bruto) comprovam: sem contexto → 0 linhas; com contexto do Tenant A → só suas linhas; filtro explícito por `TenantId` de outro tenant → 0 linhas; insert com tenant divergente → `DbUpdateException`. `dotnet test` → 4/4 aprovados.
- [x] **E2.4** ASP.NET Core Identity + emissão de JWT (claims incluindo `TenantId`/membership ativa), endpoints de login e refresh.
  - *Depende de:* E2.1.
  - *Aceite:* login retorna JWT válido; token expira conforme política curta definida em `07-Seguranca.md §5.1`. ✅ Concluído e validado end-to-end contra o Host real (não só unitário):
    - `EIP.Platform.Identity.Domain`: `ApplicationUser : IdentityUser<Guid>`, `RefreshToken` (hash apenas, nunca o valor bruto, com `TenantId` para preservar o claim entre refreshes).
    - `EIP.Platform.Identity.Infrastructure`: `AppIdentityDbContext` (schema `identity`, sem RLS — usuário não é escopado por tenant), `JwtTokenGenerator` (HMAC-SHA256, claim customizado `tenant_id`), `RefreshTokenStore` (hash SHA-256, rotação).
    - `EIP.Platform.Identity.Application`: `IAuthService`/`AuthService` — **sem MediatR** (decisão do usuário, ver §3: a v13+ do MediatR exige licença comercial paga acima de faturamento; optou-se por services de aplicação simples).
    - `EIP.Platform.Identity.Api`: `AuthController` com `POST /api/v1/auth/{register,login,refresh,select-tenant}`, erros em `ProblemDetails`.
    - **Contrato cross-domain**: `EIP.Shared.Contracts.Tenancy.IMembershipDirectory` (Identity nunca acessa a persistência do Tenant diretamente — `docs/02 §9.2`), implementado por `MembershipDirectory` no módulo Tenant usando `IDbContextFactory` + a sentinela `TenantContext.System` (bypass de RLS controlado e documentado — ver ADR-007 e a migration `AllowSystemTenantBypass`) para resolver "quais tenants este usuário pertence" antes de qualquer `TenantId` ser conhecido.
    - Login com **0** memberships ativas → token sem `tenant_id`, `requiresTenantSelection=true`, lista vazia. Com **exatamente 1** → seleciona automaticamente. Com **2+** → exige seleção explícita (`docs/08 §5.2`), retornando as opções.
    - Testado manualmente ponta a ponta contra o Host rodando com o SQL Server do E1: register → login (auto-seleção) → refresh (rotação confirmada; reuso do token antigo rejeitado) → cenário com 2 tenants → `select-tenant` rejeita tenant que o usuário não pertence e aceita o correto.
    - Lockout habilitado via `UserManager` (5 tentativas, 5 min) — brute force (`docs/07-Seguranca.md §5.1`).
- [x] **E2.5** Autorização por permissão + escopo (`Identidade + Tenant + Workspace + Empresa + Recurso + Ação`, simplificado para o que existe no MVP) com "negar por padrão".
  - *Depende de:* E2.4. ✅ Concluído e validado manualmente contra o Host real:
    - `Membership.Role` (`MembershipRole`: Owner/Admin/Member) + `EIP.Shared.Contracts.Tenancy.RolePermissions`/`TenantPermissions` (mapeamento papel→permissões; vive em Shared porque o domínio Identity precisa resolvê-lo sem depender do domínio Tenant).
    - Claim `permissions` (comma-separated) embutida no JWT em login/select-tenant/refresh — sempre **resolvida de novo** a cada emissão (nunca copiada), então uma mudança de papel/revogação de membership reflete no próximo refresh.
    - `RequirePermissionAttribute` + `PermissionAuthorizationHandler` + `PermissionAuthorizationPolicyProvider` (policy dinâmica `permission:{nome}`) — nega por padrão se a claim não contiver a permissão.
    - `FallbackPolicy` global exige autenticação para qualquer endpoint sem `[Authorize]`/`[AllowAnonymous]` explícito — inclusive endpoints de infraestrutura como `/openapi/*` (ver nota de arquitetura abaixo).
    - Endpoint de referência `GET /api/v1/tenants/{tenantId}` (`TenantsController`) exige `RequirePermission(TenantPermissions.TenantView)` **e** valida explicitamente que o `tenantId` da rota bate com o claim `tenant_id` do token — RLS bloqueia no banco, isto bloqueia na API (defesa em profundidade).
    - **Achado de arquitetura importante:** tipos de autorização do ASP.NET Core (`AuthorizeAttribute`, `IAuthorizationHandler` etc.) não podem viver em `EIP.BuildingBlocks` (classlib sem `FrameworkReference`) referenciando o pacote NuGet solto `Microsoft.AspNetCore.Authorization` — isso cria um conflito de identidade de assembly com a cópia do framework compartilhado usada pelos projetos `.Api` (que têm `FrameworkReference`), fazendo o ASP.NET Core **descartar silenciosamente** (sem erro, sem log) qualquer controller que referencie esses tipos. Corrigido criando `EIP.BuildingBlocks.Web` (novo projeto, COM `FrameworkReference`, referenciado só por `.Api`/Host — nunca por Domain/Application) para tudo que depende de ASP.NET Core. Ver `AuthorizationPolicyProvider`/`AuthorizationHandler`/`RequirePermissionAttribute` lá.
- [x] **E2.6** Testes automatizados obrigatórios de isolamento cross-tenant (`docs/08 §13`): usuário do tenant A não lista/lê/atualiza/exclui recurso do tenant B, mesmo com ID adulterado no payload.
  - *Depende de:* E2.2, E2.3, E2.5.
  - *Aceite:* suíte de teste dedicada roda no CI e falha o build se algum teste de isolamento falhar. **Este teste é o gate mínimo para considerar E2 concluído.** ✅ Concluído — `tests/Integration/EIP.Host.IntegrationTests` (`WebApplicationFactory<Program>`, HTTP real, não SQL bruto): usuário A lendo `GET /api/v1/tenants/{tenantIdDeB}` → 403 mesmo com o ID adulterado na rota; sem token → 401; `select-tenant` para tenant que não pertence → falha. 4/4 aprovados. Precisou expor `public partial class Program;` no `Program.cs` do Host (top-level statements geram uma classe implícita `internal`).
- [x] **E2.7** Auditoria mínima: login, falha de login, criação/alteração de membership (`docs/07-Seguranca.md §11.2`).
  - ✅ Concluído (parcial, por design): `AuditEvent` (schema `identity`, sem RLS — não é dado de tenant) registra `UserRegistered`, `LoginSucceeded`, `LoginFailed` (usuário não encontrado / senha errada, mesma mensagem genérica) e `LoginLockedOut`. Confirmado via os próprios testes automatizados (linhas gravadas durante os testes do E2.6). Auditoria de criação/alteração de **membership** fica pendente até existir um endpoint de gestão de membership (fora do escopo de E2, que é só autenticação) — não inventei um endpoint só para isso.

## E3 — API Versionada e Observabilidade Básica

- [x] **E3.1** Prefixo `/api/v1`, `ProblemDetails` para erros padronizados, DTOs não expõem entidades de domínio.
  - ✅ Concluído. `AddProblemDetails()` + `UseExceptionHandler()` cobrem exceções não tratadas (500) e validação automática do `[ApiController]` (400) com o mesmo formato; `CustomizeProblemDetails` injeta `traceId`/`correlationId`. `TenantsController` passou a devolver `TenantDto` (era objeto anônimo) — nenhum controller expõe entidade de domínio.
- [x] **E3.2** OpenAPI publicado e versionado junto do código.
  - ✅ Concluído. `AddOpenApi()`/`MapOpenApi()` (já existiam desde E0) + `.AllowAnonymous()` explícito, já que o `FallbackPolicy` do E2.5 passou a exigir autenticação por padrão em qualquer endpoint sem anotação — sem isso o próprio doc de API ficava atrás de login. Validado: `GET /openapi/v1.json` → 200 sem token.
- [x] **E3.3** Health checks (liveness/readiness) cobrindo dependências críticas (SQL Server, Redis, RabbitMQ).
  - ✅ Concluído. `/health/live` nunca depende de dependência externa (`Predicate = _ => false`, só reporta o processo vivo — não derruba por uma dependência instável). `/health/ready` roda os checks tagueados `ready`: `tenant-db`, `identity-db` (SQL Server via `AspNetCore.HealthChecks.SqlServer`), `redis` (`AspNetCore.HealthChecks.Redis` + `StackExchange.Redis`), `rabbitmq` (`AspNetCore.HealthChecks.Rabbitmq` + `RabbitMQ.Client` 7.x — API mudou para conexão assíncrona, então o registro usa uma factory `Func<IServiceProvider, Task<IConnection>>`, não mais string de conexão direta). Resposta em JSON custom (`HealthCheckResponseWriter`) listando status por dependência. **Validado de verdade**: parei o container `eip-rabbitmq` → `/health/ready` foi a 503 com `rabbitmq: Unhealthy` (as outras 3 dependências continuaram `Healthy`); religuei o container → voltou a `Healthy` depois de alguns segundos.
- [x] **E3.4** Middleware de `CorrelationId` (aceito ou gerado), propagado em logs.
  - ✅ Concluído. Primeiro middleware do pipeline: usa `X-Correlation-Id` do request se presente, senão gera um novo; devolve no header de resposta; empurra para o `Serilog.Context.LogContext` para aparecer em toda linha de log da requisição. Validado nos logs reais (ver E3.5).
- [x] **E3.5** Logs estruturados via Serilog; traces/métricas básicos via OpenTelemetry.
  - ✅ Concluído. Serilog (`Serilog.AspNetCore` + `Serilog.Sinks.Console`) com `UseSerilogRequestLogging()` — uma linha estruturada por requisição, com `CorrelationId` visível. OpenTelemetry (`AddAspNetCoreInstrumentation` + `AddHttpClientInstrumentation`) para traces (exportados via console — sem coletor na Fase 0) e métricas expostas em `/metrics` via `OpenTelemetry.Exporter.Prometheus.AspNetCore` (**pacote ainda beta** — o exporter Prometheus do ecossistema OpenTelemetry .NET não tem GA disponível; é o único caminho hoje). Instrumentação de EF Core (`OpenTelemetry.Instrumentation.EntityFrameworkCore`) também só existe em beta e foi deixada de fora nesta passada para não acumular mais dependências beta do que o necessário.
  - *Aceite combinado (E3.1–E3.5):* uma chamada autenticada em `/api/v1/...` aparece nos logs com `CorrelationId`, retorna erro em formato `ProblemDetails` quando aplicável, e os health checks respondem `healthy`/`unhealthy` corretamente quando uma dependência cai. ✅ Validado de ponta a ponta com o Host rodando de verdade (ver detalhes acima em cada subtarefa).

## E4 — Gateway (YARP)

- [x] **E4.1** Configurar YARP como ponto único de entrada, roteando para os módulos de `Platform`.
  - ✅ Concluído. Novo projeto `src/Gateway` (`EIP.Gateway`, `Yarp.ReverseProxy` 2.3.0), roteando `/api/{**catch-all}` para o `EIP.Host` (`ReverseProxy:Clusters:platform-cluster` em `appsettings.json`). Health checks/métricas (`/health/*`, `/metrics`) **não** passam pelo Gateway — ficam acessíveis só direto no Host, para infra (probes/scraper), não clientes; validado que `/health/live` via Gateway dá 404 (rota simplesmente não existe lá).
- [x] **E4.2** Rate limiting básico e propagação segura do contexto de autenticação/correlação.
  - *Aceite:* cliente não acessa módulos diretamente; toda chamada passa pelo Gateway. ✅ Concluído e validado com Host+Gateway rodando de verdade:
    - Rate limiting: `AddRateLimiter` com `PartitionedRateLimiter` por IP (fixed window, 100 req/10s, `QueueLimit=0`, `RejectionStatusCode=429`) — nativo do ASP.NET Core, sem pacote extra. Disparei 110 requisições rápidas: 104 passaram, 6 voltaram `429`.
    - `CorrelationId`: aceito ou **gerado no Gateway** (o ponto de entrada real — `docs/08-Multi-Tenant.md §5.1`, diferente do fallback que já existia no Host desde o E3 para acesso direto em dev), propagado ao Host via header antes do proxy. Setar o header de resposta só em `Response.OnStarting` (depois do `next()`) evita duplicar o header quando o YARP já copiou o do Host.
    - `Authorization`: YARP encaminha por padrão, sem configuração extra — validado chamando `/api/v1/auth/select-tenant` autenticado via Gateway e recebendo a resposta de negócio correta do Host (não um 401 de "token ausente").
    - Serilog + `UseSerilogRequestLogging()` também no Gateway, consistente com o Host.

## E5 — CI (GitHub Actions)

- [x] **E5.1** Pipeline: restaurar → lint/análise estática → build → testes unitários → testes de integração com dependências efêmeras (Testcontainers) → scan de segredos/dependências (`docs/14-DevOps.md §6`).
  - ✅ Concluído. `.github/workflows/ci.yml`, job `build-and-test`: `dotnet restore` → `dotnet format --verify-no-changes` (lint) → `dotnet build -c Release` → `dotnet test -c Release` → scan de dependências (`dotnet list package --vulnerable`) → scan de segredos (gitleaks CLI, binário oficial baixado direto do GitHub Releases — não a Action de marketplace, que exige licença para repositório privado).
  - **Refactor grande necessário para viabilizar isto**: os dois projetos de teste de integração (E2.3 e E2.6) dependiam de uma connection string fixa (`localhost,1433`), assumindo a stack persistente do E1 já rodando — isso não existe num runner de CI. Criado `tests/Support/EIP.Testing.Infrastructure` com `SqlServerContainerFixture` (Testcontainers, mesma imagem do E1) + `DatabaseMigrator` (aplica as migrations de Tenant e Identity, incluindo a RLS). `EIP.Host.IntegrationTests` ganhou `HostApiFixture` combinando o container efêmero com um `WebApplicationFactory<Program>` configurado via `ConfigureAppConfiguration` — Redis/RabbitMQ recebem strings sintaticamente válidas mas inalcançáveis, já que os 4 testes ali não chamam `/health/ready`. **Validado de verdade** (não só "buildou"): monitorei `docker ps` durante a execução e confirmei um container SQL Server novo (nome aleatório, porta dinâmica) subindo e sendo descartado ao final, sem tocar a stack do E1.
  - Rodar `dotnet format --verify-no-changes` como gate revelou drift real (CRLF/charset nas migrations geradas pelo EF, e uma regra de nomenclatura do `.editorconfig` (E0.3) incorretamente aplicada a campos `const`/`static readonly`, que devem ser PascalCase, não `_camelCase`). Corrigido o `.editorconfig` e rodado `dotnet format` uma vez para zerar o débito antes de tornar o check bloqueante.
  - Scan de segredos encontrou 1 falso positivo (`docs/06-API-Design.md`, um GUID de exemplo interpretado como possível API key por entropia) — corrigido trocando por um placeholder (`<uuid-gerado-pelo-cliente>`) em vez de suprimir a regra.
- [x] **E5.2** Gate obrigatório: falha de teste de isolamento multi-tenant (E2.6) ou ausência de RLS em tabela nova bloqueia o merge.
  - ✅ Parcial, por design: a falha do E2.6 já bloqueia o pipeline automaticamente (está na mesma suíte de testes do job `build-and-test`). **Não existe** hoje um analisador estático que detecte "migration nova criou tabela com `TenantId` sem `CREATE SECURITY POLICY` correspondente" — isso dependeria de uma ferramenta própria de lint de migrations, fora do escopo desta passada. Na prática, a proteção real é: todo novo domínio tenant-scoped deve vir com um teste de isolamento no estilo do E2.3/E2.6 (senão o gap fica invisível). Registrar como dívida técnica caso vire um problema recorrente.
  - **Fora do escopo desta entrega**: configurar a branch protection do GitHub exigindo o workflow como status check obrigatório — é uma configuração do repositório no GitHub (não um arquivo do código), fica para quando o repo for de fato publicado/protegido a pedido do usuário.
- [x] **E5.3** Publicação de imagem Docker versionada (mesmo que ainda não haja deploy automatizado para nenhum ambiente real).
  - *Aceite:* PR de teste proposital quebrando isolamento de tenant é bloqueado pelo pipeline. ✅ Concluído — `docker/platform/Dockerfile` (`EIP.Host`) e `docker/gateway/Dockerfile` (`EIP.Gateway`), multi-stage (`sdk:10.0` → `aspnet:10.0`), usuário não-root (`app`, já vem pronto na imagem oficial — não precisa criar). Tag pela `github.sha` no CI, **build apenas, sem push** para nenhum registry (nenhum configurado ainda; ver nota abaixo). Job `docker-build` separado, depende do `build-and-test` passar primeiro. **Validado de verdade**: rodei o container do Host conectado à rede do `docker compose` do E1 (`--network eip-local`) com as connection strings certas — `/health/ready` respondeu `Healthy` para as 4 dependências e `/api/v1/auth/login` respondeu com `ProblemDetails` correto.
  - Nada foi commitado/enviado ao GitHub nesta sessão — o workflow só roda de verdade após um push/PR real.

## E6 — Frontend Angular Inicial

- [x] **E6.1** Scaffold do app Angular (`npx @angular/cli new`), Tailwind + Angular Material configurados.
  - ✅ Concluído. `frontend/` (Angular 22, standalone, `provideRouter`/`provideHttpClient`, Vitest como test runner padrão do CLI). Tailwind 4 via `@tailwindcss/postcss` + `.postcssrc.json` (import em `styles.scss` **depois** do `@use '@angular/material'` — Sass exige que `@use` venha antes de qualquer outra regra). Angular Material via `ng add @angular/material`. `npm audit` aponta 3 vulnerabilidades moderadas, todas em `@hono/node-server` (dependência transitiva do MCP SDK que o próprio `@angular/cli` 22 empacota para sua feature de integração com assistentes de IA) — é `devDependency`, nunca embarcada no bundle publicado, e a correção automática forçaria downgrade do CLI; decidido não agir por ora (risco real baixo, dev-only).
- [x] **E6.2** Tela de login + seleção de tenant/membership, consumindo `/api/v1`.
  - *Aceite:* login end-to-end funciona contra a API local. ✅ Concluído.
    - `core/auth/auth.ts` (`AuthService`, com o novo decorator `@Service()` do Angular 22 — sinônimo moderno de `@Injectable()`, é o que o schematic do CLI já gera por padrão) guarda o par access/refresh token no `localStorage` (simplificação aceita para o MVP; cookie `HttpOnly` emitido pelo Gateway fica para um endurecimento de segurança futuro do frontend) e deriva `requiresTenantSelection`/`tenantId` decodificando o próprio JWT (claim `tenant_id`), não de estado à parte — resiste a um F5 no meio da sessão.
    - `auth-interceptor.ts` anexa `Authorization: Bearer` só em chamadas para `environment.apiBaseUrl`; em 401 fora de `/auth/login`/`/auth/refresh`, tenta um refresh único e repete a requisição.
    - Telas: `login`, `select-tenant` (mostra as opções vindas da resposta de login/registro; lista vazia após um F5 no meio do fluxo é aceita como limitação do MVP — o usuário refaz login) e `dashboard` (busca o tenant atual via `GET /api/v1/tenants/{tenantId}`, o endpoint de referência do E2.5). Guards: `authGuard` (só exige token) e `tenantGuard` (exige token **e** tenant selecionado, redirecionando para `/select-tenant` senão).
    - **Bug real encontrado e corrigido**: não havia CORS configurado em lugar nenhum. Testado com `curl -X OPTIONS` simulando o preflight do navegador contra o Gateway: o Host respondia `401` (o `FallbackPolicy` do E2.5 rejeitava o preflight antes de qualquer coisa, já que ele não carrega header de autenticação). Corrigido adicionando `AddCors`/`UseCors` **no Gateway** (não no Host — é o Gateway que o navegador acessa diretamente), com `UseCors` posicionado antes do `UseRateLimiter`/`MapReverseProxy` para responder o preflight sem sequer proxiar para o Host. Origens permitidas configuráveis via `Cors:AllowedOrigins` (`appsettings.json`), `http://localhost:4200` por padrão.
    - **Validado de verdade** (Host + Gateway + SQL Server + `ng serve`, todos rodando simultaneamente): script Node com `fetch` reproduzindo exatamente as chamadas do `AuthService` (register → login) através do Gateway, com header `Origin` simulando o navegador — confirmado `200`, formato de resposta batendo com as interfaces TypeScript, e header `Access-Control-Allow-Origin` presente na resposta real (não só no preflight). `ng build` e `ng test` (Vitest, 8 arquivos/9 testes) passam limpos.
    - **Limitação honesta**: não há ferramenta de automação de navegador neste ambiente — não foi possível clicar de fato nas telas (login → seleção de tenant → dashboard) num navegador real. O que foi validado é toda a cadeia HTTP real que o `AuthService` executa, mais a compilação/lint/testes unitários do Angular. Recomenda-se abrir `http://localhost:4200` manualmente para confirmar a renderização visual antes de considerar o épico 100% fechado.

## E7 — Conector de Referência + Execução Assíncrona Ponta a Ponta

- [x] **E7.1** Um conector de referência simples (REST genérico **ou** CSV/Excel — decidir por demanda validada, conforme `docs/15-Roadmap.md §5`).
  - ✅ Concluído. Escolhido REST genérico (docs/05-Connector-Framework.md §14: "alta prioridade inicial, validar o framework com contrato previsível"). Novo módulo `EIP.Platform.Connector` (Domain/Application/Infrastructure/Api, mesma Clean Architecture dos outros domínios):
    - `Domain`: `ConnectorInstance` (Id, TenantId, Name, BaseUrl, Status Active/Paused) e `SyncRun` (Id, TenantId, ConnectorInstanceId, CorrelationId, Status Pending/Running/Succeeded/Failed, RecordsProcessed, ErrorMessage, timestamps) — o `SyncRun` É o registro de auditoria da execução, sem tabela de auditoria separada.
    - Schema `connector`, protegido por RLS obrigatória (ADR-007) na mesma migration que cria as tabelas — `connector.fn_TenantAccessPredicate`/`connector.ConnectorAccessPolicy`, função e policy próprias do schema (não reaproveita a de `tenant`; RLS do SQL Server é declarada por schema).
    - `POST /api/v1/connectors` (registra a instância — substitui, na Fase 0, o Connector Registry completo do framework, que é escopo de Fase 1), `POST /api/v1/connectors/{id}/sync` (dispara, 202 Accepted), `GET /api/v1/connectors/{id}/sync-runs/{runId}` (relatório de execução). Novas permissões `connector.view`/`connector.manage` (`EIP.Shared.Contracts.Connectors.ConnectorPermissions`), concedidas a Owner/Admin (manage+view) e Member (só view).
    - Fonte de dados de referência: `GET /api/v1/sample/customers` no próprio Host (retorna um array JSON estático de 5 registros fake) — um stand-in local para "sistema externo", só para provar o fluxo ponta a ponta sem depender de internet/terceiros; não é feature de produto.
  - **Refactor pré-requisito**: `TenantSessionContextInterceptor` (o mecanismo de RLS via `SESSION_CONTEXT`, antes vivendo só em `EIP.Platform.Tenant.Infrastructure`) foi extraído para um novo projeto `EIP.BuildingBlocks.Data` (sibling de `BuildingBlocks`/`BuildingBlocks.Web`, com `Microsoft.EntityFrameworkCore.Relational`), para que o módulo Connector reusasse a mesma implementação em vez de copiar o código de segurança mais crítico do projeto.
- [x] **E7.2** Publicação de mensagem em RabbitMQ (com `TenantId`, correlação e versão de contrato — `docs/03 §7.1`) e worker consumidor idempotente com DLQ.
  - *Aceite:* uma sincronização é disparada pela API, processada de forma assíncrona pelo worker, e o resultado é auditável — fechando o fluxo síncrono→assíncrono ponta a ponta exigido pelo primeiro incremento. ✅ Concluído e **validado de ponta a ponta com infraestrutura real** (Host + Gateway + SQL Server + RabbitMQ + Worker, todos rodando simultaneamente):
    - `SyncRequestedMessage` (`SyncRunId`, `ConnectorInstanceId`, `TenantId`, `CorrelationId`, `ContractVersion="1.0"`) — `ConnectorSyncService.RequestSyncAsync` valida a instância, persiste o `SyncRun` em `Pending` e publica **antes** de retornar 202 (nunca processa de forma síncrona no request).
    - Novo projeto executável `src/Worker` (`EIP.Worker.Sync`, Generic Host / `Microsoft.NET.Sdk.Worker`) — terceiro composition root do monólito modular, sibling de `Host`/`Gateway`. `SyncRequestedConsumerService` (`BackgroundService`) consome `connector.sync.requested`, define o `SESSION_CONTEXT` a partir do `TenantId` da mensagem (via `ITenantContextAccessor`, o mesmo papel do middleware do Host a partir do claim JWT) e delega a `ConnectorSyncProcessor` (Application, não conhece RabbitMQ).
    - Topologia RabbitMQ (`ConnectorMessagingTopology`, declarada idempotentemente tanto pelo publisher quanto pelo worker): exchange `eip.connector` (direct) → fila `connector.sync.requested` com `x-dead-letter-exchange`/`x-dead-letter-routing-key` apontando para `connector.sync.requested.dlq`. Falha no processamento → `BasicNack(requeue: false)` → cai direto na DLQ (sem retry com backoff — fora do mínimo exigido por E7.2, que só pede idempotência + DLQ).
    - **Idempotência**: `SyncRun.TryStartProcessing()` só avança `Pending → Running` uma única vez; reentregas (RabbitMQ é at-least-once) de um run já `Running`/terminal são identificadas e puladas (ack sem reprocessar). **Validado de verdade**: republiquei manualmente (via API de management do RabbitMQ) a mesma mensagem de um `SyncRun` já `Succeeded` — o worker fez só o `SELECT`, sem nova chamada HTTP nem `UPDATE`, e o `FinishedAt` continuou idêntico ao da primeira execução.
    - **DLQ validado de verdade**: registrei uma instância com `BaseUrl` inalcançável (`http://localhost:59999/...`), disparei a sincronização — `SyncRun` foi para `Failed` com a mensagem de erro real (conexão recusada) e a mensagem apareceu na fila `connector.sync.requested.dlq` (confirmado via `GET /api/queues/.../connector.sync.requested.dlq` da API de management: `messages: 1`).
    - **Caminho feliz validado de verdade**: `POST /api/v1/connectors` → `POST .../sync` (202) → worker chama `GET /api/v1/sample/customers` de verdade via `HttpClient` → `SyncRun` vira `Succeeded` com `recordsProcessed: 5` (contagem real do array JSON) em ~300ms.
    - Defesa em profundidade replicada do padrão do `TenantsController` (E2.5): `ConnectorSyncProcessor` nunca confia cegamente no `TenantId`/`ConnectorInstanceId` da mensagem — compara explicitamente `instance.TenantId == message.TenantId` além do que a RLS já garante.
    - `dotnet test` (suíte completa, Testcontainers): 9/9 aprovados após o refactor (`DatabaseMigrator` passou a migrar também `ConnectorDbContext`). `dotnet format --verify-no-changes`: limpo (logs do worker usam `[LoggerMessage]` source-generated, não chamadas diretas de `ILogger`, para não introduzir avisos `CA1848`/`CA1873` novos no gate do CI).

---

# 6. Fora do Escopo da Fase 0

Para não perder o foco (`docs/15-Roadmap.md §3`), os itens abaixo são explicitamente adiados:

- Workspace, Branch, múltiplas empresas por tenant (só Tenant → Company no MVP).
- Banco dedicado por tenant (modo `Dedicated`) — só `Shared` na Fase 0.
- Kubernetes/Helm (Docker Compose é suficiente até haver necessidade operacional real).
- Dashboard Builder, Analytics Engine, AI Engine, Automation Engine.
- Marketplace, SDK de conectores, white label.

---

# 7. Rastreamento

| Épico | Status |
|---|---|
| E0 — Scaffolding da Solução | ✅ Concluído (2026-08) |
| E1 — Infraestrutura Local | ✅ Concluído (2026-08) |
| E2 — Identity & Tenant + RLS | ✅ Concluído (2026-08) |
| E3 — API versionada e observabilidade | ✅ Concluído (2026-08) |
| E4 — Gateway | ✅ Concluído (2026-08) |
| E5 — CI | ✅ Concluído (2026-08) — pendente apenas primeiro push real para validar no GitHub de verdade |
| E6 — Frontend Angular | ✅ Concluído (2026-08) — confirmado visualmente no navegador (login → dashboard) pelo usuário |
| E7 — Conector de referência | ✅ Concluído (2026-08) |

**Fase 0 — Definition of Done (§4): ✅ revisada e satisfeita em 2026-08-01.** Todos os 6 critérios
(5 do roadmap + o adicional da ADR-007) verificados com evidência nesta revisão, incluindo a correção
de uma violação real de RLS encontrada pelo próprio gate automatizado novo (`identity.RefreshTokens`).
Pendências conhecidas, não bloqueantes: (1) nada foi commitado/enviado ao GitHub ainda nesta sessão —
o CI nunca rodou de verdade num runner; (2) sem analisador estático de "migration nova sem RLS" (E5.2)
— a proteção real hoje é o `RlsCoverageTests` rodando contra o schema já aplicado, não uma checagem
estática do C# da migration em si.

Atualizar esta tabela conforme os épicos avançam.
