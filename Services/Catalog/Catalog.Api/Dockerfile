# Multi-stage image for the contractor registry (catalog-api).
# Build context is the repository root:
#   docker build -t catalog-api:v1 -f Services/Catalog/Catalog.Api/Dockerfile .
# The solution targets net10.0, so the SDK and ASP.NET images are 10.0
# (the handout sample uses 8.0 for a net8 project).

# Stage 1: compile. Copy .csproj files first so restore is its own cached layer.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Services/Catalog/Catalog.Domain/Catalog.Domain.csproj", "Services/Catalog/Catalog.Domain/"]
COPY ["Services/Catalog/Catalog.Dal/Catalog.Dal.csproj", "Services/Catalog/Catalog.Dal/"]
COPY ["Services/Catalog/Catalog.Bll/Catalog.Bll.csproj", "Services/Catalog/Catalog.Bll/"]
COPY ["Services/Catalog/Catalog.Api/Catalog.Api.csproj", "Services/Catalog/Catalog.Api/"]
COPY ["Platform.ServiceDefaults/Platform.ServiceDefaults.csproj", "Platform.ServiceDefaults/"]
COPY ["Platform.EventBus/Platform.EventBus.csproj", "Platform.EventBus/"]
RUN dotnet restore "Services/Catalog/Catalog.Api/Catalog.Api.csproj"
COPY . .
WORKDIR "/src/Services/Catalog/Catalog.Api"
RUN dotnet build "Catalog.Api.csproj" -c Release -o /app/build

# Stage 2: publish the Release output without a native apphost.
FROM build AS publish
RUN dotnet publish "Catalog.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: ASP.NET runtime only. Listens on 8080.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
