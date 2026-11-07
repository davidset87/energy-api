# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /app

# Restore first so the layer is cached when only source files change
COPY EnergyApi.csproj ./
RUN dotnet restore EnergyApi.csproj

COPY Controllers/ ./Controllers/
COPY Models/ ./Models/
COPY Services/ ./Services/
COPY Program.cs ./
COPY appsettings*.json ./

RUN dotnet publish EnergyApi.csproj -c Release -o /out --no-restore

# Stage 2: runtime image (slim ASP.NET, no SDK)
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Run as the non-root user that ships with the aspnet image
USER $APP_UID

ENTRYPOINT ["dotnet", "EnergyApi.dll"]