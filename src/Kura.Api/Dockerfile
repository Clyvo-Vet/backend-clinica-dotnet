# Stage 1 — build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copiar apenas o csproj da Api para restaurar dependências
COPY src/Kura.Api/Kura.Api.csproj src/Kura.Api/
COPY src/Kura.Application/Kura.Application.csproj src/Kura.Application/
COPY src/Kura.Domain/Kura.Domain.csproj src/Kura.Domain/
COPY src/Kura.Infrastructure/Kura.Infrastructure.csproj src/Kura.Infrastructure/

# Restaurar apenas o projeto principal — projetos de teste não existem no container
RUN dotnet restore src/Kura.Api/Kura.Api.csproj

# Copiar o restante do código fonte (excluindo tests via .dockerignore)
COPY src/ src/

# Publicar apontando diretamente para o csproj da Api
RUN dotnet publish src/Kura.Api/Kura.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2 — runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080

# Run as non-root for security and FIAP DevOps rubric 2.2
RUN groupadd -r kura && useradd -r -g kura kura
RUN chown -R kura:kura /app
USER kura

ENTRYPOINT ["dotnet", "Kura.Api.dll"]
