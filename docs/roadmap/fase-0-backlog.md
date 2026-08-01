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
| Frontend | Angular via `npx @angular/cli` (Node 24 / npm 11 instalados; sem CLI global) | `docs/03-Stack-Tecnologica.md §4` |
| Estrutura de código | Monólito modular, Clean Architecture (`Api/Application/Domain/Infrastructure`) por domínio | `docs/00`, `docs/02 §9.2` |
| Mecanismo de RLS | SQL Server `SECURITY POLICY` + função de filtro por `TenantId`, alimentada por `SESSION_CONTEXT('TenantId')`; um `DbCommandInterceptor`/`SaveChangesInterceptor` do EF Core define o `SESSION_CONTEXT` no início de cada unidade de trabalho, a partir do contexto de tenant autenticado (nunca de input do cliente) | `docs/adr/ADR-007`, `docs/08-Multi-Tenant.md §5-6` |
| Autenticação | ASP.NET Core Identity + emissão própria de JWT (federação OIDC/SAML fica para fase posterior) | `docs/07-Seguranca.md §5.1`, `docs/03 §9.1` |
| Object Storage local | MinIO (S3-compatible) | `docs/03-Stack-Tecnologica.md §6.2` |
| Ambiente local | Docker Compose (confirmado: Docker 29.6.1 / Compose v5.2.0 instalados) | `docs/14-DevOps.md §4` |

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

- [ ] **E0.1** Criar `EIP.sln` na raiz e estrutura `src/BuildingBlocks`, `src/Shared` com projetos de classe mínimos (sem lógica ainda), namespaces `EIP.BuildingBlocks.*` / `EIP.Shared.*`.
  - *Aceite:* `dotnet build` da solução passa.
- [ ] **E0.2** Criar projetos `EIP.Platform.Identity` e `EIP.Platform.Tenant` em Clean Architecture (`Api`, `Application`, `Domain`, `Infrastructure` por módulo), únicos domínios exigidos no primeiro incremento (`docs/03 §13`).
  - *Depende de:* E0.1.
  - *Aceite:* build limpo; `Domain` não referencia `Infrastructure`; `Api` não referencia `Domain` diretamente (passa por `Application`).
- [ ] **E0.3** Configurar `Directory.Build.props`/`.editorconfig` com nullable enable, warnings como erro para regras críticas, e análise estática básica (ex.: analisadores do .NET).
  - *Aceite:* `dotnet build` reporta avisos de estilo/nulidade quando violado.
- [ ] **E0.4** Criar `.gitignore` apropriado para .NET + Angular (bin/obj/node_modules/.env) — hoje o repositório não tem nenhum `.gitignore`.
  - *Aceite:* `git status` não lista artefatos de build depois de compilar.

## E1 — Infraestrutura Local (Docker Compose)

Objetivo: dependências locais sobem com um único comando, com healthchecks (`docs/14-DevOps.md §4`).

- [ ] **E1.1** `docker/` com `docker-compose.yml` (ou pasta `deploy/docker-compose/`) subindo SQL Server, Redis, RabbitMQ e MinIO, cada um com healthcheck.
  - *Aceite:* `docker compose up -d` sobe os 4 serviços e todos reportam `healthy`.
- [ ] **E1.2** `.env.example` com todas as variáveis necessárias, sem nenhum segredo real (`docs/07-Seguranca.md §8`).
- [ ] **E1.3** Script/documentação única de start (`scripts/dev-up.*` ou README dedicado) validando que as dependências estão prontas antes de a API subir.
  - *Aceite:* segue exatamente os passos documentados, do zero, e funciona.

## E2 — Identity & Tenant + RLS Obrigatória

Objetivo: fluxo de autenticação + isolamento de tenant realmente aplicado no banco, não só na aplicação. Este é o épico de maior risco arquitetural.

- [ ] **E2.1** Modelar entidades mínimas de `docs/08-Multi-Tenant.md §4`: `Tenant`, `Membership`, `Company` (sem `Branch`/`Workspace` ainda — fora do primeiro incremento).
  - *Aceite:* migration inicial gerada e revisada.
- [ ] **E2.2** Migration inicial já nasce com `TenantId` em toda tabela de escopo de tenant **e** `CREATE SECURITY POLICY` com função de filtro correspondente — nunca uma migration "só de schema" seguida depois por outra "de RLS".
  - *Depende de:* E2.1, E1.1 (banco disponível).
  - *Aceite:* rodar a migration cria a tabela **e** a policy na mesma transação/script; consultar a tabela sem `SESSION_CONTEXT` definido não retorna linhas de nenhum tenant.
- [ ] **E2.3** Implementar o `Tenant/Connection Resolver` (mesmo que simplificado para modo `Shared` apenas) e o interceptor EF Core que injeta `SESSION_CONTEXT('TenantId', @tenantId)` a partir do contexto autenticado da requisição — nunca de `TenantId` vindo do body/query/header não validado (`docs/08 §5.1`).
  - *Depende de:* E2.2.
  - *Aceite:* teste de integração comprova que uma query sem contexto de tenant autenticado não executa.
- [ ] **E2.4** ASP.NET Core Identity + emissão de JWT (claims incluindo `TenantId`/membership ativa), endpoints de login e refresh.
  - *Depende de:* E2.1.
  - *Aceite:* login retorna JWT válido; token expira conforme política curta definida em `07-Seguranca.md §5.1`.
- [ ] **E2.5** Autorização por permissão + escopo (`Identidade + Tenant + Workspace + Empresa + Recurso + Ação`, simplificado para o que existe no MVP) com "negar por padrão".
  - *Depende de:* E2.4.
- [ ] **E2.6** Testes automatizados obrigatórios de isolamento cross-tenant (`docs/08 §13`): usuário do tenant A não lista/lê/atualiza/exclui recurso do tenant B, mesmo com ID adulterado no payload.
  - *Depende de:* E2.2, E2.3, E2.5.
  - *Aceite:* suíte de teste dedicada roda no CI e falha o build se algum teste de isolamento falhar. **Este teste é o gate mínimo para considerar E2 concluído.**
- [ ] **E2.7** Auditoria mínima: login, falha de login, criação/alteração de membership (`docs/07-Seguranca.md §11.2`).

## E3 — API Versionada e Observabilidade Básica

- [ ] **E3.1** Prefixo `/api/v1`, `ProblemDetails` para erros padronizados, DTOs não expõem entidades de domínio.
- [ ] **E3.2** OpenAPI publicado e versionado junto do código.
- [ ] **E3.3** Health checks (liveness/readiness) cobrindo dependências críticas (SQL Server, Redis, RabbitMQ).
- [ ] **E3.4** Middleware de `CorrelationId` (aceito ou gerado), propagado em logs.
- [ ] **E3.5** Logs estruturados via Serilog; traces/métricas básicos via OpenTelemetry.
  - *Aceite combinado (E3.1–E3.5):* uma chamada autenticada em `/api/v1/...` aparece nos logs com `CorrelationId`, retorna erro em formato `ProblemDetails` quando aplicável, e os health checks respondem `healthy`/`unhealthy` corretamente quando uma dependência cai.

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
| E0 — Scaffolding da Solução | Não iniciado |
| E1 — Infraestrutura Local | Não iniciado |
| E2 — Identity & Tenant + RLS | Não iniciado |
| E3 — API versionada e observabilidade | Não iniciado |
| E4 — Gateway | Não iniciado |
| E5 — CI | Não iniciado |
| E6 — Frontend Angular | Não iniciado |
| E7 — Conector de referência | Não iniciado |

Atualizar esta tabela conforme os épicos avançam.
