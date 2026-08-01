# Build a partir da raiz do repositório: docker build -f docker/gateway/Dockerfile .
# (docs/roadmap/fase-0-backlog.md, épico E5 — imagem versionada do EIP.Gateway/YARP)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Gateway/EIP.Gateway.csproj
RUN dotnet publish src/Gateway/EIP.Gateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# A imagem oficial já traz um usuário não-root pronto ("app") — não precisa criar um.
USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EIP.Gateway.dll"]
