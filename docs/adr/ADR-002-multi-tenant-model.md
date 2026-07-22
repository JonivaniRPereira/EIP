# ADR-002 — Modelo Multi-Tenant por Organização

* **Status:** Accepted
* **Data:** 2026-07-22

## Contexto

A plataforma atenderá clientes com diferentes estruturas empresariais:

* empresas únicas;
* grupos econômicos;
* holdings com múltiplas empresas e filiais;
* empresas utilizando ERPs diferentes.

Era necessário definir a unidade de isolamento e cobrança do SaaS.

## Decisão

Adotar o modelo:

**Tenant (Organização) → Empresas → Filiais → Usuários → Permissões**

Cada cliente contratado corresponde a um **Tenant**.

## Justificativa

* Permite consolidar dados de várias empresas do mesmo grupo.
* Facilita o controle de permissões em múltiplos níveis.
* Alinha-se ao modelo de cobrança por organização e quantidade de empresas.
* Simplifica a experiência do usuário, que acessa a organização e escolhe o escopo da análise.

## Consequências

### Positivas

* Suporte nativo a grupos empresariais.
* Flexibilidade para análises consolidadas e comparativas.
* Escalabilidade organizacional.

### Negativas

* Maior complexidade no modelo de autorização.
* Necessidade de isolamento rigoroso entre tenants.

