# 06 - API Design

**Projeto:** Enterprise Intelligence Platform (EIP)  
**Versão:** 1.0  
**Status:** Oficial  
**Última atualização:** Julho/2026

---

# 1. Objetivo

Este documento estabelece o padrão de design, segurança e evolução das APIs da EIP. Ele se aplica às APIs consumidas pelo frontend, integrações de clientes, parceiros e comunicação entre módulos da plataforma.

As APIs existem para expor capacidades de negócio com contratos explícitos. Elas não devem refletir diretamente tabelas de banco, estruturas de ERP ou detalhes internos de implementação.

---

# 2. Princípios

- **API first:** contratos OpenAPI são definidos e revisados antes ou junto da implementação.
- **Orientada a recursos:** URLs representam recursos de negócio; verbos HTTP representam a ação.
- **Versionada e compatível:** mudanças são evolutivas, previsíveis e documentadas.
- **Segura por padrão:** autenticação, autorização e contexto de tenant são obrigatórios.
- **Consistente:** mesmos nomes, convenções de paginação, formatos de data e respostas de erro.
- **Assíncrona quando necessário:** tarefas longas retornam aceitação e podem ser acompanhadas por job/status.
- **Observável:** toda chamada possui correlação, telemetria e logs estruturados.
- **Mínima exposição:** respostas entregam apenas o dado necessário e nunca segredos ou detalhes operacionais internos.

---

# 3. Escopo e Tipos de API

| Tipo | Consumidor | Característica |
|---|---|---|
| Platform API | Frontend EIP e integrações autorizadas | REST/JSON, versionada sob `/api/v1` |
| Public Integration API | Sistemas de clientes e parceiros | REST/JSON, escopos explícitos, rate limit e documentação pública |
| Internal Service API | Módulos/serviços da EIP | REST ou gRPC, autenticação de serviço e contrato próprio |
| Webhook | Sistemas externos | Entrada ou saída assíncrona autenticada e idempotente |
| Event Contract | Workers e módulos por mensageria | Evento versionado, semântica de fato de negócio |

O MVP prioriza a Platform API. APIs públicas externas só devem ser abertas quando houver modelo de autenticação, quotas, auditoria e suporte operacional definidos.

---

# 4. Convenções Gerais

## 4.1 Base URL e versão

```text
https://api.eip.example/api/v1/
```

A versão maior fica no caminho. Exemplos:

```text
GET  /api/v1/companies
POST /api/v1/connectors/{connectorId}/sync-jobs
GET  /api/v1/dashboards/{dashboardId}
```

Não são permitidas APIs sem versão pública. Endpoints internos podem seguir uma convenção específica, mas seus contratos também devem ser versionados.

## 4.2 Recursos e URLs

- Usar substantivos no plural e letras minúsculas com hífen quando houver mais de uma palavra: `/sync-jobs`, `/cost-centers`.
- Identificadores são UUIDs e aparecem como segmentos de URL: `/companies/{companyId}`.
- Relações e ações subordinadas são explícitas: `/connectors/{connectorId}/sync-jobs`.
- Evitar verbos em URLs. Exceções são ações que não representam CRUD, como `/auth/token`, `/connectors/{id}/test-connection` e `/sync-jobs/{id}/retry`.
- Não expor paths de banco, nomes de tabelas, siglas de ERP ou detalhes de infraestrutura.

## 4.3 JSON e nomenclatura

Requisições e respostas usam JSON UTF-8 com propriedades em `camelCase`.

```json
{
  "companyId": "1db635af-56d8-48ef-b421-b834e8d34fb5",
  "legalName": "Empresa Exemplo Ltda.",
  "isActive": true
}
```

- IDs são UUIDs serializados como string.
- Datas e horas usam ISO 8601 em UTC: `2026-07-22T15:30:00Z`.
- Datas sem horário usam `YYYY-MM-DD`.
- Valores monetários são números decimais; consumidores não devem usar ponto flutuante binário para cálculos financeiros.
- Campos ausentes e `null` têm semântica distinta: omitir significa “não informado/não solicitado”; `null` significa valor conhecido como vazio quando permitido pelo contrato.

---

# 5. Autenticação, Autorização e Contexto

## 5.1 Autenticação

As APIs autenticadas usam OAuth 2.0/OpenID Connect e tokens JWT. O header padrão é:

```http
Authorization: Bearer <access-token>
```

Tokens devem ter curta duração, emissor e audiência validados, assinatura verificável e scopes/papéis adequados. Tokens, senhas, chaves e refresh tokens nunca podem aparecer em logs ou respostas de erro.

## 5.2 Autorização

A autorização é baseada em permissões, aplicadas antes do acesso aos recursos. Papéis podem agrupar permissões, mas uma verificação deve considerar no mínimo: usuário/cliente, tenant, workspace quando aplicável, empresa e ação solicitada.

Exemplos de permissões:

```text
companies.read
companies.manage
connectors.manage
sync-jobs.execute
dashboards.read
dashboards.manage
analytics.query
```

## 5.3 Tenant, workspace e empresa

O `TenantId` é derivado do token e da sessão autenticada; o cliente não pode defini-lo livremente no corpo da requisição.

O workspace ativo pode ser resolvido por contexto de sessão ou por um header controlado:

```http
X-Workspace-Id: 9cca5855-912a-4a30-94c9-22cfb8547660
```

O header é validado contra as permissões do token. A empresa pode ser filtro de recurso, por exemplo `?companyId={id}`, mas também será validada contra tenant e workspace. O backend deve propagar o contexto aos logs, cache, jobs e eventos.

---

# 6. Operações HTTP

| Operação | Método | Sucesso típico | Observação |
|---|---|---:|---|
| Listar recursos | `GET` | `200 OK` | Paginação e filtros opcionais |
| Obter recurso | `GET` | `200 OK` | `404` quando não existir ou não for visível |
| Criar recurso | `POST` | `201 Created` | Retorna recurso e header `Location` |
| Atualização parcial | `PATCH` | `200 OK` | Campos alteráveis definidos em contrato |
| Substituição completa | `PUT` | `200 OK` | Usar somente quando a semântica completa for clara |
| Excluir/desativar | `DELETE` | `204 No Content` | Preferir desativação para configurações auditáveis |
| Disparar trabalho | `POST` | `202 Accepted` | Retorna o recurso de job e sua URL |

`GET`, `PUT` e `DELETE` devem ser idempotentes. `POST` pode ser tornado idempotente conforme descrito na seção 9.

---

# 7. Listagem, Filtros e Ordenação

## 7.1 Paginação

Listas usam paginação por cursor sempre que a coleção puder crescer. O contrato padrão é:

```http
GET /api/v1/sync-jobs?limit=50&cursor=eyJ...
```

```json
{
  "items": [
    {
      "id": "7d30af6d-0314-45dc-b502-f0e69b6c0a6f",
      "status": "succeeded"
    }
  ],
  "nextCursor": "eyJ...",
  "hasMore": true
}
```

- `limit` padrão: 50; máximo: 100, salvo exceção documentada.
- Cursores são opacos; clientes não inferem ou manipulam seu conteúdo.
- Não retornar contagem total se o custo for relevante. Quando disponível, ela é opcional em `totalCount`.

## 7.2 Filtros e ordenação

Filtros usam query string com nomes explícitos:

```text
GET /api/v1/sync-jobs?status=failed&connectorId={id}&startedFrom=2026-07-01T00:00:00Z
GET /api/v1/sales-invoices?companyId={id}&issueDateFrom=2026-07-01&issueDateTo=2026-07-31
```

Ordenação usa `sort` e direção opcional:

```text
GET /api/v1/sync-jobs?sort=startedAt:desc
```

Cada endpoint documenta campos filtráveis e ordenáveis. Não são aceitos filtros SQL, expressões arbitrárias ou nomes de coluna fornecidos pelo cliente.

---

# 8. Criação, Atualização e Concorrência

## 8.1 Criação

O servidor gera os IDs, define campos técnicos, registra auditoria e retorna `201 Created` com o header `Location`.

```http
POST /api/v1/companies
Content-Type: application/json

{
  "legalName": "Empresa Exemplo Ltda.",
  "countryCode": "BR",
  "defaultCurrencyCode": "BRL"
}
```

## 8.2 Atualização parcial

`PATCH` atualiza somente propriedades explicitamente permitidas. Campos derivados, de auditoria, tenant e origem não podem ser alterados pelo cliente.

Para recursos editáveis em colaboração, a API deve retornar `ETag`. O cliente envia `If-Match` na alteração:

```http
If-Match: "W/\"company-42-v3\""
```

Quando a versão não corresponder, retornar `412 Precondition Failed`, evitando sobrescrita silenciosa.

## 8.3 Exclusão

O `DELETE` físico só é permitido para dados sem valor de auditoria e sem dependências. Configurações, conectores e recursos administrativos normalmente são desativados, mantendo histórico e impedindo novas execuções.

---

# 9. Idempotência e Operações Assíncronas

## 9.1 Idempotência em POST

Clientes podem enviar uma chave única em operações sensíveis a repetição:

```http
Idempotency-Key: 05e953a2-8c43-4704-a821-4aaf1b7d8c8b
```

O servidor vincula a chave ao tenant, rota e payload normalizado por período limitado. Uma repetição retorna o resultado original; reutilizar a chave com payload diferente retorna `409 Conflict`.

Aplicar no mínimo a: criação de conectores, uploads, disparo de sync jobs, exportações e comandos de automação.

## 9.2 Jobs

Operações que possam ultrapassar o tempo de uma requisição retornam `202 Accepted`:

```json
{
  "id": "8f4fc7ea-6e3f-4ce2-a7f8-4da83c00fecb",
  "status": "queued",
  "statusUrl": "/api/v1/sync-jobs/8f4fc7ea-6e3f-4ce2-a7f8-4da83c00fecb"
}
```

O status de job é consultável conforme a permissão do solicitante. Estados padrão: `queued`, `running`, `succeeded`, `failed`, `canceled`, `partiallySucceeded`.

---

# 10. Erros

Erros usam `application/problem+json`, baseado em RFC 9457/Problem Details.

```json
{
  "type": "https://api.eip.example/problems/validation-error",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more fields are invalid.",
  "instance": "/api/v1/companies",
  "traceId": "00-3c1...-01",
  "errors": {
    "legalName": ["The legalName field is required."]
  }
}
```

| Status | Uso |
|---:|---|
| `400` | Requisição inválida, incluindo validação de campos |
| `401` | Token ausente, inválido ou expirado |
| `403` | Autenticado, mas sem permissão para a ação/escopo |
| `404` | Recurso inexistente ou não visível ao contexto autorizado |
| `409` | Conflito de estado, idempotência ou regra de unicidade |
| `412` | ETag/condição de concorrência não atendida |
| `422` | Estrutura válida, mas regra de negócio impede o processamento |
| `429` | Rate limit excedido; incluir `Retry-After` quando possível |
| `500` | Falha inesperada; sem detalhes internos |
| `503` | Dependência temporariamente indisponível |

Mensagens são seguras para exibição; stack traces, SQL, endpoints internos, credenciais e dados pessoais sensíveis não podem ser retornados.

---

# 11. Rate Limiting e Resiliência

O Gateway aplica limites por identidade, tenant, rota e, para APIs públicas, por client/application. Limites podem variar por plano contratado e custo operacional da rota.

Respostas limitadas usam `429` e, quando conhecido, `Retry-After`. Endpoints de consulta analítica, exportação e IA devem ter quotas e custos controlados. O cliente deve usar timeout, retry exponencial apenas para falhas transitórias e idempotência para comandos repetíveis.

---

# 12. Webhooks

## 12.1 Recepção

Webhooks recebidos de fontes externas devem validar assinatura, timestamp e proteção contra replay. O payload é registrado de modo seguro e transformado em job assíncrono; não é processado integralmente na chamada HTTP.

## 12.2 Emissão

Webhooks enviados pela EIP usam URL configurada, evento versionado, assinatura HMAC e identificador único de entrega. O destinatário responde `2xx` para confirmar; falhas transitórias usam retry e, após o limite, ficam disponíveis para inspeção/reenvio autorizado.

Exemplo de envelope:

```json
{
  "id": "77e2fdd1-0b07-423f-8e90-3c641f36a4cc",
  "type": "connector.sync-job.succeeded.v1",
  "occurredAt": "2026-07-22T15:30:00Z",
  "data": {
    "syncJobId": "8f4fc7ea-6e3f-4ce2-a7f8-4da83c00fecb"
  }
}
```

---

# 13. Eventos Internos

Eventos representam fatos que já ocorreram, por exemplo `ConnectorSynchronized`, `CanonicalRecordProcessed` e `DashboardPublished`.

Todo evento contém, no mínimo: `eventId`, `eventType`, `occurredAt`, `schemaVersion`, `tenantId`, `correlationId`, origem e payload de negócio mínimo. Consumidores precisam ser idempotentes e não devem assumir entrega única ou ordenação global.

O evento não é substituto de uma consulta à API quando o consumidor precisa do estado atual completo.

---

# 14. Documentação e Governança

Cada API deve fornecer OpenAPI atualizado, exemplos de request/response, erros esperados, scopes/permissões, limites e política de depreciação.

- Alterações aditivas e compatíveis podem ocorrer em `/v1`.
- Alterações incompatíveis exigem `/v2`, plano de migração e período de convivência publicado.
- Endpoints depreciados devem informar `Deprecation` e/ou `Sunset` quando aplicável, além de aviso na documentação.
- Contratos públicos devem ter testes de contrato no pipeline de CI.
- Toda API nova ou alteração incompatível relevante deve possuir ADR quando impactar a arquitetura.

---

# 15. Endpoints Iniciais do MVP

| Domínio | Endpoints principais |
|---|---|
| Autenticação | `POST /api/v1/auth/token`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout` |
| Tenant/empresa | `GET/POST/PATCH /companies`, `GET /companies/{id}` |
| Workspace | `GET/POST/PATCH /workspaces` |
| Conectores | `GET/POST/PATCH /connectors`, `POST /connectors/{id}/test-connection` |
| Sincronização | `POST /connectors/{id}/sync-jobs`, `GET /sync-jobs`, `GET /sync-jobs/{id}`, `POST /sync-jobs/{id}/retry` |
| Dashboards | `GET/POST/PATCH /dashboards`, `GET /dashboards/{id}` |
| Saúde | `GET /health/live`, `GET /health/ready` |

Os endpoints de Analytics e dados canônicos serão expostos após a camada semântica definir métricas, filtros e políticas de acesso. Não é permitido criar uma API de consulta genérica que execute SQL enviado pelo cliente.

---

# 16. Checklist para Nova API

- recurso, caso de uso e consumidor definidos;
- contrato OpenAPI revisado;
- autenticação, permissões e contexto de tenant/workspace definidos;
- validações, paginação, filtros e ordenação documentados;
- erros Problem Details e códigos HTTP corretos;
- idempotência e/ou job assíncrono quando necessário;
- logs, métricas, tracing e auditoria definidos;
- testes unitários, integração e contrato implementados;
- compatibilidade e plano de evolução avaliados.
