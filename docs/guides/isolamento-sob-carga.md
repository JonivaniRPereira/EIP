# Isolamento entre tenants sob concorrência

Fase 3, E6.3 — execução local em **19/10/2026**. Resultado: **passou no escopo exercitado**, com zero divergência nas asserções. Evidência estruturada: [isolamento-carga-2026-10-19.json](../operations/isolamento-carga-2026-10-19.json).

## Cenário real

Gateway em `localhost:5000`, Host em `localhost:5080`, SQL Server, Redis e RabbitMQ locais. Não foram desativados RLS, autenticação, cache ou limitadores. Os processos já existentes foram utilizados; tentativas adicionais de iniciar Host/Gateway encontraram as portas ocupadas e não substituíram esses processos.

O script cria quatro contas/tenants sintéticos por execução, provisionados pelas APIs públicas, e um conector de clientes em cada tenant. Insere, por transação com contexto do próprio tenant, um cliente, um produto e um fato comercial sintéticos no Warehouse. Nomes e receitas são distintos: `Isolation-0`/1001, `Isolation-1`/2001, `Isolation-2`/3001 e `Isolation-3`/4001. As demais chaves de dimensão não consultadas neste cenário recebem zero: são fixtures mínimas para a consulta testada, não uma carga comercial integral.

Oito fluxos assíncronos, dois por tenant, executaram cinco rodadas. Cada fluxo faz login e então seis chamadas em paralelo: analytics, leitura do próprio sync, leitura de sync alheio, tentativa de sync alheio, sync próprio limitado pela quota e consulta com métrica inválida. Cada resultado analítico deve conter exatamente a identificação sintética esperada e o valor exato do próprio tenant. Todos os tenants usam a mesma definição de consulta, exercitando também a separação do cache.

Antes da carga, quatro syncs próprios foram aceitos com HTTP 202, IDs distintos e publicação real no RabbitMQ. As rodadas seguintes exigem 429 com `Retry-After` válido para o mesmo conector. Uma prova adicional SQL troca o contexto 40 vezes na mesma conexão e consulta `DimCustomer` **sem filtro de tenant**: vê exatamente um cliente correto em cada contexto; ao retirar o contexto, vê zero. Essa prova verifica RLS diretamente, separada da concorrência HTTP que usa o pool normal da aplicação.

## Resultados

| Operação | Chamadas | Resposta esperada observada | p95 / p99 cliente (ms) |
|---|---:|---|---:|
| Login | 40 | 40 × 200 | 175,857 / 177,598 |
| Analytics com sentinela | 40 | 40 × 200; todas as linhas/receitas corretas | 61,540 / 70,602 |
| Leitura de sync próprio | 40 | 40 × 200; ID correto | 50,966 / 52,601 |
| Leitura de sync de outro tenant | 40 | 40 × 404 | 39,575 / 52,235 |
| Disparo em conector de outro tenant | 40 | 40 × 400 | 32,129 / 52,460 |
| Quota de sync próprio | 40 | 40 × 429 com Retry-After de 1–60 s | 35,283 / 52,506 |
| Métrica inexistente | 40 | 40 × 400 | 24,051 / 26,477 |

Total da fase concorrente: **280 requisições em 44,920 s, 6,233 req/s**, incluindo quatro pausas de 11 s entre rodadas. A taxa é deliberadamente limitada; não representa capacidade máxima. Percentis por ordenação das 40 amostras de cada operação, incluindo a chamada completa pelo Gateway; não são o histograma de processamento do Host nem uma avaliação de disponibilidade de 30 dias.

Rajada adicional independente: 120 chamadas anônimas simultâneas ao catálogo, com 44 respostas 401 e **76 respostas 429** do Gateway. O limite real é 100 requisições por IP a cada 10 s, compartilhado pelas rotas. A janela já tinha tráfego da última rodada; por isso não se esperam exatamente 20 bloqueios. Nenhuma chamada anônima recebeu dados.

## Ajustes do ensaio e limites

Uma tentativa inicial de preparar fixtures precisou habilitar `QUOTED_IDENTIFIER` no cliente SQL por causa dos índices filtrados; a transação falhou sem inserir os dados comerciais. O teste seguinte lançou 1.680 chamadas sem pausa e atingiu o limitador por IP, impedindo a avaliação completa dos retornos de negócio. Foi uma execução reprovada do ensaio, não evidência de vazamento. O script foi ajustado para oito fluxos e pausas, mantendo os controles reais ativos; a rajada passou a ser avaliada separadamente. Não houve alteração do código de produção nesta atividade.

Limites materiais:

- Um cliente/produto/fato por tenant; duração curta e apenas máquina local. Não demonstra capacidade para volume de produção nem ausência universal de falhas de isolamento.
- Sync cobre autorização, persistência, aceite/publicação e quota. **Não valida conclusão do Worker, ingestão raw, transformação canônica ou retry/DLQ de ponta a ponta.** MinIO continua indisponível nesta stack; nenhum resultado presume que esse caminho funcionou.
- Prova SQL sem filtro cobre `DimCustomer`; os demais recursos são exercitados pelos endpoints e suas defesas combinadas. Não é uma prova isolada de todas as políticas RLS.
- Dados sintéticos ficam no banco e mensagens aceitas podem permanecer na fila. Não há limpeza destrutiva automática. As três tentativas criaram ao todo nove contas/organizações sintéticas; somente as quatro da execução aprovada constam na evidência final. Não reutilizar esses usuários como contas reais.

## Reexecução

Com Node 22+ e a stack local funcionando:

```powershell
node scripts/performance/Test-TenantIsolation.mjs
```

O Gateway deve estar sem outro tráfego intenso: seu limitador usa o IP, não o usuário. O script aguarda 11 s antes dos primeiros syncs e entre rodadas. O código recusa Gateway fora do loopback; usa o container local `eip-sqlserver` e banco `EIP`. `EIP_DOCKER` permite informar o executável Docker; `EIP_TEST_GATEWAY` permite outra URL local.

As credenciais são geradas em memória, não persistidas no relatório. `result.json` e `samples.json` são gravados em diretório temporário novo `eip-isolation-<runId>`; não incluem corpos de autenticação nem tokens. O resultado aprovado foi preservado no repositório; amostras permanecem no diretório temporário cujo sufixo está no `runId` da evidência. Código de saída diferente de zero indica falha de preparação ou de qualquer asserção.
