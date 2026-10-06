# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first (better layer caching on dependency restore). The solution file itself
# isn't needed here — `dotnet restore`/`publish` on FairDraw.Api.csproj directly pulls in its own
# ProjectReference graph without it.
COPY src/FairDraw.Domain/FairDraw.Domain.csproj src/FairDraw.Domain/
COPY src/FairDraw.Crypto/FairDraw.Crypto.csproj src/FairDraw.Crypto/
COPY src/FairDraw.Infrastructure/FairDraw.Infrastructure.csproj src/FairDraw.Infrastructure/
COPY src/FairDraw.Api/FairDraw.Api.csproj src/FairDraw.Api/
COPY tests/FairDraw.UnitTests/FairDraw.UnitTests.csproj tests/FairDraw.UnitTests/
COPY tests/FairDraw.IntegrationTests/FairDraw.IntegrationTests.csproj tests/FairDraw.IntegrationTests/
RUN dotnet restore src/FairDraw.Api/FairDraw.Api.csproj

COPY . .
RUN dotnet publish src/FairDraw.Api/FairDraw.Api.csproj -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is needed for the docker-compose healthcheck (GET /health); not present in the base image.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FairDraw.Api.dll"]
