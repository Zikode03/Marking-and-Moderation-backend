FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY backend/ProsperaAssess.Application/ProsperaAssess.Application.csproj backend/ProsperaAssess.Application/packages.lock.json ./backend/ProsperaAssess.Application/
COPY backend/ProsperaAssess.Application.Contracts/ProsperaAssess.Application.Contracts.csproj backend/ProsperaAssess.Application.Contracts/packages.lock.json ./backend/ProsperaAssess.Application.Contracts/
COPY backend/ProsperaAssess.DbMigrator/ProsperaAssess.DbMigrator.csproj backend/ProsperaAssess.DbMigrator/packages.lock.json ./backend/ProsperaAssess.DbMigrator/
COPY backend/ProsperaAssess.Domain/ProsperaAssess.Domain.csproj backend/ProsperaAssess.Domain/packages.lock.json ./backend/ProsperaAssess.Domain/
COPY backend/ProsperaAssess.Domain.Shared/ProsperaAssess.Domain.Shared.csproj backend/ProsperaAssess.Domain.Shared/packages.lock.json ./backend/ProsperaAssess.Domain.Shared/
COPY backend/ProsperaAssess.EntityFrameworkCore/ProsperaAssess.EntityFrameworkCore.csproj backend/ProsperaAssess.EntityFrameworkCore/packages.lock.json ./backend/ProsperaAssess.EntityFrameworkCore/
COPY backend/ProsperaAssess.HttpApi/ProsperaAssess.HttpApi.csproj backend/ProsperaAssess.HttpApi/packages.lock.json ./backend/ProsperaAssess.HttpApi/
COPY backend/ProsperaAssess.HttpApi.Host/ProsperaAssess.HttpApi.Host.csproj backend/ProsperaAssess.HttpApi.Host/packages.lock.json ./backend/ProsperaAssess.HttpApi.Host/
RUN dotnet restore backend/ProsperaAssess.HttpApi.Host/ProsperaAssess.HttpApi.Host.csproj --locked-mode
RUN dotnet restore backend/ProsperaAssess.DbMigrator/ProsperaAssess.DbMigrator.csproj --locked-mode
COPY backend/ ./backend/
RUN dotnet publish backend/ProsperaAssess.HttpApi.Host/ProsperaAssess.HttpApi.Host.csproj -c Release --no-restore -o /out/api
RUN dotnet publish backend/ProsperaAssess.DbMigrator/ProsperaAssess.DbMigrator.csproj -c Release --no-restore -o /out/migrator

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS dbmigrator
WORKDIR /app
COPY --from=build /out/migrator ./
ENTRYPOINT ["dotnet", "ProsperaAssess.DbMigrator.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS api
RUN mkdir -p /var/prospera/keys && chown app:app /var/prospera/keys
WORKDIR /app
COPY --from=build /out/api ./
ENV ASPNETCORE_URLS=http://0.0.0.0:5080
EXPOSE 5080
ENTRYPOINT ["dotnet", "ProsperaAssess.Api.dll"]
