# Diagnóstico local EIP

Resultado: **passed**. Coleta: 2026-10-19T17:22:54.1124656+00:00.

| Conferência | Resultado |
|---|---|
| Frontend | OK (HTTP 200) |
| API viva | OK (HTTP 200) |
| Dependências da API | OK (HTTP 200) |
| Gateway e origem Demo | OK (HTTP 200) |
| eip-sqlserver | running / healthy |
| eip-redis | running / healthy |
| eip-rabbitmq | running / healthy |
| eip-minio | running / healthy |
| Consumidores de sincronização | 1 |
| Mensagens em espera / processamento | 0 / 0 |
| Mensagens em retry / fila de falhas | 0 / 0 |
| Cargas pendentes / em andamento / com falha em 24 h | 0 / 0 / 0 |
| Organizações com algum sucesso no Warehouse / total | 3 / 13 |

- Verificação pontual; não é SLO nem monitoramento contínuo.
- Não executa login, ingestão ou reconciliação.
- Sem prazo de frescor acordado; ausência de carga não indica dados frescos.
