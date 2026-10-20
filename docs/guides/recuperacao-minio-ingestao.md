# Recuperação do MinIO e ingestão completa — 19/10/2026

## Recuperação local

A imagem histórica `minio/minio:RELEASE.2025-04-08T15-41-24Z` não estava disponível no ambiente.
Quay retornou 401 para essa tag; o arquivo binário oficial em `dl.min.io` retornou 410.
O [projeto oficial documenta distribuição por código-fonte](https://github.com/minio/minio/blob/master/README.md?plain=1).
Recuperamos **o mesmo MinIO**, compilando a tag já fixada; não foi usado S3Mock para substituir
o serviço local. A versão não foi promovida como recomendação ou homologação de produção.

O Dockerfile `deploy/docker-compose/minio-source.Dockerfile` usa Go 1.24 (resolveu 1.24.13)
e `go install` da tag oficial, resolvida ao módulo
`v0.0.0-20250408154124-d0cada583fce`. O binário reporta `DEVELOPMENT.GOGET`, pois a compilação
por módulo não embute o carimbo de release da imagem antiga; isso não deve ser apresentado
como binário oficial pré-compilado. Runtime com usuário 10001, curl para healthcheck e licença
AGPL incluída. O código-fonte de origem continua disponível no repositório indicado na imagem.

Imagem local construída: `eip-minio-source:RELEASE.2025-04-08T15-41-24Z`.
Identidade observada:
`sha256:1f0d3b8051e3e49485459817fddfa78b2d46385c66c04b38979636a37634a9db`.
Tags de bases podem mudar; o registro dessa identidade comprova a imagem usada neste ensaio,
não garante builds futuros bit a bit idênticos. Antes de produção, decidir versão/produto
mantido, revisar dependências/imagem e homologar atualização e restauração do storage.

Para construir/subir explicitamente a alternativa local, a partir da raiz:

```powershell
docker compose -f deploy/docker-compose/docker-compose.yml -f deploy/docker-compose/compose.minio-source.yml up -d --build minio
```

No Bash, `EIP_BUILD_MINIO_SOURCE=1 ./scripts/dev-up.sh` usa o mesmo override. Sem a variável,
o script continua usando a imagem histórica original. Não executar `down -v` para recuperar
um serviço: isso apagaria dados. Nesta máquina não havia container/volume MinIO anterior;
foi criado `eip-local_minio_data`. SQL, Redis e RabbitMQ existentes foram preservados.

O override só altera build/imagem do MinIO, mantendo credenciais do `.env`, portas, rede,
volume e healthcheck já definidos no Compose. Credenciais de desenvolvimento nunca devem
ser usadas para uma instância pública. O primeiro build exige acesso à imagem Go e aos
módulos oficiais; pode demorar alguns minutos.

## Validação real do pipeline

Script: `scripts/performance/Test-Ingestion.mjs`. Requer Gateway/Host em 5000/5080, Worker
atual em Development, MinIO em 9000 e dependências locais. Executar na raiz:

```powershell
node scripts/performance/Test-Ingestion.mjs
```

O teste cria uma organização sintética e conectores dos endpoints de amostra existentes,
sincronizando clientes → produtos → notas. Aguarda estado `Succeeded`, confere persistência
e valores, consulta analytics, reconciliação e linhagem, lê os três objetos diretamente
do MinIO com assinatura S3 e verifica SHA-256 contra metadados. Por fim, cria um painel,
confere a consulta do widget e publica uma versão. Contas/tokens gerados ficam só em memória;
dados sintéticos, objetos e painel são preservados. O relatório não inclui credenciais.

Resultado aprovado: [evidência](../operations/ingestao-completa-2026-10-19.json).

| Etapa | Resultado |
|---|---|
| Clientes / produtos | 6 / 5 persistidos |
| Notas / itens canônicos | 4 / 6 persistidos |
| Fatos Warehouse | 6, receita líquida total **11.110,00** |
| Quarentena | 0 |
| Raw MinIO | 3 objetos lidos; JSON válido e checksums conferidos |
| Analytics / painel | Receita 11.110,00, frescor presente; versão publicada |
| Linhagem | Fato → canônico → raw, sync e lote bem-sucedidos |
| Catálogo de métricas | 3 métricas com reconciliação dentro da tolerância e totais iguais |

A primeira tentativa do script omitiu, nos valores esperados, os registros `C099`, `P099`
e a nota `NF-E7-ALWAYS`, que já faziam parte das amostras. A persistência estava correta;
os valores esperados foram corrigidos para o conjunto completo e o ensaio foi repetido
com nova organização. As duas organizações ficaram preservadas. Não houve correção de
cálculo ou alteração das amostras para fazer o teste passar.

Worker recompilado em Release e mantido executando localmente; log em
`%TEMP%/eip-ingestion-worker.log`. Também processou os oito pedidos sintéticos pendentes
dos ensaios anteriores. O [snapshot posterior](../operations/operacao-apos-recuperacao-storage-2026-10-19.json)
mostra 13 tenants, zero runs pendentes e dois tenants com carga Warehouse bem-sucedida.
O snapshot anterior do fechamento foi preservado, pois descreve o estado anterior à recuperação.
MinIO healthy, API readiness 200 e frontend em `http://127.0.0.1:4200` HTTP 200.

## Suítes de integração com MinIO real

Resultado final: **122/122 testes aprovados em nove suítes, zero falhas/ignorados**.
[Resumo da recuperação e dos testes](../operations/recuperacao-minio-2026-10-19.json):
canônico 5, catálogo 4, conector 9, Data Lake 3, pipeline 4, Warehouse 11,
Host/API 73, dashboards 8 e tenant 5. TRX e logs completos permanecem em
`%TEMP%/eip-ingestion-test-results` e `%TEMP%/eip-ingestion-all-tests-restored.log`.
A primeira execução sem restaurar a solução inteira executou apenas duas suítes; não foi
usada como evidência de cobertura completa. A execução final conferiu os nove arquivos TRX.

`MinioContainerFixture` aceita `EIP_TEST_MINIO_IMAGE` para usar a imagem local de modo
explícito; o padrão continua sendo a imagem histórica. Não muda o servidor por um mock.
Restaurar toda a solução antes de executar testes evita omitir projetos ainda sem assets
no diretório de artefatos escolhido:

```powershell
$env:EIP_TEST_MINIO_IMAGE = 'eip-minio-source:RELEASE.2025-04-08T15-41-24Z'
dotnet restore EIP.slnx
dotnet test EIP.slnx --no-restore -m:1
```

Na validação foram usados diretórios temporários de artefatos/logs, sem tocar nos binários
dos serviços em execução. Resultados finais são registrados junto à evidência operacional
da recuperação. Retry/DLQ/requeue são verificados nos testes com RabbitMQ real; o ensaio
manual descrito acima cobre o caminho de sucesso, sem provocar interrupção ou perda de dados.

Essa validação local não inclui homologação de produção, interface completa no navegador,
teste de desastre do Data Lake nem política de retenção automática. Esses trabalhos continuam
separados dos ensaios SQL e das correções já concluídas.
