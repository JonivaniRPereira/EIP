# 15 - Roadmap

**Projeto:** Enterprise Intelligence Platform (EIP)  
**Versão:** 1.0  
**Status:** Oficial  
**Última atualização:** Julho/2026

---

# 1. Objetivo

Este roadmap organiza a evolução da EIP por resultados e dependências, sem assumir datas artificiais. A prioridade é validar um fluxo completo e seguro de dados empresariais antes de ampliar conectores, IA, automações ou recursos avançados de BI.

O avanço de fase depende de critérios de saída mensuráveis, não apenas da conclusão de telas ou componentes isolados.

---

# 2. Direção do Produto

A EIP evolui de uma fundação SaaS para uma plataforma de inteligência corporativa:

```text
Fundação → Dados confiáveis → Analytics e Dashboards → IA assistida → Automação → Ecossistema
```

O produto não pretende replicar toda funcionalidade de uma ferramenta de BI no primeiro ciclo. O diferencial inicial é unir conectores, modelo canônico, métricas governadas, dashboards e IA sobre um mesmo contexto corporativo.

---

# 3. Princípios de Priorização

- entregar verticalmente: origem → dado → métrica → dashboard → usuário;
- priorizar valor validável por cliente em vez de quantidade de módulos;
- adiar complexidade operacional sem caso de uso comprovado;
- segurança, isolamento multi-tenant e observabilidade são requisitos de cada fase;
- cada conector novo precisa justificar demanda comercial e datasets necessários;
- IA e automação só avançam sobre métricas e permissões confiáveis;
- métricas de produto e operação orientam a próxima prioridade.

---

# 4. Fase 0 — Fundação de Engenharia

## Objetivo

Criar uma base executável, segura e repetível para o desenvolvimento do produto.

## Entregas

- solução Angular e ASP.NET Core organizada como monólito modular;
- Docker Compose para SQL Server, Redis, RabbitMQ e Object Storage local;
- CI com build, testes, análise estática, scan de dependências/segredos e contratos;
- autenticação inicial, identidade, tenant, empresa, membership e permissões básicas;
- API versionada, OpenAPI, Problem Details, health checks e `CorrelationId`;
- logs estruturados, métricas/traces iniciais e auditoria administrativa;
- migrations, dados sintéticos e convenções de desenvolvimento.

## Critérios de saída

- ambiente local pode ser iniciado e validado de forma documentada;
- pipeline bloqueia build/teste/segurança críticos;
- usuário de tenant A não acessa recursos de tenant B em testes automatizados;
- uma API autenticada possui autorização, auditoria, logs e health checks;
- deploy de ambiente não produtivo é reproduzível a partir de artefatos versionados.

---

# 5. Fase 1 — Primeiro Fluxo de Dados Confiável

## Objetivo

Comprovar a cadeia completa de ingestão até consumo analítico com uma única origem e poucos domínios prioritários.

## Entregas

- Connector Framework, registry, instância, teste de conexão e sync job;
- primeiro conector de referência: REST API genérica, CSV/Excel ou SQL Server read-only, conforme demanda validada;
- Data Lake bruto com linhagem, checksum e segregação por tenant;
- Modelo Canônico mínimo: empresa, cliente, produto, fatura de venda, título financeiro e estoque quando disponível;
- validações, quarentena, reconciliação e reprocessamento;
- carga incremental e relatórios de execução;
- Data Warehouse inicial com fatos/dimensões prioritários e camada semântica mínima.

## Critérios de saída

- uma sincronização é executada de ponta a ponta, reprocessável e auditada;
- dado bruto, registro canônico e fato analítico podem ser rastreados entre si;
- falhas de qualidade ficam em quarentena, sem corromper o DW;
- totais/contagens de dados críticos são reconciliados com a origem dentro do limite definido;
- tenant, empresa, cache, fila e Object Storage preservam isolamento em testes.

---

# 6. Fase 2 — MVP Analítico e Dashboards

## Objetivo

Entregar ao gestor o primeiro valor recorrente: indicadores confiáveis e painéis seguros para acompanhar vendas, financeiro e/ou estoque.

## Entregas

- Analytics Engine com datasets, métricas certificadas, consultas declarativas e cache seguro;
- KPIs e métricas iniciais: receita líquida, quantidade faturada, ticket médio, contas abertas/vencidas e estoque disponível conforme dados integrados;
- Dashboard Builder com KPIs, gráficos de linha/barra, tabelas, filtros e templates;
- dashboards iniciais por domínio: comercial, financeiro e estoque quando aplicável;
- publicação/versionamento, permissões por workspace/empresa e exportação controlada;
- indicadores de frescor e qualidade visíveis para usuários;
- telemetria de uso, latência e custo de consultas.

## Critérios de saída

- gestor autorizado visualiza dashboard de produção com filtros e dados consistentes;
- métricas possuem dono, definição, versão e reconciliação aprovadas;
- dashboards não acessam ERP/Raw/DW diretamente;
- consultas respeitam orçamento de desempenho e quotas;
- publicação, exportação e acesso por escopo são auditados.

## Resultado esperado

Um cliente consegue conectar uma origem prioritária e acompanhar indicadores principais sem depender de planilhas ou modelagem manual fora da plataforma para esse caso de uso.

---

# 7. Fase 3 — Produto Operável e Expansão Controlada

## Objetivo

Preparar o MVP para operação piloto com mais usuários, empresas, fontes e rotinas de suporte.

## Entregas

- melhoria de onboarding, configuração de empresas, workspaces, papéis e planos;
- conectores adicionais priorizados por demanda e reuso do CDM;
- catálogo de dados, linhagem e dicionário de métricas mais completos;
- alertas operacionais, SLOs iniciais, runbooks, backup/restauração testados;
- quotas de API, sincronização, armazenamento e exportação;
- modo shared/dedicated operacionalmente definido para tenants elegíveis;
- hardening de segurança, revisão de LGPD e testes de carga/isolamento;
- processo de suporte e administração de tenant auditado.

## Critérios de saída

- piloto atende múltiplos tenants sem incidente de isolamento;
- conectores possuem monitoramento, retry, DLQ e documentação operacional;
- recuperação de backup e rollback de release foram testados;
- métricas de adoção, frescor, falha de sincronização e satisfação são acompanhadas;
- custos de infraestrutura e consulta são mensurados por tenant/plano.

---

# 8. Fase 4 — IA Assistida

## Objetivo

Permitir que usuários explorem dados e criem rascunhos com linguagem natural, sempre sobre métricas e ferramentas governadas.

## Entregas

- AI Engine com contexto de tenant/workspace, Tool Registry e guardrails;
- perguntas analíticas em linguagem natural convertidas para consulta declarativa;
- explicações de KPI com evidências, período, filtros e frescor;
- descoberta de datasets/métricas e resumo executivo;
- geração assistida de rascunhos de dashboard;
- quotas, custo, avaliação, telemetria, retenção e controles de privacidade;
- fluxo de confirmação para ações assistidas de maior risco.

## Critérios de saída

- IA não amplia acesso a dados nem executa SQL livre;
- respostas avaliadas apresentam evidência e recusam/informam limitações adequadamente;
- prompt injection, falhas de ferramenta e indisponibilidade possuem tratamento seguro;
- custo e qualidade são mensurados por capacidade/tenant;
- publicação ou ação externa requer aprovação conforme política.

---

# 9. Fase 5 — Automação Governada

## Objetivo

Transformar eventos e indicadores em fluxos controlados de alerta, tarefa e ação aprovada.

## Entregas

- Automation Engine com triggers por evento, agenda e condição analítica;
- notificações, relatórios/exportações e tarefas internas;
- cooldown, deduplicação, limite de frequência e prevenção de loops;
- aprovações humanas para ações de risco;
- histórico de execução, retry, DLQ, auditoria e alertas;
- integrações de saída aprovadas por prioridade comercial.

## Critérios de saída

- automações executam de forma idempotente e rastreável;
- nenhuma ação externa ocorre além da permissão/escopo aprovados;
- regras ruidosas são controladas por supressão e limites;
- aprovações e falhas são visíveis ao responsável;
- execução de automação não degrada consultas e sincronizações críticas.

---

# 10. Fase 6 — Escala e Ecossistema

## Objetivo

Expandir a plataforma de forma sustentável, baseada em demanda validada e operação madura.

## Possibilidades

- novos conectores ERP/CRM e SDK governado;
- marketplace de conectores, templates e extensões com sandbox e revisão;
- Data Warehouse/engine analítico dedicado para alto volume;
- orquestração avançada de pipelines e processamento distribuído;
- previsão, anomalia e agentes especializados avaliados;
- white label, APIs públicas para parceiros e integrações enterprise;
- Kubernetes, alta disponibilidade e multi-região quando SLOs/escala justificarem;
- suporte ampliado a residência de dados e requisitos internacionais.

## Critério de entrada

Cada item requer problema real, hipótese de valor, análise de custo/segurança/operação, métrica de sucesso, responsável e ADR quando impactar arquitetura.

---

# 11. Dependências Críticas

| Capacidade | Depende de |
|---|---|
| Dashboard confiável | CDM, DW, semântica, Analytics Engine e permissões |
| IA analítica | catálogo/métricas certificadas, Analytics Engine, isolamento e observabilidade |
| Automação por condição | eventos confiáveis, Analytics Engine, notificações e auditoria |
| Novo conector | Connector Framework, CDM, Data Lake, pipeline e reconciliação |
| Marketplace/SDK | contratos maduros, sandbox, segurança, governança e suporte |
| Banco dedicado/multi-região | operação, backup, Connection Resolver, custo e SLOs |

---

# 12. Métricas de Sucesso

## Produto e adoção

- tempo para primeira integração e primeiro dashboard útil;
- usuários ativos e recorrência de acesso;
- dashboards publicados/consultados e uso de métricas certificadas;
- taxa de conclusão de onboarding;
- satisfação de usuários e pilotos.

## Dados e operação

- taxa de sincronização bem-sucedida e atraso/frescor de dados;
- taxa de qualidade/rejeição e tempo de resolução;
- discrepância de reconciliação;
- latência e disponibilidade de APIs/consultas;
- incidentes de isolamento, segurança e recuperação;
- custo por tenant, conector, consulta e uso de IA.

## Negócio

- número de tenants/empresas conectadas;
- expansão de conectores e domínios com uso comprovado;
- retenção, conversão de piloto e receita recorrente;
- redução do tempo para gerar análise/decisão para o cliente.

---

# 13. Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| Escopo excessivo de substituição de BI | fases verticais, limites explícitos de MVP e métricas de uso |
| Conectores instáveis/heterogêneos | framework, CDM incremental, testes, reconciliação e prioridade por demanda |
| Métricas inconsistentes | camada semântica, proprietário, versão e certificação |
| Vazamento multi-tenant | filtros, RLS complementar, testes, auditoria e contexto propagado |
| Custo/risco de IA | ferramentas limitadas, quotas, contexto mínimo, avaliação e aprovação |
| Complexidade operacional precoce | monólito modular e infraestrutura proporcional à necessidade |
| Baixa confiança do usuário | frescor, linhagem, qualidade e explicação visíveis no produto |

---

# 14. Próximo Marco Recomendado

O próximo marco é a **Fase 0**, seguida de uma decisão de negócio para o primeiro cenário vertical:

1. cliente/persona prioritária;
2. origem de dados acessível;
3. empresas e domínios incluídos;
4. três a cinco KPIs que resolvem uma dor real;
5. dashboard inicial e critério de sucesso do piloto.

Com essa decisão, a EIP pode iniciar a implementação sem tentar construir simultaneamente todos os módulos planejados.
