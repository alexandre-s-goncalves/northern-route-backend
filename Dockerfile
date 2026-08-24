FROM ://microsoft.com AS build-env
WORKDIR /app

COPY *.sln ./
COPY LogisticPlatform.API/*.csproj ./LogisticPlatform.API/
COPY LogisticPlatform.Tests/*.csproj ./LogisticPlatform.Tests/
RUN dotnet restore

COPY . ./
RUN dotnet publish LogisticPlatform.API/LogisticPlatform.API.csproj -c Release -o out

FROM ://microsoft.com
WORKDIR /app
COPY --from=build-env /app/out .

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "LogisticPlatform.API.dll"]

