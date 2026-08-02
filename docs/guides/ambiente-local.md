# Guia — Ambiente Local

**Projeto:** Enterprise Intelligence Platform (EIP)
**Status:** Oficial
**Última atualização:** Agosto/2026

---

# 1. Objetivo

Este é o "comando/documentação única para iniciar e validar o ambiente" exigido por
`docs/14-DevOps.md §4` e pelo critério de saída da Fase 0 ("ambiente local pode ser iniciado e
validado de forma documentada", `docs/15-Roadmap.md §4`). Segue estes passos do zero, na ordem, e o
ambiente completo (infraestrutura + os três processos executáveis do monólito modular + frontend)
fica de pé.

Não substitui `docs/roadmap/fase-0-backlog.md` (execução/decisões) nem `docs/14-DevOps.md`
(princípios) — é só o runbook operacional do dia a dia.

---

# 2. Pré-requisitos

- Docker Desktop (com o daemon rodando — `docker info` precisa responder sem erro).
- .NET SDK 10 (`dotnet --version` → `10.0.3xx`).
- Ferramenta `dotnet-ef` instalada globalmente: `dotnet tool install --global dotnet-ef` (ou
  `dotnet tool update` se já instalada).
- Node.js 24+ / npm 11+ (para o frontend Angular). Não é necessário `@angular/cli` global — o
  projeto usa `npx @angular/cli`.

---

# 3. Passo a passo

## 3.1 Infraestrutura (SQL Server, Redis, RabbitMQ, MinIO)

```bash
./scripts/dev-up.sh
```

Copia `deploy/docker-compose/.env.example` → `.env` se ainda não existir (valores só de
desenvolvimento, nunca um segredo real), sobe os 4 containers via Docker Compose e só retorna
sucesso quando todos reportam `healthy`. Se der timeout, rode
`docker compose -f deploy/docker-compose/docker-compose.yml logs` para diagnosticar.

## 3.2 Migrations (schema + RLS)

Cada módulo tem seu próprio `DbContext`/migrations. Rodar todas contra o SQL Server que acabou de
subir (a partir da raiz do repositório):

```bash
dotnet ef database update --project src/Platform/Tenant/Infrastructure --startup-project src/Platform/Tenant/Infrastructure
dotnet ef database update --project src/Platform/Identity/Infrastructure --startup-project src/Platform/Identity/Infrastructure
dotnet ef database update --project src/Platform/Connector/Infrastructure --startup-project src/Platform/Connector/Infrastructure
```

Cada migration inicial já nasce com a política RLS correspondente (ADR-007) — não existe uma etapa
separada de "aplicar RLS depois".

## 3.3 Processos executáveis (três composition roots)

Em três terminais separados (ou em background):

```bash
dotnet run --project src/Host      # http://localhost:5080 — API real, health/metrics diretos
dotnet run --project src/Gateway   # http://localhost:5000 — ponto único de entrada externo (/api/**)
dotnet run --project src/Worker    # sem porta HTTP — consome RabbitMQ (sincronizações assíncronas, E7)
```

O cliente (frontend, Postman, curl de fora) sempre fala com o **Gateway** (`:5000`), nunca
diretamente com o Host — health checks/métricas são exceção, acessados só direto no Host (`:5080`),
como um probe/scraper de infraestrutura faria.

## 3.4 Frontend (Angular)

```bash
cd frontend
npm install
npx ng serve
```

Abre em `http://localhost:4200`. `environment.development.ts` já aponta para
`http://localhost:5000/api/v1` (o Gateway).

---

# 4. Como validar

```bash
curl http://localhost:5080/health/live     # processo vivo (nunca depende de dependência externa)
curl http://localhost:5080/health/ready    # SQL Server, Redis, RabbitMQ — cada um reportado individualmente
curl http://localhost:5000/api/v1/sample/customers   # roteamento do Gateway funcionando
```

Fluxo de autenticação de ponta a ponta via Gateway:

```bash
curl -X POST http://localhost:5000/api/v1/auth/register -H "Content-Type: application/json" \
  -d '{"email":"voce@teste.local","password":"Senha@12345","displayName":"Seu Nome"}'
```

A resposta traz `requiresTenantSelection: true` e `availableTenants: []` — um usuário novo não
pertence a nenhum tenant ainda (não existe endpoint de auto-provisionamento de tenant na Fase 0; ver
`docs/roadmap/fase-0-backlog.md §6`, fora do escopo). Para testar o fluxo completo (login →
dashboard) é preciso um Tenant + Membership `Active` para o usuário, criados diretamente via SQL
(mesmo padrão usado para os dados de demonstração desta fase).

No navegador: abra `http://localhost:4200`, faça login e confirme que chega no dashboard.

---

# 5. Portas usadas localmente

| Serviço | Porta | Observação |
|---|---|---|
| SQL Server | 1433 | `sa` / senha em `.env` (dev only) |
| Redis | 6379 | |
| RabbitMQ (AMQP) | 5672 | |
| RabbitMQ (management UI) | 15672 | `http://localhost:15672`, mesmas credenciais do `.env` |
| MinIO (API / console) | 9000 / 9001 | |
| EIP.Host | 5080 | acesso direto (health/metrics/dev) |
| EIP.Gateway | 5000 | ponto de entrada externo — use este para tudo que não é health/metrics |
| EIP.Worker.Sync | — | sem porta HTTP, só consome RabbitMQ |
| Frontend (`ng serve`) | 4200 | |

---

# 6. Problemas comuns

- **`docker info` falha / `scripts/dev-up.sh` trava no início:** Docker Desktop não está rodando —
  abra o aplicativo e tente de novo.
- **`dotnet run --project src/Host` falha ao copiar DLL ("arquivo bloqueado por outro processo"):**
  uma instância anterior do mesmo processo ainda está rodando — pare-a antes de reconstruir/rodar de
  novo.
- **Login retorna 500 mencionando erro de RLS/security policy:** normalmente falta rodar uma das
  migrations do passo 3.2 (a política de RLS de um módulo não foi criada).
- **Frontend não recebe resposta do Gateway (erro de CORS no console do navegador):** confirme que
  está acessando `http://localhost:4200` exatamente (a origem precisa bater com
  `Cors:AllowedOrigins` do `appsettings.json` do Gateway).
