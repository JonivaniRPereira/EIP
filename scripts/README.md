# scripts

Scripts auxiliares de desenvolvimento e operação (`docs/00-Arquitetura-do-Repositorio.md`).

- `dev-up.sh` — sobe as dependências locais via Docker Compose (`deploy/docker-compose/`) e só retorna sucesso quando todos os serviços estão `healthy`. Copia `.env.example` para `.env` automaticamente na primeira execução.
- `backup/`, `database/`, `migration/`, `seed/` — reservados para scripts futuros (backup, migrations manuais, dados sintéticos); ainda vazios na Fase 0.
