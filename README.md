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

For at tjekke kig i `services/CatalogService` (Inventory er ikke lavet endnu)

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Strangler Pattern implementering — funktionalitet flyttes gradvist uden driftsafbrydelse</td>
<td>
Jeg har gradvist udskillet services (CatalogService), hvor den først udskilles men stadig bruger monolittens database, og nu har sin egen database. Som gør at hvert commit, stadig har været fuldt funktionelt i produktion.

Trinene for Catalog: (1) GET-trafik routes via gatewayen til CatalogService, (2) write-endpoints flyttes, (3) CatalogService får sin egen PostgreSQL-database, (4) monolitten holder en read-only kopi af kataloget opdateret via RabbitMQ (se messaging), (5) catalog-endpoints, filter-specifications og AutoMapper fjernes fra monolitten. Monolitten har nu kun den kopi, som kurv og ordre bruger.
</td>
<td>
For at tjekke kig i `services/CatalogService/*`, `services/ReverseProxy/ReverseProxy.Api/appsettings.json` (routes) og `src/PublicApi/Messaging/CatalogSyncConsumer.cs` (kopien)

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
YARP er det eneste indgangspunkt for klienterne. Catalog-routes går til CatalogService, alt andet (catch-all) til monolitten. Gatewayen håndterer også CORS for alle services, så browserens preflights besvares, selvom den service, routen peger på, er nede.
</td>
<td>

For at tjekke kig i `services/ReverseProxy/ReverseProxy.Api/` (`Program.cs` og `appsettings*.json`)

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Asynkron beskedbaseret kommunikation mellem microservices (fx RabbitMQ, Azure Service Bus, Kafka)</td>
<td>
CatalogService publicerer `catalog.item.created/updated/deleted` på topic exchangen `catalog`, så monolitten kan holde en read-only kopi opdateret. Følgende EIP patterns anvendes:

- **Idempotent Receiver**: RabbitMQ kan levere samme besked flere gange, og events kan nå frem i forkert rækkefølge. Consumeren gemmer derfor hvert items `version` og ignorerer events med en version, der er lig med eller lavere end den gemte. Ét tjek håndterer altså både dubletter og forældede events.
- **Competing Consumers**: consumeren er bygget, så flere instanser kan læse fra samme kø. Det er netop derfor, Idempotent Receiver er nødvendig. CatalogService skaleres derimod ikke som consumer, fordi den modtager sine ændringer som HTTP-requests og derfor skaleres med replicas i Docker Swarm, hvor Swarm load balancer requests fra API Gatewayen. Versionen beskyttes med optimistic concurrency i databasen, så to instanser aldrig udsteder samme version.
- **Durable Subscriber**: monolittens kø `monolith.catalog` er durable og bundet til exchangen med `catalog.item.*`. Er monolitten nede, venter ændringerne i køen og behandles, når den starter igen (eventually consistent). Exchange og kø er durable, beskederne persistent, og RabbitMQ har en volume, så de også overlever en genstart af brokeren.
- **Event-Driven Consumer**: `CatalogSyncConsumer` kører som `BackgroundService` i monolitten, og RabbitMQ pusher beskederne til den. Den sender først ack, når ændringen er gemt.
- **Resequencer (fravalgt)**: den forudsætter en ubrudt sekvens, så et tabt event ville blokere alle efterfølgende. Da events er Document Messages med hele itemets tilstand, er det nok at droppe forældede events i stedet for at sortere dem.
</td>
<td>

For at tjekke kig i `docs/messaging.drawio`, publisheren i `services/CatalogService/CatalogService.Api/Messaging/` og consumeren i `src/PublicApi/Messaging/CatalogSyncConsumer.cs`

Se også afsnittet [EIP-patterns](#eip-patterns).

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
| `catalogservice.yml` | `services/CatalogService` | push til `main` / PR på `services/CatalogService/**` | Kalder `_dotnet-service.yml` med `service: CatalogService`. |
| `reverseproxy.yml` | `services/ReverseProxy` | push til `main` / PR på `services/ReverseProxy/**` | Kalder `_dotnet-service.yml` med `service: ReverseProxy`. |
| `monolit.yml` | `src/` (monolitten) | push / PR på `src/**`, `tests/**` | Midlertidig: bygger og tester monolitten, indtil den er udskilt i services. |

Ny service: opret `services/<Service>/` med samme struktur og et lille `<service>.yml`, der kalder `_dotnet-service.yml`.



## EIP-patterns

> **Skelet (udkast):** overskrifter og stikord er skrevet af AI (Claude) ud fra koden. Teksten skal skrives færdig og rettes til af mig.

Format pr. pattern: **Teori** (hvad mønstret er), **Hos os** (hvordan vi bruger det), **Kode** (hvor det ligger).

### Message Channels

**Send and forget + store and forward** *(grundidéen i asynkron messaging)*
- Send and forget: CatalogService publicerer og svarer admin med det samme; den venter ikke på, at monolitten har behandlet ændringen
- Store and forward: RabbitMQ gemmer beskeden og sender den videre, når modtageren er klar → derfor kan monolitten være nede (eventually consistent)
- Kode: `RabbitMq.cs` (`Publish` returnerer, når beskeden er afleveret til RabbitMQ), `CatalogSyncConsumer.cs`

**Publish-Subscribe Channel**
- Teori: én afsender, en kopi af beskeden til hver subscriber
- Hos os: den *logiske* kanal for catalog-events; implementeret som topic exchange `catalog` + én kø pr. subscriber
- Hos os: publisher kender ikke subscribers; nye services binder bare en ny kø og får deres egen kopi
- Hos os: routing keys `catalog.item.created/updated/deleted`; binding `catalog.item.*`
- Kode: `services/CatalogService/CatalogService.Api/Messaging/RabbitMq.cs` (`ExchangeDeclareAsync`)

**Point-to-Point Channel** *(inde i den enkelte subscriber)*
- Teori: hver besked behandles af præcis én modtager
- Hos os: ikke en kanal ved siden af Pub-Sub, men et lag under: exchangen giver hver *service* en kopi (Pub-Sub), køen `monolith.catalog` giver hver kopi til præcis én *replica* af monolitten (Point-to-Point → Competing Consumers)
- Hos os: køen navngives efter servicen, ikke instansen, ellers får hver replica sin egen kopi
- Kode: `src/PublicApi/Messaging/CatalogSyncConsumer.cs` (`QueueDeclareAsync`, `QueueBindAsync`)

**Guaranteed Delivery**
- Teori: store and forward med beskeden gemt på disk, så den ikke går tabt, hvis messaging-systemet går ned
- Hos os (RabbitMQ-begreber): **durable** exchange og kø (topologien overlever genstart) + **persistent** beskeder (`delivery_mode: 2`, beskeden overlever genstart) + volume på RabbitMQ
- Forskel fra Durable Subscriber: Guaranteed Delivery beskytter mod at *brokeren* går ned, Durable Subscriber mod at *modtageren* er offline
- Begrænsning: ingen publisher confirms; dual write (DB gemt, publish fejler) → Outbox som mulig løsning
- Kode: `RabbitMq.cs` (`durable: true`, `DeliveryModes.Persistent`), `docker-compose.yml` (`rabbitmq-data`)

**Dead Letter Channel** *(ikke lavet endnu)*
- Teori: beskeder, der ikke kan behandles, flyttes til en separat kanal i stedet for at gå tabt
- Hos os nu: én genlevering (`requeue: !Redelivered`), derefter droppes beskeden og logges
- Idé: quorum queue med `x-delivery-limit` + `x-dead-letter-routing-key` → `monolith.catalog.dead`
- Kode: `CatalogSyncConsumer.cs` (`BasicNackAsync`)

### Message Construction

**Event Message**
- Teori: beskeden fortæller, at noget er sket (ikke en kommando)
- Hos os: `created`, `updated`, `deleted` i datid; metadata `eventId` (sporing), `occurredAt`, `version`
- Kode: `services/CatalogService/CatalogService.Api/Messaging/CatalogEvents.cs`

**Document Message (push model)**
- Teori: beskeden bærer selve dataene
- Hos os: `created`/`updated` sender hele itemet → consumeren skal ikke kalde CatalogService tilbage; `deleted` sender kun `id` (ren notifikation)
- Hos os: `pictureUri` sendes som færdig URL, så monolitten ikke behøver catalog-logik
- Kode: `CatalogEvents.cs` (`ToData`)

**Format Indicator** *(ikke brugt)*
- Teori: felt der angiver beskedens format/schema-version
- Hos os: `version` er **itemets** version, ikke beskedformatets; forveksl dem ikke
- Hvornår: hvis beskedformatet ændres, og gamle og nye consumers skal kunne leve side om side

### Message Endpoints

**Message Endpoint**
- Teori: den kode, der forbinder en applikation med messaging-systemet
- Hos os: CatalogService (publisher) og monolitten (consumer)
- Kode: `services/CatalogService/CatalogService.Api/Messaging/`, `src/PublicApi/Messaging/`

**Messaging Gateway**
- Teori: skjuler messaging bag metoder i domænets sprog
- Hos os: `ICatalogEvents` med `CatalogItemCreated(item)` osv.; endpoints kender hverken RabbitMQ, routing keys eller JSON
- Gevinst: integrationstests erstatter gatewayen med Moq (`Mock.Of<ICatalogEvents>()`)
- Kode: `Interfaces/ICatalogEvents.cs`, `Messaging/CatalogEvents.cs`, `tests/CatalogService.IntegrationTests/CatalogServiceFactory.cs`

**Event-Driven Consumer**
- Teori: RabbitMQ pusher beskeder til consumeren (modsat Polling Consumer, der selv spørger)
- Hos os: `BackgroundService` i monolitten med `AsyncEventingBasicConsumer`; skaleres sammen med monolitten
- Kode: `CatalogSyncConsumer.cs` (`ReceivedAsync`)

**Durable Subscriber**
- Teori: subscriberen mister ikke beskeder, mens den er offline
- Hos os: durable kø → ændringer, mens monolitten er nede, behandles, når den starter (eventually consistent)
- Forudsætning: køen skal være oprettet (første opstart af monolitten)
- Kode: `CatalogSyncConsumer.cs` (`QueueDeclareAsync(durable: true)`)

**Idempotent Receiver**
- Teori: samme besked kan modtages flere gange uden at have effekt mere end én gang
- Hos os: `version <= gemt` → ignorér; ét tjek dækker både dubletter og forkert rækkefølge
- Hos os: tombstone (`IsDeleted`) i stedet for at slette, så et forsinket event ikke genopliver et item
- Kode: `CatalogSyncConsumer.cs` (`HandleMessage`), `src/ApplicationCore/Entities/CatalogItem.cs`

**Competing Consumers**
- Teori: flere consumers læser fra samme kø for at skalere
- Hos os: replicas af monolitten deler `monolith.catalog`; `prefetchCount: 1`
- Hos os: `Version` som concurrency token, så to replicas ikke overskriver hinanden
- Kode: `CatalogSyncConsumer.cs` (`BasicQosAsync`), `src/Infrastructure/Data/Config/CatalogItemConfiguration.cs`

**Transactional Client** *(delvist)*
- Teori: modtagelse/afsendelse af beskeden hænger sammen med ændringen i databasen
- Hos os (consumer): manuel ack først efter gem (`autoAck: false`) → crash før ack = beskeden leveres igen
- Hos os (publisher): publish sker efter gem, men ikke i samme transaktion → dual write; Transactional Outbox som løsning
- Kode: `CatalogSyncConsumer.cs` (`BasicAckAsync`), `CreateCatalogItemEndpoint.cs` m.fl.

# eShopOnWeb – Headless, det nye startsted hvor WebSpa ikke er SSR

> **Historisk:** afsnittet herunder beskriver udgangspunktet, før strangler-arbejdet gik i gang. Siden er Catalog udskilt i `services/CatalogService`, klienterne går gennem gatewayen (Docker: http://localhost:5300), og CORS ligger i gatewayen i stedet for PublicApi. Se `docs/ports.md` for den aktuelle opsætning.

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

