# eShopOnWeb – Headless

Fork af [dotnet-architecture/eShopOnWeb](https://github.com/dotnet-architecture/eShopOnWeb). Den server-renderede `Web`-monolit er erstattet af headless klienter, der kun taler med `PublicApi`.
Formålet er at gøre løsningen klar til **Strangler Pattern**: alle klik i UI'et er nu API-kald, så en gateway kan route dem til nye services én ad gangen.
Der er **ingen microservices endnu**. Domænet, databasen og al logik er den samme som i originalen.

## Arkitektur

```
FØR                                         EFTER
Browser ──► Web (Razor Pages, SSR)          Browser ──► WebSpa (Blazor WASM, shop) ──┐
            ├─ UI + login (cookie)          Browser ──► BlazorAdmin (Blazor WASM) ───┤ HTTP + JWT
            ├─ ApplicationCore              │                                         ▼
            └─ hoster BlazorAdmin ──► PublicApi                       PublicApi ── ApplicationCore ── Infrastructure ── DB
```

## Ændringer

1. **`src/Web` er fjernet**: Razor Pages, MVC-views, Identity UI, cookie-login, Manage-sider og health checks.
2. **Webs logik er flyttet til `PublicApi`** som nye endpoints. ApplicationCore er uændret.
   - `GET/POST/PUT api/basket…`: kurv. Erstatter Basket-siderne og `BasketViewModelService`.
   - `POST api/basket/transfer`: flytter den anonyme kurv til brugeren ved login, ligesom Webs login gjorde.
   - `POST api/orders`: checkout. Erstatter Checkout-siden.
   - `GET api/orders`, `GET api/orders/{id}`: ordrehistorik. Erstatter `OrderController` og MediatR-handlerne.
   - `POST api/register`: oprettelse af bruger. Erstatter Identity Register-siden.
   - Anonym kurv: SPA'en sender et GUID i headeren `X-Anonymous-Id`, som erstatter kurv-cookien.
3. **Ny `src/WebSpa`** (Blazor WebAssembly, shop): bygget på samme måde som BlazorAdmin (samme projekttype, `BlazorShared`-modeller, lookup-services og JWT mod PublicApi). Webs CSS og billeder er flyttet hertil, så shoppen ser ud som før.
4. **`BlazorAdmin` er nu standalone**: UI'et og admin-logikken er uændret. Den var tidligere hostet af Web og fik login via Webs cookie og `/User`-endpoint. Nu har den sin egen `index.html` og login-side, og den henter JWT fra `api/authenticate`.
5. **Konfiguration**: `baseUrls.adminBase` er tilføjet, CORS i PublicApi tillader begge klienter, og `CatalogBaseUrl` peger på WebSpa, så produktbilleder virker i begge klienter.
6. **Tests**:
   - Tests, der testede Web, er fjernet (`FunctionalTests/Web`, `UnitTests/Web`, `UnitTests/MediatorHandlers`).
   - `CatalogItemListPagedEndpoint`-testen bruger nu API'ets egen response i stedet for Webs viewmodel.
   - Nye integrationstests for kurv, checkout/ordrer og registrering i `PublicApiIntegrationTests`.
7. **Docker**: WebSpa og BlazorAdmin har hver sin Dockerfile (publish → nginx). `docker-compose` kører `webspa`, `blazoradmin`, `publicapi` og `sqlserver`.
8. **Andet**: `global.json` tillader nyere SDK'er (`latestMajor`), og `.vscode` peger på PublicApi. `azure.yaml`/`infra` (azd-deploy af Web) er **ikke** opdateret.

## Kør lokalt

| App | URL |
|---|---|
| PublicApi (Swagger) | https://localhost:5099/swagger |
| WebSpa (shop) | https://localhost:5001 |
| BlazorAdmin | https://localhost:5003 |

```bash
dotnet run --project src/PublicApi      # sæt UseOnlyInMemoryDatabase=true for at undgå LocalDB
dotnet run --project src/WebSpa
dotnet run --project src/BlazorAdmin
```

Docker: `docker compose up --build` starter shop på http://localhost:5106, admin på http://localhost:5107 og API på http://localhost:5200.

Demo-brugere: `demouser@microsoft.com` / `admin@microsoft.com`, password `Pass@word1`.

Tests: `dotnet test eShopOnWeb.sln`. Kræver .NET 8 runtime, fordi API-testene fejler, hvis de rulles frem til .NET 9/10.

## Videre: Strangler Pattern

1. Sæt en API-gateway (f.eks. YARP) foran `PublicApi`, og lad `apiBase` i begge klienter pege på gatewayen.
2. Træk ét bounded context ud ad gangen, f.eks. Catalog som `Catalog.API`, og flyt dets rute (`/api/catalog-*`) i gatewayen. Klienterne skal ikke ændres.
3. Synkronisér data, som monolitten stadig bruger (Basket og Order læser katalogdata), via events, f.eks. RabbitMQ.
4. Gentag for Basket og derefter Ordering, indtil `PublicApi` er tom og kan fjernes.
