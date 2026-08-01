# 03 - Stack Tecnológica

**Projeto:** Enterprise Intelligence Platform (EIP)  
**Versão:** 1.0  
**Status:** Oficial  
**Última atualização:** Julho/2026

---

# 1. Objetivo

Este documento define as tecnologias adotadas pela EIP e os critérios para sua utilização.

A stack deve permitir a construção progressiva de uma plataforma SaaS de inteligência corporativa: iniciar com um MVP simples de operar, preservar o isolamento entre tenants e permitir crescimento para ingestão de dados, analytics, dashboards, IA e automações.

> A arquitetura não exige que todos os componentes sejam implantados desde o início. Tecnologias de escala entram quando houver necessidade comprovada.

---

# 2. Princípios de Escolha

- **Produtividade no MVP:** reduzir serviços e dependências operacionais iniciais.
- **Ecossistema corporativo:** boa integração com ERPs, SQL Server e ambientes Microsoft.
- **Cloud native:** execução reproduzível em containers e possibilidade de escala horizontal.
- **Observabilidade e segurança:** logs, métricas, rastreamento e isolamento de tenant desde a primeira versão.
- **Evolução sem reescrita:** contratos versionados e módulos independentes permitem trocar ou escalar componentes pontualmente.

---

# 3. Stack Oficial

| Camada | Tecnologia | Uso na EIP |
|---|---|---|
| Frontend | Angular + TypeScript | Aplicação web administrativa, analytics e dashboards |
| UI | Tailwind CSS + Angular Material | Design system, componentes de formulário e acessibilidade |
| Visualização | Apache ECharts | Gráficos, KPIs e visualizações interativas |
| Backend | ASP.NET Core + C# | APIs, regras de negócio, autenticação e serviços de processamento |
| Persistência transacional | Entity Framework Core | Escritas, migrações e entidades de domínio |
| Consulta de alta performance | Dapper | Leitura analítica e consultas SQL otimizadas |
| Banco operacional e analítico inicial | SQL Server | Dados da plataforma, dados canônicos e primeiro Data Warehouse |
| Cache distribuído | Redis | Cache de KPIs, consultas, sessão e rate limiting |
| Mensageria | RabbitMQ | Sincronizações, ETL, notificações e tarefas assíncronas |
| Armazenamento de objetos | S3-compatible Object Storage | Arquivos importados, dados brutos e exportações |
| API Gateway | YARP | Roteamento, autenticação, rate limit e versionamento de APIs |
| Autenticação e autorização | ASP.NET Core Identity + JWT/OIDC | Identidade, tokens, papéis e permissões por tenant/workspace |
| Observabilidade | OpenTelemetry + Serilog + Prometheus + Grafana | Logs estruturados, traces, métricas e dashboards operacionais |
| Containers | Docker + Docker Compose | Ambiente local e integrações de desenvolvimento |
| Orquestração futura | Kubernetes + Helm | Alta disponibilidade e escala em produção quando necessária |
| CI/CD | GitHub Actions | Build, testes, análise e publicação de imagens |
| Testes | xUnit + FluentAssertions + Testcontainers | Testes unitários, integração e contratos |

---

# 4. Frontend

## 4.1 Angular e TypeScript

O frontend será uma SPA desenvolvida em **Angular** e **TypeScript**. A aplicação atende usuários administrativos, analistas e gestores, consumindo exclusivamente APIs versionadas da EIP.

Responsabilidades principais:

- autenticação e seleção de tenant/workspace;
- gestão de conectores e sincronizações;
- consulta de indicadores e dashboards;
- administração de usuários, empresas e permissões;
- exibição de alertas, relatórios e recursos de IA.

## 4.2 Interface e gráficos

- **Tailwind CSS** define tokens visuais, espaçamento e layouts responsivos.
- **Angular Material** fornece componentes acessíveis e padronizados.
- **Apache ECharts** renderiza gráficos; a configuração de cada visualização deve ser serializável e armazenada pelo domínio de Dashboard.

---

# 5. Backend e APIs

## 5.1 ASP.NET Core

Todos os módulos de backend serão implementados em **ASP.NET Core**, usando C#. Cada módulo segue as separações de API, Application, Domain e Infrastructure definidas na arquitetura do repositório.

Para o MVP, os módulos podem ser entregues como um **monólito modular**. A separação em processos independentes só deve ocorrer quando houver motivo concreto, como necessidade distinta de escala, isolamento operacional ou ciclo de implantação próprio.

## 5.2 Dados e acesso a banco

- **Entity Framework Core:** comandos transacionais, persistência de agregados e migrações.
- **Dapper:** queries de leitura e consultas analíticas que exijam SQL explícito e previsível.
- Consultas analíticas não podem atingir diretamente tabelas de domínios operacionais ou fontes externas; elas devem usar a camada de Warehouse/Semantic.

## 5.3 Contratos de API

- APIs públicas sob o prefixo `/api/v1`.
- Contratos especificados em OpenAPI antes ou junto da implementação.
- DTOs não expõem entidades de domínio.
- Erros seguem um formato único baseado em `ProblemDetails`.
- Toda requisição autenticada carrega e valida o contexto de tenant e, quando aplicável, workspace.

## 5.4 Gateway

O **YARP** será usado como API Gateway. No MVP pode coexistir com o backend principal; em topologias com múltiplos serviços será o ponto único de entrada externo.

Responsabilidades:

- roteamento;
- autenticação delegada e propagação segura de contexto;
- rate limiting;
- versionamento;
- correlação de logs e rastreamento.

---

# 6. Dados e Analytics

## 6.1 SQL Server

O **SQL Server** é o banco padrão inicial para os dados transacionais da plataforma e para o primeiro Data Warehouse. Sua adoção é alinhada aos ambientes corporativos que a EIP pretende integrar.

O banco deve separar logicamente:

- dados de plataforma (tenants, usuários, permissões e configuração);
- metadados de conectores e execuções;
- dados canônicos;
- tabelas analíticas e agregações por workspace.

Para consultas de grande volume, devem ser avaliados índices adequados, particionamento, tabelas de agregação e índices columnstore antes da introdução de outro banco analítico.

## 6.2 Object Storage e Data Lake

Dados brutos importados, arquivos CSV/Excel/XML/JSON, cargas intermediárias e exportações devem ser armazenados em Object Storage compatível com S3. No desenvolvimento local, pode ser utilizado MinIO.

Objetos precisam ser particionados por tenant, empresa, origem e data de ingestão. O controle de acesso jamais pode depender somente do caminho do arquivo; deve haver autorização no serviço que emite acesso temporário.

## 6.3 Modelo Canônico e camada semântica

O modelo canônico é um contrato interno, independente do ERP de origem. Conectores transformam seus dados para esse contrato; dashboards, métricas e IA leem apenas a camada canônica, o Warehouse ou a camada semântica.

---

# 7. Processamento Assíncrono

## 7.1 RabbitMQ

O **RabbitMQ** processa tarefas que não devem bloquear uma requisição HTTP:

- sincronização de conectores;
- importação e transformação de arquivos;
- atualização de tabelas analíticas e cache;
- geração de relatórios;
- notificações;
- execuções de IA.

Mensagens devem conter `TenantId`, identificador de correlação e versão do contrato. Consumidores precisam ser idempotentes, ter política de retry e usar fila de mensagens mortas (DLQ).

## 7.2 Workers

Workers .NET hospedam consumidores e jobs agendados. O agendamento inicial pode residir no próprio backend; uma ferramenta dedicada só será adotada se os requisitos de recorrência, auditoria e escala exigirem.

---

# 8. Cache

O **Redis** é o cache distribuído da plataforma e será usado para:

- resultados de KPIs e consultas analíticas;
- rate limiting;
- sessão ou dados efêmeros, quando aplicável;
- coordenação de tarefas de curta duração.

Chaves de cache devem obrigatoriamente incluir `TenantId`, `WorkspaceId` quando existir, versão da consulta e escopo de permissão. Cache nunca substitui o controle de autorização.

---

# 9. Segurança

## 9.1 Identidade

O MVP utilizará ASP.NET Core Identity, JWT e OpenID Connect. A solução deve estar preparada para federação corporativa posterior via provedores OIDC/SAML.

## 9.2 Requisitos mínimos

- HTTPS obrigatório fora do ambiente local;
- senhas protegidas por mecanismos nativos do provedor de identidade;
- autorização baseada em papéis e permissões;
- isolamento de tenant validado na API, persistência, eventos, cache e Object Storage;
- segredos fora do código-fonte e rotacionáveis;
- logs de auditoria para operações administrativas e acesso a dados sensíveis;
- princípios de LGPD aplicados à coleta, retenção e exclusão de dados.

---

# 10. Observabilidade

Todos os serviços devem publicar logs estruturados via **Serilog** e traces/métricas via **OpenTelemetry**.

- **Prometheus** coleta métricas de aplicação e infraestrutura.
- **Grafana** apresenta painéis operacionais e alertas.
- Cada requisição, evento e job deve compartilhar um `CorrelationId`.

Indicadores mínimos: latência e erros de API, estado de filas, duração/falha de sincronizações, cache hit rate, uso de banco e disponibilidade dos conectores.

---

# 11. Infraestrutura e Entrega

## 11.1 Desenvolvimento local

**Docker Compose** sobe dependências locais: SQL Server, Redis, RabbitMQ, Object Storage e serviços da aplicação. O ambiente deve ser iniciado por configuração documentada e sem segredos reais.

## 11.2 Produção

Imagens Docker imutáveis serão a unidade de entrega. **Kubernetes** e **Helm** serão adotados quando a operação exigir réplicas, autoscaling, alta disponibilidade ou deploys independentes de múltiplos serviços.

## 11.3 CI/CD

O pipeline no GitHub Actions deve executar, no mínimo:

1. restauração de dependências e build;
2. testes unitários e de integração;
3. análise estática e validação de formatação;
4. geração/publicação de imagens versionadas;
5. implantação controlada por ambiente.

---

# 12. Tecnologias Futuras ou Condicionais

As tecnologias abaixo não fazem parte da exigência inicial do MVP. Sua inclusão requer ADR com motivação, custo operacional, impacto em segurança e plano de migração.

| Necessidade | Alternativas a avaliar |
|---|---|
| Banco analítico dedicado | ClickHouse, Azure Data Explorer, Databricks ou Snowflake |
| Orquestração avançada de pipelines | Airflow, Dagster ou Prefect |
| Busca semântica/vetorial | PostgreSQL com pgvector, Azure AI Search ou outro serviço gerenciado |
| Processamento massivo | Spark ou Databricks |
| Autenticação corporativa avançada | Microsoft Entra ID, Auth0 ou Keycloak |
| Gestão de segredos | Azure Key Vault, AWS Secrets Manager, HashiCorp Vault |
| Feature flags | OpenFeature e provedor compatível |

---

# 13. Itens Obrigatórios para o Primeiro Incremento

O primeiro incremento executável deverá conter:

- aplicação Angular inicial;
- API ASP.NET Core modular;
- SQL Server, Redis e RabbitMQ via Docker Compose;
- autenticação, tenant e autorização básica, com isolamento multi-tenant obrigatório (`TenantId` + política RLS no SQL Server desde a primeira migration, sem exceção);
- OpenAPI e health checks;
- logs estruturados, trace básico e `CorrelationId`;
- pipeline de CI com build e testes;
- um conector de referência e uma execução assíncrona ponta a ponta.

Essa base permite validar o fluxo completo antes de investir em múltiplos conectores, dashboard builder, marketplace ou agentes de IA.
