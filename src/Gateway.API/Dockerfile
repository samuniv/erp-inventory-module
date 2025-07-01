# Multi-stage build for Gateway API
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5010

# Add non-root user for security
RUN adduser --disabled-password --gecos "" --uid 1001 appuser

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for dependency resolution
COPY ["Gateway.API.csproj", "Gateway.API/"]
COPY ["../Shared/Shared.csproj", "Shared/"]

# Restore dependencies
RUN dotnet restore "Gateway.API/Gateway.API.csproj"

# Copy source code
COPY . Gateway.API/
COPY ../Shared/ Shared/

# Build the application
WORKDIR "/src/Gateway.API"
RUN dotnet build "Gateway.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Gateway.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5010/health || exit 1

ENTRYPOINT ["dotnet", "Gateway.API.dll"]
