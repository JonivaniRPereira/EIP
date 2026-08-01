# Build a partir da raiz do repositório: docker build -f docker/platform/Dockerfile .
# (docs/roadmap/fase-0-backlog.md, épico E5 — imagem versionada do monólito modular EIP.Host)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Host/EIP.Host.csproj
RUN dotnet publish src/Host/EIP.Host.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# A imagem oficial já traz um usuário não-root pronto ("app") — não precisa criar um.
USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Sem HEALTHCHECK aqui: a imagem base não inclui curl/wget, e um healthcheck que não bate de
# verdade em /health/live só daria falsa confiança. Health real é checado pelo orquestrador
# (probes de liveness/readiness apontando para /health/live e /health/ready, ver E3.3).

ENTRYPOINT ["dotnet", "EIP.Host.dll"]
