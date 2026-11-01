# Contexto de build: raíz del repositorio (ver docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
COPY geodata/ geodata/
RUN dotnet restore src/Cumplimiento/TrazaCafe.Cumplimiento.Worker/TrazaCafe.Cumplimiento.Worker.csproj
RUN dotnet publish src/Cumplimiento/TrazaCafe.Cumplimiento.Worker/TrazaCafe.Cumplimiento.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TrazaCafe.Cumplimiento.Worker.dll"]
