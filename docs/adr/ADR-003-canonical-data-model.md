# ADR-003 — Adoção de Modelo Canônico de Dados

* **Status:** Accepted
* **Data:** 2026-07-22

## Contexto

A plataforma integrará múltiplos ERPs e sistemas, cada um com nomenclaturas, estruturas e regras próprias.

Sem um modelo unificado, os módulos de Analytics e IA precisariam conhecer as particularidades de cada ERP.

## Decisão

Criar um **Modelo Canônico de Dados (Canonical Data Model - CDM)** interno, para o qual todos os conectores deverão transformar os dados recebidos.

## Justificativa

* Desacopla Analytics e IA dos sistemas de origem.
* Simplifica a implementação de novos conectores.
* Garante consistência semântica entre diferentes ERPs.
* Permite reutilização de dashboards, KPIs e prompts de IA.

## Consequências

### Positivas

* Redução do acoplamento.
* Maior reutilização de componentes.
* Facilidade para expansão do ecossistema.

### Negativas

* Necessidade de manter mapeamentos e transformações por conector.
* Esforço inicial maior para definir o modelo canônico.
