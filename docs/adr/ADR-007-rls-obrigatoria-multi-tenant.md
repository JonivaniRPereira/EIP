# ADR-007 — RLS Obrigatória no Modelo Multi-Tenant Compartilhado

* **Status:** Accepted
* **Data:** 2026-08-01

## Contexto

O ADR-002 define o Tenant como unidade máxima de isolamento da plataforma. A documentação inicial de Segurança (`07-Seguranca.md`) e Multi-Tenant (`08-Multi-Tenant.md`) tratava Row-Level Security (RLS) como um controle complementar "quando viável" no modelo de banco compartilhado, dependendo primariamente dos filtros aplicados pela aplicação.

Isolamento de tenant apoiado somente em filtro de aplicação é um dos vetores mais comuns de vazamento cross-tenant em plataformas SaaS B2B: basta uma query, um repositório novo ou uma migration esquecerem o filtro de `TenantId` para que dados de um cliente fiquem visíveis a outro. Como a EIP ainda está na Fase 0 (nenhum código-fonte implementado), este é o momento de eliminar essa dependência de disciplina individual antes que exista qualquer tabela de produção.

## Decisão

RLS passa a ser **obrigatória, sem exceção**, em toda tabela que contenha `TenantId` no modelo de banco compartilhado, a partir da primeira migration do primeiro incremento (Fase 0). Isso se aplica a:

* dados de plataforma (tenants, usuários, permissões);
* dados canônicos e analíticos (Warehouse, Semantic Layer);
* qualquer nova tabela criada por qualquer domínio, sem exceção temporária.

O CI bloqueia merge/deploy se qualquer tabela com `TenantId` não possuir política RLS ativa e testada. Filtros de aplicação continuam obrigatórios e não são substituídos por RLS — as duas camadas são cumulativas, não alternativas.

Este ADR substitui, para efeito de obrigatoriedade, a redação anterior de "RLS quando viável"/"RLS quando adotado" presente em `07-Seguranca.md`, `08-Multi-Tenant.md` e `09-Data-Warehouse.md`, que foram atualizados para refletir esta decisão.

## Justificativa

* Elimina a janela em que uma tabela nova opera sem RLS até alguém "lembrar" de adicionar.
* Move a garantia de isolamento de uma disciplina de code review para um controle de banco de dados verificável automaticamente.
* Custo de implementar RLS desde o início é baixo comparado ao custo de retrofitá-la em tabelas já populadas em produção.
* Alinhado ao princípio de "Security by Design" já declarado em `02-Arquitetura.md` e "Isolamento obrigatório" em `07-Seguranca.md`.

## Consequências

### Positivas

* Isolamento de tenant deixa de depender apenas de filtros manuais na aplicação.
* Reduz o risco residual de vazamento cross-tenant mesmo diante de bugs de aplicação.
* Torna o requisito auditável e testável em CI desde a Fase 0.

### Negativas

* Aumenta o esforço inicial de setup de cada tabela (política RLS + `TenantId` propagado ao contexto de sessão/conexão).
* Exige que toda migration inclua a criação da política RLS correspondente, sob revisão obrigatória.
* Cada nova tabela com `TenantId` sem política RLS correspondente passa a ser um defeito bloqueante, não um débito técnico aceitável.

## Escopo

Aplica-se apenas ao modelo de banco compartilhado (`DataIsolationMode = Shared`). Tenants em modo `Dedicated` têm isolamento físico e não dependem de RLS para a mesma garantia, embora seu uso continue recomendado como defesa em profundidade.
