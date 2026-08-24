FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env

WORKDIR /app

COPY *.sln ./
COPY LogisticPlatform.API/*.csproj ./LogisticPlatform.API/
COPY LogisticPlatform.Tests/*.csproj ./LogisticPlatform.Tests/

RUN dotnet restore

COPY . ./

RUN dotnet publish LogisticPlatform.API/LogisticPlatform.API.csproj \
    -c Release \
    -o /app/out \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app

COPY --from=build-env /app/out .

ENV ASPNETCORE_URLS=http://+:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "LogisticPlatform.API.dll"]