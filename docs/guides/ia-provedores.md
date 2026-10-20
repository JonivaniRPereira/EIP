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

## Contrato da plataforma

Com tenant selecionado e `analytics.query`:

- `GET /api/v1/ai/status`: estado efetivo, atualmente `enabled: false` e
  `code: ai_model_not_configured`; provedor/modelo efetivos nulos.
- `GET /api/v1/ai/providers`: seleção e perfis públicos, sempre
  `invocationEnabled: false` nesta etapa. Sem nomes de variáveis ou valores de
  credenciais na resposta.

`IAiModelAdapter` recebe pergunta, catálogo semântico e tenant autenticado.
Retorna proposta de consulta declarativa, pedido de esclarecimento ou erro.
Proposta não executa consulta: o fluxo futuro deve passar pela ferramenta
`queryAnalytics` e sua autorização. O adaptador desabilitado retorna erro
explícito, respeita cancelamento e não fabrica consulta, números ou resposta.
Não há endpoint público de perguntas, memória de conversa ou persistência de
prompts nesta entrega.

## Continuação de F4.3

O usuário definiu suporte a vários provedores configuráveis na plataforma.
Esta entrega prepara o contrato e os perfis no backend. A tela administrativa
para editar configurações, armazenamento de segredos e políticas por organização
continuam pendentes; a configuração atual é global por instalação e só é editada
pelo operador da aplicação.

Antes de habilitar modelos: implementar transportes específicos e seus testes,
governança da configuração administrativa, orçamento/quotas efetivos, política de
envio de dados e credenciais. Selecionar perfil não autoriza enviar perguntas ou
agregados a terceiros. Não há fallback automático para outro provedor.

[Backlog da Fase 4](../roadmap/fase-4-backlog.md).
