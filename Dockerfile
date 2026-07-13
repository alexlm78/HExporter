# Build framework-dependent (Oracle.ManagedDataAccess.Core no es trim-safe; sin single-file aquí).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/HExporter.Cli/HExporter.Cli.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# Credenciales por env var (nunca en la imagen): HEXPORTER_Oracle__ConnectionString.
# Montar un volumen en /out para recibir el archivo exportado (--out /out/archivo.csv).
VOLUME ["/out"]

ENTRYPOINT ["dotnet", "hexporter.dll"]
