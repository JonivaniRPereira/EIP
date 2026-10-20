# Medição de uso por organização

Implementação da Fase 3 E4.2. Atualizado em 18/10/2026.

## Consulta

`GET /api/v1/admin/tenants/{tenantId}/usage?from=2026-10-01T00:00:00Z&to=2026-11-01T00:00:00Z`

Usar o Gateway e um token autenticado. `from` e `to` são obrigatórios, aceitam offsets e são
normalizados para UTC. O intervalo inclui `from` e exclui `to`; o máximo é 366 dias.
Tenant vazio/sentinela de sistema ou período inválido retornam `400`; tenant inexistente, `404`.

A resposta contém `tenantId`, `from`, `to`, `synchronizations`, `analyticsQueries`,
`dataLakeBytes`, `dataLakeObjects` e `measuredAt`.

## O que é contado

| Campo | Semântica |
|---|---|
| `synchronizations` | Runs persistidos no período, por `CreatedAt`, em qualquer status. Pedido bloqueado pela quota não cria run e não conta. Retry/requeue do mesmo run não aumenta o total. Falha de publicação após persistência conta como run solicitado, mesmo sem processamento concluído. |
| `analyticsQueries` | Execuções bem-sucedidas do Analytics Engine após ativar esta entrega, persistidas ao final. Inclui cache hit e consultas feitas pelos widgets dos dashboards. Consultas inválidas, recusadas por guardrail ou que falham não contam. Metadados de catálogo não contam. |
| `dataLakeBytes` / `dataLakeObjects` | Soma de tamanhos e quantidade dos objetos atualmente presentes no prefixo do tenant cuja última modificação no storage está no período. Não são bytes transferidos, faturamento, versões antigas ou um snapshot histórico de objetos já removidos. |

Não há backfill das consultas analíticas anteriores: só os logs antigos existiam, sem contador
durável. Analytics é registrado no SQL Server (`administration.AnalyticsQueries`), com índice
por tenant/data e política RLS criada na mesma migration. Não são gravados SQL, payloads, valores
retornados ou dados pessoais nesse registro. A falha de persistência da medição impede a resposta
bem-sucedida da consulta; não se confirma sucesso descartando a contagem silenciosamente.

As três fontes são lidas sequencialmente, sem transação distribuída. `measuredAt` marca o fim da
leitura; atividade concorrente pode ocorrer entre as leituras. Para reconciliação, usar um período
encerrado e sem mutações de storage durante a conferência.

## Acesso administrativo

A permissão é `platform.tenant-usage.read`, persistida como claim de usuário do ASP.NET Identity
com tipo `platform_permission`. Ela é conferida no banco a cada requisição. A revogação vale mesmo
para tokens já emitidos. Usuário inexistente, bloqueado ou sem a claim não é autorizado.

Esta permissão não pertence a `RolePermissions` de tenant. Ser Owner/Admin de uma organização,
ou enviar uma claim com o mesmo texto no token, não concede acesso. Não existe endpoint público
para concedê-la; registro e provisionamento de tenant não a atribuem. A conta demo permanece sem
essa permissão. Provisionar/revogar apenas por operação administrativa confiável do Identity,
preferencialmente `UserManager.AddClaimAsync` / `RemoveClaimAsync`, identificando o usuário por ID.

Sem autenticação, a API retorna `401`; com autenticação sem a permissão global, `403`, inclusive
ao consultar a própria organização. Após autorização, o reader usa o contexto do tenant alvo e
restaura o contexto anterior em `finally`. Não usa sentinela de sistema para ler as contagens;
os filtros RLS continuam ativos.
Consultas administrativas bem-sucedidas geram log estruturado com usuário, tenant alvo e período,
além do CorrelationId do request.

## Ambiente e validação

Aplicar a migration do novo módulo antes de iniciar a API:

```bash
dotnet ef database update --project src/Platform/Administration/Infrastructure --startup-project src/Platform/Administration/Infrastructure
```

Host: configurar `ConnectionStrings:UsageDb` e `DataLake` (mesmo bucket/endpoint usados pelo
Worker). A factory de migrations aceita `EIP_USAGE_DB_CONNECTION`. O valor padrão é somente
para desenvolvimento local, como os demais módulos.

O reader lista objetos com prefixo de tenant e paginação S3, sem baixar os conteúdos. Bucket ainda
inexistente significa zero objetos; erro de conexão ou de permissão do storage é propagado, sem
ser apresentado como zero. O endpoint exige um serviço S3 disponível mesmo se as contagens SQL
estiverem acessíveis.

`TenantUsageTests` usa SQL Server e Redis efêmeros e o emulador Adobe S3Mock 5.1.0 apenas para
testes. O caminho é HTTP S3 + SDK real + arquivos gravados/lidos pelo serviço; não substitui uma
homologação específica com MinIO. O download das imagens MinIO configuradas falhou nesta retomada
(Docker Hub: acesso negado; Quay: 401). O Compose e o storage de desenvolvimento não foram trocados
pelo emulador.

Os testes verificam atividade real via API, contagem incluindo cache hit, período vazio, acesso
negado para Owner, acesso administrativo a outro tenant, revogação, RLS de leitura/escrita,
restauração de contexto e o gate estrutural. `RawStorageUsageReaderTests` cobre paginação e os
limites inclusivo/exclusivo do período com respostas controladas do SDK.
