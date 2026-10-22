FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Build context er repo-roden (samme som CI-pipelinen)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["services/ReverseProxy/ReverseProxy.Api/ReverseProxy.Api.csproj", "ReverseProxy.Api/"]
RUN dotnet restore "ReverseProxy.Api/ReverseProxy.Api.csproj"
COPY services/ReverseProxy/ReverseProxy.Api/ ReverseProxy.Api/
RUN dotnet publish "ReverseProxy.Api/ReverseProxy.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReverseProxy.Api.dll"]
