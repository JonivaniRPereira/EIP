# Recuperação conjunta de SQL e raw — laboratório Demo

Ensaio local de **19/10/2026**, concluído no escopo da organização Demo.
[Resultado estruturado](../operations/recuperacao-conjunta-demo-2026-10-19.json).

## Executar

Com a stack local disponível, Node.js, PowerShell e a imagem local de
[MinIO recuperada](recuperacao-minio-ingestao.md):

```powershell
./scripts/backup/Test-JointRecovery.ps1
```

O padrão é a organização Demo `5e17b5c9-5862-4029-a627-d95261cbf8a7`.
`-TenantId` permite selecionar outra organização de demonstração já carregada;
`-DockerPath` e `-OutputDirectory` permitem informar o Docker e uma pasta nova.
Os nomes dos serviços de origem são os da stack local. Credenciais S3 são lidas
da configuração local do Host sem impressão; o storage de origem deve ser loopback.
Não é um comando para ambientes externos.

## O que o procedimento verifica

1. Faz backup SQL `COPY_ONLY` com checksum e restaura em duas instâncias novas,
   com hashes de todas as tabelas, `DBCC CHECKDB` e provas RLS. A primeira cópia é
   removida e a segunda fica disponível temporariamente para a conferência dos objetos.
2. Sob o contexto da organização selecionada, enumera as referências `RawObjectUri`
   nas tabelas canônicas e do Warehouse, inclusive referências históricas persistidas.
   Exige chaves do prefixo da organização. Referências da fixture `synthetic://`
   são contadas separadamente, pois representam diagnóstico artificial sem objeto real.
3. Lê cada objeto real necessário na origem, confere JSON, tenant nos metadados e
   checksum SHA-256. Guarda os bytes e um manifesto na pasta de evidência.
4. Sobe MinIO com credenciais aleatórias, filesystem novo e porta efêmera apenas em
   loopback. Recria bucket/chaves a partir dos arquivos salvos, preservando os metadados.
   Releitura compara bytes/checksum, conteúdo JSON e metadados.
5. Altera deliberadamente um objeto **no storage descartável**. Exige que o checksum
   divergente seja detectado; restaura os bytes do backup e confirma novamente.
6. Remove os containers descartáveis após conferir seus nomes e labels exclusivos.
   O `result.json` conjunto só é emitido depois dessas verificações e da limpeza.

`Test-Recovery.ps1` ganhou a opção explícita `-KeepRestoredContainer` para essa
composição; seu comportamento padrão continua removendo as duas instâncias.
No resultado SQL interno, `keptRestoredContainer=true` descreve a retenção temporária
para a etapa raw. O resultado conjunto registra `isolatedContainersRemoved=true`
após a remoção pelo procedimento principal.

## Evidência observada

| Conferência | Resultado |
|---|---|
| SQL completo | 39 tabelas, 884 linhas e 7 políticas; hashes, integridade e RLS aprovados |
| Objetos reais referenciados pela Demo | 3 objetos, 3.131 bytes; conteúdo e metadados idênticos |
| Referência artificial de diagnóstico | 1 `synthetic://`, contada separadamente; registro preservado no SQL |
| Dados Demo na cópia SQL | 6 clientes, 5 produtos, 4 vendas e 6 fatos; receita líquida 11.110 |
| Corrupção apenas na cópia raw | Detectada e reparada a partir do arquivo salvo |
| Ambiente original após o ensaio | Diagnóstico local aprovado; front/API/Gateway e dependências disponíveis |
| Containers descartáveis | Removidos; nenhum container dos ensaios permaneceu |

Os arquivos brutos da origem e as conexões da aplicação foram preservados. O
diagnóstico posterior confirmou consumidor ativo, filas vazias e nenhuma carga
pendente/em andamento/com falha em 24 horas. Não foram repetidas as suítes da API
e do frontend: esta atividade validou os scripts e a infraestrutura de recuperação.

## Evidências e limites

A pasta de saída contém `sql/database.bak`, manifestos SQL, `raw/manifest.json`,
objetos em arquivos numerados, `raw-result.json` e `result.json`. O backup SQL contém
o banco completo, incluindo credenciais persistidas e dados de outras organizações.
Manter a pasta restrita e fora do Git. Somente o resumo foi versionado.

O backup SQL é completo, mas a recuperação raw está limitada às referências reais
da organização Demo selecionada. Não inclui objetos sem referência atual/histórica,
outras organizações, políticas/lifecycle/versionamento do bucket, fila, Redis ou
configuração/chaves do ambiente. Os objetos referenciados foram lidos após o backup
SQL, aproveitando o padrão de gravação por chave da aplicação; isso não comprova
um snapshot distribuído atômico diante de alterações/exclusões externas.

A aplicação não foi reiniciada contra as cópias, e analytics/painel não foram
homologados nessas instâncias. Não há compromisso de RPO/RTO. Essa etapa será
necessária para um ensaio completo de retomada do serviço.

**Ensaio posterior em 19/10:** a opção `-VerifyApplication` foi acrescentada e
[login/leitura da API foram validados em cópias com cache isolado](retomada-aplicacao.md).
O resultado anterior acima preserva seu escopo SQL/raw. O Worker e a ingestão
ainda não foram retomados nas cópias.
