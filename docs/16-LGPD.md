# 16. Inventário de dados pessoais e atendimento manual

Revisão técnica: **18/10/2026 — E6.2 da Fase 3**. Este documento descreve o funcionamento existente e suas lacunas; não atesta conformidade integral nem institui prazos de guarda.

Atualização de ambiente (19/10): [MinIO recuperado e leitura de objetos sintéticos validada](guides/recuperacao-minio-ingestao.md).
A indisponibilidade citada nesta revisão descreve 18/10; a recuperação não resolve as lacunas de retenção/exclusão.

## 1. Escopo e evidência

O inventário foi confrontado com as **39 tabelas do banco local EIP**, incluindo tabelas de migração, por consulta somente a `sys.tables`, `sys.columns` e tipos. A evidência [lgpd-schema-2026-10-18.json](operations/lgpd-schema-2026-10-18.json) contém nomes e tipos, sem valores de registros. Também foram inspecionados modelos, autenticação, ingestão, cache, mensageria, frontend e scripts operacionais. Não houve exclusão nem exportação de registros pessoais nesta revisão.

O esquema mostra o que pode ser armazenado, não comprova que todos os campos estejam preenchidos. O armazenamento de objetos não foi inspecionado em execução: MinIO está indisponível no ambiente local. Seus fluxos foram revisados no código.

Os papéis de controlador/operador, bases legais, destinatários, responsáveis e localização de hospedagem precisam ser definidos por finalidade e contrato antes do piloto. Como hipótese operacional, o cliente decide sobre os dados do ERP e a operação EIP atende suas instruções; contas e segurança exigem análise própria. O desenho técnico não define sozinho esses papéis. Referência: [LGPD, especialmente arts. 5, 6, 7, 11, 15–19, 37 e 39](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm).

## 2. Inventário SQL — cobertura de todas as tabelas

Identificadores e vínculos continuam pessoais quando permitem associação a alguém. Campos livres podem conter dados além da finalidade pretendida.

| Local / tabelas | Dados e origem | Finalidade técnica / observação |
|---|---|---|
| `identity.AspNetUsers` | Cadastro: nome de exibição, e-mail, usuário e versões normalizadas; ID. Segurança: hash de senha, stamps, confirmação, bloqueio e contagem de falhas. O esquema também prevê telefone e indicadores de 2FA. | Autenticação. Cadastro público atual preenche nome/e-mail/senha; presença de colunas de telefone/2FA não significa fluxo implementado. Nunca fornecer hashes/stamps em resposta ao titular. |
| `identity.AspNetUserClaims`, `AspNetUserRoles`, `AspNetUserLogins`, `AspNetUserTokens` | UserId, permissões/claims, roles; esquema prevê identificador de provedor externo e valores de tokens. | Autorização e suporte Identity. Login externo e gestão de tokens externos não são fluxos públicos habilitados hoje; tratar valores como confidenciais se utilizados. |
| `identity.AspNetRoles`, `AspNetRoleClaims` | Nomes e claims de papéis; valores livres podem conter identificadores. | Catálogo de autorização, ligado às associações de usuário. |
| `identity.RefreshTokens` | UserId, TenantId opcional, hash de token, criação/expiração/revogação e referência de substituição. | Sessões. O servidor persiste hash, não o refresh token bruto. |
| `identity.AuditEvents` | UserId opcional, e-mail inclusive em tentativas de login sem usuário existente, evento, horário e detalhe. | Registro de cadastro, login e falhas/bloqueio. Não tem escopo de tenant por linha; consultar por identidade verificada, com acesso operacional restrito. |
| `tenant.Tenants`, `tenant.Companies` | Nome/slug do tenant; nome, nome fantasia, documento fiscal, país/moeda da empresa, status e horários. | Organização. Documento fiscal/nome podem identificar empresário individual; não presumir que sejam sempre dados apenas de pessoa jurídica. |
| `tenant.Memberships` | UserId, TenantId, papel, status e horários. | Vínculo de acesso. Uma conta pode participar de vários tenants. |
| `canonical.Customers` | Código, nome, documento fiscal, e-mail, cidade/estado/país, atividade e linhagem da origem. | Cadastro comercial recebido do conector. Não há tabela/fluxo de fornecedor implementado nesta fatia. |
| `canonical.SalesInvoices`, `canonical.SalesInvoiceItems` | Cliente, número/série de documento, pedido, datas, valores/impostos, itens e descrição livre; IDs de origem e linhagem. | Análise de vendas, vinculável ao cliente. Descrição e identificador externo também podem conter dados pessoais. |
| `canonical.Products`, `canonical.ProductCategories` | Código, nome, descrição de produto via nomes/categoria, hierarquia, status e linhagem. | Catálogos normalmente não pessoais; nomes/códigos livres podem identificar pessoas. |
| `canonical.QuarantineEntries` | Regra, motivo, entidade, IDs de execução/conector, correlação, URI bruta, criação/resolução. | Diagnóstico e reprocessamento. Motivo pode reproduzir valores inválidos. Resolver quarentena não apaga registro nem objeto bruto. |
| `connector.ConnectorInstances`, `connector.SyncRuns` | Nome, URL configurada, tenant/empresa, watermark; IDs, horários, contagens, status e mensagem de erro da execução. | Integração e rastreio. URLs/nomes antigos podem conter informação pessoal; cadastro atual limita URLs à lista exata permitida. Erros públicos novos são genéricos, sem garantia de saneamento de linhas antigas. |
| `warehouse.DimCustomer` | CustomerId/código, nome, e-mail, localização, status e intervalos históricos. | **SCD tipo 2: versões antigas conservam os dados anteriores.** Não replica TaxId como coluna, mas mantém vínculo ao cliente canônico. |
| `warehouse.DimTenant`, `warehouse.DimCompany` | IDs e nomes de organização, país/moeda e horário de carga. | Dimensões de organização, potencial identificação de pessoa natural. |
| `warehouse.DimProduct`, `warehouse.DimProductCategory` | IDs, códigos, nomes, categoria; versões históricas de produto. | Catálogo analítico; campos livres podem carregar identificação indevida. |
| `warehouse.FactSalesInvoiceItem` | CustomerKey, documentos/itens, valores, datas por dimensão, IDs de origem, URI bruta, lote e status. | Histórico comercial ligado a todas as versões do cliente; não fica anônimo apenas removendo nome da dimensão atual. |
| `warehouse.LoadBatches` | Tenant/origem, correlação, contagens, horários, status e erro. | Operação de carga; examinar mensagens antigas e correlações ao atender um pedido. |
| `warehouse.DimDate`, `warehouse.DimCurrency` | Calendário e catálogo de moedas. | Sem dado pessoal intrínseco; joins com fatos dão contexto às transações. |
| `dashboard.Dashboards`, `dashboard.DashboardVersions` | OwnerId, PublishedByUserId, nome, widgets serializados, filtros/títulos/consultas, horários e versões publicadas. | Painéis e autoria. Texto/filtros podem conter nomes ou códigos de clientes. Arquivamento não elimina histórico. |
| `administration.AnalyticsQueries` | TenantId, ID de evento e horário da consulta. | Medição de uso; não grava texto da consulta nem UserId nesta tabela. Metadados são vinculáveis ao tenant. |
| `administration`, `canonical`, `connector`, `dashboard`, `identity`, `tenant`, `warehouse`: `__EFMigrationsHistory` | Identificador da migração e versão do produto, em cada um dos sete schemas. | Sem dado pessoal intrínseco; incluídas na conferência das 39 tabelas. |

Linhagem comum canônica: TenantId, CompanyId, SourceSystemId, SourceEntity, SourceRecordId, datas de origem/ingestão/processamento, IsDeleted, SchemaVersion, CorrelationId e RawObjectUri. Os campos `IsDeleted`, `IsActive`, status de conta/tenant e `EffectiveTo` não representam eliminação física.

Fontes de implementação: `src/Platform/Identity/{Domain,Infrastructure,Application}`, `src/Platform/Tenant/Domain`, `src/Data/{Canonical,Warehouse,Connector}`, `src/Intelligence/Dashboard` e `src/Platform/Administration`. Os nomes exatos de cada coluna estão na evidência de esquema.

## 3. Cópias fora do SQL e retenção efetiva

| Local | Conteúdo / acesso | Retenção implementada hoje |
|---|---|---|
| SQL Server, todos os grupos acima | Conta, comercial, auditoria, versões e rastreio. APIs autenticadas com autorização; tabelas com tenant usam RLS conforme política do módulo; Identity contém dados globais. Operador de banco tem acesso privilegiado. | **Sem prazo de descarte definido e sem rotina automática de expurgo identificada.** Expiração lógica, arquivamento e resolução não removem linhas. |
| Data Lake S3/MinIO | JSON completo recebido antes da validação canônica; pode conter campos extras, inclusive sensíveis não previstos no modelo. Chave por tenant/origem/entidade/data/execução; metadados e checksum. Acesso por serviço com validação do prefixo de tenant. | Sem lifecycle/expurgo configurado no projeto. `IRawObjectStore` oferece Put/Get/ListKeys, não Delete. Cargas rejeitadas também podem permanecer brutas. Não há minimização por campo antes da gravação. |
| Redis — consultas analíticas | Resultados com dimensões como nome do cliente e métricas. Chaves incluem tenant e versão dos dados/consulta. | TTL padrão de **60 segundos**, configurável (`Analytics:CacheTtlSeconds`). É expiração do cache, não prazo do SQL/raw. Quota de sync usa contador/IDs por janela, padrão 60 segundos. |
| RabbitMQ — principal/retry/DLQ | Envelope de sync com IDs, tenant, correlação e parâmetros de reprocessamento, sem o JSON comercial completo. Referências são vinculáveis. | Retries padrão 10/30/90 segundos de espera; isso não define prazo de guarda. Filas duráveis; principal/DLQ sem política de descarte automático identificada. Consumir/confirmar remove mensagem, não dados de origem. |
| Navegador — `localStorage` (`eip.session`) | Tokens brutos e expiração; JWT contém identidade/e-mail/tenant/claims. O navegador também apresenta resultados pessoais em memória/tela. | Access token padrão 15 minutos (+ tolerância de relógio de 30 segundos); refresh 7 dias. Expiração não limpa automaticamente armazenamento local. Logout remove sessão local e tenta revogar o refresh informado; JWT já emitido continua válido até expirar. |
| Logs e telemetria de Host/Gateway/Worker/infraestrutura | Console, correlação, URLs, IDs, erros internos e possíveis valores retornados pela origem. Exceções internas continuam nos logs mesmo com erro público genérico. Infraestrutura pode registrar IP/metadados de conexão. | Sem retenção/rotação central definida no repositório. Confirmar conteúdo e política de cada coletor no ambiente final; não há comprovação de mascaramento universal. Métricas agregadas não devem receber e-mail/CPF como labels. |
| Backups SQL, volumes, exportações e artefatos operacionais | `.bak` inclui todas as tabelas, hashes de senha, auditoria e dados comerciais. Evidências temporárias de testes podem conter dados sintéticos, logs e cópias completas. | Scripts não instituem expiração automática dos backups/evidências. Ver [backup/restauração](guides/backup-restauracao.md). Não versionar dumps, tokens ou exportações pessoais; excluir `*.bak` do Git não protege o arquivo no disco. |

O registro de refresh expirado/revogado permanece no banco. Não foi identificado fluxo de exclusão do ERP propagado de ponta a ponta; contadores/status não comprovam esse recurso. Alterar só o cadastro canônico deixa raw, SCD2, fatos, cache, painéis e backups potencialmente identificáveis.

## 4. Como atender acesso hoje — procedimento manual

Não existe portal LGPD, endpoint de exportação integral nem canal público específico configurado. Hoje o pedido precisa chegar ao responsável pelo projeto/cliente pelos canais já utilizados. Antes do piloto, publicar contato e atribuir responsáveis; não inventar endereço de atendimento.

1. Abrir protocolo restrito com data, direito solicitado, responsável e forma de retorno. Confirmar identidade e representação com evidência proporcional, sem pedir documentos excessivos nem registrar senha/token. Distinguir usuário da plataforma de cliente/pessoa nos dados do ERP.
2. Confirmar o controlador e o escopo autorizado. Mapear UserId global e seus vínculos quando a solicitação é da própria conta; para dados comerciais, validar tenant/empresa, código/ID da origem e CustomerId. E-mail isolado não identifica todos os registros; mesmo e-mail pode existir em vários contextos. Administrar um tenant não concede acesso irrestrito aos dados pessoais de outros usuários.
3. Operador autorizado prepara consultas **somente de leitura**, parametrizadas, com TenantId explícito e contexto RLS correto. Não desabilitar RLS para produzir a resposta. Identity global exige trilha e permissão operacional específica. Levantar cadastro, vínculos, auditoria pertinente, todas as versões de DimCustomer, fatos/documentos associados, linhagem, quarentena, autoria/filtros de painéis e objetos brutos referenciados. Verificar logs/exportações/backups pertinentes; não entregar backup inteiro.
4. Produzir exportação restrita ao titular, com origem, finalidade, categorias e duração conhecida. Remover dados de terceiros, segredos e tokens; documentos compartilhados exigem redação seletiva. Registrar locais pesquisados e os indisponíveis, inclusive eventual indisponibilidade do storage. Não afirmar completude sem concluir essas verificações.
5. Revisar com o responsável do controlador, entregar por meio privado com acesso limitado e registrar resposta/pendências. Definir prazo e descarte da própria exportação e do protocolo, sem reproduzir o conteúdo pessoal em logs/Git.

Para confirmação/acesso, observar as formas simplificada imediata e completa em até 15 dias do art. 19; não aplicar automaticamente esse prazo a todo pedido de exclusão. As condições dos demais direitos dependem do tratamento e das hipóteses legais. Referência: [orientação da ANPD sobre direitos dos titulares](https://www.gov.br/anpd/pt-br/assuntos/titular-de-dados/direito-dos-titulares).

## 5. Como avaliar e executar exclusão/correção hoje

**Não há exclusão integral automatizada nem script genérico validado.** O procedimento abaixo é um roteiro de operação assistida: cada pedido requer plano técnico delimitado e revisão do responsável pelo tratamento. Esta revisão documental não executou essas operações.

1. Usar a identificação e o protocolo da seção anterior. O controlador registra a decisão por categoria: corrigir, eliminar, anonimizar, bloquear ou conservar com fundamento e duração aplicáveis. Exclusão não é incondicional; eventuais hipóteses de conservação do art. 16 precisam ser justificadas para o caso, sem assumir obrigação fiscal universal.
2. Impedir reintrodução: combinar correção/remoção na origem, suspender ingestão/reprocessamento do escopo afetado, drenar ou segregar mensagens pertinentes e definir como impedir reaparecimento. Hoje não existe mecanismo persistente de supressão implementado. Não excluir um lote inteiro que também contenha dados de outras pessoas.
3. Para conta, mapear todos os tenants antes de remover a identidade global. Bloquear novos acessos e revogar todos os refresh tokens pertinentes por operação administrativa controlada; logout revoga apenas o token apresentado. Considerar JWTs válidos por até a expiração. Avaliar memberships, claims/logins/tokens, autoria de dashboards e auditoria separadamente: não pressupor cascata entre módulos nem apagar outros tenants.
4. Para dados comerciais, preparar operação transacional e ensaio em cópia protegida: localizar CustomerId, chaves de **todas** as versões SCD2, fatos, documentos/itens, IDs externos, URI raw, quarentena e mensagens. Remover ou anonimizar seletivamente conforme decisão, mantendo consistência de joins e agregações. Pseudonimizar um nome enquanto CPF/IDs/vínculos permanecem não comprova anonimização.
5. Sanitizar/remover objetos brutos via ferramenta administrativa de storage com escopo exato de tenant; a aplicação não tem Delete. Quando um objeto contém várias pessoas, reescrever conteúdo/linhagem/checksum e validar os leitores antes de remover a cópia anterior. Verificar versões/replicações do storage do ambiente. Corrigir versões de painéis e campos livres pertinentes. Invalidar caches do escopo ou aguardar TTL com consultas suspensas; limpar sessões nos dispositivos controlados.
6. Examinar logs, exportações, snapshots e backups. Se houver conservação autorizada, restringir acesso, registrar motivo/prazo e impedir uso comum. Manter registro mínimo protegido das decisões para **reaplicar exclusões/correções antes de reabrir uma restauração**. O runbook atual de recuperação não aplica automaticamente esse registro; acrescentar essa etapa à operação e validar em cópia. Não prometer eliminação total enquanto cópias não tiverem destinação comprovada.
7. Validar ausência/transformação em cada local e integridade dos demais dados, testar consultas/cache/reprocessamento e documentar contagens e referências sem copiar dados pessoais. Retomar fluxos apenas quando a origem e a supressão estiverem resolvidas. Responder com o que foi realizado, o que foi conservado e por quê, além de pendências e prazos definidos pelo responsável.

## 6. Pendências para transformar o roteiro em capacidade operacional

| Prioridade | Entrega | Responsável a designar |
|---|---|---|
| Antes do piloto | Canal e responsável por solicitações; papéis contratuais, finalidade/base legal por categoria, aviso de privacidade, destinos/localização e fornecedores. | Negócio / responsável pelo tratamento |
| Antes do piloto | Matriz de retenção com prazo e marco inicial por categoria, fundamentos de exceção e descarte de protocolos/exportações/backups/logs. Hoje os prazos estão **não definidos**, salvo TTLs técnicos. | Responsável pelo tratamento + operação |
| Antes do piloto | Minimizar payload antes do raw; restringir campos livres e revisar mascaramento de erros/logs, evitando coleta indevida de dados sensíveis. | Dados + segurança |
| Antes do piloto | Exportação e exclusão assistidas testadas em cópia com dados sintéticos: SCD2, raw compartilhado, dependências, múltiplos tenants, restauração e supressão contra reingestão. Implementar ferramentas que faltam. | Engenharia + operação |
| Antes do piloto | Expurgo verificável e políticas de lifecycle/rotação, inclusive DLQ, backups e tokens expirados; evidência de execução e falhas. | Operação + engenharia |
| No ambiente final | Validar acesso mínimo, criptografia, região, coletores, operadores e trilha de atendimento; completar controles apontados na [revisão de segurança](security/revisao-api-2026-10-18.md). | Segurança + operação |

E6.2 encerra o inventário e o diagnóstico previstos no backlog. As lacunas acima permanecem abertas; não constituem funções implementadas nem declaração de prontidão para produção.
