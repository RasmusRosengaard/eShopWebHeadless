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
- **Invalid Message Channel**: beskeder, consumeren ikke kan forstå (fx ugyldig JSON eller manglende `item`), prøves ikke igen, men flyttes til køen `monolith.catalog.invalid`, så de ikke blokerer eller går tabt. En lille konsol-app, `InvalidMessageConsumer`, læser køen.
- **Resequencer (fravalgt)**: den forudsætter en ubrudt sekvens, så et tabt event ville blokere alle efterfølgende. Da events er Document Messages med hele itemets tilstand, er det nok at droppe forældede events i stedet for at sortere dem.
</td>
<td>

For at tjekke kig i `docs/messaging.drawio`, publisheren i `services/CatalogService/CatalogService.Api/Messaging/` og consumeren i `src/PublicApi/Messaging/CatalogSyncConsumer.cs` samt `services/InvalidMessageConsumer/`

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

Hvert pattern starter med definitionen fra *Enterprise Integration Patterns* (Hohpe & Woolf), derefter hvordan det er brugt, og hvor koden ligger.

### Message Channels

**Send and forget + store and forward** *(grundprincipperne i messaging)*
- *Send and forget:* afsenderen afleverer beskeden til messaging-systemet og fortsætter uden at vente på modtageren
- *Store and forward:* messaging-systemet gemmer beskeden og sender den videre, når modtageren er tilgængelig
- CatalogService svarer admin, så snart beskeden er afleveret til RabbitMQ; RabbitMQ holder beskeden, til monolitten er klar → monolitten må være nede (eventually consistent)
- Kode: `RabbitMq.cs` (`Publish`), `CatalogSyncConsumer.cs`

**Publish-Subscribe Channel**
- *Definition:* én kanal, der leverer en kopi af hver besked til hver interesseret modtager. Kanalen har én input channel, der deles ud i flere output channels, én pr. subscriber; hver output channel har kun én subscriber
- Input channel = topic exchange `catalog`; output channel = én kø pr. subscribende service, for monolitten `monolith.catalog`
- Køen er navngivet efter servicen, ikke instansen: monolitten er *én* subscriber, uanset antal replicas (se Competing Consumers)
- CatalogService kender ikke sine subscribers; en ny service binder blot sin egen kø
- Routing keys `catalog.item.created/updated/deleted`; monolitten binder `catalog.item.*`
- Kode: `RabbitMq.cs` (`ExchangeDeclareAsync`), `src/PublicApi/Messaging/CatalogSyncConsumer.cs` (`QueueDeclareAsync`, `QueueBindAsync`)

**Point-to-Point Channel** *(ikke brugt)*
- *Definition:* en kanal, der sikrer, at kun én modtager forbruger en given besked
- CatalogService sender aldrig til én bestemt modtager; den publicerer events til alle interesserede. Point-to-Point hører typisk sammen med Command Messages, som vi ikke bruger

**Guaranteed Delivery**
- *Definition:* messaging-systemet gemmer beskeder i et persistent lager, så de ikke går tabt, selvom systemet går ned (store and forward med disk)
- RabbitMQ: **durable** exchange og kø (topologien overlever genstart) + **persistent** beskeder (`delivery_mode: 2`) + volume `rabbitmq-data`
- Forskel fra Durable Subscriber: Guaranteed Delivery beskytter mod, at *brokeren* går ned; Durable Subscriber mod, at *modtageren* er offline
- Gælder ikke vejen fra CatalogService til RabbitMQ: ingen publisher confirms, og dual write (DB gemt, publish fejler) → Transactional Outbox som løsning
- Kode: `RabbitMq.cs` (`durable: true`, `DeliveryModes.Persistent`), `docker-compose.yml`

**Dead Letter Channel** *(ikke lavet endnu)*
- *Definition:* beskeder, som messaging-systemet *ikke kan levere*, flyttes til en separat kanal i stedet for at gå tabt
- Tilfælde: beskeden fejler gentagne gange, fx fordi SQL Server er nede
- Nu: én genlevering (`requeue: !Redelivered`), derefter droppes og logges beskeden
- Idé: `x-delivery-limit` (quorum queue) + `x-dead-letter-exchange = ""` (default exchange) og `x-dead-letter-routing-key = "monolith.catalog.dead"` → kø `monolith.catalog.dead`. Routing key er påkrævet: uden den beholder beskeden `catalog.item.*`, og default exchange finder ingen kø med det navn. Den oprindelige routing key ligger i headeren `x-death`
- Kode: `CatalogSyncConsumer.cs` (`BasicNackAsync`)

**Invalid Message Channel**
- *Definition:* beskeder, som modtageren *ikke kan forstå* (forkert format, manglende felter), flyttes af modtageren til en separat kanal
- Forskel fra Dead Letter (udledt af EIP, siderne nævner kun hinanden som relaterede): Dead Letter = levering lykkes ikke (messaging-systemets afgørelse); Invalid = beskeden er leveret, men ugyldig (modtagerens afgørelse). En ugyldig besked skal ikke prøves igen
- Tilfælde: JSON kan ikke deserialiseres, tom besked, ukendt routing key, eller `item` mangler på et `created`/`updated`
- `HandleMessage` tjekker beskeden, før databasen røres, og kaster `InvalidMessageException`. Consumeren publicerer så den uændrede besked via default exchange til kø `monolith.catalog.invalid` og acker originalen (publish før ack, så den hellere havner der to gange end går tabt). Fejlen logges i monolitten
- `services/InvalidMessageConsumer` er en lille konsol-app, der læser køen og printer beskederne (autoAck)
- Kode: `CatalogSyncConsumer.cs` (`HandleMessage`, `catch ... InvalidMessageException`), `services/InvalidMessageConsumer/Program.cs`

### Message Construction

**Event Message**
- *Definition:* en besked, der underretter om, at noget er sket, så modtageren kan reagere asynkront
- `catalog.item.created/updated/deleted` i datid (ikke kommandoer som `UpdateItem`); afsenderen bestemmer ikke, hvad modtageren gør
- Metadata: `eventId` (unikt id til sporing), `occurredAt`, `version`
- Kode: `services/CatalogService/CatalogService.Api/Messaging/CatalogEvents.cs`

**Document Message**
- *Definition:* en besked, der overfører en datastruktur; modtageren bestemmer selv, hvad den gør med dataene
- `created`/`updated` er både Event og Document Message (EIP's *push model*): hele itemet sendes med, så monolitten ikke skal kalde CatalogService tilbage (*pull model*)
- `deleted` er en ren Event Message med kun `id`
- `pictureUri` sendes som færdig URL, så monolitten ikke behøver catalog-logik
- Kode: `CatalogEvents.cs` (`ToData`)

**Format Indicator** *(ikke brugt)*
- *Definition:* et felt i beskeden, der angiver dens format (versionsnummer, foreign key til et schema eller selve schemaet)
- `version` i vores beskeder er **itemets** version, ikke beskedformatets; de to må ikke forveksles
- Bliver relevant, når beskedformatet ændres, og gamle og nye consumers skal leve side om side (fx `formatVersion: 2`)

### Message Endpoints

**Message Endpoint**
- *Definition:* den del af en applikation, der forbinder den med en messaging-kanal og sender eller modtager beskeder
- CatalogService (sender) og monolitten (modtager)
- Kode: `services/CatalogService/CatalogService.Api/Messaging/`, `src/PublicApi/Messaging/`

**Messaging Gateway**
- *Definition:* en klasse, der indkapsler al messaging-kode og udstiller domænespecifikke metoder til resten af applikationen
- `ICatalogEvents` med `CatalogItemCreated(item)` osv.; endpoints kender hverken RabbitMQ, routing keys eller JSON
- Gatewayen kan udskiftes: integrationstestene bruger `Mock.Of<ICatalogEvents>()`
- Kode: `Interfaces/ICatalogEvents.cs`, `Messaging/CatalogEvents.cs`, `tests/CatalogService.IntegrationTests/CatalogServiceFactory.cs`

**Messaging Mapper**
- *Definition:* flytter data mellem domæneobjekter og beskeder, så hverken domænet kender beskedformatet eller beskeden kender domænet
- Publisher: `ToData` mapper `CatalogItem` til beskedens `item` og laver relativ `PictureUri` om til færdig URL (`ComposePicUri`); `CatalogItemDeleted` beregner `version + 1`. JSON-serialiseringen sker i `RabbitMq.Publish`
- Consumer: JSON → `CatalogEventMessage` (privat klasse) → `CatalogItem.FromCatalogService(...)` / `SyncFromCatalogService(...)`; entiteten får kun primitive værdier og kender ikke beskedklassen
- Forskel fra **Message Translator** *(ikke brugt)*: en Translator oversætter en *besked* til en anden *besked* (format A → format B) mellem to kanaler. Messaging Mapper ligger i endpointet og mapper mellem *domæneobjekt* og besked. Begge services mapper direkte til/fra det fælles beskedformat, så der er ingen Translator
- Kode: `Messaging/CatalogEvents.cs` (`ToData`), `src/PublicApi/Messaging/CatalogSyncConsumer.cs` (`HandleMessage`)

**Event-Driven Consumer**
- *Definition:* en consumer, der får beskeder leveret automatisk, når de ankommer (modsat Polling Consumer, der selv spørger)
- `BackgroundService` i monolitten med `AsyncEventingBasicConsumer`; RabbitMQ pusher, og `ReceivedAsync` kaldes
- Kode: `CatalogSyncConsumer.cs` (`ReceivedAsync`)

**Durable Subscriber**
- *Definition:* en subscriber på en Publish-Subscribe Channel, hvor messaging-systemet gemmer beskeder, mens subscriberen er afkoblet
- Durable kø `monolith.catalog`: ændringer, mens monolitten er nede, behandles, når den starter igen
- Forudsætning: køen skal være oprettet (første opstart af monolitten); beskeder publiceret før det har ingen kø at lande i
- Kode: `CatalogSyncConsumer.cs` (`QueueDeclareAsync(durable: true)`)

**Idempotent Receiver**
- *Definition:* en modtager, der kan modtage den samme besked flere gange uden at effekten gentages
- `message.version <= gemt version` → ignorér. Dækker dubletter (genlevering efter manglende ack)
- Samme tjek afviser også forældede events, der ankommer i forkert rækkefølge; det går ud over ren idempotens og erstatter en Resequencer
- Tombstone (`IsDeleted`) i stedet for at slette rækken, så et forsinket `updated` ikke genopliver et slettet item
- Kode: `CatalogSyncConsumer.cs` (`HandleMessage`), `src/ApplicationCore/Entities/CatalogItem.cs`

**Competing Consumers**
- *Definition:* flere consumers på samme kanal, som konkurrerer om beskederne og behandler dem samtidig; hver besked behandles af kun én af dem
- Replicas af monolitten deler Pub-Sub-kanalens output channel `monolith.catalog`; `prefetchCount: 1`, så en replica kun tager én besked ad gangen
- Samme kombination som EIP's Google Cloud Pub/Sub-eksempel: topic giver Publish-Subscribe, subscription giver Competing Consumers
- `Version` som concurrency token, så to replicas ikke overskriver hinanden
- Kode: `CatalogSyncConsumer.cs` (`BasicQosAsync`), `src/Infrastructure/Data/Config/CatalogItemConfiguration.cs`

**Transactional Client** *(ikke brugt)*
- *Definition:* en klient, hvis session med messaging-systemet er transaktionel, så afsendelse/modtagelse og fx en databaseændring commit'es samlet
- Consumer: manuel ack efter gem (`autoAck: false`) giver *at-least-once*, ikke en transaktion; crash mellem gem og ack → beskeden leveres igen, og Idempotent Receiver fanger dubletten
- Publisher: publish sker efter gem, men i en anden transaktion → dual write; Transactional Outbox løser det
- Kode: `CatalogSyncConsumer.cs` (`BasicAckAsync`), `CreateCatalogItemEndpoint.cs` m.fl.
