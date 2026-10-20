# Configuração e reprocessamento pela interface

Em **Integrações**, “Configurar” permite editar nome e endereço. Empresa e tipo de
dado são fixos. O endereço continua sujeito à lista exata autorizada pelo operador;
o usuário da organização não pode ampliar essa lista. Apenas `connector.manage`
pode salvar; uma integração de outra organização retorna 404.

A edição grava somente nome, endereço e horário de alteração. O avanço de watermark
grava somente o marcador e seu horário, sem sobrescrever a configuração de uma
carga que já estava em andamento. O marcador só avança, inclusive em gravações
concorrentes. Alterar endereço não dispara carga nem reinicia o marcador. Use a
mesma origem; para outra origem de dados, registre uma nova integração.

“Reprocessar desde…” pede data/hora inicial no fuso do navegador e exibe confirmação
antes do envio. O valor é convertido para ISO UTC e enviado como `reprocessFrom`
ao endpoint de sincronização existente. Datas inválidas ou futuras são recusadas.
O limite de sincronizações também se aplica a esse pedido.

A busca considera registros **atualizados desde** a data, conforme o contrato da
origem. Não é um filtro pela data comercial da venda e não existe data final nesta
versão. O processamento é assíncrono; a interface acompanha o ID exato retornado.
Reprocessamentos preservam o watermark da carga incremental e usam os mesmos
upserts, sem apagar dados anteriores.

Se houver falha no salvamento, o formulário mantém os valores para correção.
Cancelar antes da confirmação não envia reprocessamento. A confirmação 202 indica
pedido aceito; a conclusão deve ser conferida no histórico.

## Verificação local em 19/10/2026

Pelo navegador, a integração Demo de clientes foi renomeada para
“Demonstração — Clientes REST”, mantendo seu endereço. O pedido desde
01/01/2026 00:00 no navegador foi recebido pela API como `2026-01-01T03:00:00Z`.
A execução `4c217ae1-3f58-4875-b616-8ab82db44d23` concluiu com 6 recebidos,
6 aceitos, 6 atualizados e zero rejeitados. O watermark antes e depois foi
`2026-10-19T15:15:23.3561713Z`. São dados de demonstração, não um piloto real.

Frontend: 33 testes aprovados. Processamento: 10 testes aprovados, incluindo
reprocessamento desde uma data e preservação do watermark. Builds de produção
do front e Release de Host/Worker aprovados; ambos os serviços locais atualizados.
Permanece o aviso já existente de Sass `@import`.
API: 78/78 aprovados, incluindo bloqueio de destino não autorizado, isolamento,
permissão de edição, recusa de data futura e gravação de watermark com configuração
mais nova que a instância carregada pelo processamento.

[Execução conferida na interface](../operations/screenshots/reprocessamento-data-2026-10-19.png).
