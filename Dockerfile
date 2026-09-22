FROM mcr.microsoft.com/dotnet/sdk:10.0.100-alpine3.22 AS build

WORKDIR /workspace

# Copy project metadata first so source changes do not invalidate the restore layer.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ContextDepot/ContextDepot.csproj src/ContextDepot/
COPY src/ContextDepot.Application/ContextDepot.Application.csproj src/ContextDepot.Application/
COPY src/ContextDepot.Domain/ContextDepot.Domain.csproj src/ContextDepot.Domain/
COPY src/ContextDepot.Infrastructure/ContextDepot.Infrastructure.csproj src/ContextDepot.Infrastructure/

RUN dotnet restore src/ContextDepot/ContextDepot.csproj

COPY src/ ./src/
COPY locales/ ./locales/

RUN dotnet publish src/ContextDepot/ContextDepot.csproj \
    -c Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine3.22 AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ContextDepot__MarkdownRoot=/home/context-depot/knowledge \
    Serilog__WriteTo__1__Args__path=/home/context-depot/logs/context-depot-.log

# ICU, Kerberos, timezone data and CA certificates are required by the runtime,
# PostgreSQL connectivity and HTTPS-based embedding providers.
RUN apk add --no-cache \
    ca-certificates \
    icu-libs \
    krb5-libs \
    tzdata \
    && mkdir -p /home/context-depot/knowledge /home/context-depot/logs

COPY --from=build /app/publish ./

# Azure App Service persists /home when WEBSITES_ENABLE_APP_SERVICE_STORAGE=true.
# Keeping one volume root also makes standalone container deployments portable.
VOLUME ["/home/context-depot"]

EXPOSE 8080

ENTRYPOINT ["dotnet", "ContextDepot.dll"]
