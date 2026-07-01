# Étape 1 : build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY EnergyApi.csproj ./
RUN dotnet restore EnergyApi.csproj

COPY Controllers/ ./Controllers/
COPY Models/ ./Models/
COPY Services/ ./Services/
COPY Program.cs ./
COPY appsettings*.json ./

RUN dotnet publish EnergyApi.csproj -c Release -o /out

# Étape 2 : image finale
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "EnergyApi.dll"]