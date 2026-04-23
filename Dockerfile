# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/BlitzBridge.McpServer/BlitzBridge.McpServer.csproj", "src/BlitzBridge.McpServer/"]
COPY ["src/BlitzBridge.ServiceDefaults/BlitzBridge.ServiceDefaults.csproj", "src/BlitzBridge.ServiceDefaults/"]
RUN dotnet restore "src/BlitzBridge.McpServer/BlitzBridge.McpServer.csproj"

COPY . .
WORKDIR /src/src/BlitzBridge.McpServer
RUN dotnet publish "BlitzBridge.McpServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

USER $APP_UID
ENTRYPOINT ["dotnet", "BlitzBridge.McpServer.dll", "--transport", "http"]
