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

## Docker (`docker compose up`)

Kilde: `docker-compose.yml` og `docker-compose.override.yml`.

| Service | Compose-navn | Værtsport → containerport | URL fra din maskine |
|---|---|---|---|
| ReverseProxy (gateway) | `reverseproxy` | `5300 → 8080` | `http://localhost:5300` |
| PublicApi | `eshoppublicapi` | `5200 → 8080` | `http://localhost:5200` |
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
        │  catch-all route → legacy-cluster
        ▼
PublicApi             lokalt :5099   ·  docker eshoppublicapi:8080
        │
        ▼
SQL Server            docker sqlserver:1433
```

## Hvor portene er konfigureret

| Hvad | Fil | Værdi |
|---|---|---|
| Klienternes API-adresse, lokalt | `src/WebSpa/wwwroot/appsettings(.Development).json`, `src/BlazorAdmin/wwwroot/appsettings(.Development).json` | `apiBase` → `http://localhost:5050/api/` |
| Klienternes API-adresse, Docker | `src/WebSpa/wwwroot/appsettings.Docker.json`, `src/BlazorAdmin/wwwroot/appsettings.Docker.json` | `apiBase` → `http://localhost:5300/api/` |
| Gateway → PublicApi, lokalt | `services/ReverseProxy/appsettings.Development.json` | `https://localhost:5099/` |
| Gateway → PublicApi, Docker | `services/ReverseProxy/appsettings.Docker.json` | `http://eshoppublicapi:8080/` |
| PublicApi CORS (tilladte klienter) | `src/PublicApi/appsettings*.json` | `webBase`, `adminBase` |

## Health check

| Miljø | URL |
|---|---|
| Lokalt | `http://localhost:5050/health` |
| Docker | `http://localhost:5300/health` |
