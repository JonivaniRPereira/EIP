# ADR-006 — Redis como Camada de Cache Distribuído

* **Status:** Accepted
* **Data:** 2026-07-22

## Contexto

Dashboards, KPIs e consultas analíticas serão acessados por múltiplos usuários simultaneamente. Reexecutar consultas pesadas para cada requisição aumentaria significativamente a carga no Data Warehouse.

## Decisão

Adotar **Redis** como camada de cache distribuído para:

* KPIs;
* resultados de consultas analíticas;
* sessões e tokens;
* respostas de IA com reutilização controlada.

## Justificativa

* Baixa latência.
* Estruturas de dados ricas.
* Suporte a expiração e invalidação.
* Escalabilidade horizontal.

## Consequências

### Positivas

* Redução da carga no banco de dados.
* Melhoria do tempo de resposta.
* Compartilhamento de cache entre instâncias.

### Negativas

* Necessidade de estratégia de invalidação de cache.
* Consumo adicional de memória.

