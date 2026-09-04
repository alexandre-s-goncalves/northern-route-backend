FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["LogisticPlatform.API/LogisticPlatform.API.csproj", "LogisticPlatform.API/"]
RUN dotnet restore "LogisticPlatform.API/LogisticPlatform.API.csproj"
COPY . .
WORKDIR "/src/LogisticPlatform.API"
RUN dotnet publish "LogisticPlatform.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "LogisticPlatform.API.dll"]
