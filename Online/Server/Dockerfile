FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Assets/Scripts/Core/ Assets/Scripts/Core/
COPY Assets/Resources/EarlyEraContent.json Assets/Resources/DemoBalance.json Assets/Resources/
COPY Online/Server/ Online/Server/
RUN dotnet publish Online/Server/Origins.Server.csproj -c Release -o /app --no-self-contained
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
ENTRYPOINT ["dotnet", "Origins.Server.dll"]
