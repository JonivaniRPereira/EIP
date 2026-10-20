# scripts

Scripts auxiliares de desenvolvimento e operação (`docs/00-Arquitetura-do-Repositorio.md`).

- `dev-up.sh` — sobe as dependências locais via Docker Compose (`deploy/docker-compose/`) e só retorna sucesso quando todos os serviços estão `healthy`. Copia `.env.example` para `.env` automaticamente na primeira execução.
- `backup/Test-Recovery.ps1` — ensaio de backup e recuperação SQL em duas instâncias descartáveis,
  com hashes de dados, DBCC e provas RLS. Ver `docs/guides/backup-restauracao.md`.
- `backup/Test-JointRecovery.ps1` — compõe o ensaio SQL com cópia/restauração dos objetos
  raw referenciados pela Demo em MinIO isolado, incluindo detecção de corrupção.
  Preserva serviços existentes; backup/objetos ficam fora do Git. Ver `docs/guides/recuperacao-conjunta-demo.md`.
  Com `-VerifyApplication -HostDll <publicação>`, verifica login/analytics/painel/linhagem
  em um Host Development temporário, com SQL/raw restaurados e cache/broker novos.
  Requer a senha existente da Demo via ambiente. Ver `docs/guides/retomada-aplicacao.md`.
  Acrescentar `-WorkerDll <publicação>` retoma três syncs incrementais Demo em cópias,
  verificando raw/canônico/Warehouse, marcadores e filas. Ver `docs/guides/retomada-ingestao.md`.
  `-VerifyRetry` provoca uma falha transitória na origem isolada e exige recuperação
  automática (`docs/guides/retry-retomada.md`). Em execução separada, `-VerifyVolume`
  processa 200 clientes, 50 produtos e 2.000 notas sintéticas pelo fluxo completo
  (`docs/guides/volume-pipeline.md`). Ambas as opções exigem Worker publicado.
- `rollback/Test-Rollback.ps1` — aplica/reverte uma migration real e troca entre imagens de duas
  versões do Host, em ambiente descartável. Ver `docs/guides/rollback-release.md`.
- `performance/Measure-Slo.ps1` — baseline sequencial de login, analytics e probes, com deltas
  dos histogramas reais de `/metrics`. Ver `docs/guides/slos-iniciais.md`.
- `performance/Test-TenantIsolation.mjs` — carga concorrente no Gateway com quatro tenants,
  dados sintéticos distintos, acessos cruzados, quota e prova SQL RLS sem filtro.
  Ver `docs/guides/isolamento-sob-carga.md`; cria e mantém dados sintéticos locais.
- `performance/Get-OperationalSnapshot.ps1` — snapshot administrativo somente de leitura,
  com falhas/pendências e último sucesso por tenant, respeitando contexto RLS individual.
  Ver `docs/guides/conector-operacoes.md` §5; sem coleta agendada ou alerta automático.
- `performance/Test-LocalOperation.ps1` — diagnóstico manual do laboratório: front/API/Gateway,
  saúde dos containers, contadores de filas/consumidores e cargas por organização.
  Gera resumo e JSON em pasta nova; somente leitura, sem consumir mensagens.
  Ver `docs/guides/diagnostico-local.md`.
- `performance/Test-Ingestion.mjs` — ingestão real com organização sintética, leitura/checksum
  MinIO, canônico/Warehouse, analytics, reconciliação, linhagem e publicação de painel.
  Ver `docs/guides/recuperacao-minio-ingestao.md`; preserva dados sintéticos.
- `performance/Test-AnalyticsVolume.mjs` — 20 mil fatos sintéticos em duas organizações
  novas e 120 consultas via Gateway com quatro usuários. Confere valores por cliente e
  isolamento, registra latência no cliente e preserva as fixtures. Inserção direta no
  Warehouse; não mede ingestão/canônico. Ver `docs/guides/volume-analytics.md`.
- `database/`, `migration/`, `seed/` — reservados para scripts futuros.
