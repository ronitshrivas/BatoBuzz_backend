FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY gateway/BatoBuzz.Gateway/BatoBuzz.Gateway.csproj gateway/BatoBuzz.Gateway/
RUN dotnet restore gateway/BatoBuzz.Gateway/BatoBuzz.Gateway.csproj
COPY gateway/ gateway/
RUN dotnet publish gateway/BatoBuzz.Gateway/BatoBuzz.Gateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BatoBuzz.Gateway.dll"]
