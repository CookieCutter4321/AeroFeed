# syntax=docker/dockerfile:1

# Create a stage for building the application.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
COPY . /source

WORKDIR /source/AngularApp1.Server
ARG TARGETARCH
RUN apk add --no-cache nodejs npm

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish -a ${TARGETARCH/amd64/x64} --use-current-runtime --self-contained false -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID

ENTRYPOINT ["dotnet", "AeroFeed.Server.dll"]
