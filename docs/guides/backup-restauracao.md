# Backup e restauração do SQL Server

Runbook da Fase 3 E5.1. Ambiente local com Docker Compose; atualizado em 18/10/2026.

## Ensaio reproduzível

Para conferir também os objetos raw referenciados pela organização Demo em storage
isolado, ver [recuperação conjunta](recuperacao-conjunta-demo.md). A opção
`-KeepRestoredContainer` deste script é usada apenas para compor esse procedimento;
quem a usar diretamente deve remover a cópia após conferir sua label `eip.recovery.run`.

Pré-requisitos: Docker disponível, `eip-sqlserver` saudável, banco `EIP` com as migrations atuais
e imagem SQL Server 2022 CU14 local. O script usa o `sqlcmd` da própria imagem; não exige cliente
SQL nem ferramenta adicional no Windows.

Na raiz do repositório, em PowerShell:

```powershell
./scripts/backup/Test-Recovery.ps1
```

Caso o Docker não esteja no PATH, o script procura a instalação por usuário em
`$env:LOCALAPPDATA/Programs/DockerDesktop/resources/bin/docker.exe`. Também aceita
`-DockerPath`, `-SourceContainer`, `-Database` e `-OutputDirectory`. O diretório de saída precisa
ser novo. Sem esse parâmetro, cria uma pasta única em `$env:TEMP/eip-recovery-<id>`.

O script executa, nesta ordem:

1. Descobre os arquivos lógicos do banco de origem e cria `BACKUP DATABASE ... WITH COPY_ONLY,
   CHECKSUM, INIT` em um arquivo exclusivo. Copia o `.bak` para fora do container e calcula SHA-256.
2. Sobe um SQL Server descartável sem portas publicadas e com senha aleatória. Verifica o backup
   com `RESTORE VERIFYONLY ... WITH CHECKSUM` e restaura com `MOVE` para arquivos novos.
3. Executa `DBCC CHECKDB`, o gate de cobertura RLS e provas de leitura/gravação entre tenants.
   Gera um manifesto com contagem e SHA-256 do conteúdo de cada tabela, ordenado por chave primária,
   e as definições/estados dos predicados e políticas RLS.
4. Remove esse container de teste com seu armazenamento descartável. Sobe uma segunda instância
   nova e restaura novamente a partir do backup externo.
5. Repete as verificações e exige manifestos idênticos. Grava `result.json` apenas se tudo passar.
6. Remove os containers criados por este ensaio e o arquivo temporário de backup na origem.
   Mantém o backup exportado e os manifestos no diretório de evidências.

A comparação usa o snapshot do backup nas duas restaurações; não compara com o banco vivo, que
pode receber alterações enquanto o ensaio roda. O container original, seus volumes e os processos
da aplicação continuam disponíveis. A remoção de containers exige nome distinto da origem e
label `eip.recovery.run` igual ao ID gerado para esta execução. Nenhum container preexistente é
removido. Falhas conservam os arquivos de diagnóstico e interrompem o fluxo; não geram sucesso.

## Evidência de sucesso

Na pasta exibida pelo script:

| Arquivo | Conteúdo |
|---|---|
| `database.bak` | Backup completo do banco, exportado antes da perda simulada |
| `baseline.json` | Contagens, hashes e políticas da primeira cópia restaurada |
| `restored.json` | Mesmos dados verificados na instância substituta |
| `result.json` | Status, versão da imagem, hash do backup, total de tabelas/linhas/políticas e duração |

Exigir `status=passed`, `tableHashesMatch=true`, `checkDbPassed=true`, `rlsProbePassed=true`
e `restoredToNewInstance=true`. Código de saída zero sem esses artefatos não constitui aceite.

A prova RLS inclui: nenhuma leitura sem contexto; tenant A lê apenas sua empresa; tentativa de
INSERT com TenantId B sob contexto A é bloqueada pelo SQL Server. As empresas de prova são
inseridas somente nas cópias e desfeitas por rollback. O manifesto desativa políticas somente
na cópia descartável, dentro de uma transação revertida, para calcular hashes de **todos** os
dados. A prova RLS é repetida depois dessa transação para confirmar que as políticas continuam
ativas. Nunca executar `manifest.sql` manualmente contra o banco em uso: ele foi feito para a cópia.

## Recuperação operacional

Em uma recuperação real, preservar primeiro o `.bak` fora do host/container perdido e conferir
seu SHA-256. Usar uma instância SQL Server compatível, vazia e isolada; executar `RESTORE VERIFYONLY`,
restaurar com `MOVE`, rodar `DBCC CHECKDB` e verificar a cobertura/comportamento RLS antes de
apontar Host/Worker para ela. A validação de dados usa um manifesto obtido da cópia do mesmo backup.
Se não houver manifesto anterior, ainda é necessário conferir dados conhecidos do negócio; não
declarar equivalência histórica apenas porque o comando RESTORE funcionou.

Não usar `WITH REPLACE` sobre o banco em uso. A troca da conexão da aplicação e a retomada de
processamento são uma etapa posterior à validação da instância recuperada. Conservar a origem
até terminar a conferência. Aplicar migrations só se forem necessárias para a versão da aplicação
que será iniciada; atualizar o schema antes da conferência invalida a comparação dos manifestos.

## Limites e diagnóstico

- O `.bak` contém os dados e credenciais persistidas do ambiente. Guardar em local restrito,
  fora do Git. A pasta temporária é evidência local; não implementa retenção nem backup remoto.
- Este ensaio cobre SQL Server. Data Lake/MinIO, fila RabbitMQ, Redis e arquivos de configuração
  têm recuperação própria. Não é uma prova de consistência distribuída entre banco e storage.
- Usa backup completo pontual; não configura agendamento, cadeia de logs ou recuperação para
  qualquer instante. A duração local do ensaio não é um RTO de produção.
- Suporta arquivos SQL `ROWS`/`LOG`. FILESTREAM, TDE/certificados, criptografia de backup e bancos
  com componentes adicionais exigem procedimento específico e preservação das chaves.
- Falha em `VERIFYONLY` ou hash: preservar o arquivo, obter outra cópia íntegra e repetir em
  instância nova. Falha no `DBCC CHECKDB`: não liberar a recuperação como concluída.
- Hashes de tabela divergentes ou RLS falhando: preservar manifestos e investigar antes de trocar
  conexões. Tabela sem chave primária impede o manifesto determinístico e gera falha explícita.
- Falha ao iniciar container: conferir memória do Docker e logs do container de teste enquanto
  ele existir. O script limita a espera de prontidão a dois minutos e executa limpeza em `finally`.

## Registro da validação

A evidência da execução local está em `docs/operations/recuperacao-sql-2026-10-18.json`.
O arquivo versionado contém somente o resumo técnico; backup e manifestos permanecem na pasta
local indicada na execução.
