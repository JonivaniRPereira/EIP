# ADR-001 — Escolha do ASP.NET Core para o Backend

* **Status:** Accepted
* **Data:** 2026-07-22

## Contexto

A plataforma EIP precisa suportar:

* APIs REST de alta performance.
* Processamento concorrente.
* Integração intensiva com SQL Server.
* Escalabilidade horizontal.
* Execução em containers Linux.
* Evolução para microsserviços.
* Integração com serviços de IA via APIs.

As alternativas consideradas foram:

* ASP.NET Core (C#)
* NestJS (Node.js/TypeScript)
* Laravel (PHP)
* FastAPI (Python)

## Decisão

Adotar **ASP.NET Core** como tecnologia principal para os serviços de backend.

Complementarmente:

* **Entity Framework Core** para operações transacionais e mapeamento ORM.
* **Dapper** para consultas analíticas de alta performance.

## Justificativa

* Excelente desempenho em APIs HTTP.
* Integração nativa e otimizada com SQL Server.
* Ecossistema maduro para aplicações corporativas.
* Suporte robusto a containers e Kubernetes.
* Ferramentas avançadas de observabilidade e diagnóstico.
* Forte suporte a arquiteturas modulares e Clean Architecture.

## Consequências

### Positivas

* Maior capacidade de throughput por instância.
* Melhor alinhamento com ambientes corporativos Microsoft.
* Facilidade para escalar serviços individualmente.

### Negativas

* Curva de aprendizado adicional para partes da equipe sem experiência em C#.
* Maior complexidade inicial em comparação com frameworks mais opinativos.

## Alternativas Consideradas

### NestJS

* **Prós:** alta produtividade, TypeScript unificado com o frontend.
* **Contras:** menor desempenho bruto em cenários de alta carga.

### Laravel

* **Prós:** produtividade excepcional e ecossistema maduro.
* **Contras:** menor alinhamento com SQL Server e com a estratégia de plataforma analítica de grande escala.

### FastAPI

* **Prós:** excelente para workloads de IA e ciência de dados.
* **Contras:** não ideal como plataforma principal de microsserviços corporativos.
