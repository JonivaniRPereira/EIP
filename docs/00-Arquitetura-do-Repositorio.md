# 00 - Arquitetura do Repositório

> Enterprise Intelligence Platform (EIP)

**Versão:** 1.0

**Status:** Oficial

---

# Objetivo

Este documento define a organização oficial do repositório da Enterprise Intelligence Platform (EIP).

Seu objetivo é padronizar a estrutura do código-fonte, estabelecer responsabilidades entre os componentes da plataforma e garantir que todas as equipes de desenvolvimento sigam os mesmos princípios arquiteturais.

Toda evolução do projeto deverá respeitar as diretrizes aqui estabelecidas.

---

# Princípios da Arquitetura

A arquitetura da plataforma é baseada nos seguintes princípios:

- Domain Driven Design (DDD)
- Clean Architecture
- API First
- Event Driven Architecture
- SOLID
- CQRS (quando aplicável)
- Multi-Tenant
- Cloud Native
- Platform First

Esses princípios são obrigatórios para qualquer componente desenvolvido dentro da plataforma.

---

# Estrutura do Repositório

```
EIP
│
├── docs/
├── src/
├── tests/
├── docker/
├── deploy/
├── scripts/
│
├── README.md
├── CHANGELOG.md
└── LICENSE
```

Cada diretório possui uma responsabilidade específica.

---

# Diretório docs

Contém toda documentação oficial do projeto.

```
docs/

adr/
api/
architecture/
assets/
database/
deployment/
development/
diagrams/
guides/
operations/
roadmap/
security/
```

Nenhuma documentação técnica deverá ser criada fora deste diretório.

---

# Diretório src

Contém todo o código-fonte da plataforma.

A organização segue os domínios definidos pela arquitetura.

```
src/

Platform/

Data/

Intelligence/

Gateway/

Infrastructure/

Shared/

BuildingBlocks/
```

Cada diretório representa uma camada arquitetural.

---

# Platform

Representa o núcleo da plataforma.

É responsável pelas funcionalidades administrativas e de governança.

```
Platform/

Administration/

Billing/

Identity/

Tenant/

Workspace/
```

Responsabilidades:

- autenticação;
- autorização;
- gestão de clientes;
- gestão de Workspaces;
- planos comerciais;
- licenciamento;
- administração.

Nenhum componente fora desta camada poderá manipular diretamente essas regras.

---

# Data

Representa toda a plataforma de dados.

```
Data/

Connector/

Pipeline/

Canonical/

DataLake/

Warehouse/

Semantic/

Catalog/
```

Responsabilidades:

- ingestão de dados;
- transformação;
- modelo canônico;
- armazenamento;
- catálogo;
- metadados;
- analytics.

Todo dado externo obrigatoriamente passa por esta camada.

---

# Intelligence

Responsável pela inteligência de negócio.

```
Intelligence/

Analytics/

Dashboard/

Reporting/

Automation/

Notification/

AI/
```

Responsabilidades:

- dashboards;
- indicadores;
- IA;
- automações;
- notificações;
- relatórios.

Nenhuma Engine possui acesso direto às fontes externas.

Toda informação deve vir da Data Platform.

---

# Gateway

Representa o ponto único de entrada da plataforma.

Responsabilidades:

- autenticação;
- autorização;
- rate limiting;
- roteamento;
- versionamento;
- logging;
- observabilidade.

Nenhum cliente acessará diretamente as Engines.

---

# Infrastructure

Implementações técnicas da plataforma.

```
Infrastructure/

Caching/

Email/

Identity/

Logging/

Messaging/

Persistence/

Scheduler/

Storage/

Telemetry/
```

Esta camada contém apenas detalhes de implementação.

Nenhuma regra de negócio poderá existir nesta camada.

---

# Shared

Biblioteca compartilhada.

```
Shared/

Abstractions/

Constants/

Contracts/

DTOs/

Enums/

Events/

Exceptions/

Extensions/

Helpers/

Interfaces/
```

Contém apenas componentes reutilizáveis.

Não deve conter regras específicas de negócio.

---

# BuildingBlocks

Componentes arquiteturais reutilizáveis.

```
BuildingBlocks/

Caching/

CQRS/

DDD/

EventBus/

Exceptions/

Extensions/

Mediator/

Messaging/

Results/

Security/

Validation/
```

Essa camada representa a infraestrutura arquitetural utilizada por toda a plataforma.

---

# Tests

Todos os testes automatizados.

```
tests/

Architecture/

Contract/

EndToEnd/

Integration/

Performance/

Unit/
```

Todos os módulos deverão possuir cobertura de testes.

---

# Docker

Arquivos relacionados à conteinerização.

```
docker/

gateway/

platform/

data/

analytics/

ai/

redis/

rabbitmq/

sqlserver/
```

---

# Deploy

Infraestrutura de implantação.

```
deploy/

docker-compose/

helm/

kubernetes/

terraform/
```

---

# Scripts

Scripts auxiliares.

```
scripts/

backup/

database/

migration/

seed/
```

---

# Dependências Permitidas

Os domínios de negócio não formam uma cadeia linear. `Platform`, `Data` e `Intelligence` são contextos independentes, que se comunicam por contratos de API ou eventos de negócio.

```text
Clientes
   │
Gateway ──→ APIs dos domínios: Platform • Data • Intelligence
                              │                 │
                              └── contratos e eventos ──┘

Todos os domínios ──→ BuildingBlocks / Shared / Infrastructure (abstrações técnicas)
```

Regras obrigatórias:

- `Gateway` é ponto de entrada e não contém regras de negócio.
- `Platform`, `Data` e `Intelligence` não acessam persistência interna uns dos outros.
- `Intelligence` consome dados publicados por `Data` por APIs, eventos ou camada semântica governada.
- `Infrastructure` contém implementações técnicas; regras de domínio dependem de abstrações, nunca de detalhes concretos.
- A comunicação entre Engines ocorre exclusivamente por APIs ou eventos versionados.

---

# Convenções de Nomenclatura

## Projetos

```
Platform.Identity

Platform.Tenant

Platform.Workspace

Data.Connector

Data.Pipeline

Data.Canonical

Intelligence.Analytics

Intelligence.AI
```

---

## Namespaces

Seguem exatamente a estrutura dos projetos.

Exemplo:

```
EIP.Platform.Identity

EIP.Data.Connector

EIP.Intelligence.Analytics
```

---

## APIs

Todas as APIs devem seguir:

```
/api/v1/
```

Versionamento obrigatório.

---

## Eventos

Formato:

```
EntityCreated

EntityUpdated

EntityDeleted

PipelineExecuted

ConnectorSynchronized
```

Eventos devem representar fatos de negócio.

---

## DTOs

Sempre utilizar o sufixo:

```
Dto
```

Exemplos:

```
CustomerDto

InvoiceDto

DashboardDto
```

---

## Commands

```
CreateWorkspaceCommand

CreateTenantCommand

SynchronizeConnectorCommand
```

---

## Queries

```
GetWorkspaceQuery

GetDashboardQuery

GetAnalyticsQuery
```

---

## Handlers

```
CreateWorkspaceHandler

SynchronizeConnectorHandler
```

---

# Regras de Desenvolvimento

Todo novo módulo deverá:

- seguir DDD;
- seguir Clean Architecture;
- possuir testes;
- possuir documentação;
- possuir ADR quando alterar decisões arquiteturais;
- possuir APIs versionadas;
- publicar eventos quando necessário.

---

# Processo de Criação de Novos Componentes

Antes de criar qualquer Engine é obrigatório responder:

1. Esse domínio realmente existe?
2. Pode reutilizar outro domínio?
3. Deve ser uma Engine independente?
4. Qual evento ele publica?
5. Qual evento ele consome?
6. Qual contrato ele expõe?

Somente após essas respostas o desenvolvimento poderá iniciar.

---

# Objetivo Final

A Enterprise Intelligence Platform foi concebida para evoluir continuamente.

A organização deste repositório deverá permanecer consistente durante todo o ciclo de vida da plataforma.

Novas funcionalidades serão adicionadas por meio da criação de novas Engines, preservando os princípios arquiteturais definidos neste documento.

A arquitetura deve privilegiar simplicidade, desacoplamento, escalabilidade e capacidade de evolução contínua.
