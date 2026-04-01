# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Whitelabel.sln ./
COPY src/ ./src/

RUN dotnet publish src/Whitelabel.Api/Whitelabel.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 5000
ENTRYPOINT ["dotnet", "Whitelabel.Api.dll"]
