# Stage 1 — build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY KuraApi.slnx .
COPY src/Kura.Api/Kura.Api.csproj src/Kura.Api/
COPY src/Kura.Application/Kura.Application.csproj src/Kura.Application/
COPY src/Kura.Domain/Kura.Domain.csproj src/Kura.Domain/
COPY src/Kura.Infrastructure/Kura.Infrastructure.csproj src/Kura.Infrastructure/
RUN dotnet restore
COPY . .
RUN dotnet publish src/Kura.Api/Kura.Api.csproj -c Release -o /app/publish

# Stage 2 — runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kura.Api.dll"]
