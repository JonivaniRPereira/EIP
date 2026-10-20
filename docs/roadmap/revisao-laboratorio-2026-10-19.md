# Revisão consolidada do laboratório local

Data: **19/10/2026**. Resultado: **L01–L05 concluídos nos escopos descritos abaixo**.
Direção confirmada pelo usuário: continuar localmente com dados Demo/sintéticos.
Esta revisão não inicia piloto externo nem altera o fechamento histórico da Fase 3.

## Aceites e evidências

| Etapa | Aceite conferido | Evidência e limite |
|---|---|---|
| L01 — Diagnóstico | Front/API/Gateway, dependências, consumidor, filas e cargas consultados; indisponibilidade detectada sem parar serviços | [Guia](../guides/diagnostico-local.md). Conferência pontual, sem monitoramento contínuo ou meta de frescor. |
| L02 — Recuperação SQL/raw | SQL restaurado com hashes/DBCC/RLS; objetos Demo recuperados com metadados/checksums; corrupção na cópia detectada e reparada | [Resultado](../operations/recuperacao-conjunta-demo-2026-10-19.json). SQL completo, raw limitado às três referências reais da Demo; uma referência artificial contada separadamente. |
| L03 — Volume analítico | 20 mil fatos em duas organizações; quatro usuários, 120 consultas 200, sem divergência | [Resultado](../operations/volume-analytics-2026-10-19.json). Inserção direta no Warehouse; p95 242,388 ms e p99 577,292 ms com cache e pausas. Não prova ingestão desse volume. |
| L04 — Retomada de leitura | Login restaurado, anônimo negado, analytics/widgets 11.110, linhagem e reconciliação; cache inicialmente vazio | [Resultado](../operations/retomada-aplicacao-2026-10-19.json). Segunda API Development e dependências próprias; sem frontend/Gateway próprios. |
| L05 — Retomada incremental | Três syncs concluídos, um recebido/atualizado por entidade e zero rejeitados; novos raws, marcadores/frescor avançados, sem duplicação e filas vazias | [Resultado](../operations/retomada-ingestao-2026-10-19.json). Worker e fila próprios; cenário pequeno Demo, sem recuperação de mensagens históricas. |

As cinco evidências JSON foram lidas e seus estados `passed` conferidos. No L05,
as três execuções, a ausência de rejeições, os avanços dos marcadores, a igualdade
das contagens antes/depois e a remoção dos recursos temporários foram verificados.
As diferenças de contagem total do banco entre os backups refletem snapshots de
momentos diferentes, incluindo fixtures e auditoria; não devem ser comparadas
como se fossem o mesmo backup.

O diagnóstico foi executado novamente nesta revisão:
[snapshot agregado](../operations/revisao-laboratorio-snapshot-2026-10-19.json).
Front/API/Gateway disponíveis; SQL, Redis, RabbitMQ e MinIO saudáveis; um consumidor;
filas vazias. Quinze organizações, sem cargas pendentes/em andamento/com falha em
24 horas. Três organizações têm algum sucesso no Warehouse; as demais não são
classificadas como dados frescos.

Não foram repetidos todos os ensaios nem as suítes unitárias/integração nesta
revisão. Os últimos resultados de código continuam API 78/78, processamento 10/10
e front 33/33, em suas atividades respectivas. Não somar esses números à execução
anterior de 122 como se fossem testes adicionais.

## Pendências e sequência proposta

| Ordem | Atividade | Critério de conclusão |
|---|---|---|
| 1 | L06 — Falha/retry na retomada isolada | Origem controlada falha e recupera apenas nas cópias; retries e recuperação observados, sem duplicar fatos. Distinguir falha temporária de esgotamento/DLQ; preservar o ambiente original. |
| 2 | L07 — Volume maior pelo pipeline completo | Declarar volume e ritmo; origem sintética autorizada, raw → canônico → Warehouse → consulta, totais e rejeições conferidos. Medir ingestão separadamente das consultas. |
| 3 | Aceite remoto do CI do front (P02) | Publicar alterações em fluxo de revisão acordado e registrar execução real no runner. Instalação/build/testes locais já passaram; essa evidência não substitui GitHub Actions. |

Atualização posterior: **L06 concluído** para falha temporária e retry, conforme
[guia e evidências](../guides/retry-retomada.md). A mesma execução se recuperou
após 10.265 ms, sem duplicação; cinco filas conferidas vazias e recursos removidos.
A limitação do filtro anterior está registrada no guia.

**L07 executado**: 200 clientes, 50 produtos e 2.000 notas pelo pipeline completo,
zero rejeições; raw/canônico/fatos e consulta com receita 211.110 BRL conferidos.
[Resultado e limites](../guides/volume-pipeline.md). Detectado alerta na comparação
do lote incremental com o histórico completo da origem; essa verificação não está
homologada na primeira execução do L07. A [correção posterior](../guides/reconciliacao-incremental.md)
passou em 13 testes do pipeline e 10 do conector, com recorte final de 11 testes
de reconciliação; o recorte integra os 13. Repetição do volume passou sem alerta
indevido e com 211.110 BRL. Worker local atualizado, original saudável.
Próxima atividade: revisar e organizar o conjunto acumulado para entrega revisável;
Atualização posterior: commit `959e3ae` publicado pelo usuário, com
[CI remoto aprovado](../operations/ci-remoto-2026-10-19.json) nos três jobs.
P02 concluído para esse commit; sem deploy ou publicação de imagens.

L06 foi a ampliação técnica recomendada dentro da escolha local do usuário.
L06/L07 são novos ensaios, não critérios retroativos para invalidar os aceites
limitados de L01–L05. L06/L07 foram executados após a revisão, nos limites descritos.

Antes de publicar o trabalho acumulado, revisar o conjunto de alterações locais,
segredos/artefatos e a separação dos commits. Esta revisão não cria commit, PR,
push ou deploy e não remove fixtures ou backups.

## Limites que permanecem

- Sem homologação de Production, TLS/segredos/ACLs do ambiente final ou atendimento real.
- Sem snapshot distribuído atômico, recuperação integral de todos os objetos/tenants,
  restauração do estado anterior do broker ou metas RPO/RTO acordadas.
- Sem capacidade máxima certificada, disponibilidade mensal ou ingestão de 20 mil itens comprovada.
- Sem política completa de retenção/expurgo e validação integral de tratamento de dados.

Os critérios P01–P08 de [preparação do piloto](preparacao-piloto.md) continuam
separados. Cliente, dados reais, ambiente, responsáveis e metas dependem de
definição própria quando houver decisão de sair do laboratório.
