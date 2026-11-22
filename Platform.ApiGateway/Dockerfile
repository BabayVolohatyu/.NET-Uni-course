# Multi-stage image for the YARP gateway.
# Build context is the repository root:
#   docker build -t gateway:v1 -f Platform.ApiGateway/Dockerfile .
# The project targets net10.0, so the images are the 10.0 SDK and ASP.NET runtime.

# Stage 1: compile. Copy .csproj files first so restore is its own cached layer.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Platform.ApiGateway/Platform.ApiGateway.csproj", "Platform.ApiGateway/"]
COPY ["Platform.ServiceDefaults/Platform.ServiceDefaults.csproj", "Platform.ServiceDefaults/"]
RUN dotnet restore "Platform.ApiGateway/Platform.ApiGateway.csproj"
COPY . .
WORKDIR "/src/Platform.ApiGateway"
RUN dotnet build "Platform.ApiGateway.csproj" -c Release -o /app/build

# Stage 2: publish the Release output without a native apphost.
FROM build AS publish
RUN dotnet publish "Platform.ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: ASP.NET runtime only. Listens on 8080.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Platform.ApiGateway.dll"]
