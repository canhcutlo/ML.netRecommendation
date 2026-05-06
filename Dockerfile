# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["MLAppHyperT/MLAppHyperT.csproj", "MLAppHyperT/"]
RUN dotnet restore "MLAppHyperT/MLAppHyperT.csproj"

COPY MLAppHyperT/ MLAppHyperT/
WORKDIR /src/MLAppHyperT
RUN dotnet publish "MLAppHyperT.csproj" -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Non-root user for security
RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MLAppHyperT.dll"]
