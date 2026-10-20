# Retomada da aplicação sobre cópias restauradas

Ensaio L04 de **19/10/2026**, aprovado localmente.
[Resultado estruturado](../operations/retomada-aplicacao-2026-10-19.json).

## Procedimento

Publicar o Host em uma pasta temporária nova e definir `EIP_RECOVERY_DEMO_PASSWORD`
com a senha **existente** da conta `demo@eip.local`, sem gravá-la em arquivos versionados.
Então executar, na raiz:

```powershell
./scripts/backup/Test-JointRecovery.ps1 -VerifyApplication -HostDll '<caminho absoluto>/EIP.Host.dll'
```

Essa opção valida a conta e o painel da organização Demo padrão. Não é uma
verificação genérica de usuários/painéis de qualquer tenant. O parâmetro sem
`-VerifyApplication` mantém o ensaio anterior de SQL/raw.

O procedimento restaura o SQL e os objetos raw conforme o
[ensaio conjunto](recuperacao-conjunta-demo.md). Com a opção de aplicação, o SQL
restaurado publica uma porta efêmera apenas em loopback. Redis e RabbitMQ novos
usam credenciais/estado próprios e uma rede Docker temporária com as cópias.

Um novo processo nativo do Host usa o binário publicado, em **Development**, com
porta aleatória local. Todas as conexões SQL são sobrescritas para a instância
restaurada; Redis, RabbitMQ, endpoint/credenciais/bucket do storage também apontam
para as instâncias descartáveis. A chave JWT é nova. A configuração e as portas
dos serviços originais permanecem como estavam.

O Redis deve estar vazio antes da inicialização. Depois da readiness, o ensaio:

1. Exige resposta 401 no acesso anônimo aos painéis.
2. Faz login com a conta Demo restaurada e confere a organização do token.
3. Consulta receita por mês e exige total 11.110 com frescor do Warehouse presente.
4. Confere que a consulta povoou o Redis isolado.
5. Localiza o painel Demo restaurado e exige o mesmo total na consulta dos widgets.
6. Consulta linhagem e verifica que o objeto referenciado faz parte do manifesto raw restaurado.
7. Exige reconciliações dentro da tolerância, com totais canônicos e do Warehouse iguais a 11.110.

Ao terminar, encerra somente o processo filho criado pelo ensaio, remove Redis e
RabbitMQ após validar suas labels, desconecta as cópias e remove a rede temporária.
O procedimento principal remove MinIO e SQL descartáveis. O resultado conjunto
é emitido após a limpeza.

## Resultado observado

| Verificação | Resultado |
|---|---|
| SQL restaurado | 39 tabelas, 21.063 linhas e 7 políticas; hashes, integridade e RLS aprovados |
| Raw Demo | 3 objetos restaurados; conteúdo/metadados/checksums idênticos |
| Login / acesso anônimo | Login aprovado / anônimo negado com 401 |
| Readiness da segunda API | Healthy |
| Consulta analítica / painel restaurado | 11.110 / 11.110 BRL |
| Cache exclusivo | Vazio inicialmente; 1 chave após consulta |
| Linhagem / reconciliação | Aprovadas |
| Recursos temporários | Processo, containers e rede removidos |
| Ambiente original após o ensaio | Diagnóstico aprovado; front/API/Gateway disponíveis e filas vazias |

O total de linhas SQL inclui as fixtures da carga de 20 mil fatos, preservadas
no banco completo; o teste funcional e a cópia raw continuam limitados à Demo.
O SHA-256 do binário do Host está no resultado para rastrear a publicação usada.

## Evidências e limites

`result.json` contém `applicationReadback` com as verificações. O log da segunda
API fica em `restored-host.log` na pasta temporária, junto do backup e dos objetos
brutos. Manter essa pasta restrita; somente o resumo foi versionado. Os registros
de login/refresh/uso criados pelo teste ficam apenas no SQL descartável.

A validação usa chamadas à API, incluindo os widgets do painel. Não inicia outro
frontend nem Gateway, não mede renderização no navegador e não troca a conexão da
aplicação aberta. O raw foi lido no storage isolado pelo verificador S3; a consulta
de linhagem pela API comprova os vínculos, sem alegar download de raw por essa rota.

Não inicia Worker nem retoma ingestão; o broker é novo e não restaura mensagens
anteriores. Não homologa Production, TLS/segredos operacionais, recuperação de
todos os tenants, snapshot distribuído atômico ou metas de RPO/RTO.
As suítes de API/frontend não foram repetidas: o build Release e o ensaio funcional
de recuperação foram executados de fato.

Próxima ampliação: retomar uma sincronização nas cópias com Worker e fila isolados,
conferindo escrita raw/canônica/Warehouse e o marcador, sem alterar as origens.

**Atualização posterior em 19/10:** essa ampliação foi
[executada no L05 com três sincronizações incrementais Demo](retomada-ingestao.md).
O resultado L04 acima preserva seu escopo original de leitura.
