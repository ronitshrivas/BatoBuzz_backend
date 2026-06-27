FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/BatoBuzz.Shared/BatoBuzz.Shared.csproj shared/BatoBuzz.Shared/
COPY services/identity/BatoBuzz.Identity/BatoBuzz.Identity.csproj services/identity/BatoBuzz.Identity/
RUN dotnet restore services/identity/BatoBuzz.Identity/BatoBuzz.Identity.csproj
COPY shared/ shared/
COPY services/identity/ services/identity/
RUN dotnet publish services/identity/BatoBuzz.Identity/BatoBuzz.Identity.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BatoBuzz.Identity.dll"]
