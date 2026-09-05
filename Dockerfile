# =============================================================================
#  SEMS - Backend (ASP.NET Core 8)
#
#  Construccion en dos etapas: la imagen final solo lleva el runtime, no el SDK
#  ni el codigo fuente. Pasa de unos 800 MB a unos 220 MB.
# =============================================================================

# ------------------------------------------------------------ etapa de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primero solo los archivos de proyecto: mientras no cambien las dependencias,
# Docker reutiliza la capa del restore y la build tarda segundos en vez de minutos.
COPY SemsBackend.sln ./
COPY src/Sems.Api/Sems.Api.csproj src/Sems.Api/
COPY tests/Sems.Api.Tests/Sems.Api.Tests.csproj tests/Sems.Api.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/Sems.Api/Sems.Api.csproj -c Release -o /app --no-restore

# ---------------------------------------------------------- etapa de runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Usuario sin privilegios: si alguien logra ejecutar codigo dentro del
# contenedor, no lo hace como root.
RUN adduser --disabled-password --gecos "" --uid 1001 sems
USER sems

COPY --from=build --chown=sems:sems /app ./

# El proveedor de hosting inyecta PORT; 8080 es el valor por defecto.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Sems.Api.dll"]
