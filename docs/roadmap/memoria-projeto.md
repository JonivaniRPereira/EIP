# Memória do projeto

Atualização: **19/10/2026**. Registro de continuidade versionado; detalhes e critérios
permanecem nos backlogs e evidências, sem credenciais ou payloads pessoais neste arquivo.

- Fases 0–2: encerradas historicamente. Fase 3: revisão formal encerrada no escopo reduzido
  de engenharia; [fechamento e próximos passos](fechamento-fase-3-2026-10-19.md).
- Implementado: organização por autoatendimento, shell, conector REST com retry/DLQ,
  catálogo/linhagem, quotas, uso por tenant, runbooks de recuperação/rollback/SLO,
  correções de segurança, inventário LGPD e carga concorrente com dados distintos.
- Última revalidação após recuperar MinIO (19/10): **122/122 testes em nove suítes**,
  incluindo Data Lake com MinIO real, pipeline, RLS e RabbitMQ/retry/DLQ.
  [Resumo](../operations/recuperacao-minio-2026-10-19.json). A carga do mesmo dia teve
  280 chamadas concorrentes sem divergência, conforme o relatório separado.
- Stack local após o fechamento: MinIO recuperado por compilação da tag oficial fixada,
  Worker atualizado em execução. [Validação completa](../guides/recuperacao-minio-ingestao.md):
  raw → canônico → Warehouse → consulta → linhagem → painel publicado passou, receita 11.110,00.
  Snapshot posterior: 13 tenants, zero pendências e dois tenants com sucesso Warehouse.
  O snapshot anterior (11 tenants/oito pendências) permanece como evidência histórica.
- Interface revalidada (19/10): login e criação/edição/publicação de painel pelo navegador;
  menu compacto e URL persistente do rascunho corrigidos, 17 testes e build de produção
  aprovados. [Evidência visual e limites](../operations/validacao-interface-2026-10-19.md).
- Integrações pela interface entregues (19/10): cadastro, seleção de empresa, disparo e
  histórico com atualização automática/quota. [Guia e evidências](../guides/integracoes-interface.md).
  Empresa Demo carregada pela UI (clientes/produtos/vendas); painel mostra 11110 BRL.
  Revalidação: API 75/75 e frontend 21/21; builds aprovados. Não somar esses resultados
  à suíte de 122 como se fossem testes distintos.
- Diagnóstico de falhas entregue (19/10): quarentena, DLQ, confirmação de reprocessamento,
  histórico solicitado e rastreio do ID exato da execução. Quota da quarentena corrigida;
  reprocessamento integral sem filtro/preservando watermark. [Guia/evidências](../guides/falhas-interface.md).
  API 76/76, conector 10/10 e frontend 27/27; builds aprovados. Fluxo visual comprovado
  com fixture sintética identificada e execução real do Worker, sem induzir falha na origem.
- Localização pt-BR e paginação entregues (19/10): datas e números brasileiros,
  histórico e quarentena em grupos de 20 com cursor estável e filtros no servidor.
  API 77/77, frontend 31/31 e builds Host/front aprovados. API local atualizada;
  painel Demo conferido no navegador com 11.110 BRL. [Guia](../guides/localizacao-paginacao.md).
- Configuração e reprocessamento desde uma data entregues (19/10): edição de nome/
  endereço autorizado; empresa e entidade fixas; confirmação da data no fuso local.
  Gravação parcial protege a configuração de cargas concorrentes e mantém watermark
  monotônico. API 78/78, processamento 10/10 e frontend 33/33; builds aprovados.
  Host/Worker locais atualizados. Carga Demo desde janeiro concluída com 6 aceitos,
  zero rejeitados e watermark preservado. [Guia](../guides/configuracao-reprocessamento.md).
- Preparação do piloto organizada (19/10): [tarefas P01–P08, dependências e aceites](preparacao-piloto.md).
  Escopo/cliente/origem/ambiente/responsáveis/metas ainda a definir; não há entrada homologada.
  P02 implementado e validado localmente: job frontend, Node 24.18.0/npm 11.16.0,
  instalação limpa, JUnit (33/33), build e verificação de fontes/licenças locais aprovados.
  Roboto/ícones incluídos por Fontsource 5.3.0; links Google Fonts e aviso Sass removidos.
  Aceite P02 em runner GitHub permanece pendente de publicação/execução real.
  P01 reúne decisões de escopo; P03–P08 cobrem ambiente, recuperação, dados, operação,
  homologação representativa e decisão de início. README alinhado ao fechamento real da Fase 3.
- Sem piloto real, billing, Workspace, conectores novos nem Grafana/Alertmanager nesta fase.
  Usuário confirmou em 19/10 continuar localmente com dados Demo. Diagnóstico reutilizável
  entregue: [guia e ensaios L01–L05](../guides/diagnostico-local.md).
  Front/API/Gateway e quatro dependências passaram; um consumidor, filas vazias,
  13 organizações, zero cargas pendentes/running/falhas em 24 h, três com sucesso Warehouse.
  Detecção negativa usa porta vazia, sem parar serviços. L02 concluído no escopo Demo:
  [recuperação conjunta](../guides/recuperacao-conjunta-demo.md), SQL com 39 tabelas/884 linhas,
  integridade/hashes/RLS aprovados; 3 objetos raw referenciados (3.131 bytes) idênticos,
  metadados preservados e corrupção na cópia detectada/reparada. Containers temporários
  removidos; diagnóstico original passou após o ensaio. Sem reiniciar a aplicação contra
  as cópias e sem RPO/RTO homologados. L03 executado no escopo de Warehouse/consultas:
  [20 mil fatos sintéticos em duas organizações](../guides/volume-analytics.md), quatro
  usuários, 120 consultas 200, zero divergências; p95 242,388 ms/p99 577,292 ms no cliente,
  com cache e pausas explícitos. Inserção direta SQL, sem provar ingestão de 20 mil itens.
  Diagnóstico posterior aprovado; 15 organizações, sem filas/cargas pendentes/falhas.
  L04 concluído em leitura pela API: [retomada nas cópias](../guides/retomada-aplicacao.md),
  SQL com 39 tabelas/21.063 linhas, raw Demo restaurado, Redis vazio e broker próprios.
  Login/anônimo 401, analytics/painel 11.110 BRL, linhagem e reconciliação aprovados;
  cache isolado povoado. Processo, containers e rede temporários removidos; original saudável.
  L04 não iniciou Gateway/frontend/Worker próprios. L05 posteriormente concluído:
  [retomada de ingestão](../guides/retomada-ingestao.md), Worker e fila próprios com
  três sincronizações incrementais Demo, um registro aceito/atualizado por entidade,
  zero rejeitados; novos raws com checksum/metadados, canônico e fatos sem duplicação,
  receita 11.110, marcadores e frescor avançados, linhagem/reconciliação aprovadas.
  Recursos temporários removidos e original saudável. Sem recuperação de mensagens
  históricas nem capacidade de ingestão de 20 mil itens comprovada.
  [Revisão consolidada L01–L05](revisao-laboratorio-2026-10-19.md) concluída: evidências
  coerentes e novo diagnóstico aprovado. Próxima ampliação proposta: L06, falha/retry
  nas cópias; depois L07, ingestão maior completa. CI remoto segue pendente. Piloto
  externo permanece sem homologação e com seus critérios próprios.
  Não há conformidade LGPD integral, disponibilidade mensal comprovada ou certificação de produção.
- Identidade visual solicitada em 19/10: [JP Code Craft aplicada](../guides/identidade-visual.md),
  paleta verde/papel, títulos Georgia, logo local, tema/controles e rodapé global com
  direitos reservados e crédito/link de desenvolvimento. Painel largo/compacto, login,
  menu e página longa conferidos; front 33/33, build e fontes locais aprovados.
  Sem alteração de API ou dados. A prioridade visual substituiu a ampliação L06 neste turno;
  L06 continua proposta, ainda não executada.
- L06 concluído após a identidade visual: [falha temporária/retry](../guides/retry-retomada.md),
  origem isolada 503 → 200 após 10.265 ms, mesma execução recuperada, marcador
  preservado durante falha, três syncs/novos raws, zero duplicação, receita 11.110.
  Cinco filas vazias e recursos temporários removidos; original saudável.
  Corrigido filtro de retries em L01/L05, antes omitidos pelo prefixo errado.
  Sem esgotamento/DLQ neste cenário. Próxima atividade L07: volume pelo pipeline completo.
- L07 executado: [volume pelo pipeline](../guides/volume-pipeline.md), 200 clientes,
  50 produtos e 2.000 notas novas, 2.250 aceitos/zero rejeitados; três raws com SHA/bytes
  iguais à origem. Finais 206/55/2.004/2.006, receita 211.110; canônico/fatos, linhagem
  e frescor conferidos. Succeeded em 7.804/2.414/48.531 ms mais espera Warehouse
  70.747 ms; consulta única 74,804 ms. Sem certificação de capacidade/20 mil itens.
  Alerta origem/canônico compara delta 2.000/200.000 com histórico 2.004/211.110;
  próxima atividade: corrigir escopo incremental com testes que preservem divergência real.
  Recursos temporários removidos. Front/API/Gateway/Worker originais estavam ausentes,
  foram restabelecidos e diagnóstico final passou. Sessões Host 36888, Worker 5220,
  Gateway 31336, frontend 58534. Binários originais publicados reutilizados, sem mudança
  de código de produção. CI remoto e critérios de piloto continuam separados.
- Reconciliação incremental corrigida em 19/10: [guia](../guides/reconciliacao-incremental.md).
  Processor passa rawObjectUri apenas quando updatedSince não é null; store soma
  por tenant/origem/raw. Extração completa mantém comparação histórica. Sem migration.
  Pipeline 13/13 e conector 10/10; recorte final 11/11 integra os 13. Inclui lote vazio,
  lote ausente, update de nota antiga, outra origem com mesmo raw, divergência real,
  tolerância e relatório completo que omite histórico. Build Release aprovado.
  Repetição de 200/50/2.000 aceita sem rejeições, total 211.110, origem sem alerta
  indevido (assert explícito no script), raw/canônico/fatos/linhagem/filas conferidos.
  Recursos descartáveis removidos. Worker anterior identificado e trocado com fila
  vazia; atual publicado em %TEMP%/eip-incremental-reconciliation-worker, sessão 83847,
  cwd src/Worker, DOTNET_ENVIRONMENT=Development. Diagnóstico final passou.
  Próxima atividade: revisão/organização das alterações acumuladas para entrega;
  não foi criado commit/push/PR nem executado CI remoto. Front não alterado nesta etapa.
- Login Demo corrigido em 19/10: senha existente foi validada com HTTP 200; front
  em 127.0.0.1 era bloqueado pelo CORS que só permitia localhost. Adicionadas as duas
  origens explícitas em src/Gateway/appsettings.Development.json, sem alterar base/Production.
  Gateway identificado/reiniciado, sessão 16953. Preflight permite ambas e rejeita
  example.invalid. Login pelo formulário chegou a /dashboard com Empresa Demo ativa;
  screenshot docs/operations/screenshots/login-demo-corrigido-2026-10-19.png.
  Senha não alterada. Nenhuma mudança de API/front necessária.
- Revisão/organização da entrega concluída localmente em 19/10:
  [áreas, dependências e tarefas D01–D04](organizacao-entrega-2026-10-19.md).
  Inventário explícito dos arquivos candidatos; TestResults/ ignorado sem apagar saídas.
  Gate de formatação identificou seis arquivos, corrigidos; verificação completa e
  build Release passaram. Nove suites .NET 137/137, zero ignorados/falhas; front33/33,
  build e fontes passaram. Resultados não se somam aos testes históricos anteriores.
  CI ganhou build MinIO da tag fonte fixada e EIP_TEST_MINIO_IMAGE explícito.
  Gitleaks8.30.1 oficial/checksum: token expirado embutido em regra de .claude/settings
  removido (apenas essa regra, sem ampliar acesso); hashes OpenAPI do rollback renomeados
  para *SchemaSha256 no JSON/emissor para evitar falso positivo; valores preservados.
  Scan repetido sem achados e sem exceções; sem scan/rewrite de histórico Git.
  Auditoria NuGet de 51 projetos sem pacotes vulneráveis reportados; sem novo npm audit.
  Diagnóstico original passou. Próxima D03: revisar trechos e organizar publicação;
  D04: aceite remoto de CI/imagens permanece pendente. Esta execução não fez staging,
  commit/push/PR; observou o commit externo c72bb4d ("New fetures") durante a revisão.
  Ele foi preservado. Inventário usa base 3c2de50 e inclui o commit mais ajustes finais
  locais, que ainda precisam compor a próxima entrega (inclusive retirada do JWT expirado).
- Após a revisão, usuário autorizou commit dos sete ajustes finais de entrega.
  Fechamento reúne remoção do JWT expirado da regra local, campos *SchemaSha256
  do rollback e inventário/resumo/documentação. Validação funcional da revisão
  permanece 137 backend + 33 front aprovados; rechecagem de scanner e sintaxe
  para o candidato final. Push/PR e CI remoto não incluídos nessa autorização.
- Usuário informou push concluído; origin/main confirmado em 959e3ae. CI remoto
  39350842675 aprovado nos três jobs: frontend, build/lint/testes/scans e imagens
  Host/Gateway/Worker (sem publicação/deploy). MinIO fonte fixada construído no runner.
  [Evidência completa por etapa](../operations/ci-remoto-2026-10-19.json).
  D03/D04 e P02 concluídos para esse SHA exato. Não somar contagens locais/remotas
  como testes diferentes; artefatos detalhados não baixados. CI anterior c72bb4d
  falhou no scanner, agora corrigido. P01/P03–P08 continuam separados; laboratório
  Demo permanece a direção do usuário. Registros locais anteriores são históricos.
- Usuário autorizou avançar fases/tarefas; Fase 4 (IA assistida, docs/15 §8) iniciada
  em 19/10, com [backlog F4.1–F4.7](fase-4-backlog.md). F4.1 concluído localmente:
  novos projetos AI.Application/AI.Api, registro fechado de listDatasets, schema
  vazio, analytics.query + tenant obrigatório, limite4KiB, logs mínimos. Catálogo
  igual ao Analytics; nenhum LLM, chat, SQL, envio externo ou tabela/migration nova.
  Host91/91, incluindo12 testes de ferramentas; Release/formatação aprovados.
  HTTP real401/404/400/413 e paridade confirmados. Corrigido handler global que
  convertia BadHttpRequestException413 em500; regressão mantém exceções internas
  sanitizadas em500. API local atual em %TEMP%/eip-ai-foundation-host-final,
  cwd src/Host, Development, localhost5080, sessão3139. Worker/Gateway/front mantidos.
  Diagnóstico posterior aprovado com15organizações e filas vazias. Front inalterado.
  Próxima F4.2: queryAnalytics via serviço medido com permissões, RLS, quota e frescor;
  depois adaptador de modelo, interface, rascunhos e avaliações. Provedor/modelo e
  política de dados ainda abertos; não chamar modelos externos nesta base.
  CI remoto anterior não certifica essa nova entrega; manter limite de piloto Demo.
- F4.2 concluído localmente: queryAnalytics com contrato fechado, tenant do JWT,
  analytics.query e IDeclarativeAnalyticsQueryService medido. Sem SQL/HTTP livre;
  schema estrito, recusa de campos extras/repetidos, nulos e métricas duplicadas.
  Retorna consulta validada, origem, dados e metadados de versão/execução/frescor.
  Reutiliza RLS/cache/limites e contadores; não existe quota mensal analítica/IA
  atual, corrigida a referência anterior a quota. Isso permanece em F4.3/F4.7.
  Host108/108, Release e formatação aprovados; paridade, isolamento entre tenants,
  frescor nulo/conhecido, filtros/ordem/limite, timeout e medição conferidos.
  Demo via Gateway: receita11110 e proteções401/400/413; diagnóstico aprovado,
  15organizações, filas vazias. Evidência fase-4-consultas-2026-10-19.json.
  API em %TEMP%/eip-ai-query-host, cwd src/Host, Development, localhost5080,
  sessão54999. Demais serviços mantidos. Sem tabela/migration/UI/modelo novo.
  Próxima F4.3: adaptador e política de dados; provedor/modelo/orçamento ainda
  pendentes, nenhuma chamada externa autorizada/configurada. CI desta entrega
  não validado remotamente; não fazer push automaticamente.
- Usuário definiu IA para vários provedores configuráveis na plataforma, sem
  escolher modelo único. F4.3a (base) concluída localmente: IAiModelAdapter,
  adaptador desabilitado, perfis globais por instalação em Host:AI, com exemplos
  OpenAI/Anthropic/Google e modelos/limites/referências de credencial separados.
  Referências são nomes EIP_AI_*, não chaves; esta base não lê/envia credenciais.
  Configuração inválida impede startup; activeProfile não habilita chamadas.
  GET /ai/status e /ai/providers exigem tenant+analytics.query e não expõem refs.
  Host118/118, Release/formatação e HTTP Demo aprovados; diagnóstico15tenants,
  filas vazias. API em %TEMP%/eip-ai-providers-host, sessão54842, src/Host,
  Development localhost5080. Front/Worker/Gateway mantidos. Sem migration/LLM.
  F4.3 permanece em andamento: transportes específicos, tela administrativa,
  governança/persistência de configurações/segredos, orçamento/quotas e política
  de envio por organização ainda pendentes. Próxima etapa prepara configuração
  administrativa e transportes; não ativar/enviar dados só porque há perfis.
  Guia docs/guides/ia-provedores.md e evidência fase-4-provedores-2026-10-19.json.
  Não somar testes históricos; CI deste candidato ainda não aprovado remotamente.
- F4.3b concluída localmente; registro final20/10 com evidências coletadas19/10.
  Tela /settings/ai no menu Provedores de IA, Owner/Admin via ai.settings.manage.
  GET/PUT /ai/settings e GET /ai/settings/providers: perfis por tenant com revisão
  otimista, limite32KiB e campos extras/credenciais/ativação recusados. Defaults
  da instalação só valem enquanto tenant não salvou. GET /ai/providers efetivo.
  Novo AI.Infrastructure/AiDbContext em schema ai, conexão TenantDb, migration
  20261020015523_InitialProviderSettings com tabela e RLS juntas. Aplicada localmente.
  DbContextFactory design-time exige EIP_AI_DB_CONNECTION; testes migrator atualizados.
  PKCS10.0.13 herdado de BuildingBlocks.Data, sem manter dependência vulnerável inicial.
  Host128/128 e frontend39/39, build/Release/formatação aprovados; testes cobrem
  RLS, permissões, entrada inválida, persistência e duas gravações concorrentes.
  Browser validou salvar/recarregar; Demo perfis originais restaurados a30s,
  revisão2, nenhuma seleção. Screenshot ia-configuracao-2026-10-19.png.
  HTTP401/409/400/413 e totalDemo11110; diagnóstico15tenants e filas vazias.
  API atual %TEMP%/eip-ai-settings-host, sessão88135, src/Host Development localhost5080.
  Front/Gateway/Worker mantidos. Sem dados de negócio alterados; configuraçãoDemo salva.
  Próxima F4.3c: transportes e armazenamento protegido de credenciais, com política
  de dados/custo; IA continua desabilitada e nenhuma chamada externa autorizada.
  Evidência docs/operations/fase-4-configuracao-2026-10-19.json; CI novo não validado.
- F4.3c local concluída20/10: credenciais separadas dos perfis via /ai/settings/credentials
  e PUT/DELETE /ai/settings/profiles/{id}/credential, ai.settings.manage. Só flags/ids
  devolvidos; corpo limitado8KiB, segredo8-2048ASCIIsem espaços, DTOToStringredacted.
  Cipher em coluna ProtectedCredentials, mesma RLS e revisão do registrosettings.
  DataProtection purpose tenant/profile/provider; perfis removidos/renomeados/com
  provedor alterado descartam chaves na mesma gravação. Concorrência impede misturas.
  DevelopmentWindows usa %LOCALAPPDATA%/EIP/AiKeyRing, persistente/DPAPI do usuário,
  applicationName EIP.AI.Credentials.v1. Não apagar esse keyring ou movê-lo ao Git.
  Produção/outrosSO: protetor indisponível503, sem fallback. CIHostfixture usa somente
  protetor efêmero de teste em bancos temporários. Cofre de produção continua pendente.
  UIcampo password separado, limpa após tentativa/reload/destroy, confirmação remoção,
  bloqueia credenciais enquanto perfis dirty. Defaults podem ser salvos sem editar.
  Host143/143, frontend47/47, builds/formatação aprovados. Migration aplicada localmente:
  20261020110338_ProtectedProviderCredentials (coluna nullable; downgrade perde cifras).
  Browser salvou chave sintética/campo limpo; API não a expôs; keyring DPAPI conferido
  só por metadados. Teste limpo por API com revisionguard: Demo sem chaves, revisão4,
  perfis/modelos/limites originais. HTTP401/409/400/413, totalDemo11110, diagnóstico15tenants
  comfilasvazias. Screenshot ia-credenciais-2026-10-20.png sem valores sensíveis.
  APIatual %TEMP%/eip-ai-credentials-host, sessão52440, src/Host Development localhost5080.
  Front/Gateway/Worker mantidos. Evidência fase-4-credenciais-2026-10-20.json.
  Próxima: transportes multiprovedor e política de dados/custo; nenhum LLM chamado,
  nenhum orçamento/envio autorizado por cadastrar chave. Não ativar modelos ainda.
  CI deste código não validado remotamente; manter push manual do usuário.
- Preservar alterações locais e dados existentes. Testes deixam organizações sintéticas;
  limpeza deve ser delimitada por IDs e planejada, nunca apagar tenants indiscriminadamente.
