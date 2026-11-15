# Multi-stage build for Catalog.Api
# Stage 1 — Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore
COPY Services/Catalog/Catalog.Domain/Catalog.Domain.csproj Services/Catalog/Catalog.Domain/
COPY Services/Catalog/Catalog.Dal/Catalog.Dal.csproj         Services/Catalog/Catalog.Dal/
COPY Services/Catalog/Catalog.Bll/Catalog.Bll.csproj         Services/Catalog/Catalog.Bll/
COPY Services/Catalog/Catalog.Api/Catalog.Api.csproj         Services/Catalog/Catalog.Api/
RUN dotnet restore Services/Catalog/Catalog.Api/Catalog.Api.csproj

# Copy source and build
COPY Services/Catalog/ Services/Catalog/
WORKDIR /src/Services/Catalog/Catalog.Api
RUN dotnet publish -c Release -o /app/publish

# Stage 2 — Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
