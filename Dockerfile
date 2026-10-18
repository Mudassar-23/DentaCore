# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY DentaCore.sln .
COPY src/ src/
RUN dotnet restore src/DentaCore.API/DentaCore.API.csproj
RUN dotnet publish src/DentaCore.API/DentaCore.API.csproj --configuration Release --output /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DentaCore.API.dll"]
