#!/usr/bin/env bash
# Sobe as dependências locais da EIP (SQL Server, Redis, RabbitMQ, MinIO) via Docker Compose
# e só retorna sucesso quando todos os serviços reportam "healthy" (docs/14-DevOps.md §4).
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_DIR="${ROOT_DIR}/deploy/docker-compose"
COMPOSE_FILE="${COMPOSE_DIR}/docker-compose.yml"
ENV_FILE="${COMPOSE_DIR}/.env"
ENV_EXAMPLE="${COMPOSE_DIR}/.env.example"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker não encontrado no PATH. Instale o Docker Desktop antes de continuar." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "O daemon do Docker não está respondendo. Abra o Docker Desktop e tente novamente." >&2
  exit 1
fi

if [ ! -f "${ENV_FILE}" ]; then
  echo "Nenhum .env encontrado em ${COMPOSE_DIR}; copiando de .env.example (valores de desenvolvimento apenas)."
  cp "${ENV_EXAMPLE}" "${ENV_FILE}"
fi

echo "Subindo dependências locais..."
docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" up -d

SERVICES="eip-sqlserver eip-redis eip-rabbitmq eip-minio"
TIMEOUT_SECONDS=180
ELAPSED=0

echo "Aguardando healthchecks (timeout: ${TIMEOUT_SECONDS}s)..."
while true; do
  ALL_HEALTHY=true
  for name in ${SERVICES}; do
    status="$(docker inspect --format='{{if .State.Health}}{{.State.Health.Status}}{{else}}sem-healthcheck{{end}}' "${name}" 2>/dev/null || echo "nao-encontrado")"
    if [ "${status}" != "healthy" ]; then
      ALL_HEALTHY=false
    fi
    printf '  %-16s %s\n' "${name}" "${status}"
  done

  if [ "${ALL_HEALTHY}" = true ]; then
    echo "Todos os serviços estão healthy."
    exit 0
  fi

  if [ "${ELAPSED}" -ge "${TIMEOUT_SECONDS}" ]; then
    echo "Timeout aguardando os serviços ficarem healthy. Veja 'docker compose -f ${COMPOSE_FILE} logs'." >&2
    exit 1
  fi

  sleep 5
  ELAPSED=$((ELAPSED + 5))
  echo "--- checando novamente (${ELAPSED}s) ---"
done
