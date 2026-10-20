# Revisão de segurança da API — Fase 3 E6.1

Data: 18/10/2026. Revisão manual da superfície implementada, seguida de correções e testes.
O skill `security-review` citado no backlog não estava disponível no catálogo nem nos diretórios
de skills consultados. Não foi usado um review limitado ao diff como substituto da leitura da API.

## Escopo e fronteiras

Foram lidos os controllers de Identity, Tenant, Connector/Quarantine, Semantic, Analytics,
Dashboard, Catalog e Administration; composição de autenticação/autorização, Gateway/CORS,
resolução de tenant/RLS, refresh tokens, cliente HTTP do worker, chaves do storage e erros públicos.
As correções de rotação exigiram também ajustar a coordenação de refresh/logout no frontend.

| Superfície | Fronteira revisada |
|---|---|
| Register/login/refresh/logout | Anônimos por desenho; entrada validada e limitada; token de refresh é credencial de posse, armazenada por hash. Logout não revela existência do token. |
| Select-tenant | JWT válido e membership ativa; usuário bloqueado não emite novos tokens. |
| Criar organização | Autenticação exigida pela fallback policy; criação interna sob contexto de sistema, sem aceitar TenantId do cliente. |
| Tenant, conectores, quarentena, métricas, analytics, dashboards e catálogo | Permissões por operação, tenant obtido do token validado e RLS; recursos cruzados não são liberados. |
| Administração/uso | Permissão global persistida, revogação conferida a cada pedido; fora de Development, também exige `amr=mfa` confiável. |
| Health/metrics | Anônimos no Host para probes/coleta interna. Gateway publica somente `/api/**`; esses endpoints exigem isolamento de rede do Host. |
| OpenAPI e fontes `/sample/*` | Disponíveis somente em Development; não são endpoints de negócio públicos em produção. |
| Worker HTTP | Destinos exatos configurados pelo operador, sem redirects/cookies, com limites e validação anterior ao acesso de rede. |

## Achados confirmados e correções

| ID | Problema anterior | Correção e prova |
|---|---|---|
| SEC-01 | Duas requisições podiam ler o mesmo refresh token ativo antes da revogação e emitir substitutos. | Consumo por `UPDATE` condicional atômico no SQL. Oito concorrentes produzem um sucesso e sete 401; o substituto continua utilizável. |
| SEC-02 | Refresh e select-tenant não verificavam bloqueio de conta. | Verificação de lockout antes de emitir token; login bloqueado usa a mesma mensagem genérica de credenciais inválidas. Teste cobre refresh/select/login. |
| SEC-03 | URL controlada pelo tenant era enviada ao worker sem restrição de destino; cliente seguia redirects e recebia payload sem limite. | Lista de URLs exatas em `ReferenceRest:AllowedEndpoints`, negando por padrão. Sem credenciais na URL, query ou fragmento; apenas HTTP(S). Redirect automático e cookies desativados; 3xx rejeitado. Limite padrão de 8 MiB, conferido por tamanho declarado e durante streaming; timeout também na leitura do corpo. Testes de URLs hostis, HTTP real com redirect, tamanho conhecido/desconhecido e extração válida com watermark. |
| SEC-04 | Cadastro aceitava CompanyId sem confirmar o vínculo com o tenant. | Consulta pelo diretório de empresas sob o contexto autenticado, antes da persistência. Empresa inexistente/de outro tenant recebe o mesmo erro; cadastro válido continua funcionando. |
| SEC-05 | Configuração de desenvolvimento (inclusive chave JWT pública e SQL `sa`) podia iniciar o Host fora de Development. | Startup recusa chave padrão/curta, conexões padrão, `sa`, SQL sem TLS/certificado validado, storage HTTP/padrão, broker sem TLS e Redis sem TLS. Worker também exige conexões/destinos seguros fora de Development. Perfil `local` do worker explicita Development. Testes de configurações recusadas e configuração explícita aceita. |
| SEC-06 | Exceções de processamento/publicação eram persistidas como texto no SyncRun retornado ao cliente. | Mensagens públicas genéricas; detalhes ficam nos logs operacionais com correlação. Teste confirma ausência do texto original no run. Exceção interna injetada na API resulta em 500 sem detalhe/stack sensível no corpo. |
| SEC-07 | PKCS 9.0.4 transitivo apresentava avisos de vulnerabilidade. | Pin da versão corrigida 10.0.13 em BuildingBlocks.Data e Identity.Infrastructure; auditoria NuGet da solução sem entradas vulneráveis conhecidas. |
| SEC-08 | DTOs de autenticação não tinham limites explícitos; respostas de credenciais não instruíam caches a evitar armazenamento. | Validação obrigatória, limites de tamanho e e-mail; erros 400. Host/Gateway enviam `Cache-Control: no-store` em `/api`, `nosniff` e `Referrer-Policy`; Host adiciona CSP para suas respostas de API. |
| SEC-09 | Sair no frontend apagava apenas o estado local, mantendo o refresh token válido. | Endpoint idempotente `/auth/logout` consome o token, sem informar se existia; frontend chama a revogação e limpa o estado local. Refresh concorrente no cliente compartilha um único pedido, e resposta tardia não restaura sessão encerrada. |
| SEC-10 | SourceEntity arbitrária podia entrar na construção de uma chave do Data Lake. | API restringe às entidades implementadas; storage rejeita segmento com separadores/caracteres inválidos antes de acessar S3. |
| SEC-11 | Claims obrigatórias/tenant reservado dependiam apenas do emissor; policy dinâmica não exigia autenticação explicitamente. | JWT aceita HS256 e rejeita sub vazio/inválido, tenant vazio/inválido ou sentinela de sistema. Policies de permissão também exigem usuário autenticado. Testes com JWTs assinados contendo tenants reservados são negados. |

O consumo de refresh é propositalmente irreversível: falha posterior à reserva exige novo login,
em vez de permitir replay do token anterior. Logout revoga o refresh fornecido; access tokens já
emitidos continuam válidos até expiração (15 minutos, com skew configurado de 30 segundos).
Não foi introduzida uma blacklist global de access tokens. Se o browser estiver offline, o logout
limpa a sessão local, mas não confirma revogação no servidor.

As versões corrigidas de PKCS foram conferidas nos avisos publicados pela Microsoft:
[CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx) e
[CVE-2026-70162](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).
A auditoria mede os pacotes NuGet resolvidos; não é uma varredura de sistema operacional, runtime
compartilhado ou de imagens Docker.

## Verificações mantidas

- Assinatura, emissor, audiência, expiração e claims do JWT; fallback exige autenticação.
- Refresh tokens aleatórios de 32 bytes, persistidos somente por hash e com índice único.
- Permissões de tenant separadas da permissão global; RLS com filtro e bloqueio de escrita.
- Gate automatizado cobre toda tabela com TenantId, sem novas exceções.
- SQL parametrizado/LINQ e catálogo analítico declarativo, sem SQL arbitrário do cliente.
- Cache analítico inclui tenant e versão/frescor dos dados; testes impedem compartilhamento cruzado.
- Worker confere tenant/instância; DLQ filtra tenant e responde NotFound ao recurso de outro tenant.
- CORS usa origens explícitas; curingas, credenciais e caminhos em origem são recusados, e fora
  de Development exige HTTPS. Rate limiting por IP do Gateway e quota de sync continuam ativos.

## Validação

Resumo técnico: `docs/operations/seguranca-api-2026-10-18.json`.

- 73 testes de integração da API passaram (incluindo regressões e isolamento existentes).
- Nove testes do conector/worker passaram; os dois testes de idempotência/erro foram reexecutados
  após fortalecer a asserção de sanitização.
- 16 testes do frontend passaram, incluindo refresh compartilhado e logout durante refresh.
- Build de produção do frontend passou; aviso Sass de depreciação já existente permanece.
- Build Release final de toda a solução passou, incluindo worker e Gateway.
- Auditoria NuGet de 51 projetos: nenhuma entrada de pacote vulnerável conhecida após os pins.

Após reiniciar os binários corrigidos, o ambiente local confirmou login 200, logout 204,
refresh revogado 401, cadastro de destino proibido 400, cabeçalhos `no-store`/`nosniff`, CORS
permitindo o frontend e negando origem estrangeira, Gateway `/metrics` 404 e API `Healthy`.
Uma execução separada de Host/Worker em Production com as configurações locais padrão foi
recusada antes de iniciar serviços. A conta demo e a aplicação de desenvolvimento foram preservadas.

Testes SQL/Redis/RabbitMQ usam containers efêmeros. Testes de uso S3 continuam usando S3Mock;
o download do MinIO configurado ainda está indisponível. O teste de redirect sobe HTTP local real
e usa o mesmo registro de HttpClient do worker, confirmando que o destino redirecionado não é chamado.

## Operação e limites

Configurar **a mesma lista de endpoints** no Host e no Worker. Os três destinos locais `/sample`
estão explicitamente autorizados em Development; URLs externas ou caminhos diferentes passam a
ser recusados até autorização do operador. Não colocar chaves/tokens em URLs. Domínios autorizados
devem pertencer a provedores confiáveis; controle de DNS e egress de rede continuam responsabilidade
do ambiente. A lista é exata, sem wildcard, e não é editável pelo tenant.

O mecanismo atual de login local não emite uma prova MFA. Consequentemente, mesmo com grant
global, o endpoint administrativo fica fechado fora de Development até existir autenticação
confiável que emita `amr=mfa`. Não habilitar a exceção de Development em um ambiente público.
Nenhum grant de plataforma foi adicionado à conta demo.

Esta revisão não certifica implantação em produção: TLS externo, segredos reais/rotação,
ACLs efetivas do banco, isolamento do Host/probes/broker/storage, atualização do runtime/imagens,
MFA/federação e testes de carga/penetração exigem verificação no ambiente que será implantado.
O armazenamento de tokens no browser continua o desenho MVP em localStorage já registrado na
Fase 0; migração para sessão HttpOnly e análise de XSS da interface são trabalho próprio de frontend.
LGPD (E6.2) e carga/isolamento concorrente (E6.3) continuam pendentes.
