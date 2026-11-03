# note: hold versionen i sync med services/InvalidMessageConsumer/global.json
# Konsol-app, så runtime-imaget er nok (ikke aspnet), og der er ingen port.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

# Build context er repo-roden (samme som de andre services)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["services/InvalidMessageConsumer/InvalidMessageConsumer.csproj", "InvalidMessageConsumer/"]
RUN dotnet restore "InvalidMessageConsumer/InvalidMessageConsumer.csproj"
COPY services/InvalidMessageConsumer/ InvalidMessageConsumer/
RUN dotnet publish "InvalidMessageConsumer/InvalidMessageConsumer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InvalidMessageConsumer.dll"]
