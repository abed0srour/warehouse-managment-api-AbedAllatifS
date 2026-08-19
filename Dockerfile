FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy scprojfiles into build env
COPY ["src/Warehouse.Presentation/Warehouse.Presentation.csproj", "src/Warehouse.Presentation/"]
COPY ["src/Warehouse.Application/Warehouse.Application.csproj", "src/Warehouse.Application/"]
COPY ["src/Warehouse.Domain/Warehouse.Domain.csproj", "src/Warehouse.Domain/"]
COPY ["src/Warehouse.Infrastructure/Warehouse.Infrastructure.csproj", "src/Warehouse.Infrastructure/"]

RUN dotnet restore "src/Warehouse.Presentation/Warehouse.Presentation.csproj"

COPY . .

# Build Presentation project
WORKDIR "/src/src/Warehouse.Presentation"
RUN dotnet build "Warehouse.Presentation.csproj" 

FROM build AS publish
RUN dotnet publish "Warehouse.Presentation.csproj" 

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Warehouse.Presentation.dll"]