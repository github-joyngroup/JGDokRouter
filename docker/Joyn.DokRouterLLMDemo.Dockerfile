FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["./Runners/Joyn.DokRouterLLMDemo", "./Runners/Joyn.DokRouterLLMDemo"]
COPY ["./Modules", "./Modules"]
COPY ["NuGet.config", "."]
RUN dotnet restore "./Runners/Joyn.DokRouterLLMDemo/Joyn.DokRouterLLMDemo.csproj" --configfile "./NuGet.config"
COPY . .
RUN dotnet build "./Runners/Joyn.DokRouterLLMDemo/Joyn.DokRouterLLMDemo.csproj" -o /app/build

FROM build AS publish
RUN dotnet publish "./Runners/Joyn.DokRouterLLMDemo/Joyn.DokRouterLLMDemo.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir /logs
RUN chmod 777 /logs

ENTRYPOINT ["dotnet", "Joyn.DokRouterLLMDemo.dll"]

#RUN ls -la /src/