# Preparação do piloto

Revisão: **19/10/2026**. Situação: **planejamento organizado; entrada no piloto ainda não homologada**.
Este documento acompanha o trabalho posterior ao fechamento da Fase 3, sem reabrir
seus critérios históricos nem iniciar automaticamente a Fase 4.

**Direção confirmada pelo usuário em 19/10:** continuar localmente com dados de
demonstração. [Ensaios locais e diagnóstico](../guides/diagnostico-local.md) passam
a orientar a sequência imediata. Essa escolha não define cliente, ambiente ou
autorização de dados reais; P01–P08 abaixo continuam sendo os critérios do piloto externo.

**Continuidade local:** [revisão consolidada L01–L07](revisao-laboratorio-2026-10-19.md),
incluindo falha/retry e volume pelo pipeline completo nas cópias restauradas;
[reconciliação incremental corrigida](../guides/reconciliacao-incremental.md).
A sequência atual está na [organização da entrega](organizacao-entrega-2026-10-19.md).
Esses aceites locais não encerram automaticamente P03–P08.

## Base já disponível

O ambiente local comprovou ingestão raw → canônico → Warehouse → consulta → painel,
isolamento entre organizações, quota, retry/DLQ e recuperação SQL. A interface permite
cadastrar/configurar integrações, acompanhar históricos e reprocessar desde uma data.
As verificações mais recentes foram API 78/78, processamento 10/10 e frontend 33/33;
não são testes distintos a somar à suíte anterior de 122.

Evidências: [memória](memoria-projeto.md), [fechamento da Fase 3](fechamento-fase-3-2026-10-19.md),
[ingestão completa](../guides/recuperacao-minio-ingestao.md) e
[configuração/reprocessamento](../guides/configuracao-reprocessamento.md).
Essas provas usam desenvolvimento e dados sintéticos/Demo. Não comprovam disponibilidade
mensal, recuperação distribuída, atendimento de usuários reais ou ambiente de piloto.

## Tarefas e sequência

Responsáveis abaixo são **papéis propostos**, ainda sem atribuição nominal.
“Concluído” exige a evidência de aceite, não apenas implementação ou preenchimento de um campo.

| ID | Prioridade / tarefa | Situação | Depende de | Papel responsável | Evidência de conclusão |
|---|---|---|---|---|---|
| P01 | Definir escopo e limites do piloto | Decisão pendente | — | Responsável do produto + cliente | Ficha abaixo preenchida: fonte compatível, empresas/usuários, dados autorizados, volume, janela, sucesso e interrupção. Origem diferente do conector REST exige estimativa própria. |
| P02 | Tornar a validação do front reproduzível no CI | Concluído para o commit 959e3ae | — | Engenharia | Job frontend aprovado no [runner real](https://github.com/JonivaniRPereira/EIP/actions/runs/39350842675), incluindo instalação pelo lockfile, testes, build e fontes/licenças. Jobs .NET/scans e imagens também passaram. [Evidência](../operations/ci-remoto-2026-10-19.json). Isso não inicia o piloto externo. |
| P03 | Preparar ambiente e acessos do piloto | A fazer | P01 | Engenharia + operação | Ambiente separado de Development; TLS, segredos/rotação, contas com privilégios mínimos, portas/egress e origens permitidas conferidos. Host/Worker com a mesma lista exata de destinos; probes/métricas/broker/storage restritos. Runtime e imagens homologados/reproduzíveis, inclusive storage. Autenticação e acesso administrativo testados sem exceção de Development; avaliar sessão do navegador e XSS conforme revisão de segurança. |
| P04 | Definir e ensaiar recuperação completa | A fazer | P01, P03 | Operação + engenharia | Metas de perda tolerável de dados (RPO) e tempo de retomada (RTO) acordadas; cópia fora do host e acesso/guarda definidos. Ensaio em ambiente isolado com SQL, raw/storage, configuração/chaves e estratégia de fila/reprocessamento; conferência de consistência, readiness e fluxo comercial. Versão anterior e compatibilidade de schema ensaiadas; tempos medidos e comparados às metas. |
| P05 | Definir tratamento de dados e atendimento assistido | Decisão e implementação pendentes | P01; ensaio em P03 | Responsável por dados do cliente + operação + engenharia | Finalidades, papéis, dados permitidos, responsáveis e canal real registrados conforme inventário existente. Prazos de guarda aprovados para SQL/raw/logs/filas/backups/exportações; rotina ou procedimento executável validado em cópia isolada. Pedido sintético de acesso/correção/exclusão percorrido com verificação de identidade, isolamento, revisão da resposta e tratamento das cópias. Não executar expurgo em dados atuais como parte deste planejamento. |
| P06 | Preparar acompanhamento e resposta a incidentes | A fazer | P01, P03 | Operação | Janela e responsável de acompanhamento, canal de incidente e escalonamento definidos. Histórico persistente de probes via Gateway, frescor por origem, falhas/retry/DLQ e capacidade. Ausência de sucesso não aparece como dado fresco. Simular indisponibilidade e confirmar detecção, comunicação e retomada na janela acordada. A ferramenta de monitoramento será escolhida pelo ambiente. |
| P07 | Homologar fluxo e carga representativos | A fazer | P02–P06 | Engenharia + usuário responsável do cliente | Dataset autorizado com volumes/cache/concorrência previstos em P01. Validar login/permissões, isolamento cruzado, sync incremental, reprocessamento, reconciliação, painel e falha/retry/DLQ. Medir metas acordadas e registrar resultado, desvios e limitações. Demonstração de 6 registros não substitui esse ensaio. |
| P08 | Registrar decisão de início e plano de saída | A fazer | P01–P07 | Responsável do produto + cliente + operação | Revisão das evidências e pendências; decisão explícita com participantes, duração, suporte e critérios de parada. Exportação/encerramento e destino dos dados definidos. Nenhum item obrigatório pode ser considerado atendido só por ausência de erro observado. |

P01 e P02 podem avançar em paralelo como atividades do projeto. Enquanto as decisões de P01
não existem, P02 pôde ser implementado com o repositório atual. A implementação e os
testes locais foram concluídos em 19/10; seu aceite remoto permanece pendente. O próximo
passo de preparação de ambiente depende das definições de P01.
Não há estimativa de prazo ou custo de infraestrutura aprovada nesta revisão.

## Ficha de escopo a preencher em P01

| Decisão | Valor atual |
|---|---|
| Cliente, patrocinador e usuário de homologação | A definir |
| Empresa(s) e usuários autorizados | A definir |
| Sistema de origem e contrato disponível | A definir; implementado hoje: REST de referência para clientes, produtos e vendas |
| Dados sintéticos ou reais autorizados e finalidade | A definir |
| Volume inicial, crescimento e simultaneidade esperados | A definir |
| Hospedagem, região, domínio e orçamento | A definir |
| Janela/duração do piloto e acompanhamento | A definir |
| RPO, RTO, frescor e metas de desempenho aceitos | A definir; metas locais são referência, não compromisso do piloto |
| Critério de sucesso e conferência de valores | A definir com o cliente |
| Responsáveis nominais e canais de suporte/dados/incidentes | A definir; não criar contatos fictícios |

Segredos, tokens, dados pessoais e exportações ficam em locais restritos, fora deste documento.
Registrar aqui apenas referências de evidência que possam ser versionadas.

## Entrada, interrupção e encerramento

Para entrada: P01–P07 devem ter evidências revisadas e P08 deve registrar a decisão.
Se houver redução de escopo, descrevê-la antes da revisão e indicar o impacto nos critérios;
não transformar pendência técnica em aceite implícito.

Proposta de critérios de interrupção a confirmar em P01: suspeita de acesso entre organizações,
divergência de reconciliação sem explicação, perda de integridade/recuperabilidade ou indisponibilidade
além da janela acordada. Nesses casos, conter o acesso/processamento afetado, preservar evidências,
acionar os responsáveis e aplicar o runbook pertinente; retomar após verificar a correção.

Ao encerrar: registrar resultados de negócio e operação, exportações autorizadas, destino/guarda
dos dados e decisão sobre continuidade. Billing, Workspace, IA e conectores adicionais continuam
fora deste plano até priorização própria.

## Referências operacionais

- [Revisão de segurança e seus limites](../security/revisao-api-2026-10-18.md).
- [Inventário de dados e lacunas](../16-LGPD.md).
- [Backup SQL](../guides/backup-restauracao.md) e [rollback](../guides/rollback-release.md): ensaios locais, não recuperação completa do piloto.
- [SLOs e limites da medição](../guides/slos-iniciais.md).
- [Operação do conector](../guides/conector-operacoes.md) e [isolamento sob carga](../guides/isolamento-sob-carga.md).
