# ============================================================
# CarRentalSystem — Multi-stage Docker build
# Stage 1: Build
# Stage 2: Runtime (self-contained)
# ============================================================

# ── Stage 1: Publish ───────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /src

# Copy everything and publish
COPY . .
WORKDIR "/src/CarRentalSystem"

RUN dotnet publish "CarRentalSystem.csproj" \
    --configuration Release \
    --output /app/publish \
    --self-contained true \
    --runtime linux-x64 \
    -p:PublishSingleFile=false \
    -p:IncludeNativeLibrariesForSelfExtract=true

# ── Stage 2: Runtime ───────────────────────────────────
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

# Use the built-in 'app' user provided by Microsoft (.NET 8+)
# Copy published output
COPY --from=publish /app/publish .

# Copy EF Core tools (optional, usually not needed in runtime unless doing migrations)
# RUN dotnet tool install --global dotnet-ef --version 10.0.* || true
# ENV PATH="$PATH:/root/.dotnet/tools"

# Set ownership to the built-in 'app' user
RUN chown -R app:app /app
USER app

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Expose port
EXPOSE 5000

# Entry point
ENTRYPOINT ["dotnet", "CarRentalSystem.dll"]
