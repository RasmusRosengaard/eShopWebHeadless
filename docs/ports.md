# Portoversigt

Alle porte i løsningen, både lokalt (`dotnet run`) og i Docker (`docker compose up`).

## Lokalt (`dotnet run`)

Kilde: `Properties/launchSettings.json` i hvert projekt.

| Service | HTTPS | HTTP | Bemærkning |
|---|---|---|---|
| ReverseProxy (gateway) | – | `http://localhost:5050` | Kun http-profil |
| PublicApi | `https://localhost:5099` | `http://localhost:5098` | Swagger: `/swagger` |
| WebSpa | `https://localhost:5001` | `http://localhost:5000` | |
| BlazorAdmin | `https://localhost:5003` | `http://localhost:5002` | |
| CatalogService | `https://localhost:7017` | `http://localhost:5048` | Standard er http-profilen |

## Docker (`docker compose up`)

Kilde: `docker-compose.yml` og `docker-compose.override.yml`.

| Service | Compose-navn | Værtsport → containerport | URL fra din maskine |
|---|---|---|---|
| ReverseProxy (gateway) | `reverseproxy` | `5300 → 8080` | `http://localhost:5300` |
| PublicApi | `eshoppublicapi` | `5200 → 8080` | `http://localhost:5200` |
| CatalogService | `catalogservice` | `5400 → 8080` | `http://localhost:5400` |
| WebSpa | `eshopwebspa` | `5106 → 80` | `http://localhost:5106` |
| BlazorAdmin | `eshopblazoradmin` | `5107 → 80` | `http://localhost:5107` |
| SQL Server | `sqlserver` | `1433 → 1433` | `localhost,1433` |

Inde i Docker-netværket kalder containerne hinanden ved servicenavn og containerport, fx `http://eshoppublicapi:8080`. `localhost` inde i en container er containeren selv.

## Trafikflow

```
Browser (WebSpa / BlazorAdmin)
        │  apiBase
        ▼
ReverseProxy (YARP)   lokalt :5050   ·  docker :5300
        │                                    │
        │  catch-all → legacy-cluster        │  catalog-routes → catalog-cluster (kommer i fase 2.2)
        ▼                                    ▼
PublicApi                                 CatalogService
lokalt :5099                              lokalt :5048
docker eshoppublicapi:8080                docker catalogservice:8080
        │                                    │
        └──────────────┬─────────────────────┘
                       ▼
               SQL Server (delt database)
               docker sqlserver:1433
```

## Hvor portene er konfigureret

| Hvad | Fil | Værdi |
|---|---|---|
| Klienternes API-adresse, lokalt | `src/WebSpa/wwwroot/appsettings(.Development).json`, `src/BlazorAdmin/wwwroot/appsettings(.Development).json` | `apiBase` → `http://localhost:5050/api/` |
| Klienternes API-adresse, Docker | `src/WebSpa/wwwroot/appsettings.Docker.json`, `src/BlazorAdmin/wwwroot/appsettings.Docker.json` | `apiBase` → `http://localhost:5300/api/` |
| Gateway → PublicApi, lokalt | `services/ReverseProxy/ReverseProxy.Api/appsettings.Development.json` | `https://localhost:5099/` |
| Gateway → PublicApi, Docker | `services/ReverseProxy/ReverseProxy.Api/appsettings.Docker.json` | `http://eshoppublicapi:8080/` |
| PublicApi CORS (tilladte klienter) | `src/PublicApi/appsettings*.json` | `webBase`, `adminBase` |
| CatalogService CORS (tilladte klienter) | `services/CatalogService/CatalogService.Api/appsettings(.Docker).json` | `webBase`, `adminBase` |
| CatalogService billed-URL'er | `services/CatalogService/CatalogService.Api/appsettings(.Docker).json` | `CatalogBaseUrl` → `https://localhost:5001` / `http://localhost:5106` |
| CatalogService → database | `services/CatalogService/CatalogService.Api/appsettings.Development.json` / `appsettings.Docker.json` | LocalDB / `sqlserver,1433` |

## Health check

| Service | Lokalt | Docker |
|---|---|---|
| ReverseProxy (gateway) | `http://localhost:5050/health` | `http://localhost:5300/health` |
| CatalogService | `http://localhost:5048/health` | `http://localhost:5400/health` |
