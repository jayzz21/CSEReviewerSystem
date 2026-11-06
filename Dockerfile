FROM node:22-bookworm-slim AS styles
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY CseReviewer/Styles/ CseReviewer/Styles/
COPY CseReviewer/wwwroot/ CseReviewer/wwwroot/
RUN npm run css

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY CseReviewer/CseReviewer.csproj CseReviewer/
RUN dotnet restore CseReviewer/CseReviewer.csproj
COPY CseReviewer/ CseReviewer/
COPY --from=styles /src/CseReviewer/wwwroot/app.css CseReviewer/wwwroot/app.css
RUN dotnet publish CseReviewer/CseReviewer.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out .
COPY deploy/appsettings.json ./appsettings.json
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "CseReviewer.dll"]
