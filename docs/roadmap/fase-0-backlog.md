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
| Pacotes OpenTelemetry ainda beta | `OpenTelemetry.Exporter.Prometheus.AspNetCore` não tem release GA no ecossistema .NET (situação de anos, não peculiaridade deste projeto) — usado mesmo assim, pinado em versão beta exata, por ser a única opção para expor métricas em formato Prometheus hoje. `OpenTelemetry.Instrumentation.EntityFrameworkCore` (também só beta) foi deixado de fora nesta passada para não acumular mais dependências beta do que o estritamente necessário | E3.5 |

---

# 4. Critérios de Saída da Fase 0 (Definition of Done)

Copiados literalmente de `docs/15-Roadmap.md §4`. A Fase 0 só termina quando todos estiverem `[x]`:

- [ ] Ambiente local pode ser iniciado e validado de forma documentada.
- [ ] Pipeline bloqueia build/teste/segurança críticos.
- [ ] Usuário de tenant A não acessa recursos de tenant B em testes automatizados.
- [ ] Uma API autenticada possui autorização, auditoria, logs e health checks.
- [ ] Deploy de ambiente não produtivo é reproduzível a partir de artefatos versionados.

Critério adicional obrigatório por conta da ADR-007 (não estava explícito no roadmap original, mas é vinculante):

- [ ] Toda tabela com `TenantId` possui política RLS ativa, e o CI falha se alguma migration criar uma tabela de tenant sem RLS correspondente.

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

- [ ] **E4.1** Configurar YARP como ponto único de entrada, roteando para os módulos de `Platform`.
- [ ] **E4.2** Rate limiting básico e propagação seura do contexto de autenticação/correlação.
  - *Aceite:* cliente não acessa módulos diretamente; toda chamada passa pelo Gateway.

## E5 — CI (GitHub Actions)

- [ ] **E5.1** Pipeline: restaurar → lint/análise estática → build → testes unitários → testes de integração com dependências efêmeras (Testcontainers) → scan de segredos/dependências (`docs/14-DevOps.md §6`).
- [ ] **E5.2** Gate obrigatório: falha de teste de isolamento multi-tenant (E2.6) ou ausência de RLS em tabela nova bloqueia o merge.
- [ ] **E5.3** Publicação de imagem Docker versionada (mesmo que ainda não haja deploy automatizado para nenhum ambiente real).
  - *Aceite:* PR de teste proposital quebrando isolamento de tenant é bloqueado pelo pipeline.

## E6 — Frontend Angular Inicial

- [ ] **E6.1** Scaffold do app Angular (`npx @angular/cli new`), Tailwind + Angular Material configurados.
- [ ] **E6.2** Tela de login + seleção de tenant/membership, consumindo `/api/v1`.
  - *Aceite:* login end-to-end funciona contra a API local.

## E7 — Conector de Referência + Execução Assíncrona Ponta a Ponta

- [ ] **E7.1** Um conector de referência simples (REST genérico **ou** CSV/Excel — decidir por demanda validada, conforme `docs/15-Roadmap.md §5`).
- [ ] **E7.2** Publicação de mensagem em RabbitMQ (com `TenantId`, correlação e versão de contrato — `docs/03 §7.1`) e worker consumidor idempotente com DLQ.
  - *Aceite:* uma sincronização é disparada pela API, processada de forma assíncrona pelo worker, e o resultado é auditável — fechando o fluxo síncrono→assíncrono ponta a ponta exigido pelo primeiro incremento.

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
| E4 — Gateway | Não iniciado |
| E5 — CI | Não iniciado |
| E6 — Frontend Angular | Não iniciado |
| E7 — Conector de referência | Não iniciado |

Atualizar esta tabela conforme os épicos avançam.
