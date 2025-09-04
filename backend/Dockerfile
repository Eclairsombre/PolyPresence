# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Use the ASP.NET runtime image for running the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Créer le répertoire Assets s'il n'existe pas déjà
RUN mkdir -p /app/Assets

# Copier explicitement les assets
COPY Assets/ /app/Assets/

ENTRYPOINT ["dotnet", "backend.dll"]

