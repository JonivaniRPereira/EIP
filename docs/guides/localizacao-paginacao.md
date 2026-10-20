# Localização e históricos — 19/10/2026

O front usa pt-BR para datas e números. O painel apresenta `11.110 BRL`, tabelas
formatam valores numéricos e gráficos usam vírgula decimal nos valores. Textos de
origem continuam textos. A mudança é de apresentação; datas persistidas e fuso
horário do navegador permanecem como estavam.

Em Integrações, execuções e quarentena carregam em grupos de 20. O botão de
anteriores aparece somente quando há mais registros. A consulta pede 21 registros
para detectar a próxima página; o cursor contém data e identificador do último
registro exibido. Ordenação decrescente por ambos evita empates de data e o
deslocamento das páginas por novas execuções.

Carregar execuções anteriores pausa a atualização automática. “Atualizar execuções”
retorna às mais recentes. Uma execução solicitada pode continuar destacada fora
da página sem alterar o cursor. Trocar integração ou filtro reinicia a consulta.
Os filtros de integração e reprocessamento solicitado são aplicados no servidor
antes do limite. A fila de falhas mantém sua consulta limitada anterior.

As APIs continuam retornando arrays. Execuções aceitam `limit` (1–100), `before`
e `beforeId` juntos. Quarentena aceita os mesmos parâmetros e `includeRequested`;
chamadas antigas sem limite/cursor mantêm o comportamento anterior.
Todas as consultas mantêm o isolamento por organização.

Regressões cobrem páginas com a mesma data, inserção entre consultas, filtros e
troca de contexto. A validação visual usa os dados Demo existentes; os testes de
paginação usam dados sintéticos isolados dos testes de integração.

Validação: API 77/77, frontend 31/31, publicação Release do Host e build de produção
do front aprovados. API local atualizada. Permanece o aviso já existente de
depreciação de Sass `@import`. [Evidência visual](../operations/screenshots/painel-pt-br-2026-10-19.png).
