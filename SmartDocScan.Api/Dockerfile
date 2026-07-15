FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SmartDocScan.Api/SmartDocScan.Api.csproj SmartDocScan.Api/
RUN dotnet restore SmartDocScan.Api/SmartDocScan.Api.csproj

COPY SmartDocScan.Api/ SmartDocScan.Api/
RUN dotnet publish SmartDocScan.Api/SmartDocScan.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y ghostscript && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SmartDocScan.Api.dll"]
