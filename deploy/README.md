# deploy

Infraestrutura de implantação da plataforma (`docs/00-Arquitetura-do-Repositorio.md`).

- `docker-compose/` — stack local de dependências (SQL Server, Redis, RabbitMQ, MinIO) usada em desenvolvimento. Ver `docs/roadmap/fase-0-backlog.md` (E1) e `scripts/dev-up.sh`.
- `helm/`, `kubernetes/`, `terraform/` — reservados para quando houver necessidade operacional real de orquestração/IaC (`docs/14-DevOps.md §8`); não fazem parte da Fase 0.
