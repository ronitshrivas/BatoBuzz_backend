FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/BatoBuzz.Shared/BatoBuzz.Shared.csproj shared/BatoBuzz.Shared/
COPY services/feed/BatoBuzz.Feed/BatoBuzz.Feed.csproj services/feed/BatoBuzz.Feed/
RUN dotnet restore services/feed/BatoBuzz.Feed/BatoBuzz.Feed.csproj
COPY shared/ shared/
COPY services/feed/ services/feed/
RUN dotnet publish services/feed/BatoBuzz.Feed/BatoBuzz.Feed.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BatoBuzz.Feed.dll"]
