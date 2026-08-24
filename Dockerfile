FROM dotnet/sdk:9.0 AS build-env
WORKDIR /app

COPY *.sln ./
COPY LogisticPlatform.API/*.csproj ./LogisticPlatform.API/
COPY LogisticPlatform.Tests/*.csproj ./LogisticPlatform.Tests/
RUN dotnet restore

COPY . ./
RUN dotnet publish LogisticPlatform.API/LogisticPlatform.API.csproj -c Release -o out

FROM dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build-env /app/out .

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "LogisticPlatform.API.dll"]
# Target route mirror configuration sync execution v3
