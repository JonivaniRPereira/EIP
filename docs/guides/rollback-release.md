# Rollback de migration e de imagem

Runbook da Fase 3 E5.2. Validado localmente em 18/10/2026.

## Ensaio reproduzível e isolado

Na raiz do repositório, com PowerShell, .NET SDK 10, `dotnet-ef`, Git/tar e Docker:

```powershell
./scripts/rollback/Test-Rollback.ps1
```

Parâmetros opcionais: `-DockerPath`, `-OutputDirectory` (pasta nova) e `-PreviousRef`.
O padrão de ref anterior é o commit existente `3c2de50`, que antecede a medição E4.2.
A candidata é o código atual do diretório de trabalho, incluindo alterações ainda não commitadas.
Não são apresentados como releases de produção. Os binários são publicados em diretórios
temporários independentes, sem substituir as DLLs usadas pela aplicação local.

O script executa dois ensaios independentes:

1. Cria um SQL Server descartável, com porta aleatória ligada somente a `127.0.0.1`, e aplica o
   módulo Tenant até `20260801174154_AddRoleToMembership`. Insere dois tenants, duas empresas e
   uma membership sintéticos. Calcula um manifesto de dados/políticas.
2. Aplica a migration real `20260802013236_AddCompanyCountryCodeAndTradeName`, confirma a coluna
   nova, testa RLS e executa `dotnet ef database update 20260801174154_AddRoleToMembership`.
   Confirma a remoção da coluna, roda `DBCC CHECKDB` e exige o mesmo manifesto anterior.
3. Extrai a ref anterior usando `git archive`, publica o Host dessa ref e o Host atual e cria
   duas imagens locais diferentes, usando o runtime ASP.NET 10 e usuário `app`, como a imagem do
   projeto. Este empacotamento local não substitui o build multi-stage da CI.
4. Inicia a imagem anterior, depois a candidata e então a **mesma imagem anterior** novamente.
   Nas três etapas, exige HTTP 200 em `/health/live` e obtém a definição OpenAPI. Verifica o ID
   da imagem e o hash da definição anterior depois do rollback. Só o campo OpenAPI `servers`
   é removido da comparação, pois as portas de teste são aleatórias.
5. Registra os resultados e remove apenas os containers com label `eip.rollback.run` deste ensaio.
   Restaura o valor anterior da variável de conexão ao terminar. As imagens e evidências locais
   permanecem disponíveis para inspeção.

A connection string do ensaio é gerada a partir do container criado pelo script e só existe no
processo. A checagem de propriedade do container precede cada migration, consulta e remoção.
O banco `eip-sqlserver/EIP` da aplicação aberta não é alvo do rollback.

## Procedimento para uma reversão real

Antes de alterar o ambiente, identificar a versão em execução e a anterior: commit, imagem
imutável, configurações de execução e migrations de **cada módulo**. Tags mutáveis como `latest`
não são evidência suficiente. No registro de imagens, preservar o digest do manifest; no ensaio
local, `result.json` registra os IDs locais de imagem, que são identificadores diferentes.

1. Criar e validar um backup recuperável conforme `backup-restauracao.md`. Preservar as imagens
   existentes e a configuração de deploy (rede, portas, variáveis, segredos e volumes).
2. Verificar compatibilidade da versão anterior com o schema atual. Se ela tolerar a migration
   aditiva, preferir voltar apenas a imagem, mantendo o schema e os dados novos.
3. Se o schema também precisar voltar, suspender escritas e workers envolvidos. Revisar o método
   `Down` e os dados que serão removidos. Usar uma conexão explicitamente conferida para o ambiente
   correto; não depender dos defaults de desenvolvimento da factory de migrations.
4. Executar a reversão do módulo afetado, escolhendo a migration anterior exata. No ensaio:

   ```powershell
   dotnet ef database update 20260801174154_AddRoleToMembership --project src/Platform/Tenant/Infrastructure --startup-project src/Platform/Tenant/Infrastructure
   ```

5. Recriar o processo/container com a imagem anterior identificada, mantendo a configuração
   aprovada do ambiente. Confirmar que o ID/digest em execução corresponde à imagem escolhida.
   Não reconstruir uma tag antiga a partir do código atual: isso não recupera o artefato anterior.
6. Validar `/health/live`, `/health/ready`, login/permissões, consultas, sincronização e dados
   conhecidos antes de retomar escritas/workers. Conferir RLS e erros nos logs. Se a validação
   falhar, manter o ambiente indisponível para escrita e recuperar o backup em instância separada;
   não alternar repetidamente migrations sem avaliar o estado dos dados.

Neste repositório, o Compose atual contém dependências, e o Host local roda como processo .NET.
O ensaio cria seus próprios containers do Host; ele não muda a imagem de um serviço Host já
definido no Compose, pois esse serviço ainda não existe nesse arquivo.

## Limites do que foi provado

A migration usada no ensaio remove `CountryCode` e `TradeName` ao executar `Down`. Os dados
anteriores foram preservados porque foram criados **antes** dessas colunas; o ensaio não prova
preservação de valores gravados nas colunas que serão descartadas. Quando esses valores já forem
necessários, usar correção para frente, exportação/reconciliação ou recuperação planejada do backup.
O mesmo cuidado vale para a migration inicial da medição: revertê-la apagaria seu histórico novo.

O ensaio de migration e o ensaio de imagens são separados: não representam uma combinação
homologada de uma release antiga com um schema antigo. Os containers de imagem não usam banco,
Redis ou RabbitMQ para as verificações HTTP; `/health/live` é independente dessas dependências.
`fullReadinessTested=false` é registrado explicitamente. A definição OpenAPI confirma qual API
voltou, mas não substitui os fluxos de negócio e a readiness exigidos numa reversão real.

Não existe promoção/publicação em registro nem troca da aplicação aberta neste ensaio. A execução
requer acesso à imagem base ASP.NET e às dependências .NET, ou caches previamente disponíveis.

## Evidência de 18/10/2026

Resumo versionado: `docs/operations/rollback-2026-10-18.json`.

- Migration aplicada e revertida via EF Core, com logs de `Applying` e `Reverting`.
- Quatro tabelas e oito linhas anteriores ao upgrade (cinco de negócio e três do histórico de
  migrations) com hashes idênticos após a reversão; RLS e `DBCC CHECKDB` passaram.
- Duas publicações do Host e duas imagens distintas; ID e hash OpenAPI da anterior recuperados
  exatamente após executar a candidata. Liveness passou nas três instâncias.
- Ensaio em 85,41 segundos no ambiente local; isso não define RTO de produção.

Os logs, manifestos, definições OpenAPI, fontes extraídas e publicações ficam na pasta
`$env:TEMP/eip-rollback-<id>` exibida pelo script. `result.json` só é gravado após as verificações
passarem. Falhas conservam os arquivos para diagnóstico e interrompem o fluxo. Imagens criadas
possuem tags `eip-rollback-host:<id>-previous` e `<id>-candidate`; sua remoção posterior deve usar
essas tags/IDs específicos, sem limpeza global de imagens de outros projetos.
