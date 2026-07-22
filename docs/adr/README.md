# ADR - Architecture Decision Records

Este diretório contém os **Architecture Decision Records (ADRs)** da Enterprise Intelligence Platform (EIP).

Os ADRs registram decisões arquiteturais importantes, o contexto em que foram tomadas, as alternativas consideradas e as consequências esperadas.

## Convenções

* Cada ADR possui um identificador sequencial (`ADR-001`, `ADR-002`, ...).
* O identificador nunca é reutilizado.
* O status pode ser:

  * **Proposed** — proposta em avaliação.
  * **Accepted** — decisão aprovada.
  * **Superseded** — substituída por outro ADR.
  * **Deprecated** — não recomendada para novos desenvolvimentos.

## Processo

1. Criar um novo ADR com status **Proposed**.
2. Revisar tecnicamente.
3. Aprovar e alterar o status para **Accepted**.
4. Caso uma decisão seja substituída, criar um novo ADR e marcar o anterior como **Superseded**.

