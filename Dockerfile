# syntax=docker/dockerfile:1

# --- Front end: React + Vite ---------------------------------------------------
FROM node:25-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# --- API: .NET publish -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
# global.json pins the exact local SDK; the image's SDK 10 is used instead.
COPY Directory.Build.props .editorconfig ./
COPY src/TaskHub.Domain/TaskHub.Domain.csproj src/TaskHub.Domain/
COPY src/TaskHub.Infrastructure/TaskHub.Infrastructure.csproj src/TaskHub.Infrastructure/
COPY src/TaskHub.Api/TaskHub.Api.csproj src/TaskHub.Api/
RUN dotnet restore src/TaskHub.Api/TaskHub.Api.csproj
COPY src/ src/
# The OpenAPI document is already committed in web/openapi: no build-time generation here.
RUN dotnet publish src/TaskHub.Api/TaskHub.Api.csproj -c Release -o /app --no-restore -p:OpenApiGenerateDocuments=false

# --- Runtime -----------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot

# SQLite database on a volume, writable by the image's non-root "app" user.
RUN mkdir -p /data && chown "$APP_UID" /data
VOLUME /data
ENV ConnectionStrings__TaskHub="Data Source=/data/taskhub.db" \
    ASPNETCORE_HTTP_PORTS=8080

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskHub.Api.dll"]
