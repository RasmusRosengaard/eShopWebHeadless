# Headless client: published as static files and served by nginx (no .NET runtime in the final image).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
WORKDIR "/app/src/WebSpa"
RUN dotnet publish "./WebSpa.csproj" -c Release -o /app/publish

FROM nginx:alpine AS final
COPY src/WebSpa/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
