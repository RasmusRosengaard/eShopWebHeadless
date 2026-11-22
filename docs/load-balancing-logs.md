# Se load balancing i loggene (Swarm)

Kør kommandoerne i Git Bash, mens stacken kører (`docker stack deploy -c docker-stack.yml eshop`). Hver loglinje starter med replicaens navn, fx `eshop_catalogservice.3@docker-desktop`. Navnet viser, hvilken replica der håndterede kaldet eller beskeden.

## HTTP: YARP → CatalogService (round-robin)

ReverseProxy fordeler requests på skift mellem catalogservice-replicas.

```bash
# Følg loggen live (klik rundt i shoppen på http://localhost:5106)
docker service logs -f --tail 0 --no-task-ids eshop_catalogservice | grep "LIMIT @p"

# Send 20 requests gennem gatewayen
for i in $(seq 1 20); do curl -s -o /dev/null "localhost:5300/api/catalog-items?pageIndex=$i"; done

# Tæl requests pr. replica det seneste minut (forventet: lige mange)
docker service logs --since 1m --no-task-ids eshop_catalogservice 2>&1 | grep "LIMIT @p" | awk '{print $1}' | sort | uniq -c
```

## RabbitMQ: CatalogService → monolitten (competing consumers)

PublicApi-replicas konkurrerer om beskederne på køen `monolith.catalog`. Med `prefetchCount: 1` og manuel ack får hver replica én besked ad gangen.

```bash
# Skalér monolitten, så der er flere consumers
docker service scale eshop_eshoppublicapi=2

# Følg loggen live, og ret derefter nogle priser i BlazorAdmin (http://localhost:5107)
docker service logs -f --tail 0 --no-task-ids eshop_eshoppublicapi | grep "Synkroniserede"
```

Forventet: hver besked behandles af præcis én replica.

```
eshop_eshoppublicapi.1@docker-desktop | Synkroniserede catalog.item.updated for item 1 (version 3)
eshop_eshoppublicapi.2@docker-desktop | Synkroniserede catalog.item.updated for item 2 (version 3)
```

Du kan også se det i RabbitMQ UI på http://localhost:15673 (catalog / catalogpassword) under **Queues** → `monolith.catalog`. Der står antallet af consumers, og hver har prefetch count 1.

> I PowerShell skriver du `| Select-String "..."` i stedet for `| grep "..."`.
