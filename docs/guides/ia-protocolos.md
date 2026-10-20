# Protocolos de provedores — preparação local

`AiProviderProtocol` monta pedidos HTTP, mas não os envia. Não está registrado
no Host e não lê credenciais persistidas. `DisabledAiModelAdapter` continua ativo.

Suporta OpenAI Responses, Anthropic Messages e Google generateContent, usando
hosts fixos e credenciais em cabeçalhos. O corpo contém pergunta, catálogo e
schema declarativo; o TenantId de contexto não é serializado. Não há registros
de negócio ou histórico de conversa. A pergunta e o catálogo ainda podem conter
informações sensíveis: a política de envio continua obrigatória.

Limites: pergunta de 4096 caracteres, contexto de 32768 bytes, resposta de 65536
bytes e proposta de 4096 bytes. Modelo aceita apenas letras/números ASCII,
ponto, hífen e underscore; nomes com caminhos não são suportados nesta etapa.
Timeout configurado é validado, mas só será aplicado pelo futuro transporte.

A resposta deve terminar normalmente e conter uma única proposta JSON: `query`
ou `clarification`. Recusa, truncamento, tool calls e campos extras são rejeitados.
O parser da consulta proíbe campos livres/SQL/tenant. A validação semântica e a
autorização devem ocorrer no Analytics antes de qualquer execução; este protocolo
nunca executa propostas. A conformidade é validada localmente, sem promessa de
structured output nativo para todos os modelos.

Próxima etapa: transporte com redirects desativados, timeout/cancelamento,
leitura limitada antes de carregar o corpo, erros HTTP sanitizados e testes com
handler simulado. Depois: seleção autenticada de perfil/credencial, orçamento,
política de dados e avaliação de qualidade. Não conectar estes pedidos à rede
antes dessas barreiras.

Referências oficiais consultadas em 20/10/2026:

- [OpenAI: geração de texto](https://developers.openai.com/api/docs/guides/text)
- [Anthropic: Messages](https://platform.claude.com/docs/en/api/messages/create)
- [Google: generateContent](https://ai.google.dev/api/generate-content)
