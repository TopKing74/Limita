# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first to leverage Docker layer caching
COPY Limita.sln ./
COPY src/Limita.Domain/Limita.Domain.csproj src/Limita.Domain/
COPY src/Limita.Application/Limita.Application.csproj src/Limita.Application/
COPY src/Limita.Infrastructure/Limita.Infrastructure.csproj src/Limita.Infrastructure/
COPY src/Limita.Persistence/Limita.Persistence.csproj src/Limita.Persistence/
COPY src/Limita.API/Limita.API.csproj src/Limita.API/

# Restore dependencies for Limita.API
RUN dotnet restore src/Limita.API/Limita.API.csproj

# Copy the rest of the application source code
COPY src/ src/

# Build and publish Limita.API in Release configuration
RUN dotnet publish src/Limita.API/Limita.API.csproj -c Release -o /app/publish --no-restore

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

# Expose HTTP port
EXPOSE 8080

# Configure entrypoint
ENTRYPOINT ["dotnet", "Limita.API.dll"]
