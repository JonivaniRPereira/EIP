# Falha temporária e retry na retomada

L06 aprovado em 19/10/2026, somente nas cópias restauradas da Demo.
[Resultado completo](../operations/retry-retomada-2026-10-19.json).

## Reprodução

Preparar binários e senha existente por variável de ambiente conforme
[retomada da aplicação](retomada-aplicacao.md). Executar na raiz:

```powershell
./scripts/backup/Test-JointRecovery.ps1 -VerifyApplication -HostDll '<publicação>/EIP.Host.dll' -WorkerDll '<publicação>/EIP.Worker.Sync.dll' -VerifyRetry
```

`-VerifyRetry` exige Worker. Sem essa opção, permanece o cenário L05.
Uma origem HTTP efêmera em loopback responde 503 na primeira extração de clientes
e encaminha as seguintes exclusivamente ao endpoint Demo da API restaurada.
O Worker permite esse endereço exato. Somente os conectores da cópia SQL recebem
as URLs do ensaio. Os tempos reais de retry do Worker permanecem iguais.

## Resultado

- Primeiro acesso: 503 às 20:44:26.589 UTC; segundo: 200 às 20:44:36.854 UTC.
- Intervalo: **10.265 ms**, com uma mensagem observada em `connector.sync.retry.10s`.
- Estado Failed observado e marcador preservado; a mesma execução terminou Succeeded.
- Três sincronizações concluídas, um registro aceito por entidade, zero rejeitados;
  três novos raws com checksums/metadados conferidos.
- Antes/depois: 6 clientes, 5 produtos, 4 vendas, 6 fatos; receita 11.110 BRL.
- Linhagem, reconciliação e avanço do frescor aprovados.
- Fila principal, três filas de retry e DLQ vazias ao final.
- Processos filhos, containers e rede removidos; consulta às etiquetas confirmou
  ausência de recursos remanescentes.

O [diagnóstico posterior](../operations/diagnostico-apos-retry-2026-10-19.json)
aprovou a aplicação original e dependências, com 15 organizações. Backup e logs
detalhados permanecem na pasta temporária local do ensaio.

## Observação das filas e limites

O filtro anterior do diagnóstico e do L05 selecionava somente
`connector.sync.requested*`, omitindo as filas reais `connector.sync.retry.*`.
Foi corrigido e agora exige a presença das três filas. O L05 anterior comprovou
o esvaziamento da principal/DLQ; sua afirmação sobre retries excedia a observação.
O L06 confere explicitamente as cinco filas.

Este cenário valida uma falha transitória e recuperação automática. Não esgota
as tentativas de 30/90 segundos nem valida DLQ por esgotamento. Não mede capacidade,
ingestão em grande volume ou recuperação de mensagens antigas. Sem alteração no
código de produção; scripts validados por sintaxe e ensaio funcional real, sem
repetição das suítes API/front.
