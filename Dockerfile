# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["StandRiseServer.csproj", "./"]
COPY ["RyzenBot/RyzenBot.csproj", "RyzenBot/"]
RUN dotnet restore "StandRiseServer.csproj"
RUN dotnet restore "RyzenBot/RyzenBot.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/"
RUN dotnet build "StandRiseServer.csproj" -c Release -o /app/build
RUN dotnet build "RyzenBot/RyzenBot.csproj" -c Release -o /app/build_bot

# Publish the application
FROM build AS publish
RUN dotnet publish "StandRiseServer.csproj" -c Release -o /app/publish /p:UseAppHost=false
RUN dotnet publish "RyzenBot/RyzenBot.csproj" -c Release -o /app/publish_bot /p:UseAppHost=false

# Final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /app/publish_bot .

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080 

ENTRYPOINT ["dotnet", "StandRiseServer.dll"]
