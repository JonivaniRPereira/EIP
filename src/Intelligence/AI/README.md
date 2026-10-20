# AI Engine — base da Fase 4

`Application` mantém o registro fechado e contratos de ferramentas.
`Api` expõe, sob `analytics.query` e tenant selecionado:

- `GET /api/v1/ai/tools`: ferramentas somente de leitura, `listDatasets` e `queryAnalytics`, com schemas.
- `POST /api/v1/ai/tools/listDatasets`, corpo `{}`: definições de datasets/métricas
  do mesmo catálogo analítico, com tenant da sessão e instante de execução.
- `POST /api/v1/ai/tools/queryAnalytics`: contrato declarativo (`dataset`, `metrics`,
  `dimensions`; opcionais `filters`, `orderBy`, `limit`). Retorna `tenantId`, `tool`,
  `source`, consulta validada, `data` e `metadata` (versão, execução, frescor e linhas).
  Exemplo: `{"dataset":"sales","metrics":["netRevenue"],"dimensions":["date.month"]}`.

`queryAnalytics` usa `IDeclarativeAnalyticsQueryService`, incluindo medição de uso,
cache por tenant/frescor, RLS e limites configurados no Analytics. Na configuração
atual: 5.000 linhas, 400 dias quando há filtro de período e timeout de execução de
10 segundos. Frescor nulo significa ausência de carga concluída conhecida.
Consultas inválidas retornam 400. O contexto sempre vem do token; propriedades
extras, repetidas, valores nulos e métricas duplicadas são recusados.

Argumentos extras, contexto/SQL/prompt enviados pelo cliente e ferramentas não
registradas são recusados. O corpo tem limite de 4 KiB; não há chamadas externas.
Não há provedor, chat ou geração por IA nesta entrega. Nenhuma chave é necessária.
Há registro de consultas bem-sucedidas, inclusive cache hit; não há quota mensal
analítica/IA implementada. O rate limit do Gateway continua aplicável.

Preparação multiprovedor: `IAiModelAdapter`, implementação desabilitada e perfis
na seção `AI` da configuração do Host. Descoberta autenticada em `GET /api/v1/ai/status`
e `GET /api/v1/ai/providers`, sem expor referências/valores de credenciais.
OpenAI/Anthropic/Google são perfis iniciais, sem transportes HTTP implementados.
Selecionar perfil/modelo não habilita chamadas. [Configuração e pendências](../../../docs/guides/ia-provedores.md).

Configuração administrativa em `/settings/ai`, com `ai.settings.manage` (Owner/Admin).
`GET/PUT /api/v1/ai/settings` grava perfis por organização em `ai.ProviderSettings`,
com RLS, auditoria mínima e revisão para conflitos. `GET /ai/providers` retorna os
perfis efetivos. Aplicar a migration do novo projeto `Infrastructure` antes da API.
Chaves e ativação não são aceitas nessa edição de perfis; chamadas continuam desabilitadas.

Credenciais têm endpoints próprios sob `/api/v1/ai/settings`: status em
`credentials`, PUT/DELETE em `profiles/{id}/credential`. Mesma permissão administrativa
e revisão; somente presença é devolvida. Data Protection vincula tenant/perfil/provedor;
cifras ficam em coluna separada, RLS existente. Troca de provedor/id remove a chave.
Development Windows usa keyring persistente com DPAPI; demais ambientes recusam
cadastro até implementar cofre. Sem API de texto claro, rede ou fallback inseguro.
Aplicar também `ProtectedProviderCredentials`. [Operação e recuperação](../../../docs/guides/ia-provedores.md).

Próxima etapa: transportes, cofre de produção e política de dados/orçamento.
[Backlog e critérios](../../../docs/roadmap/fase-4-backlog.md).
