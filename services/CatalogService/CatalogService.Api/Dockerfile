# note: hold versionen i sync med services/CatalogService/global.json
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
# curl til health checket i docker-compose.yml og docker-stack.yml (aspnet-imaget har det ikke)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Build context er repo-roden (samme som CI-pipelinen)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["services/CatalogService/CatalogService.Api/CatalogService.Api.csproj", "CatalogService.Api/"]
RUN dotnet restore "CatalogService.Api/CatalogService.Api.csproj"
COPY services/CatalogService/CatalogService.Api/ CatalogService.Api/
RUN dotnet publish "CatalogService.Api/CatalogService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CatalogService.Api.dll"]
