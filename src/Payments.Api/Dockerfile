# Build context is the repo root: see .dockerignore.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Version pins and build settings change far less often than code, so copying
# them first keeps the restore layer cached. .editorconfig is needed because
# EnforceCodeStyleInBuild is on: without it the container build would apply
# different rules than a local one.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Payments.Api/Payments.Api.csproj src/Payments.Api/
RUN dotnet restore src/Payments.Api/Payments.Api.csproj

COPY src/Payments.Api/ src/Payments.Api/
RUN dotnet publish src/Payments.Api/Payments.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .
# The Alpine image runs as root unless told otherwise; APP_UID is the
# unprivileged user it ships with.
USER $APP_UID
ENTRYPOINT ["dotnet", "Payments.Api.dll"]
