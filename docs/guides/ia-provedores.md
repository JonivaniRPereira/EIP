# Preparação da IA para vários provedores

A instalação mantém vários perfis na seção `AI` da configuração do Host.
Cada perfil tem identificador próprio, provedor, modelo, referência de credencial,
tempo de espera e limite de saída. Pode haver vários perfis do mesmo provedor.
Os exemplos iniciais são OpenAI, Anthropic e Google; são perfis de preparação,
sem transportes HTTP de modelo implementados nesta entrega.

## Configuração atual

Em `src/Host/appsettings.json`, `AI:Profiles` contém os perfis e
`AI:ActiveProfileId` registra a seleção da instalação. Os modelos começam vazios
e nenhum perfil começa selecionado. Exemplo de substituição por ambiente:

```powershell
$env:AI__ActiveProfileId = 'openai-primary'
$env:AI__Profiles__0__Model = '<modelo escolhido pelo operador>'
```

Essa seleção não habilita chamadas externas. O único adaptador registrado é o
desabilitado. Não há chave de ativação para contornar essa condição. Reinicie a
API após modificar a configuração; o catálogo é um snapshot validado na partida.

`CredentialEnvironmentVariable` aceita apenas o nome de uma variável com prefixo
`EIP_AI_`, letras maiúsculas, números e sublinhado. Nunca inserir o valor da chave
nesse campo. Referências iniciais: `EIP_AI_OPENAI_API_KEY`,
`EIP_AI_ANTHROPIC_API_KEY` e `EIP_AI_GOOGLE_API_KEY`. Esta base ainda não lê essas
variáveis nem envia credenciais. Os valores devem permanecer fora do Git e do
frontend quando os transportes forem implementados.

Até 20 perfis, identificadores únicos de até 64 caracteres, modelo com até 128
caracteres, timeout entre 1 e 120 segundos e saída entre 1 e 32.768 tokens.
Perfil ativo inexistente ou configuração inválida impede a partida. Estes são
limites de configuração da plataforma, ainda sem execução/cobrança de modelo.

## Configuração por organização

Owner e Admin têm a permissão `ai.settings.manage` e acessam **Provedores de IA**
no menu, em `/settings/ai`. A tela permite criar/remover perfis, escolher provedor
e modelo, ajustar limites e selecionar um perfil preferido. Para selecionar um
perfil, informe seu modelo; rascunhos não selecionados podem ficar sem modelo.
Não há campos para chaves, endpoints externos ou ativação de chamadas.

Ao salvar, a configuração fica em `ai.ProviderSettings`, no mesmo banco físico
de `TenantDb`, com schema/histórico próprios e RLS de leitura e escrita. O servidor
obtém tenant e autor do JWT. Guarda somente perfis, seleção, revisão e último
autor/instante; logs de gravação contêm tenant, autor e revisão, sem modelos,
payloads ou credenciais. Não há histórico de versões completo nesta etapa.

Organizações sem configuração salva herdam os perfis iniciais da instalação.
Após salvar, passam a ter seleção/perfis próprios. O catálogo público de IA
reflete esses perfis efetivos. A lista de provedores permitidos continua definida
pelo operador da instalação; não se pode criar um destino de rede arbitrário.

A revisão evita sobrescrita concorrente: duas sessões com a mesma revisão não
conseguem gravar silenciosamente uma sobre a outra. Resposta 409 mantém as edições
na tela e orienta recarregar. Falhas de gravação também preservam o formulário.
Recarregar substitui edições locais pela versão atual do servidor.

## Contrato da plataforma

Com tenant selecionado e `analytics.query`:

- `GET /api/v1/ai/status`: estado efetivo, atualmente `enabled: false` e
  `code: ai_model_not_configured`; provedor/modelo efetivos nulos.
- `GET /api/v1/ai/providers`: seleção e perfis públicos, sempre
  `invocationEnabled: false` nesta etapa. Sem nomes de variáveis ou valores de
  credenciais na resposta.

Com tenant selecionado e **`ai.settings.manage`**:

- `GET /api/v1/ai/settings`: configuração efetiva e revisão (zero se ainda herda padrões).
- `GET /api/v1/ai/settings/providers`: provedores permitidos para a edição.
- `PUT /api/v1/ai/settings`: corpo com `revision`, `activeProfileId` e `profiles`;
  cada perfil tem `id`, `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`.
  Até 32 KiB; 400 para argumentos inválidos, 409 para revisão desatualizada.

Campos extras, inclusive tenant, ativação ou credenciais, são recusados. Membros
comuns continuam sem permissão de editar, mesmo tendo `analytics.query`.
Tokens anteriores à nova permissão precisam ser renovados por refresh/login.

## Migration

Antes de executar a API nova, aplicar `InitialProviderSettings` do projeto
`src/Intelligence/AI/Infrastructure`. A variável de design-time
`EIP_AI_DB_CONNECTION` deve apontar para o mesmo banco de `TenantDb` dessa
instalação, obtida da configuração segura do operador:

```powershell
dotnet ef database update --project src/Intelligence/AI/Infrastructure --context AiDbContext
```

A migration cria tabela, função e política RLS na mesma transação. A configuração
de execução usa a conexão de `TenantDb` e o interceptor de contexto existente.
O projeto de apoio aos testes aplica a migration também nos bancos temporários.

`IAiModelAdapter` recebe pergunta, catálogo semântico e tenant autenticado.
Retorna proposta de consulta declarativa, pedido de esclarecimento ou erro.
Proposta não executa consulta: o fluxo futuro deve passar pela ferramenta
`queryAnalytics` e sua autorização. O adaptador desabilitado retorna erro
explícito, respeita cancelamento e não fabrica consulta, números ou resposta.
Não há endpoint público de perguntas, memória de conversa ou persistência de
prompts nesta entrega.

## Continuação de F4.3

O usuário definiu suporte a vários provedores configuráveis na plataforma.
O contrato, os perfis iniciais e a edição persistida por organização estão
implementados. Armazenamento de segredos e políticas de custo/envio por organização
continuam pendentes. Configurações iniciais globais continuam sob o operador.

Antes de habilitar modelos: implementar transportes específicos e seus testes,
armazenamento protegido de credenciais, orçamento/quotas efetivos, política de
envio de dados e credenciais. Selecionar perfil não autoriza enviar perguntas ou
agregados a terceiros. Não há fallback automático para outro provedor.

[Backlog da Fase 4](../roadmap/fase-4-backlog.md).
