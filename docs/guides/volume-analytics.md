# Volume de consultas com dados sintéticos

Ensaio local L03 em **19/10/2026**. [Resultado estruturado](../operations/volume-analytics-2026-10-19.json).

## Cenário e execução

```powershell
node scripts/performance/Test-AnalyticsVolume.mjs
```

Requer stack local, Node.js e Docker. O script cria duas contas/organizações
sintéticas exclusivas pela API e insere diretamente no Warehouse 10.000 fatos
por organização, com 20 clientes e um produto. Os valores por cliente e os totais
são calculados independentemente no gerador e conferidos por SQL e pela API.
Não altera o tenant Demo nem organizações anteriores.

Quatro usuários virtuais fazem 30 consultas cada, com pausa de um segundo após
cada resposta. Cada organização tem dois usuários. Todas as consultas passam pelo
Gateway, agrupam receita por cliente e exigem os 20 grupos com os valores esperados.
Nomes e valores distintos por organização ajudam a detectar resposta cruzada.
O limite de linhas varia de 20 a 49 para variar as chaves de cache; não há bypass
do cache nem relaxamento das quotas ou da autorização.

Os tempos são medidos no cliente, incluindo Gateway/transporte. Percentis usam
ordenação e posto `ceil(p*N)`. A janela concorrente não inclui cadastro, inserção
SQL ou as duas leituras iniciais. Inclui as pausas no cálculo de requisições/segundo.

## Resultado observado

| Indicador | Resultado |
|---|---:|
| Organizações sintéticas novas | 2 |
| Fatos por organização / total | 10.000 / 20.000 |
| Receita esperada, organização A | 1.495.000 |
| Receita esperada, organização B | 2.495.000 |
| Consultas concorrentes / respostas 200 | 120 / 120 |
| Falhas de consulta ou divergências de valores | 0 |
| Janela concorrente | 33,925 s |
| Ritmo com pausas | 3,537 consultas/s |
| Latência p50 / p95 / p99 | 105,642 / 242,388 / 577,292 ms |
| Leituras iniciais A / B | 389,85 / 113,45 ms |

O diagnóstico posterior passou: front/API/Gateway disponíveis, quatro containers
saudáveis, um consumidor e filas vazias. O ambiente passou a ter 15 organizações;
três têm algum sucesso de carga no Warehouse. As duas fixtures novas não criam
histórico de carga bem-sucedida: `dataFreshnessAt` permanece nulo e isso é exigido
pelo script. A presença dos fatos não é apresentada como ingestão concluída.

## Cobertura e limites

O ensaio cobre fatos sintéticos inseridos diretamente no Warehouse e consultas
reais com agrupamento por cliente. Usa chaves auxiliares sentinela, um produto e
um único dia; não representa a distribuição de um ERP real nem consultas por
empresa, calendário ou moeda. O esquema dimensional completo não é exercitado.

Não executa extração REST, raw, canônico, reconciliação canônico/Warehouse ou
processamento de 20 mil registros pelo Worker. A conferência é entre o gerador,
os fatos SQL e as respostas analíticas. O pipeline pequeno já possui evidência
separada em [ingestão completa](recuperacao-minio-ingestao.md).

Há consultas com potencial reaproveitamento de cache. O contrato público não
expõe `cacheHit`, portanto não são reportadas taxas de acerto nem percentis de
cache frio. Quatro usuários com pausas não medem throughput máximo, capacidade
certificada, disponibilidade mensal ou capacidade de produção.

## Evidências e repetição

Cada execução cria uma pasta exclusiva `%TEMP%/eip-volume-<id>` com `result.json`
e `samples.json`. O resultado registra IDs sintéticos, receitas esperadas, tempos
e eventuais falhas, sem tokens/senhas. Contas, auditoria, uso e dados sintéticos
permanecem no banco. A repetição cria novas organizações; não há limpeza automática.
O resultado versionado corresponde à primeira execução aprovada de 19/10.

As próximas ampliações devem mudar uma variável por vez, com volume/cenário
declarados e evidência própria. A retomada da aplicação sobre cópias recuperadas
continua um ensaio separado do [backup/restauração SQL/raw](recuperacao-conjunta-demo.md).
