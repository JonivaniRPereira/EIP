# Gateway

Ponto único de entrada externo da plataforma (`docs/02-Arquitetura.md`). Roteia `/api/**` para o
Host (`src/Host`) via YARP, aplica rate limiting básico por IP e garante `CorrelationId`
(aceito ou gerado aqui — docs/08-Multi-Tenant.md §5.1) propagado para o backend.

Health checks e métricas (`/health/*`, `/metrics`) não passam pelo Gateway — são acessados
diretamente pela infraestrutura (probes, scraper do Prometheus), não por clientes.

Autenticação/autorização de negócio continuam responsabilidade dos módulos no Host; o Gateway
apenas encaminha o header `Authorization` como recebido (comportamento padrão do YARP).

Para rodar localmente junto do Host, ver `docs/roadmap/fase-0-backlog.md` (épico E4).
