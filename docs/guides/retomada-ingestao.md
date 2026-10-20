# Retomada de ingestão nas cópias restauradas

Nota posterior: a observação das filas de retry foi corrigida e validada no
[L06](retry-retomada.md). O filtro original L05 omitia `connector.sync.retry.*`;
o resultado histórico comprovou filas principal/DLQ vazias, sem essa cobertura.

Ensaio local **L05, 19/10/2026**, aprovado para a organização Demo.
[Resultado estruturado](../operations/retomada-ingestao-2026-10-19.json).

## Execução

Publicar Host e Worker em pastas temporárias e fornecer a senha existente da
conta Demo via `EIP_RECOVERY_DEMO_PASSWORD`, conforme o
[procedimento de leitura](retomada-aplicacao.md). Na raiz:

```powershell
./scripts/backup/Test-JointRecovery.ps1 -VerifyApplication -HostDll '<publicação>/EIP.Host.dll' -WorkerDll '<publicação>/EIP.Worker.Sync.dll'
```

Sem `-WorkerDll`, o procedimento mantém apenas o ensaio anterior de leitura.
Com essa opção, um processo Worker novo usa o SQL restaurado, MinIO descartável,
Redis e RabbitMQ próprios já preparados para a segunda API. O ambiente é
Development e as portas publicadas são efêmeras em loopback.

Somente na cópia SQL, os três conectores Demo passam a apontar para os endpoints
`/api/v1/sample/customers`, `/products` e `/sales-invoices` da **segunda API**.
O Worker recebe a lista exata desses três destinos. A configuração, as URLs e
os marcadores da aplicação original permanecem como estavam.

## Conferências realizadas

Após as provas de login/leitura do L04, o ensaio exige um consumidor ativo no
broker novo. Dispara pela API restaurada uma sincronização normal para cada
entidade, na ordem clientes → produtos → vendas. Cada marcador restaurado já
tem uma data; a origem Demo retorna apenas seu registro com `updatedAt` variável.

Para cada execução, exige sucesso, um recebido/aceito e zero rejeitados; procura
no canônico a referência raw contendo o novo ID da execução. Relê o objeto do
MinIO isolado e confere JSON, SHA-256, tenant e ID de execução nos metadados.
O marcador deve avançar em relação ao valor restaurado e ao instante do pedido.

Depois exige um fato no Warehouse referenciando o novo raw de vendas, contagens
e receita iguais às anteriores, frescor analítico mais recente, reconciliação
correta e linhagem com execução/carga concluídas. A fila principal, retries e
DLQ precisam estar sem mensagens prontas ou em processamento.

## Resultado observado

| Entidade | Recebidos | Aceitos | Atualizados | Rejeitados | Marcador |
|---|---:|---:|---:|---:|---|
| Clientes | 1 | 1 | 1 | 0 | Avançou |
| Produtos | 1 | 1 | 1 | 0 | Avançou |
| Vendas | 1 | 1 | 1 | 0 | Avançou |

- Três novos objetos raw: **223, 168 e 266 bytes**, com checksums e metadados conferidos.
- Antes/depois: **6 clientes, 5 produtos, 4 vendas e 6 fatos**; receita **11.110 BRL**.
- Frescor do Warehouse: de **09:16:29** para **15:16:43**, horário de São Paulo, em 19/10.
- Linhagem e reconciliação aprovadas; nenhuma duplicação de linhas de negócio.
- Filas isoladas vazias ao final; Worker/Host filhos encerrados e containers/rede removidos.
- Diagnóstico posterior do ambiente original aprovado, com 15 organizações, filas vazias
  e nenhuma carga pendente/em andamento/com falha em 24 horas.

Hashes dos binários, IDs das execuções, marcadores completos e hashes/chaves raw
constam em `applicationReadback.ingestion` no resultado. A etapa SQL/raw anterior
também foi repetida, incluindo integridade/RLS e detecção de corrupção na cópia.

Uma primeira tentativa parou na preparação do verificador, antes de publicar
qualquer sincronização: o comando de atualização não devolvia JSON ao helper.
O retorno foi corrigido para exigir uma linha afetada por conector e o ensaio foi
repetido em pasta nova. As evidências da tentativa foram preservadas e seus
recursos temporários removidos.

## Limites e preservação

Esse é um teste incremental pequeno da origem Demo. Não processa 20 mil registros
pelo conector, não simula falha/retry nesta nova etapa e não restaura mensagens de
um broker antigo. Não mede capacidade máxima, RPO/RTO ou operação em Production.
Gateway e frontend próprios não são iniciados. Os resultados de leitura da API
e os widgets do painel são verificados nas cópias.

O raw inicial foi restaurado; os novos objetos foram produzidos pelo Worker no
storage descartável e conferidos antes de sua remoção. Os novos runs e avanços
de marcador existiram somente no banco descartável. Backup, manifestos e logs
locais ficam na pasta temporária restrita; apenas o resumo foi versionado.

Build Release do Worker e ensaio funcional real aprovados. As suítes de API e
frontend não foram repetidas, pois não houve mudança em código de produção.
O conjunto L01–L05 está registrado para uma revisão consolidada do laboratório;
os critérios de piloto externo continuam separados.
