# Utilisation de l'image officielle .NET 7 SDK
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

# Définition du répertoire de travail
WORKDIR /app

# Copie et restauration des dépendances
COPY *.csproj ./
RUN dotnet restore

# Copie du reste du code et compilation
COPY . . 
RUN dotnet publish -c Release --self-contained false --no-restore -o /app/out

# Utilisation d'une image runtime plus légère pour l'exécution
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime

# Définition du répertoire de travail
WORKDIR /app

# Copie de l'application compilée depuis l'étape de build
COPY --from=build /app/out ./

# Exposition des ports si nécessaire (pas obligatoire pour un worker)
EXPOSE 5000

# Définition de la commande de démarrage
CMD ["dotnet", "Worker.dll"]
