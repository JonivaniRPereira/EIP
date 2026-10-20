# Volume sintético pelo pipeline completo

Ensaio executado em **19/10/2026**, aprovado para transporte, aceitação,
contagens e reconciliação canônico/fatos, com uma pendência na comparação
origem/canônico descrita abaixo. [Resultado](../operations/volume-pipeline-2026-10-19.json).
Atualização posterior: [correção validada](reconciliacao-incremental.md), com nova
execução do mesmo volume sem o alerta indevido. Os números abaixo preservam a
primeira execução e sua limitação histórica.

## Resultado observado

Todos os 2.250 registros foram aceitos, sem rejeições. Foram produzidos três
objetos raw novos, com 24.981, 7.481 e 515.391 bytes, iguais às respostas da origem.
Contagens finais: 206 clientes, 55 produtos, 2.004 notas e 2.006 fatos;
receita **211.110 BRL**. Linhagem, frescor e reconciliação canônico/fatos aprovados.

| Medição no cliente | Tempo |
|---|---:|
| Clientes: pedido até Succeeded observado | 7.804 ms |
| Produtos: pedido até Succeeded observado | 2.414 ms |
| Notas: pedido até Succeeded observado | 48.531 ms |
| Espera adicional pelos fatos no Warehouse | 70.747 ms |
| Consulta analítica posterior, uma amostra | 74,804 ms |

Recursos descartáveis removidos e ausência confirmada pelas etiquetas. O primeiro
diagnóstico do original encontrou os processos locais de front/API/Gateway/Worker
ausentes, com os quatro containers saudáveis. Os processos foram restabelecidos;
o [diagnóstico final](../operations/diagnostico-apos-volume-2026-10-19.json) passou,
com um consumidor e filas vazias. Não foram modificados os dados originais.

### Pendência identificada: reconciliação da origem incremental

O Worker registrou alerta ao comparar o lote novo (2.000 notas / 200.000 BRL)
com todo o histórico canônico da origem (2.004 notas / 211.110 BRL). A diferença
corresponde às quatro notas Demo preexistentes, e os fatos conferem com o canônico.
O `status: passed` do verificador cobre seus asserts; não significa que essa
comparação origem/canônico passou. A próxima correção deve alinhar o escopo da
comparação incremental, preservando a detecção de divergências reais e incluindo
testes com histórico anterior mais um lote incremental. Essa correção foi
posteriormente concluída e validada no guia citado no início desta página.

Não houve mudança em código de produção; scripts verificados por sintaxe e
execução funcional real. As suítes API/front não foram repetidas nesta atividade.

## Procedimento L07

Preparar Host/Worker publicados e senha existente da Demo conforme
[retomada da aplicação](retomada-aplicacao.md). Na raiz:

```powershell
./scripts/backup/Test-JointRecovery.ps1 -VerifyApplication -HostDll '<publicação>/EIP.Host.dll' -WorkerDll '<publicação>/EIP.Worker.Sync.dll' -VerifyVolume
```

`-VerifyVolume` exige Worker e é separado de `-VerifyRetry`. SQL completo e raw
Demo são restaurados; API, Worker, Redis, RabbitMQ e MinIO são descartáveis.
Apenas conectores da cópia SQL recebem as URLs da origem sintética local.

## Volume e ritmo

Três respostas JSON, em sequência, sem concorrência nem pausas programadas:
200 clientes, 50 produtos e 2.000 notas, cada uma com um item de 100 BRL.
As chaves usam prefixo L07; cada nota referencia cliente/produto sintético.
A origem fornece os registros novos com updatedAt atual, posteriores ao marcador
restaurado. O parâmetro updatedSince recebido é registrado. Não há paginação da
origem: cada entidade cabe em uma resposta dentro do limite vigente do conector.

Não há inserção direta de registros de negócio no SQL. A única alteração direta
de preparação é a URL dos conectores no banco descartável. Os 2.250 registros
passam por HTTP → raw → validação/canônico → carga de fatos → consulta da API.

## Critérios e medições

- Um pedido à origem por entidade; quantidades recebidas/aceitas exatas, zero rejeitados.
- SHA-256 e bytes do raw iguais aos da resposta original, mais metadados do run/tenant.
- Marcadores avançados e contagens finais iguais à Demo restaurada mais a fixture.
- 206 clientes, 55 produtos, 2.004 notas, 2.006 fatos; receita total 211.110 BRL.
- Linhagem do novo raw, reconciliação canônico/fatos e frescor analítico conferidos.
- Cinco filas presentes e sem mensagens prontas/em processamento ao final.

`runs[].elapsedMilliseconds` mede pedido de sync até observação de Succeeded,
incluindo polling. Esse estado antecede a conclusão da carga Warehouse no Worker.
`volume.warehouseWaitMilliseconds` mede a espera adicional até a contagem de fatos
esperada. `volume.analyticalQueryMilliseconds` mede somente a consulta HTTP posterior,
com uma amostra e cache previamente povoado pelo ensaio de leitura da Demo.
Não usar esses tempos como percentis ou capacidade máxima sustentada.

## Limites

Fixture limitada a uma organização e três payloads; não comprova ingestão de
20 mil fatos, múltiplas origens simultâneas ou capacidade de produção. Não repete
o cenário de falha/DLQ. O ambiente original e seus dados são preservados; somente
o resumo das evidências deve entrar no repositório, sem backups, credenciais ou logs.
