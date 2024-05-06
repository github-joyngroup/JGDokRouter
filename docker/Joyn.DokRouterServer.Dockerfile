FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ./ ./
RUN dotnet restore "./Joyn.DokRouterServer/./Joyn.DokRouterServer.csproj"
COPY . .
WORKDIR "/src/Joyn.DokRouterServer"
RUN dotnet build "./Joyn.DokRouterServer.csproj" -o /app/build

FROM build AS publish
RUN dotnet publish "./Joyn.DokRouterServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir /logs
RUN mkdir /assets
RUN chmod 777 /logs
RUN chmod 777 /assets

ENTRYPOINT ["dotnet", "Joyn.DokRouterServer.dll"]
