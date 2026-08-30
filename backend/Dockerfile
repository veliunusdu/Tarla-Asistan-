# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY src/TarlaAsistani.Domain/TarlaAsistani.Domain.csproj src/TarlaAsistani.Domain/
COPY src/TarlaAsistani.Application/TarlaAsistani.Application.csproj src/TarlaAsistani.Application/
COPY src/TarlaAsistani.Infrastructure/TarlaAsistani.Infrastructure.csproj src/TarlaAsistani.Infrastructure/
COPY src/TarlaAsistani.API/TarlaAsistani.API.csproj src/TarlaAsistani.API/

RUN dotnet restore src/TarlaAsistani.API/TarlaAsistani.API.csproj

# Copy source code and build
COPY src/ src/
WORKDIR /app/src/TarlaAsistani.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY scripts/start-render.sh /app/start-render.sh
RUN chmod +x /app/start-render.sh
ENTRYPOINT ["/app/start-render.sh"]
