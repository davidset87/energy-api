# Étape 1 : build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY EnergyApi.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /out

# Étape 2 : image finale
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .


EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "EnergyApi.dll"]