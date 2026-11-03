## Hvad er et færdigt produkt?

Krav fra: https://drive.google.com/file/d/18PVhcuDnv0K4dXrhbqM-yhhQRgGZaDMz/view?usp=sharing

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
- Mangler: vurdering med cohesion og coupling

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

</td>
<td>

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>API Gateway — samlet indgangspunkt med autentificering, load balancing og rate-limiting</td>
<td>

- YARP = eneste indgang; catalog-routes → CatalogService, catch-all → monolitten
- CORS samlet i gatewayen → preflights besvares, selv hvis servicen bag er nede
- Autentificering: JWT valideres i servicerne, ikke i gatewayen
- Mangler: load balancing (én destination pr. cluster) og rate-limiting

</td>
<td>

`services/ReverseProxy/ReverseProxy.Api/` (`Program.cs`, `appsettings*.json`)

</td>
</tr>

<!-- ============================================================ -->
<tr>
<td>Asynkron beskedbaseret kommunikation mellem microservices (fx RabbitMQ, Azure Service Bus, Kafka)</td>
<td>

- CatalogService publicerer `catalog.item.created/updated/deleted` via RabbitMQ → monolitten opdaterer sin read-only kopi
- Asynkront: CatalogService venter ikke på monolitten; monolitten må være nede (eventually consistent)
- EIP-patterns: se [EIP-patterns](#eip-patterns)

</td>
<td>

`docs/messaging.drawio`, publisher `services/CatalogService/CatalogService.Api/Messaging/`, consumer `src/PublicApi/Messaging/CatalogSyncConsumer.cs`, `services/InvalidMessageConsumer/`

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

- Én `<service>.yml` pr. service, trigges kun af egen mappe → bygges og testes uafhængigt
- Docker-image pr. service; hele systemet kører i `docker-compose.yml`
- Mangler: rigtig deploy (staging/production er i dag kun `echo`-trin, production med manuel godkendelse)

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

</td>
<td>

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

## EIP-patterns (Hohpe & Woolf) 

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
- Kode: `CatalogEvents.cs` (`ToData`)

**Format Indicator** *(ikke brugt)*
- *Definition:* felt, der angiver beskedens format (versionsnr., schema-reference eller schema)
- Vores `version` = **itemets** version, ikke formatets
- Relevant, hvis formatet ændres og gamle/nye consumers skal leve side om side (fx `formatVersion: 2`)

### Message Endpoints

**Message Endpoint**
- *Definition:* koden, der forbinder en applikation med en kanal
- CatalogService (sender), monolitten (modtager)
- Kode: `services/CatalogService/CatalogService.Api/Messaging/`, `src/PublicApi/Messaging/`

**Messaging Gateway**
- *Definition:* klasse, der skjuler messaging bag domænemetoder
- `ICatalogEvents.CatalogItemCreated(item)` osv.; endpoints kender ikke RabbitMQ, routing keys eller JSON
- Udskiftelig: integrationstests bruger `Mock.Of<ICatalogEvents>()`
- vs. **Channel Adapter** *(ikke brugt)*: Adapter kobler appen til messaging udefra via dens API eller data (fx CDC/Debezium på databasen); Gateway ligger i appens egen kode → vi har kildekoden til både CatalogService og monolitten og kan ændre den direkte
- Kun i CatalogService: i monolitten er messaging isoleret i `CatalogSyncConsumer`, men der er intet domæneinterface, som monolitten kalder → Event-Driven Consumer + Messaging Mapper + Service Activator, ikke gateway
- Kode: `Interfaces/ICatalogEvents.cs`, `Messaging/CatalogEvents.cs`, `tests/CatalogService.IntegrationTests/CatalogServiceFactory.cs`

**Messaging Mapper**
- *Definition:* flytter data mellem domæneobjekter og beskeder; ingen af dem kender den anden
- Publisher: `ToData` (`CatalogItem` → `item`, `ComposePicUri` → færdig URL); deleted beregner `version + 1`; JSON i `RabbitMq.Publish`
- Consumer: JSON → `CatalogEventMessage` → `FromCatalogService(...)` / `SyncFromCatalogService(...)`; entiteten får kun primitive værdier
- vs. **Message Translator** *(ikke brugt)*: Translator = besked → anden besked mellem kanaler; Mapper = domæneobjekt ↔ besked i endpointet
- Kode: `CatalogEvents.cs` (`ToData`), `CatalogSyncConsumer.cs` (`HandleMessage`)

**Event-Driven Consumer**
- *Definition:* får beskeder leveret automatisk (vs. Polling Consumer, der selv spørger)
- `BackgroundService` + `AsyncEventingBasicConsumer`; RabbitMQ pusher → `ReceivedAsync`
- Kode: `CatalogSyncConsumer.cs` (`ReceivedAsync`)

**Service Activator**
- *Definition:* forbinder beskeder på en kanal med en service, der også kan kaldes uden messaging; kalder den som en hvilken som helst anden klient (kan være et simpelt metodekald)
- Envejs (ingen Request-Reply): besked → `IRepository<CatalogItem>` + `CatalogItem.FromCatalogService` / `SyncFromCatalogService`
- Repository og entitet kender ikke RabbitMQ; samme repository bruges af resten af monolitten
- Kode: `CatalogSyncConsumer.cs` (`HandleMessage`)

**Durable Subscriber**
- *Definition:* Pub-Sub-subscriber, hvis beskeder gemmes, mens den er afkoblet
- Durable kø `monolith.catalog` → ændringer behandles, når monolitten starter igen
- Forudsætning: køen skal findes (første opstart); beskeder før det går tabt
- Kode: `CatalogSyncConsumer.cs` (`QueueDeclareAsync(durable: true)`)

**Idempotent Receiver**
- *Definition:* samme besked kan modtages flere gange uden gentaget effekt
- `version <= gemt` → ignorér → dækker dubletter (genlevering efter manglende ack)
- Afviser også forældede events i forkert rækkefølge (ud over ren idempotens) → erstatter Resequencer
- Resequencer fravalgt: kræver ubrudt sekvens → ét tabt event blokerer resten. Document Messages med hele tilstanden → nok at droppe forældede
- Tombstone (`IsDeleted`) → forsinket `updated` genopliver ikke et slettet item
- Kode: `CatalogSyncConsumer.cs` (`HandleMessage`), `src/ApplicationCore/Entities/CatalogItem.cs`

**Competing Consumers**
- *Definition:* flere consumers på samme kanal; hver besked behandles af kun én
- Kun relevant, når monolitten skaleres: replicas deler `monolith.catalog`
- `prefetchCount: 1`: max 1 uacked besked pr. consumer → jævn fordeling (ledig replica tager næste), kun 1 genlevering ved crash. Uden: én replica kan få hele køen, mens de andre står stille
- CatalogService er ikke consumer; skaleres via HTTP (replicas bag gatewayen)
- `Version` som concurrency token → replicas overskriver ikke hinanden
- Kode: `CatalogSyncConsumer.cs` (`BasicQosAsync`), `src/Infrastructure/Data/Config/CatalogItemConfiguration.cs`
