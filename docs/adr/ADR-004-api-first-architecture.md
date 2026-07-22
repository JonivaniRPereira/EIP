# ADR-004 — Arquitetura API First

* **Status:** Accepted
* **Data:** 2026-07-22

## Contexto

A plataforma será composta por múltiplos serviços e deverá oferecer APIs públicas para integrações e parceiros.

## Decisão

Adotar uma abordagem **API First**, na qual:

* todos os serviços expõem APIs versionadas;
* contratos OpenAPI são definidos antes da implementação;
* a comunicação entre serviços ocorre por contratos explícitos.

## Justificativa

* Facilita integração com terceiros.
* Permite desenvolvimento paralelo entre equipes.
* Reduz dependências implícitas entre serviços.
* Melhora a governança de APIs.

## Consequências

### Positivas

* Contratos claros e versionados.
* Melhor experiência para integradores.
* Maior desacoplamento.

### Negativas

* Necessidade de disciplina na gestão de versões.
* Overhead inicial na definição dos contratos.

