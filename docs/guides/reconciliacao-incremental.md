# Reconciliação de cargas incrementais

Correção concluída em **19/10/2026** e aplicada ao Worker local após validação.
[Testes](../operations/reconciliacao-incremental-testes-2026-10-19.json),
[ensaio com volume](../operations/reconciliacao-incremental-volume-2026-10-19.json)
e [diagnóstico final](../operations/diagnostico-apos-reconciliacao-2026-10-19.json).

## Problema e correção

No primeiro L07, o pipeline reportou 2.000 notas novas / 200.000 BRL e a
reconciliação consultou todo o histórico do conector: 2.004 notas / 211.110 BRL.
As quatro notas anteriores eram legítimas, mas geraram alerta fora da tolerância.

Quando a extração tem `updatedSince` (marcador automático ou reprocessamento por
data), o Worker agora passa a chave raw da extração à reconciliação. O store
combina os filtros de tenant, origem e `RawObjectUri`; somente as notas efetivamente
persistidas daquele lote entram na contagem/soma. Isso também inclui atualizações
de notas antigas, cuja referência raw é substituída pelo upsert.

Quando a extração é completa (`updatedSince == null`, inclusive reprocessamento
completo), a comparação continua usando todo o histórico da origem. Um relatório
completo que omita notas persistidas continua divergente. Consultas do serviço
sem o filtro opcional mantêm esse contrato anterior.

Contagem exata e tolerância de valor de 1% permanecem iguais. Um lote sem registros
tem totais zero; um lote reportado mas não persistido é detectado. A reconciliação
canônico/fatos continua consultando o histórico completo do conector.

## Validação

Os testes SQL de reconciliação cobrem inclusão e atualização incrementais com
histórico anterior; mesma chave raw em outra origem; diferença de valor/quantidade;
valores dentro/fora da tolerância; lote vazio e lote ausente; totais históricos e
relatório completo incompleto. A suíte de conector cobre também watermark,
reprocessamento e idempotência de retry.

O verificador `-VerifyVolume` agora exige explicitamente ausência do alerta
Canônico↔Origem no log do Worker para a fixture correta, além das verificações
de raw, contagens, fatos, receita, linhagem, frescor e filas.

Resultados automatizados de 19/10/2026: pipeline **13/13**, conector **10/10**.
O recorte final de reconciliação passou **11/11**, incluindo a omissão de histórico
em relatório completo; esse recorte faz parte dos 13 e não deve ser somado de novo.
O build Release do Worker passou. Permanecem avisos de análise já existentes em
migrations e outros arquivos, sem erros de compilação.

Na repetição real do L07, os 200 clientes, 50 produtos e 2.000 notas foram aceitos
sem rejeições. SHA-256/bytes e metadados raw conferidos; contagens finais
206 clientes / 55 produtos / 2.004 notas / 2.006 fatos; receita **211.110 BRL**.
`originCanonicalWarningAbsent: true` confirma a ausência do alerta indevido.
Linhagem, frescor, reconciliação canônico/fatos e cinco filas vazias aprovados.
Consulta posterior: 56,879 ms em uma amostra; não é certificação de desempenho.

Recursos isolados removidos, com conferência posterior pelas etiquetas. A fila
original estava vazia antes da troca do Worker identificado; a nova publicação
Release iniciou em Development e assumiu o consumidor. Diagnóstico posterior
aprovado: front/API/Gateway disponíveis, quatro dependências saudáveis, um
consumidor, filas vazias e 15 organizações. Dados originais preservados.

## Limites

A comparação usa o estado canônico atual vinculado ao raw, sem introduzir snapshot
imutável ou isolamento entre execuções concorrentes sobre a mesma nota. O relatório
do pipeline se refere aos registros aceitos; não substitui totais de controle
independentes fornecidos por um ERP. Não houve migration nem mudança no frontend.
