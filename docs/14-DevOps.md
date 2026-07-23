# 14 - DevOps

**Projeto:** Enterprise Intelligence Platform (EIP)  
**Versão:** 1.0  
**Status:** Oficial  
**Última atualização:** Julho/2026

---

# 1. Objetivo

Este documento define como a EIP é construída, testada, empacotada, implantada, observada e recuperada. O objetivo é entregar mudanças pequenas e confiáveis, com ambientes reproduzíveis, segurança no ciclo de entrega e capacidade de operação mensurável.

---

# 2. Princípios

- infraestrutura e configuração versionadas;
- build reproduzível e imagens imutáveis;
- automação de qualidade e segurança no pipeline;
- promoção controlada entre ambientes;
- observabilidade desde o primeiro incremento;
- rollback e recuperação planejados antes de produção;
- acesso operacional mínimo, auditado e separado por ambiente;
- não introduzir Kubernetes ou serviços distribuídos antes da necessidade operacional.

---

# 3. Ambientes

| Ambiente | Finalidade | Dados |
|---|---|---|
| Local | desenvolvimento individual e testes rápidos | sintéticos/anonimizados |
| Development | integração contínua de equipes | sintéticos ou amostra aprovada |
| Staging | homologação e validação de release | equivalentes funcionais; nunca cópia irrestrita de produção |
| Production | operação de clientes | dados reais protegidos |

Ambientes possuem contas, redes, segredos, bancos, filas e Object Storage independentes. Produção não compartilha credenciais nem dados com os demais.

---

# 4. Desenvolvimento Local

Docker Compose é o padrão para subir dependências locais: SQL Server, Redis, RabbitMQ, Object Storage compatível com S3 e serviços da aplicação.

Requisitos:

- configuração de exemplo sem segredo real;
- volumes locais descartáveis e documentados;
- health checks para dependências;
- dados seed sintéticos;
- comando/documentação única para iniciar e validar o ambiente;
- versões de dependências fixadas e atualizáveis de forma controlada.

---

# 5. Estratégia de Código e Branches

- `main` representa a linha pronta para entrega e deve permanecer íntegra;
- mudanças ocorrem em branches curtas com prefixo `codex/` quando criadas por Codex;
- pull request/revisão é obrigatório para mudanças em produção, segurança, infraestrutura e contratos;
- commits são pequenos, descritivos e vinculados à decisão/tarefa quando existir;
- versionar código, schemas, OpenAPI, IaC e configurações seguras; nunca versionar segredos;
- feature flags protegem capacidades incompletas ou liberações graduais quando necessário.

O fluxo exato de proteção de branch e revisão será configurado no provedor de repositório antes da primeira entrega produtiva.

---

# 6. Integração Contínua

Todo pull request executa pipeline automatizado. Etapas mínimas:

1. restaurar dependências com versões bloqueadas;
2. verificar formatação, lint e análise estática;
3. compilar backend e frontend;
4. executar testes unitários;
5. executar testes de integração selecionados com dependências efêmeras;
6. validar contratos OpenAPI/eventos e migrations;
7. analisar dependências, segredos e imagens;
8. publicar relatórios de cobertura, falhas e artefatos quando aplicável.

O merge é bloqueado em falhas críticas de build, testes, segurança, contrato ou formatação definida pelo projeto.

---

# 7. Entrega Contínua e Releases

## 7.1 Artefatos

Cada release produz artefatos versionados e rastreáveis:

- imagens Docker imutáveis com tag de versão e referência de commit;
- pacote de frontend estático;
- migrations de banco versionadas;
- contratos OpenAPI/eventos publicados;
- manifests/configurações de deploy;
- SBOM e resultado de scan quando adotados.

## 7.2 Promoção

O mesmo artefato validado é promovido entre ambientes; não recompilar para cada ambiente. Configurações e segredos são injetados no deploy e não embutidos na imagem.

Promoção para produção exige aprovação configurada, checks de saúde, plano de rollback e observabilidade disponível. Mudanças de alto risco podem usar feature flag, canary ou rollout gradual conforme maturidade da operação.

## 7.3 Banco de dados

Migrations são compatíveis com rollout progressivo: expandir schema, publicar aplicação compatível, migrar dados quando necessário e somente depois remover legado. Migrations destrutivas exigem backup validado, janela aprovada e plano de reversão.

---

# 8. Containers e Infraestrutura

## 8.1 Docker

Imagens são pequenas, versionadas, executadas como usuário não-root e sem ferramentas desnecessárias. Multi-stage builds são preferíveis. Health check, variáveis obrigatórias e portas expostas são documentados.

Não usar tags mutáveis como única referência de produção. Imagens passam por scan de vulnerabilidades antes de publicação.

## 8.2 Infraestrutura como código

Terraform e/ou templates equivalentes definem recursos de nuvem, rede, banco, storage, observabilidade e permissões. Helm é usado quando houver Kubernetes. Toda alteração de infraestrutura é revisada, planejada e registrada antes de aplicar.

## 8.3 Kubernetes

Kubernetes é condicional e entra quando houver múltiplas réplicas, escala independente, alta disponibilidade ou operação que justifique sua complexidade. Até esse ponto, deploys em containers gerenciados ou Docker Compose controlado podem atender o MVP.

Quando adotado, usar namespaces por ambiente, recursos/limites, probes, network policies, secrets externos, autoscaling e rollout controlado.

---

# 9. Configuração e Segredos

Configuração segue hierarquia explícita: valores seguros versionados, configuração por ambiente e segredos resolvidos por secret manager.

- nenhum segredo em Git, imagem, log ou variável exibida em diagnóstico;
- acesso a segredos por identidade de workload e menor privilégio;
- rotação e expiração monitoradas;
- configuração validada na inicialização;
- mudanças de configuração auditadas e promovidas como parte do release quando impactarem comportamento.

---

# 10. Observabilidade

Todos os componentes publicam logs estruturados via Serilog e traces/métricas via OpenTelemetry. Prometheus coleta métricas e Grafana exibe painéis e alertas.

## 10.1 Sinais mínimos

| Sinal | Exemplos |
|---|---|
| Logs | erro de API, falha de conector, mudança administrativa, exceção de worker |
| Métricas | taxa de requisição/erro, latência, cache hit rate, filas, uso de banco, jobs |
| Traces | API → job → worker → pipeline → DW, sempre com `CorrelationId` |
| Health | liveness, readiness e dependências críticas |
| Auditoria | permissões, exportações, conectores, automações e acessos privilegiados |

Dashboards operacionais iniciais: disponibilidade, APIs, filas/workers, sincronizações, DW, segurança, custo/uso e SLOs.

## 10.2 Alertas

Alertas devem ser acionáveis, com severidade, responsável e runbook. Cobrir indisponibilidade, aumento de erros/latência, fila parada/DLQ, falha de backup, expiração de segredo/certificado, falha de sincronização, uso anormal e acesso suspeito.

---

# 11. Confiabilidade e SLOs

Cada serviço crítico terá SLI/SLO progressivos, definidos após observação do uso real. Exemplos:

- disponibilidade da API;
- latência p95 de consultas analíticas;
- atraso máximo de sincronização por conector;
- sucesso de execução de jobs;
- RPO/RTO de dados e configurações.

Error budgets orientam o equilíbrio entre velocidade de novas funcionalidades e trabalho de confiabilidade. Objetivos não substituem a comunicação de incidentes nem a análise de causa raiz.

---

# 12. Backup, Recuperação e Rollback

- backups de banco e Object Storage são automatizados, criptografados e testados por restauração;
- configurações, secrets por referência, contratos e IaC permitem reconstruir ambiente;
- cada release possui procedimento de rollback compatível com migrations;
- runbooks definem recuperação de API, fila, conector, DW e dados;
- RPO/RTO são definidos por ambiente, serviço e contrato;
- incidentes geram análise pós-incidente e ações rastreáveis.

Backup sem teste de restauração não é considerado pronto para produção.

---

# 13. Segurança no Pipeline

O pipeline executa análise estática, scan de dependências, detecção de segredos, scan de imagem e validação de IaC. Vulnerabilidades críticas e segredos bloqueiam a entrega até mitigação ou exceção aprovada/documentada.

Credenciais de CI têm escopo mínimo, expiração e acesso separado por ambiente. Ações de terceiros são fixadas em versões/revisões confiáveis. Produção requer identidade e aprovação adequadas.

---

# 14. Runbooks Operacionais

Antes de produção, manter runbooks curtos para:

- rollback de release e migration;
- indisponibilidade de API ou dependência;
- fila acumulada/DLQ;
- falha de conector e reprocessamento;
- indisponibilidade/recuperação do DW;
- rotação de segredo/certificado;
- restauração de backup;
- incidente de segurança e comunicação;
- degradação/desativação de recursos de IA.

Runbooks indicam sintomas, impactos, diagnóstico seguro, passos de contenção, escalonamento e validação pós-recuperação.

---

# 15. Critérios de Prontidão para Produção

- CI verde, testes e contratos aprovados;
- imagem/versionamento rastreáveis e scans sem risco não aceito;
- configuração/segredos corretos por ambiente;
- migrations, rollback e backup testados;
- health checks, logs, métricas, traces e alertas ativos;
- SLO/limites iniciais e responsáveis definidos;
- isolamento de tenant, permissões e auditoria validados;
- runbooks e processo de incidente disponíveis;
- aprovações de segurança/negócio aplicáveis concluídas.

---

# 16. Fora do Escopo Inicial

Não são requisitos do MVP:

- multi-região ativa-ativa;
- deploy contínuo automático em produção sem gates;
- Kubernetes antes de necessidade operacional;
- plataforma interna completa de desenvolvedor;
- observabilidade de custo sofisticada para todos os recursos;
- chaos engineering abrangente.

Essas capacidades serão incorporadas de modo incremental, orientadas por risco, escala e SLOs.
