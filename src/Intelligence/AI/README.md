# AI Engine — base da Fase 4

`Application` mantém o registro fechado e contratos de ferramentas.
`Api` expõe, sob `analytics.query` e tenant selecionado:

- `GET /api/v1/ai/tools`: uma ferramenta somente de leitura, `listDatasets`, com schema.
- `POST /api/v1/ai/tools/listDatasets`, corpo `{}`: definições de datasets/métricas
  do mesmo catálogo analítico, com tenant da sessão e instante de execução.

Argumentos extras, contexto/SQL/prompt enviados pelo cliente e ferramentas não
registradas são recusados. O corpo tem limite de 4 KiB; não há chamadas externas.
Não há provedor, chat ou geração por IA nesta primeira entrega. Nenhuma chave é necessária.

Próxima etapa: `queryAnalytics` com o serviço medido e os limites já existentes.
[Backlog e critérios](../../../docs/roadmap/fase-4-backlog.md).
