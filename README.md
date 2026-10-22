## Hvad er et færdigt produkt?

Spørgsmål/krav taget udfra: https://drive.google.com/file/d/18PVhcuDnv0K4dXrhbqM-yhhQRgGZaDMz/view?usp=sharing

<table>
<tr>
<th>Krav</th>
<th>Hvordan kan vi se, om dette er opfyldt?</th>
<th>Hvor i koden?</th>
</tr>

<!-- ============================================================ -->
<tr>
<td>Identificér og afgræns services (min. 3 microservices, fx katalog, ordre, betaling)</td>
<td>

Min 3 grænser, dog holder jeg mig til 1-2 microservices grundet teamets størrelse

Henholdvis Catalog, Ordering, Inventory - her anvendt DDD i form af bounded contexts, som afgrænser hver service til et forretningsområde. De vurderes efterfølgende med cohesion og coupling.

</td>
<td>

For at tjekke kig i `services/CatalogMicroservice` samt `services/InventoryMicroservice`

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Strangler Pattern implementering — funktionalitet flyttes gradvist uden driftsafbrydelse</td>
<td>

</td>
<td>

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Lagerstyringssystem implementeret som selvstændig microservice</td>
<td>

</td>
<td>

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>API Gateway — samlet indgangspunkt med autentificering, load balancing og rate-limiting</td>
<td>

</td>
<td>

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Asynkron beskedbaseret kommunikation mellem microservices (fx RabbitMQ, Azure Service Bus, Kafka)</td>
<td>

</td>
<td>

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Fejlhåndtering og overvågning (fx Circuit Breaker, logging, ELK eller Prometheus/Grafana)</td>
<td>

</td>
<td>

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>CI/CD pipeline — automatisk build, test og deployment, containerisering (fx Docker)</td>
<td>
Hver service har sin egen service.yml, som kun bliver triggeret af ændringer i sin egen mappe, så en service kan bygges, testes og deployes uafhængigt af de andre. 
</td>
<td>
Selve pipelinen ligger ét sted .github/workflows/_dotnet-service.yml.
I samme  mappe ligger alle service.yml filerne, for mere information se skemaet i afsnit "CI/CD microservices".
</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Dokumentation af arkitekturbeslutninger, API'er og microservices (fx i README)</td>
<td>

</td>
<td>

</td>
</tr>
</table>

## CI/CD microservices

Idé: **ét workflow pr. deploybar enhed**. Hvert workflow trigges kun af ændringer i sin egen mappe, så en service kan bygges, testes og deployes uafhængigt af de andre. Selve pipelinen ligger ét sted (`_dotnet-service.yml`) og genbruges af alle services.

| Workflow | Deploybar enhed | Trigger | Funktion |
|---|---|---|---|
| `_dotnet-service.yml` | – (genbrugelig skabelon) | `workflow_call` | Fælles pipeline: build → unit tests → integrationstests → Docker-image → staging → production (manuel godkendelse). Discord-besked ved fejl. .NET-version læses fra servicens `global.json`. |
| `reverseproxy.yml` | `services/ReverseProxy` | push til `main` / PR på `services/ReverseProxy/**` | Kalder `_dotnet-service.yml` med `service: ReverseProxy`. |
| `monolit.yml` | `src/` (monolitten) | push / PR på `src/**`, `tests/**` | Midlertidig: bygger og tester monolitten, indtil den er udskilt i services. |

Ny service: opret `services/<Service>/` med samme struktur og et lille `<service>.yml`, der kalder `_dotnet-service.yml`.



# eShopOnWeb – Headless, det nye startsted hvor WebSpa ikke er SSR

Fork af [dotnet-architecture/eShopOnWeb](https://github.com/dotnet-architecture/eShopOnWeb). Den server-renderede `Web`-monolit er erstattet af headless klienter, der kun taler med `PublicApi`.
Formålet er at gøre løsningen klar til **Strangler Pattern**: alle klik i UI'et er nu API-kald, så en gateway kan route dem til nye services én ad gangen.
Der er **ingen microservices endnu**. Domænet, databasen og al logik er den samme som i originalen. 

## Arkitektur

```
FØR
  Browser ──► Web (SSR) ────────────────────────────────┐
               │ hoster                    in-process   ▼
               └─► BlazorAdmin ──► PublicApi ──► ApplicationCore + Infrastructure ──► DB

EFTER
  Browser ──► WebSpa ──────┐
                           ├──► PublicApi ──► ApplicationCore + Infrastructure ──► DB
  Browser ──► BlazorAdmin ─┘   HTTP + JWT
                                └────────────────── monolit ─────────────────┘
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

