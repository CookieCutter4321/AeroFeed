# syntax=docker/dockerfile:1
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
RUN apk add --no-cache nodejs npm

WORKDIR /source

COPY AeroFeed.Client/package*.json ./AeroFeed.Client/
WORKDIR /source/AeroFeed.Client
RUN npm ci

WORKDIR /source
COPY . . 

WORKDIR /source/AeroFeed.Client
RUN npm run build -- --configuration production

WORKDIR /source/AeroFeed.Server
ARG TARGETARCH
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "AeroFeed.Server.csproj" \
    -a ${TARGETARCH/amd64/x64} \
    --use-current-runtime \
    --self-contained false \
    -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app .
COPY --from=build /source/AeroFeed.Client/dist/ng-tailadmin/browser ./wwwroot 

USER $APP_UID
ENTRYPOINT ["dotnet", "AeroFeed.Server.dll"]