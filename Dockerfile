ARG APP_VERSION
ARG VCS_REF

FROM oven/bun:1.4.2 AS frontend-build

ARG APP_VERSION

WORKDIR /workspace/src/ContextDepot.Web
COPY src/ContextDepot.Web/package.json src/ContextDepot.Web/bun.lock ./
RUN bun install --frozen-lockfile
COPY src/ContextDepot.Web/ ./
COPY locales/ /workspace/locales/
COPY scripts/ /workspace/scripts/
RUN RELEASE_BUILD=true VITE_APP_VERSION="$APP_VERSION" bun run --bun build

FROM mcr.microsoft.com/dotnet/sdk:10.0.100-alpine3.22 AS build

ARG APP_VERSION

WORKDIR /workspace

# Copy project metadata first so source changes do not invalidate the restore layer.
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY scripts/ ./scripts/
COPY src/ContextDepot/ContextDepot.csproj src/ContextDepot/
COPY src/ContextDepot.Application/ContextDepot.Application.csproj src/ContextDepot.Application/
COPY src/ContextDepot.Domain/ContextDepot.Domain.csproj src/ContextDepot.Domain/
COPY src/ContextDepot.Infrastructure/ContextDepot.Infrastructure.csproj src/ContextDepot.Infrastructure/

RUN dotnet restore src/ContextDepot/ContextDepot.csproj -p:Configuration=Release \
    -p:ReleaseBuild=true -p:AppVersion="$APP_VERSION"

COPY src/ ./src/
COPY locales/ ./locales/
COPY --from=frontend-build /workspace/src/ContextDepot/wwwroot ./src/ContextDepot/wwwroot

RUN dotnet publish src/ContextDepot/ContextDepot.csproj \
    -c Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false \
    -p:ReleaseBuild=true \
    -p:AppVersion="$APP_VERSION"

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine3.22 AS runtime

ARG APP_VERSION
ARG VCS_REF

LABEL org.opencontainers.image.version=$APP_VERSION \
    org.opencontainers.image.revision=$VCS_REF

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
