# Build a partir da raiz do repositório: docker build -f docker/worker/Dockerfile .
# (docs/roadmap/fase-0-backlog.md, épico E7 — imagem versionada do worker assíncrono EIP.Worker.Sync)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Worker/EIP.Worker.Sync.csproj
RUN dotnet publish src/Worker/EIP.Worker.Sync.csproj -c Release -o /app/publish --no-restore

# Runtime simples (não aspnet): o worker é um Generic Host sem Kestrel/HTTP, não precisa do runtime
# ASP.NET Core.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# A imagem oficial já traz um usuário não-root pronto ("app") — não precisa criar um.
USER app

COPY --from=build --chown=app:app /app/publish .

ENTRYPOINT ["dotnet", "EIP.Worker.Sync.dll"]
