# docker

Dockerfiles de build das imagens da plataforma, organizados por serviço (`gateway/`, `platform/`, `data/`, `analytics/`, `ai/`, `redis/`, `rabbitmq/`, `sqlserver/` — ver `docs/00-Arquitetura-do-Repositorio.md`).

Nenhum Dockerfile próprio existe ainda: a Fase 0 usa imagens oficiais (SQL Server, Redis, RabbitMQ, MinIO) sem customização. As subpastas serão criadas quando houver necessidade real de imagem customizada (ex.: imagem do `EIP.Host`).

Para subir o ambiente local de desenvolvimento (Docker Compose), veja `deploy/docker-compose/`.
