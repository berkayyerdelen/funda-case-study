FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Host/Funda.Feeds.WorkerService/Funda.Feeds.WorkerService.csproj", "src/Host/Funda.Feeds.WorkerService/"]
RUN dotnet restore "src/Host/Funda.Feeds.WorkerService/Funda.Feeds.WorkerService.csproj"
COPY . .
WORKDIR "/src/src/Host/Funda.Feeds.WorkerService"
RUN dotnet build "Funda.Feeds.WorkerService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Funda.Feeds.WorkerService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Funda.Feeds.WorkerService.dll"]
