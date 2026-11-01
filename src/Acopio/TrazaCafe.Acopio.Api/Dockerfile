# Contexto de build: raíz del repositorio (ver docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet restore src/Acopio/TrazaCafe.Acopio.Api/TrazaCafe.Acopio.Api.csproj
RUN dotnet publish src/Acopio/TrazaCafe.Acopio.Api/TrazaCafe.Acopio.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TrazaCafe.Acopio.Api.dll"]
