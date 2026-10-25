FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY backend/ ./backend/
RUN dotnet publish backend/ProsperaAssess.HttpApi.Host/ProsperaAssess.HttpApi.Host.csproj -c Release -o /out/api
RUN dotnet publish backend/ProsperaAssess.DbMigrator/ProsperaAssess.DbMigrator.csproj -c Release -o /out/migrator

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS dbmigrator
WORKDIR /app
COPY --from=build /out/migrator ./
ENTRYPOINT ["dotnet", "ProsperaAssess.DbMigrator.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
COPY --from=build /out/api ./
ENV ASPNETCORE_URLS=http://0.0.0.0:5080
EXPOSE 5080
ENTRYPOINT ["dotnet", "ProsperaAssess.Api.dll"]
