# ============================================================
# CarRentalSystem — Multi-stage Docker build
# Stage 1: Build
# Stage 2: Runtime (self-contained)
# ============================================================

# ── Stage 1: Build ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy .csproj files first for better Docker layer caching
COPY ["CarRentalSystem/CarRentalSystem.csproj", "CarRentalSystem/"]
COPY ["CarRentalSystem.Tests/CarRentalSystem.Tests.csproj", "CarRentalSystem.Tests/"]

# Restore dependencies (cached as a separate layer)
RUN dotnet restore "CarRentalSystem/CarRentalSystem.csproj"
RUN dotnet restore "CarRentalSystem.Tests/CarRentalSystem.Tests.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/CarRentalSystem"

# Build in Release mode
RUN dotnet build "CarRentalSystem.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/build

# ── Stage 2: Publish ───────────────────────────────────
FROM build AS publish
WORKDIR "/src/CarRentalSystem"

RUN dotnet publish "CarRentalSystem.csproj" \
    --configuration Release \
    --no-build \
    --output /app/publish \
    --self-contained true \
    --runtime linux-x64 \
    -p:PublishSingleFile=false \
    -p:IncludeNativeLibrariesForSelfExtract=true

# ── Stage 3: Runtime ───────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# ── Install SQL Server command-line tools for ef migrations ──
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    gnupg \
    ca-certificates \
    apt-transport-https \
    && curl -sSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /usr/share/keyrings/microsoft-prod.gpg \
    && echo "deb [arch=amd64,arm64,armhf signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/debian/12/prod bookworm main" > /etc/apt/sources.list.d/mssql-release.list \
    && apt-get update \
    && ACCEPT_EULA=Y apt-get install -y --no-install-recommends \
        mssql-tools18 \
        unixodbc-dev \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

ENV PATH="$PATH:/opt/mssql-tools18/bin"

# Create non-root user for security
RUN groupadd --gid 1000 appgroup \
    && useradd --uid 1000 --gid 1000 --shell /bin/bash --create-home appuser

# Copy published output
COPY --from=publish /app/publish .

# Copy EF Core tools
RUN dotnet tool install --global dotnet-ef --version 10.0.* || true
ENV PATH="$PATH:/root/.dotnet/tools"

# Set ownership
RUN chown -R 1000:1000 /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Expose port
EXPOSE 5000

# Entry point
ENTRYPOINT ["dotnet", "CarRentalSystem.dll"]
