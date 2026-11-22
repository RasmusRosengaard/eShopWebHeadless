## Hvad er et færdigt produkt?

Krav fra: https://drive.google.com/file/d/18PVhcuDnv0K4dXrhbqM-yhhQRgGZaDMz/view?usp=sharing

Uddybes i rapporten (*Semesterprojekt i System Integration & Store Systemer*); afsnitsnavne nedenfor henviser til den.

| Underspørgsmål (problemformulering) | Afsnit i rapporten |
|---|---|
| Opdeling i services og vurdering af grænser | Servicegrænser |
| Uafhængig build, test og udrulning | Uafhængig build, test og udrulning |
| Udskillelse mens systemet er i drift | Udskillelse af funktionalitet med Strangler Fig |
| Skalering og trafik kun til raske replikaer | Skalering med Docker Swarm |
| Kommunikation uden afhængigheder, ingen tabte beskeder | Asynkron kommunikation mellem microservices |
| Test af services og asynkron kommunikation | Test af microservices og asynkron kommunikation |
| Opdagelse og håndtering af fejl | Monitorering og observability af microservices |
| Valg og begrænsninger | Diskussion |

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

- Bounded contexts (DDD): Catalog, Ordering, Inventory; ét forretningsområde pr. service
- Catalog: udskilt · Ordering: stadig i monolitten · Inventory: ikke lavet
- Kun 1–2 bygges pga. teamets størrelse → kravet om min. 3 er bevidst ikke fuldt opfyldt
- Grænser fastholdes med høj cohesion (al katalogfunktionalitet i CatalogService) og lav coupling (egen database, data deles kun via asynkrone beskeder)
- Kriterie: en grænse er god, hvis servicen kan ændres og udrulles uden ændringer i andre services
- Catalog vurderes som god grænse: fælles ændringer med monolitten skyldes kun den midlertidige read-only kopi, ikke delt katalogfunktionalitet. Shared database fravalgt (tæt kobling via skemaet)
- Rapport: *Servicegrænser*, *Diskussion*

</td>
<td>

`services/CatalogService` (Inventory ikke lavet endnu)

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Strangler Pattern implementering — funktionalitet flyttes gradvist uden driftsafbrydelse</td>
<td>

Catalog udskilt i trin; systemet virkede efter hvert trin:
1. GET-trafik → gateway → CatalogService
2. Write-endpoints flyttet (stadig på monolittens database)
3. Egen PostgreSQL-database
4. Monolitten holder en read-only kopi via RabbitMQ (se messaging)
5. Catalog-endpoints, filter-specs og AutoMapper fjernet fra monolitten → kun kopien til kurv og ordre er tilbage

</td>
<td>

`services/CatalogService/*`, `services/ReverseProxy/ReverseProxy.Api/appsettings.json` (routes), `src/PublicApi/Messaging/CatalogSyncConsumer.cs` (kopien)

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Lagerstyringssystem implementeret som selvstændig microservice</td>
<td>

- Ikke implementeret grundet tidsmangel (Inventory er identificeret som bounded context, men ikke bygget)

</td>
<td>

–

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>API Gateway — samlet indgangspunkt med autentificering, load balancing og rate-limiting</td>
<td>

- YARP = eneste indgang; catalog-routes → CatalogService, catch-all → monolitten
- CORS samlet i gatewayen → preflights besvares, selv hvis servicen bag er nede
- Autentificering: JWT valideres i servicerne, ikke i gatewayen
- Skalering med Docker Swarm (`docker-stack.yml`): `catalogservice` kører i `replicas: 2`, skaleres mens det kører med `docker service scale eshop_catalogservice=N`
  - Kun CatalogService skaleres → uafhængigt af monolitten; stateless (JWT), replicas deler én database
- Load balancing i YARP, ikke i Swarm: Swarms VIP fordeler pr. TCP-forbindelse, og YARP genbruger sin forbindelse → alt ville ramme én replica
  - Destination `http://tasks.catalogservice:8080/`: Swarm-DNS-navn, der returnerer IP'en på hver replica
  - `AddDnsDestinationResolver` (refresh hvert 10. sek.) → én YARP-destination pr. replica; nye/fjernede replicas opdages automatisk
  - `RoundRobin`: replicas på skift (A, B, A, B …); valgt for simplicitet frem for YARPs standard `PowerOfTwoChoices` (vælger 2 tilfældige, sender til den med færrest aktive requests)
  - Aktivt health check: YARP kalder `/health` hvert 10. sek. → replica, der ikke svarer, får ingen trafik
  - Swarm har sit eget health check på samme endpoint (`curl -f http://localhost:8080/health`, hvert 5. sek.) og genstarter containeren (`restart_policy`)
  - Begrænsning: `/health` tjekker kun HTTP, ikke databasen → deep health check ville være bedre (rapport: *Diskussion*)
- Migration og seed i separat service `catalog-migrator` (samme image, `--migrate`), som kører én gang og stopper → replicas migrerer/seeder ikke samtidig
- Rullende opdatering: `update_config` (én replica ad gangen, `start-first`)
- `docker-compose.yml` bruges til build og udvikling (1 instans af catalogservice)
- Mangler: rate-limiting
- Rapport: *Udskillelse af funktionalitet med Strangler Fig*, *Skalering med Docker Swarm*

</td>
<td>

`docker-stack.yml`, `services/ReverseProxy/ReverseProxy.Api/` (`Program.cs`, `appsettings*.json`), `services/CatalogService/CatalogService.Api/Program.cs` (`--migrate`)

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Asynkron beskedbaseret kommunikation mellem microservices (fx RabbitMQ, Azure Service Bus, Kafka)</td>
<td>

- CatalogService publicerer `catalog.item.created/updated/deleted` via RabbitMQ → monolitten opdaterer sin read-only kopi
- Asynkront: CatalogService venter ikke på monolitten; monolitten må være nede (eventually consistent)
- Ingen ved, at der bruges RabbitMQ, udover Messaging Gatewayen (`ICatalogEvents` → `IEventBus`)
- Pris: eventual consistency; monolittens kopi kan i kort tid være bagud
- EIP-patterns: se [EIP-patterns](#eip-patterns)
- Rapport: *Asynkron kommunikation mellem microservices*

</td>
<td>

`docs/messaging.drawio` (EIP), `docs/messaging-overview.drawio` (RabbitMQ: exchanges, queues, bindings, connections/channels), publisher `services/CatalogService/CatalogService.Api/Messaging/`, consumer `src/PublicApi/Messaging/CatalogSyncConsumer.cs`, `services/InvalidMessageConsumer/`

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Fejlhåndtering og overvågning (fx Circuit Breaker, logging, ELK eller Prometheus/Grafana)</td>
<td>

- Health checks: `/health` i CatalogService og ReverseProxy; bruges af YARP og Swarm (se API Gateway)
- Invalid Message Channel: beskeder, CatalogSyncConsumer ikke kan behandle (tom/ugyldig JSON, ukendt routing key, manglende `item`), flyttes til `monolith.catalog.invalid` og printes af `InvalidMessageConsumer`
- Fejl under behandling: én genlevering (`nack`, `requeue: true`), fejler den igen → invalid-køen
- Logs: kun i hver containers konsol
- Ikke implementeret grundet tidsmangel: correlation ID, central logning (ELK), metrics og traces (Prometheus/Grafana), Circuit Breaker
- Rapport: *Monitorering og observability af microservices*

</td>
<td>

`services/CatalogService/CatalogService.Api/Program.cs` (health checks), `src/PublicApi/Messaging/CatalogSyncConsumer.cs` (`HandleMessage`, catch-blokke), `services/InvalidMessageConsumer/Program.cs`

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>CI/CD pipeline — automatisk build, test og deployment, containerisering (fx Docker)</td>
<td>

- Én `<service>.yml` pr. service, trigges kun af egen mappe → bygges og testes uafhængigt
- Docker-image pr. service; hele systemet kører i Docker Swarm (`docker-stack.yml`) eller, til udvikling, `docker-compose.yml`
- Jobs: build → fast-tests (unit) → slow-tests (integration) → package → staging → deploy
- Mangler: rigtig deploy (staging/production er i dag kun `echo`-trin, production med manuel godkendelse)
  - `package` bygger et nyt image fra kildekoden uden registry → ikke det testede artifact
  - Løsning: gem testet image i et registry, og lad deploy køre `docker service update` på Swarm (via SSH eller self-hosted runner) → bruger de rullende opdateringer
- Rapport: *Uafhængig build, test og udrulning*, *Diskussion*

</td>
<td>

- Pipeline: `.github/workflows/_dotnet-service.yml`
- `<service>.yml`-filerne i samme mappe; se [CI/CD microservices](#cicd-microservices)

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Dokumentation af arkitekturbeslutninger, API'er og microservices (fx i README)</td>
<td>

- Denne README: krav, [CI/CD](#cicd-microservices), [EIP-patterns](#eip-patterns), [Test](#test)
- Rapporten: begrundelse for hvert valg; *Diskussion* om bl.a. read-only kopi vs. shared database, monorepo vs. multi-repo, deploy og health checks
- Diagrammer i `docs/`
- Mangler: API-dokumentation (ingen Swagger/OpenAPI)

</td>
<td>

`README.md`, `docs/messaging.drawio`, `docs/messaging-overview.drawio`

</td>
</tr>
</table>

## CI/CD microservices

- Ét workflow pr. deploybar enhed, trigges kun af egen mappe
- Fælles pipeline ét sted (`_dotnet-service.yml`), genbruges af alle services
- Egen .NET-version pr. service: hver service har sin egen `global.json` → kan opgraderes uafhængigt
- CI følger med: `setup-dotnet` læser `services/<service>/global.json` → hver service bygges og testes på sin egen version

| Enhed | .NET | Sat i |
|---|---|---|
| CatalogService | 10 | `services/CatalogService/global.json` |
| ReverseProxy | 10 | `services/ReverseProxy/global.json` |
| InvalidMessageConsumer | 10 | `services/InvalidMessageConsumer/global.json` |
| Monolitten | 8 | `global.json` (rod) + `monolit.yml` (`dotnet-version: '8.0.x'`) |

| Workflow | Enhed | Trigger | Funktion |
|---|---|---|---|
| `_dotnet-service.yml` | – (skabelon) | `workflow_call` | build → unit → integration → Docker-image → staging → production (manuel godkendelse). Discord ved fejl. .NET-version fra servicens `global.json` |
| `catalogservice.yml` | `services/CatalogService` | push `main` / PR på `services/CatalogService/**` | `_dotnet-service.yml` med `service: CatalogService` |
| `reverseproxy.yml` | `services/ReverseProxy` | push `main` / PR på `services/ReverseProxy/**` | `_dotnet-service.yml` med `service: ReverseProxy` |
| `invalidmessageconsumer.yml` | `services/InvalidMessageConsumer` | push `main` / PR på `services/InvalidMessageConsumer/**` | Selvstændigt: build + Docker-image (konsol-app uden tests) |
| `monolit.yml` | `src/` | push / PR på `src/**`, `tests/**` | Midlertidig, indtil monolitten er udskilt |

Ny service: `services/<Service>/` med samme struktur + `<service>.yml`, der kalder `_dotnet-service.yml`.

### Monorepo vs. multi-repo
- Monorepo valgt: ændringer på tværs (fx CatalogSyncConsumer sammen med CatalogServices events) kræver ingen koordinering mellem repositories
- Multi-repo-fordelen (uafhængige pipelines) opnås via path filters
- Bagside: grænsen håndhæves kun af path filters og disciplin; intet forhindrer en service i at referere direkte til en andens kode

## Test

Testpyramiden: mange hurtige unit tests, færre langsomme integrationstests.

| Niveau | Hvad | Job i CI | Kode |
|---|---|---|---|
| Unit | `CatalogEvents` isoleret; `IEventBus` er mock → tjekker routing key og at `Publish` kaldes én gang | `fast-tests` | `services/CatalogService/tests/CatalogService.UnitTests/` |
| Integration | Endpoints + forretningslogik + database via `WebApplicationFactory`; Postgres → in-memory, `ICatalogEvents` → mock | `slow-tests` (kun efter `fast-tests`) | `services/CatalogService/tests/CatalogService.IntegrationTests/` |

- Mangler: containerbaserede integrationstests (rigtig Postgres/RabbitMQ) og end-to-end tests (klient → ReverseProxy → CatalogService → monolitten)
- Asynkron kommunikation testes derfor kun op til Messaging Gatewayen
- Rapport: *Test af microservices og asynkron kommunikation*

## EIP-patterns (Hohpe & Woolf) 

Kun patterns, der bruges i projektet og nævnes i rapporten (afsnit *Asynkron kommunikation mellem microservices* og *Monitorering og observability af microservices*).

| Kategori | Patterns |
|---|---|
| Grundprincipper | Send and forget, store and forward |
| Message Channels | Publish-Subscribe Channel, Point-to-Point Channel, Guaranteed Delivery, Invalid Message Channel |
| Message Construction | Event Message, Document Message |
| Message Endpoints | Messaging Gateway, Messaging Mapper, Durable Subscriber, Idempotent Receiver, Competing Consumers (forberedt) |

### EIP vs. RabbitMQ (begreber)

EIP beskriver *mønstre*, RabbitMQ har *byggeklodser*. Samme ord betyder ikke altid det samme.

**Falske venner** (samme ord, forskellig betydning)

| Ord | EIP | RabbitMQ |
|---|---|---|
| **channel** | Message Channel: den logiske vej, en besked sendes ad | `IChannel`: en virtuel forbindelse inde i en TCP-connection, som klienten sender kommandoer over. Har intet med routing at gøre |
| **topic** | Navnet på en Publish-Subscribe Channel | En exchange-*type*, der router på mønstre (`*`, `#`) i routing key |
| **dead letter** | Systemet kan ikke levere beskeden | `x-dead-letter-exchange`: udløses også, når consumeren selv afviser (`nack`, `requeue: false`), dvs. tilfælde, EIP kalder invalid |
| **subscriber / consumer** | Subscriber = én modtager på en Pub-Sub Channel | Consumer = én forbindelse, der læser fra én kø. Én EIP-subscriber = én kø, som kan have flere consumers (replicas) |

**Mapping**

| EIP | RabbitMQ | Hos os |
|---|---|---|
| Publish-Subscribe Channel | Exchange (input) + én kø pr. subscriber (output) | `catalog` (topic) → `monolith.catalog` |
| Point-to-Point Channel | Én kø; hver besked går til én consumer | `monolith.catalog.invalid` via default exchange |
| Guaranteed Delivery | Durable exchange/kø + persistent besked (+ publisher confirms) | Ja, men uden publisher confirms |
| Durable Subscriber | Durable kø, ikke exclusive/auto-delete, navngivet efter service | `monolith.catalog` |
| Competing Consumers | Flere consumers på samme kø + `prefetchCount` | Kun ved flere monolit-replicas |
| Invalid Message Channel | Intet indbygget: almindelig kø, consumeren selv publicerer til | `monolith.catalog.invalid` |

### Message Channels

**Send and forget + store and forward** *(grundprincipper)*
- *Send and forget:* afsender afleverer og fortsætter uden at vente på modtager
- *Store and forward:* systemet gemmer og sender videre, når modtager er klar
- CatalogService svarer admin, så snart RabbitMQ har beskeden → monolitten må være nede
- Kode: `RabbitMq.cs` (`Publish`), `CatalogSyncConsumer.cs`

**Publish-Subscribe Channel**
- *Definition:* én input channel splittes i én output channel pr. subscriber; hver output channel har kun én subscriber
- Input = exchange `catalog`; output = én kø pr. service (monolitten: `monolith.catalog`)
- Kø navngivet efter service, ikke instans → monolitten er *én* subscriber uanset replicas
- CatalogService kender ikke sine subscribers; ny service binder bare sin egen kø (i dag kun 1 subscriber)
- Routing keys `catalog.item.created/updated/deleted`; binding `catalog.item.*`
- Exchange-type **topic** (bindings med mønstre: `*` = ét ord, `#` = nul eller flere):
  - Med 1 subscriber, der vil have alt, løser fanout og direct det samme → topic er valgt for fremtidige subscribers
  - Subscriber vælger selv via binding, publisher ændres ikke: fx kun `catalog.item.updated`, eller `catalog.#` hvis der kommer `catalog.brand.*`
  - vs. fanout: alle får alt og må selv filtrere. vs. direct: én binding pr. eksakt key, ny eventtype kræver nye bindings hos alle
  - Topic dækker begge: binding uden wildcard = direct, `#` = fanout
  - Bagside: ny key som `catalog.item.priceChanged` rammer `catalog.item.*`, men monolitten kender den ikke → invalid-køen (opdages, går ikke tabt)
  - Fanout ignorerer routing key ved routing, men den leveres stadig → ikke et argument for topic
- Kode: `RabbitMq.cs` (`ExchangeDeclareAsync`), `CatalogSyncConsumer.cs` (`QueueDeclareAsync`, `QueueBindAsync`)

**Guaranteed Delivery**
- *Definition:* beskeder gemmes persistent, så de overlever, at systemet går ned
- RabbitMQ: **durable** exchange/kø + **persistent** beskeder (`delivery_mode: 2`) + volume `rabbitmq-data`
- vs. Durable Subscriber: beskytter mod at *brokeren* går ned; Durable Subscriber mod at *modtageren* er offline
- Ikke vejen CatalogService → RabbitMQ: ingen publisher confirms
- Kode: `RabbitMq.cs` (`durable: true`, `DeliveryModes.Persistent`), `docker-compose.yml`

**Invalid Message Channel**
- *Definition:* beskeder, modtageren *ikke kan forstå*, flyttes af modtageren til en separat kanal
- Tilfælde: ugyldig JSON, tom besked, ukendt routing key, `item` mangler på `created`/`updated` → flyttes med det samme (aldrig retry)
- Tjek før databasen → `InvalidMessageException`
- Fejl under behandling (fx SQL Server nede): én genlevering (`requeue: true`), fejler den igen (`Redelivered`) → også invalid i stedet for at gå tabt
- Kanalen er en **Point-to-Point Channel**: én kø uden binding, kun én modtager får hver besked (`InvalidMessageConsumer`)
- Flytning: publish uændret via default exchange til `monolith.catalog.invalid` → ack original (publish før ack: hellere to gange end tabt). Fejl logges i monolitten
- `services/InvalidMessageConsumer`: konsol-app, printer beskederne (autoAck). Kører som monitor i Docker: `docker compose logs -f invalidmessageconsumer`; lokalt `dotnet run`
- Kode: `CatalogSyncConsumer.cs` (`HandleMessage`, `catch ... InvalidMessageException || Redelivered`), `services/InvalidMessageConsumer/Program.cs`

### Message Construction

**Event Message**
- *Definition:* besked om, at noget er sket
- `created/updated/deleted` i datid, ikke kommandoer; afsender bestemmer ikke, hvad modtager gør
- Metadata: `eventId` (sporing), `occurredAt`, `version`
- Kode: `services/CatalogService/CatalogService.Api/Messaging/CatalogEvents.cs`

**Document Message**
- *Definition:* besked, der overfører data; modtager bestemmer selv brugen
- `created`/`updated` = Event + Document (*push model*): hele itemet med → intet kald tilbage (*pull model*)
- `deleted` = ren Event Message, kun `id`
- `pictureUri` som færdig URL → monolitten behøver ingen catalog-logik
- Bagside: `ToData` udvælger kun felter, monolitten bruger → formatet er tilpasset monolitten (afhængighed i dataformatet)
- Kode: `CatalogEvents.cs` (`ToData`)

### Message Endpoints

**Messaging Gateway**
- *Definition:* klasse, der skjuler messaging bag domænemetoder
- `ICatalogEvents.CatalogItemCreated(item)` osv.; endpoints kender ikke RabbitMQ, routing keys eller JSON
- Skift af kommunikationsmetode kræver kun ændringer bag gatewayen
- Udskiftelig: integrationstests bruger `Mock.Of<ICatalogEvents>()`; selve gatewayen sender via `IEventBus` (`RabbitMq`), som unit tests mocker
- Kun i CatalogService: i monolitten er messaging isoleret i `CatalogSyncConsumer`, men der er intet domæneinterface, som monolitten kalder → ikke en gateway
- Kode: `Interfaces/ICatalogEvents.cs`, `Interfaces/IEventBus.cs`, `Messaging/CatalogEvents.cs`, `services/CatalogService/tests/CatalogService.IntegrationTests/CatalogServiceFactory.cs`, `services/CatalogService/tests/CatalogService.UnitTests/Messaging/CatalogEventsTests.cs`

**Messaging Mapper**
- *Definition:* flytter data mellem domæneobjekter og beskeder; ingen af dem kender den anden
- Publisher: `ToData` (`CatalogItem` → `item`, `ComposePicUri` → færdig URL); deleted beregner `version + 1`; JSON i `RabbitMq.Publish`
- Consumer: JSON → `CatalogEventMessage` → `FromCatalogService(...)` / `SyncFromCatalogService(...)`; entiteten får kun primitive værdier
- Kode: `CatalogEvents.cs` (`ToData`), `CatalogSyncConsumer.cs` (`HandleMessage`)

**Durable Subscriber**
- *Definition:* Pub-Sub-subscriber, hvis beskeder gemmes, mens den er afkoblet
- Durable kø `monolith.catalog` → ændringer behandles, når monolitten starter igen
- Forudsætning: køen skal findes (første opstart); beskeder før det går tabt
- Kode: `CatalogSyncConsumer.cs` (`QueueDeclareAsync(durable: true)`)

**Idempotent Receiver**
- *Definition:* samme besked kan modtages flere gange uden gentaget effekt
- `version <= gemt` → ignorér → dækker dubletter (genlevering efter manglende ack)
- Afviser også forældede events i forkert rækkefølge (fx to CatalogService-replicas, der ændrer samme item kort efter hinanden)
- Tombstone (`IsDeleted`) → forsinket `updated` genopliver ikke et slettet item
- Kode: `CatalogSyncConsumer.cs` (`HandleMessage`), `src/ApplicationCore/Entities/CatalogItem.cs`

**Competing Consumers**
- *Definition:* flere consumers på samme kanal; hver besked behandles af kun én
- Kun relevant, når monolitten skaleres: replicas deler `monolith.catalog`
- `prefetchCount: 1`: max 1 uacked besked pr. consumer → jævn fordeling (ledig replica tager næste), kun 1 genlevering ved crash. Uden: RabbitMQ fordeler med round robin uanset hvor travle de er
- CatalogService er ikke consumer; skaleres via HTTP (replicas bag gatewayen)
- `Version` som concurrency token → replicas overskriver ikke hinanden
- Kode: `CatalogSyncConsumer.cs` (`BasicQosAsync`), `src/Infrastructure/Data/Config/CatalogItemConfiguration.cs`
