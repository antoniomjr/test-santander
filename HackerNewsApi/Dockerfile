FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["HackerNewsApi/HackerNewsApi.csproj", "HackerNewsApi/"]
RUN dotnet restore "HackerNewsApi/HackerNewsApi.csproj"
COPY . .
WORKDIR "/src/HackerNewsApi"
RUN dotnet build "HackerNewsApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HackerNewsApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HackerNewsApi.dll"]
