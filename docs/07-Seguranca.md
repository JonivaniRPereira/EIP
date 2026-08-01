# 07 - Segurança

**Projeto:** Enterprise Intelligence Platform (EIP)  
**Versão:** 1.0  
**Status:** Oficial  
**Última atualização:** Julho/2026

---

# 1. Objetivo

Este documento define os requisitos mínimos de segurança da Enterprise Intelligence Platform (EIP). Segurança é requisito de arquitetura, implementação e operação; não é uma etapa posterior ao desenvolvimento.

A EIP processa dados empresariais potencialmente sensíveis de múltiplas organizações. A plataforma deve garantir confidencialidade, integridade, disponibilidade, rastreabilidade e isolamento entre tenants durante todo o ciclo de vida do dado.

---

# 2. Princípios

- **Security by design e by default:** controles seguros são o comportamento padrão.
- **Menor privilégio:** usuários, serviços e conectores recebem apenas os acessos necessários.
- **Defesa em profundidade:** controles independentes nas camadas de identidade, API, dados, rede, infraestrutura e operação.
- **Zero trust interno:** chamadas entre componentes são autenticadas e autorizadas; rede interna não é sinônimo de confiança.
- **Isolamento obrigatório:** todo acesso é limitado por tenant, workspace, empresa e permissão quando aplicável.
- **Rastreabilidade:** ações relevantes são auditáveis com identidade, contexto e correlação.
- **Segredos fora do código:** credenciais são protegidas, rotacionáveis e nunca expostas em logs.
- **Privacidade por padrão:** coletar e reter somente o necessário para a finalidade definida.

---

# 3. Escopo e Ativos Protegidos

| Ativo | Exemplos | Proteção prioritária |
|---|---|---|
| Dados de clientes | vendas, financeiro, estoque, PII e documentos | isolamento, criptografia, autorização e retenção |
| Credenciais de conectores | senhas ERP, tokens API, connection strings | cofre de segredos, acesso mínimo e rotação |
| Identidades | usuários, sessões, tokens, permissões | autenticação forte, expiração e auditoria |
| Dados brutos | CSV, Excel, JSON, XML e respostas de API | Object Storage segregado, acesso temporário e linhagem |
| Dados analíticos | modelo canônico, Warehouse, cache e exportações | controle de acesso, mascaramento e rastreamento |
| Plataforma | APIs, workers, filas, bancos, imagens e pipeline | hardening, atualização e monitoramento |
| Auditoria | logs de acesso e ações administrativas | integridade, retenção e acesso restrito |

---

# 4. Modelo de Ameaças

A modelagem de ameaças é obrigatória para novos módulos, integrações, exposição de APIs públicas e mudanças na infraestrutura. O exercício deve considerar pelo menos:

| Ameaça | Controle principal |
|---|---|
| Acesso de um tenant aos dados de outro | contexto derivado do token, filtros obrigatórios, testes de isolamento e RLS obrigatória em banco compartilhado |
| Credencial de conector vazada | secret manager, mascaramento, rotação e menor privilégio |
| Token roubado ou sessão comprometida | curta duração, validação JWT, revogação, MFA para perfis privilegiados e detecção de anomalia |
| Injeção SQL, comando ou template | queries parametrizadas, validação estrita e proibição de execução arbitrária |
| Upload malicioso | validação de tipo/tamanho, antivírus quando aplicável, armazenamento isolado e processamento assíncrono |
| Webhook forjado/replay | assinatura, timestamp, nonce e idempotência |
| Exfiltração por exportação/API | autorização granular, rate limits, auditoria e limites de volume |
| Mensagem adulterada ou reprocessada | autenticação de broker, envelopes versionados, idempotência e DLQ |
| Indisponibilidade/DoS | rate limits, quotas, timeouts, circuit breakers e monitoramento |
| Dependência vulnerável | atualização, SCA, scan de imagens e política de correção |

Cada ameaça relevante deve indicar proprietário, risco, controles preventivos/detectivos e risco residual.

---

# 5. Identidade e Controle de Acesso

## 5.1 Autenticação

A EIP utiliza OAuth 2.0/OpenID Connect e JWT conforme definido no API Design. O backend valida assinatura, emissor, audiência, expiração e claims obrigatórias em toda requisição autenticada.

Requisitos:

- senhas são protegidas exclusivamente pelos mecanismos seguros do provedor de identidade; nunca em texto claro ou criptografia reversível;
- tokens de acesso têm curta duração e refresh tokens possuem rotação, revogação e armazenamento seguro;
- MFA é obrigatório para administradores de plataforma e recomendado/parametrizável para administradores de tenant;
- login, troca de senha, recuperação de conta e alteração de MFA são auditados;
- após falhas repetidas, aplicar controles de proteção contra brute force sem expor se uma conta existe;
- federação corporativa via OIDC/SAML requer mapeamento explícito de domínio, tenant e permissões.

## 5.2 Autorização

Autorização é baseada em permissões e aplicada no servidor, nunca somente na interface. Uma decisão avalia:

```text
Identidade + Tenant + Workspace + Empresa + Recurso + Ação + Escopo de dados
```

- Papéis agrupam permissões; não substituem verificações de escopo.
- O princípio de negar por padrão é obrigatório.
- Recursos globais da plataforma possuem fronteira diferente dos recursos do tenant e devem ser explicitamente identificados.
- Contas de serviço usam identidades próprias, scopes mínimos e expiração/rotação de credenciais.

## 5.3 Acesso privilegiado

Ações administrativas de alto impacto — alterar permissões, acessar suporte, exportar dados em massa, modificar conectores ou segredos, reprocessar grandes cargas e excluir dados — exigem permissão específica, auditoria reforçada e, quando aplicável, MFA recente ou aprovação adicional.

---

# 6. Isolamento Multi-Tenant

O isolamento de tenant é a principal fronteira de segurança da EIP. `TenantId` é derivado de uma identidade autenticada e propagado com segurança; ele não pode ser confiado apenas porque veio no body, query string ou mensagem.

## 6.1 Regras obrigatórias

- toda entidade de tenant contém `TenantId`; dados empresariais também contêm `CompanyId`;
- repositórios e queries aplicam filtro de tenant de forma centralizada e testada;
- consultas administrativas não podem burlar o filtro sem uma permissão de plataforma explícita e log de auditoria;
- chaves de cache, jobs, eventos, objetos e índices incluem tenant e demais escopos necessários;
- workers validam o contexto associado à instância de conector antes de processar mensagens;
- URLs de objetos não permitem inferir ou acessar dados de outro tenant;
- testes de integração devem executar cenários de tentativa de acesso cruzado.

## 6.2 Banco compartilhado e dedicado

No modelo compartilhado, a aplicação aplica filtros obrigatórios e o banco aplica Row-Level Security (RLS) obrigatória em toda tabela que contenha `TenantId`, sem exceção e desde a primeira migration, além de constraints e contas de acesso restritas. RLS é uma camada complementar obrigatória: não substitui as validações da aplicação, mas sua ausência bloqueia a liberação em produção (ver checklist da seção 15 e ADR-007).

No modelo dedicado, a resolução da conexão é controlada pelo Tenant/Connection Resolver. O serviço não aceita connection strings ou identificadores de banco enviados pelo cliente.

---

# 7. Proteção de Dados

## 7.1 Em trânsito

- HTTPS/TLS é obrigatório fora do desenvolvimento local.
- Serviços internos usam TLS/mTLS quando a topologia e o risco exigirem; ao mínimo, tráfego é autenticado e restrito por rede.
- TLS obsoleto, certificados inválidos e redirecionamento inseguro não são permitidos.
- Webhooks e integrações validam certificados e não permitem downgrade de segurança.

## 7.2 Em repouso

- Bancos, Object Storage, backups e discos usam criptografia em repouso.
- Dados altamente sensíveis podem exigir criptografia adicional no nível da aplicação, com chaves gerenciadas fora do banco.
- Chaves de criptografia devem ser rotacionáveis, com acesso restrito e separação entre ambientes.
- Backups herdam classificação, criptografia e política de acesso do dado original.

## 7.3 Classificação e minimização

Dados devem ser classificados, no mínimo, como Público, Interno, Confidencial ou Sensível. Dados pessoais e financeiros devem receber classificação adequada na ingestão/catálogo.

O pipeline coleta apenas atributos necessários ao caso de uso. Campos pessoais sem uso analítico devem ser excluídos, mascarados ou ter retenção reduzida. Dados de produção não podem ser copiados para desenvolvimento sem anonimização aprovada.

## 7.4 Cache, exportações e relatórios

- Cache não armazena segredo ou PII além do necessário e tem TTL definido.
- Chaves incluem tenant, workspace, permissão e versão da consulta.
- Exportações seguem autorização do dashboard/dado, possuem expiração, trilha de auditoria e, quando necessário, marca d’água ou classificação.
- Links assinados para arquivos são curtos, específicos e não reutilizáveis quando o provedor suportar.

---

# 8. Segredos e Credenciais

Todos os segredos são armazenados em um cofre de segredos gerenciado na produção. O banco da aplicação conserva apenas identificadores/referências, nunca o valor do segredo.

| Regra | Aplicação |
|---|---|
| Sem segredos no repositório | `.env` local não versionado; CI bloqueia chaves detectadas |
| Menor privilégio | usuário de banco read-only para conectores; scopes mínimos em APIs |
| Rotação | credenciais, tokens, chaves e certificados possuem plano e procedimento de rotação |
| Mascaramento | logs, erros e telas ocultam integralmente valores sensíveis |
| Separação de ambientes | segredos de desenvolvimento, homologação e produção nunca se misturam |
| Acesso auditado | leitura/alteração de segredo gera evento de auditoria |

O segredo só é materializado em memória pelo componente que precisa usá-lo e pelo menor período possível.

---

# 9. Segurança de Aplicação e APIs

## 9.1 Requisitos de implementação

- validar entradas por schema, tipo, tamanho, formato e regras de negócio;
- usar queries parametrizadas/ORM; nunca concatenar entrada do usuário em SQL;
- codificar saída no contexto correto para prevenir XSS;
- usar proteção CSRF quando houver autenticação baseada em cookie;
- aplicar CORS com origens, métodos e headers explicitamente permitidos;
- limitar tamanho de payload, taxa de requisições e profundidade de consultas;
- usar timeouts, cancelamento, retry controlado e circuit breaker em integrações;
- não retornar stack traces, SQL, headers internos ou dados de outro tenant;
- testar autorização em todos os endpoints, inclusive os administrativos e de erro.

## 9.2 Upload e processamento de arquivos

Uploads passam por limite de tamanho, validação de extensão/mime real, identificação de arquivo corrompido e armazenamento inicial isolado. Arquivos são processados por worker; não podem ser executados, descompactados sem limite ou interpretados como código.

Arquivos com macros, formatos incomuns ou conteúdo suspeito devem seguir política específica de bloqueio/quarentena. Se houver antivírus ou scanner, a liberação ocorre somente após resultado seguro.

## 9.3 IA e linguagem natural

Recursos de IA devem tratar prompts, contexto e resultados como dados potencialmente sensíveis. É obrigatório:

- aplicar as mesmas permissões de dados antes de montar contexto para o modelo;
- não permitir que instruções do usuário substituam políticas do sistema ou autorização;
- limitar ferramentas, escopos e ações que um agente pode executar;
- registrar uso e decisão sem gravar conteúdo sensível além da política aprovada;
- validar respostas antes de executar automações, exports ou alterações;
- deixar claro ao usuário quando uma resposta é inferência e não dado confirmado.

---

# 10. Infraestrutura, Rede e Operação

## 10.1 Ambientes

Desenvolvimento, homologação e produção são isolados por credenciais, contas, rede e dados. Produção não compartilha banco, fila, Object Storage ou secret store com outros ambientes.

## 10.2 Rede

- bancos, Redis, RabbitMQ e Object Storage não são expostos publicamente;
- somente Gateway/Ingress e serviços estritamente necessários recebem entrada externa;
- regras de firewall/network policy permitem comunicação mínima entre workloads;
- administração de infraestrutura requer canal autenticado, MFA e auditoria;
- portas, serviços e imagens não utilizados são removidos ou desativados.

## 10.3 Containers e dependências

- imagens são versionadas, minimizadas, executadas como usuário não-root e verificadas no CI;
- dependências possuem lockfiles e análise de vulnerabilidades (SCA);
- vulnerabilidades críticas são corrigidas conforme SLA de segurança definido;
- manifests não armazenam segredos em texto claro;
- pacotes, plugins e ações de CI devem ser de fontes confiáveis e fixados em versões/revisões aprovadas.

---

# 11. Logs, Auditoria e Monitoramento

## 11.1 Logs operacionais

Logs estruturados incluem `traceId`, `correlationId`, tenant anonimizado ou identificador seguro, componente, operação, duração e resultado. Não registram tokens, senhas, connection strings, payloads brutos completos, dados financeiros detalhados ou PII sem necessidade aprovada.

## 11.2 Auditoria

Auditoria registra ao menos:

- login, falhas de login, MFA e alterações de credenciais;
- criação, alteração, pausa e exclusão de conectores;
- testes de conexão, sincronizações e reprocessamentos;
- alterações de usuários, permissões, workspace e empresa;
- exportações, acessos de suporte e ações administrativas privilegiadas;
- mudanças de configuração, segredo por referência e políticas de retenção.

O evento possui autor, tenant, recurso, ação, data/hora, origem, resultado e `CorrelationId`. Logs de auditoria são protegidos contra alteração indevida e têm retenção conforme política legal e contratual.

## 11.3 Detecção e alertas

Monitorar e alertar para: falhas de autenticação, escalada de privilégio, acessos negados anormais, alteração de permissões, uso excessivo de API, acesso a dados em massa, segredos próximos da expiração, aumento de DLQ, conexões suspeitas e alterações inesperadas de infraestrutura.

---

# 12. LGPD, Retenção e Direitos do Titular

A EIP deve operar de acordo com a LGPD e obrigações contratuais aplicáveis. A classificação dos papéis de controlador e operador deve ser definida por produto/contrato, sem presumir que a EIP é controladora de todos os dados processados.

Requisitos mínimos:

- inventário e finalidade dos dados pessoais processados;
- base legal e responsabilidades documentadas pelo cliente quando aplicável;
- política de retenção por tipo de dado, tenant e obrigação legal;
- capacidade de localizar, exportar, corrigir, anonimizar ou excluir dados conforme obrigação e limites legais;
- exclusão propagada para camadas derivadas, cache e exportações controladas, preservando registros exigidos por lei;
- avaliação de impacto e revisão jurídica para tratamentos de alto risco;
- contrato com subprocessadores e transferência internacional avaliados antes do uso.

Pedidos de titulares e incidentes de privacidade seguem procedimento rastreável, com prazos e responsáveis definidos.

---

# 13. Backup, Continuidade e Resposta a Incidentes

## 13.1 Backup e recuperação

Backups são automatizados, criptografados, monitorados e testados por restauração periódica. A estratégia define RPO/RTO por classe de serviço, retenção, localização e responsável. Um backup sem teste de restauração não é considerado controle suficiente.

## 13.2 Incidentes

Todo incidente de segurança deve seguir um processo de:

1. detecção e triagem;
2. contenção para reduzir impacto;
3. preservação de evidências e investigação;
4. erradicação da causa e recuperação validada;
5. comunicação a responsáveis, clientes e autoridades quando necessária;
6. análise pós-incidente, ações corretivas e atualização de controles.

O processo deve manter canais de escalonamento, responsáveis de plantão, critérios de severidade e registros protegidos. Não apagar evidências para “limpar” um incidente.

---

# 14. Segurança no Ciclo de Desenvolvimento

- revisão de código obrigatória para mudanças de autenticação, autorização, dados, conectores e infraestrutura;
- análise estática, scan de dependências e de imagens no CI;
- testes automatizados de autenticação, autorização e isolamento multi-tenant;
- validação de migrations, configurações e permissões antes da produção;
- segredo detectado em commit/pipeline bloqueia a entrega e exige rotação;
- correções de segurança têm prioridade sobre novas funcionalidades conforme severidade;
- pentest e revisão de segurança são exigidos antes da abertura de API pública ou disponibilização enterprise relevante.

---

# 15. Checklist de Liberação

Antes de colocar um recurso em produção, confirmar:

- autenticação, autorização e escopo de tenant/workspace testados;
- toda tabela com `TenantId` possui política RLS ativa e coberta por teste automatizado de acesso cruzado;
- entradas, erros, upload e integrações validados;
- segredos fora do código e com acesso mínimo;
- logs mascarados, auditoria e alertas configurados;
- criptografia em trânsito/repouso e backups verificados;
- dependências e imagens analisadas;
- política de retenção/classificação definida;
- plano de rollback, recuperação e resposta a incidente documentado;
- responsáveis técnicos e de negócio identificados.
