FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/BatoBuzz.Shared/BatoBuzz.Shared.csproj shared/BatoBuzz.Shared/
COPY BatoBuzz.Api/BatoBuzz.Api.csproj BatoBuzz.Api/
RUN dotnet restore BatoBuzz.Api/BatoBuzz.Api.csproj
COPY shared/ shared/
COPY BatoBuzz.Api/ BatoBuzz.Api/
RUN dotnet publish BatoBuzz.Api/BatoBuzz.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BatoBuzz.Api.dll"]
