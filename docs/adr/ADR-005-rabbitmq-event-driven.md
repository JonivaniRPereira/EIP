# ADR-005 — Uso de RabbitMQ para Arquitetura Event-Driven

* **Status:** Accepted
* **Data:** 2026-07-22

## Contexto

A plataforma executará tarefas assíncronas e potencialmente demoradas, como:

* ETL;
* importação de arquivos;
* geração de relatórios;
* execução de IA;
* envio de notificações.

Executar essas tarefas durante requisições HTTP aumentaria a latência e reduziria a escalabilidade.

## Decisão

Adotar **RabbitMQ** como barramento de mensageria para processamento assíncrono e comunicação orientada a eventos.

## Justificativa

* Maturidade e estabilidade.
* Suporte a filas, exchanges e roteamento avançado.
* Facilidade de operação em ambientes Docker e Kubernetes.
* Excelente integração com .NET.

## Consequências

### Positivas

* Desacoplamento entre produtores e consumidores.
* Melhor resiliência e escalabilidade.
* Possibilidade de reprocessamento de mensagens.

### Negativas

* Complexidade operacional adicional.
* Necessidade de monitoramento e tratamento de mensagens mortas (DLQ).

