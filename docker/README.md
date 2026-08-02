# docker

Dockerfiles de build das imagens da plataforma, organizados por serviço (ver
`docs/00-Arquitetura-do-Repositorio.md`):

- `platform/Dockerfile` — imagem do `EIP.Host` (monólito modular).
- `gateway/Dockerfile` — imagem do `EIP.Gateway` (YARP).
- `worker/Dockerfile` — imagem do `EIP.Worker.Sync` (worker assíncrono de sincronização, E7); usa
  `mcr.microsoft.com/dotnet/runtime` (não `aspnet`) na imagem final, já que é um Generic Host sem
  Kestrel/HTTP.
- `data/`, `analytics/`, `ai/`, `redis/`, `rabbitmq/`, `sqlserver/` — reservados para quando houver
  necessidade real de imagem customizada desses componentes; a Fase 0 usa as imagens oficiais
  (SQL Server, Redis, RabbitMQ, MinIO) sem customização.

Build a partir da raiz do repositório (o contexto precisa enxergar `src/` inteiro):

```
docker build -f docker/platform/Dockerfile -t eip-host:local .
docker build -f docker/gateway/Dockerfile -t eip-gateway:local .
docker build -f docker/worker/Dockerfile -t eip-worker-sync:local .
```

Para subir o ambiente local de desenvolvimento (dependências via Docker Compose), veja
`deploy/docker-compose/`. O CI (`.github/workflows/ci.yml`, épico E5/E7) constrói as três imagens a
cada push/PR, mas ainda não publica em nenhum registry real.
