# Resumo da Conversa de Arquitetura

Este documento resume as decisões tomadas sobre a plataforma SaaS de Inteligência Corporativa.

## Visão
- Plataforma SaaS
- API First
- Cloud Native
- Multi-Tenant
- Multiempresa
- Multi-ERP
- IA First

## Stack
### Frontend
- Angular
- TypeScript
- Tailwind CSS
- Angular Material
- Apache ECharts

### Backend
- ASP.NET Core
- C#
- Entity Framework Core
- Dapper

### Infraestrutura
- SQL Server
- Redis
- RabbitMQ
- Docker
- Kubernetes
- NGINX

## Arquitetura
Angular -> API Gateway -> Microsserviços -> SQL Server/Data Warehouse -> IA.

## Microsserviços
- Identity
- Connector
- Analytics
- Dashboard
- AI
- Automation
- Notification
- Scheduler
- Billing
- Audit

## Conceitos
- Modelo Canônico de Dados
- Connector Framework
- Data Warehouse
- Dashboard Builder
- IA Conversacional
- Agentes de IA
- Marketplace de conectores

## Multi-Tenant
Tenant -> Empresas -> Filiais -> Usuários -> Permissões.

## Roadmap
1. Fundação
2. Analytics
3. IA
4. Marketplace

## Branding
Evitar nomes genéricos; buscar marca própria e manter EIP como nome da arquitetura.
