FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files maintaining the directory structure
COPY ["src/Logging/examples/AdvancedExample/Femur.Logging.AdvancedExample.csproj", "src/Logging/examples/AdvancedExample/"]
COPY ["src/Logging/Bootstrap/src/Femur.Logging.Bootstrap.csproj", "src/Logging/Bootstrap/src/"]
COPY ["src/DependencyInjection/src/Femur.DependencyInjection.csproj", "src/DependencyInjection/src/"]

# Restore dependencies
RUN dotnet restore "src/Logging/examples/AdvancedExample/Femur.Logging.AdvancedExample.csproj"

# Copy source code
COPY src/Logging/examples/AdvancedExample/. src/Logging/examples/AdvancedExample/
COPY src/Logging/Bootstrap/src/. src/Logging/Bootstrap/src/
COPY src/DependencyInjection/src/. src/DependencyInjection/src/

# Build the application
WORKDIR "/src/src/Logging/examples/AdvancedExample"
RUN dotnet build "Femur.Logging.AdvancedExample.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Femur.Logging.AdvancedExample.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set OTLP endpoint environment variable (will be overridden by docker-compose)
ENV OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317

ENTRYPOINT ["dotnet", "Femur.Logging.AdvancedExample.dll"]
