# Fase 4 — IA assistida

Iniciada em **19/10/2026**, a pedido do usuário após aceite remoto da entrega da
Fase 3. Referências: [roadmap §8](../15-Roadmap.md) e [AI Engine](../11-AI-Engine.md).
Validação continua local com dados Demo. O piloto externo mantém seus critérios
P01/P03–P08; início desta fase não equivale a homologação de produção.

## Sequência de implementação

| ID | Tarefa | Aceite | Situação |
|---|---|---|---|
| F4.1 | Base de ferramentas e descoberta | Registro fechado, schema, permissão analytics.query e tenant da sessão; catálogo igual ao Analytics; recusa de ferramentas/argumentos não permitidos; testes e API local. | Concluído localmente. [Evidência](../operations/fase-4-ferramentas-2026-10-19.json). |
| F4.2 | Ferramenta queryAnalytics | Consulta declarativa passa pelo serviço analítico medido; mesmas permissões, RLS, medição, limites, filtros, cache e frescor da consulta normal; divergência/cross-tenant/SQL arbitrário recusados. | Concluído localmente. [Evidência](../operations/fase-4-consultas-2026-10-19.json). |
| F4.3 | Adaptador de modelo e política de dados | Escolher provedor/modelo, credenciais fora do Git, destinos permitidos, orçamento e contrato de dados; contexto mínimo e modo desabilitado seguro. Sem envio externo antes dessa configuração/autorização. | Planejado; decisão de provedor ainda aberta. |
| F4.4 | Pergunta analítica e interface | Intenção gera proposta validada; resposta com dataset/métrica/período/filtros/frescor, erro e ambiguidade explícitos. Nenhum número inventado. | Após F4.2/F4.3. |
| F4.5 | Rascunhos assistidos de painel | Proposta não publicada, revisão e confirmação explícita pelos fluxos existentes; permissão de publicação conferida separadamente. | Após F4.4. |
| F4.6 | Avaliações e guardrails de IA | Casos sintéticos de intenção, ambiguidade, injection em dados/prompt, falha/timeout de modelo/ferramenta e acesso cruzado; métricas e critérios de aceite registrados. | Antes de habilitar IA para usuários. |
| F4.7 | Operação de IA | Quotas/custo/tokens/latência por tenant, limites por fluxo, auditoria mínima, política de retenção, sessões isoladas e fallback. | Antes do aceite da fase. |

F4.1 não implementa modelo, linguagem natural, sessão conversacional, consulta de
fatos ou geração de painéis. O registro agora contém `listDatasets` e `queryAnalytics`;
ferramentas futuras não aparecem como disponíveis antecipadamente. O catálogo é
global na implementação atual, mas só é exposto a sessão com tenant selecionado e
permissão analítica. Não se trata de avaliação completa de prompt injection.

## Limites desta etapa

Sem Workspace, billing, novo conector ou ações externas. Nenhuma tabela nova;
nenhuma migration/RLS adicional. Não há chamada de LLM, envio de dados a terceiros
ou custo de tokens. A operação de descoberta só lê definições do catálogo existente;
limite de corpo 4 KiB e rate limit atual do Gateway. Não há quota mensal de IA ainda.
Log da ferramenta contém nome, tenant e sucesso da consulta, sob correlação HTTP existente.

F4.2 adiciona consultas de agregados por `queryAnalytics`, sem modelo ou interface
conversacional. O contrato fechado aceita dataset, métricas, uma dimensão, filtros,
ordenação e limite. Resposta inclui consulta validada, origem `analytics`, dados,
tenant autenticado e metadados de versão/execução/frescor. Frescor nulo é mantido.
Propriedades desconhecidas ou repetidas e métricas duplicadas são recusadas.

A ferramenta usa a interface analítica **medida**, mantendo RLS, cache por tenant
e frescor, contagem de consultas bem-sucedidas (inclusive cache hit) e os limites
configurados. Hoje: até 5.000 linhas, até 400 dias para filtro de período explícito
e timeout de execução de 10 segundos. Sem filtro de período, vale a cobertura
disponível, assim como na consulta normal. Não existe quota mensal analítica
implementada; a referência anterior a reutilizar quota foi corrigida para medição.
Orçamento/quotas de IA continuam em F4.3/F4.7, antes da habilitação de um modelo.

## Validação de F4.1

API Host **91/91** aprovada, incluindo 12 casos novos de ferramentas (não somar
novamente esse recorte). Publicação Release e formatação dos arquivos alterados
aprovadas; frontend não foi alterado nem teve testes repetidos nesta etapa.
Pela API real da Demo, a ferramenta retornou `sales`, com `netRevenue`,
`invoicedQuantity` e `averageTicket`, iguais ao catálogo analítico existente.

Conferências HTTP reais: anônimo 401, ferramenta não registrada 404, tentativa de
fornecer tenant no corpo 400 e corpo acima de 4 KiB 413. A primeira validação
revelou que o handler global convertia o bloqueio de tamanho em 500. Foi ajustado
para preservar o status de `BadHttpRequestException`; outras exceções continuam
500. Teste cobre ambos os casos sem expor mensagens internas.

[Diagnóstico posterior](../operations/fase-4-diagnostico-2026-10-19.json) aprovado:
front/API/Gateway e dependências disponíveis, um consumidor, filas vazias e
15 organizações. Somente a API local foi reiniciada com a publicação nova;
nenhum dado de negócio alterado e nenhuma migration criada. O CI remoto anterior
vale para 959e3ae, não para esta nova entrega ainda não publicada.

## Validação de F4.2

API Host **108/108** aprovada, sem ignorados. Casos com dados sintéticos conferem
paridade com consulta normal (instante de execução comparado separadamente),
isolamento de dados/cache entre tenants, frescor conhecido/nulo, filtros,
ordenação, limite, timeout e medição de ambas as entradas, inclusive cache hit.
Consultas inválidas não entram nos contadores; leitura dos contadores nos testes
usa o contexto RLS correspondente. Contrato recusa campos extras/repetidos,
SQL, tenant no corpo, nulos, métricas repetidas e limites inválidos.

Release e formatação aprovados. Na Demo pelo Gateway: mesmos dados do Analytics,
receita de **R$ 11.110,00**, versão/frescor presentes; HTTP 401/400/413 conferidos.
[Diagnóstico posterior](../operations/fase-4-consultas-diagnostico-2026-10-19.json)
aprovado, 15 organizações, um consumidor e filas vazias. API local atualizada;
front/Worker/Gateway mantidos. Sem alteração de dados de negócio ou migration.
Sem LLM, chamadas externas ou testes novos de frontend. CI remoto desta entrega
aguarda publicação; a aprovação anterior não certifica este código.

## Critérios de saída da fase

- [ ] IA mantém exatamente o acesso do usuário e não executa SQL livre.
- [ ] Respostas avaliadas apresentam evidências e limitações.
- [ ] Injection, falhas e indisponibilidade têm tratamento seguro testado com modelo.
- [ ] Custo e qualidade são medidos por capacidade/tenant.
- [ ] Publicação e ações externas exigem autorização/confirmação aplicável.

Concluir F4.1/F4.2 não marca esses critérios como satisfeitos. Fase 5 (automação governada)
permanece posterior ao aceite das capacidades usadas; Fase 6 exige demanda e análise
de custo/segurança conforme o roadmap. Nenhuma ação externa é iniciada por este backlog.
