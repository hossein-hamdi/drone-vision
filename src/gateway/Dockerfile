FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /src
COPY Gateway.csproj ./
RUN dotnet restore Gateway.csproj
COPY . .
ENV DOTNET_USE_POLLING_FILE_WATCHER=1 DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true
CMD ["dotnet", "watch", "--non-interactive", "--project", "Gateway.csproj", "run", "--no-launch-profile"]

FROM development AS build
RUN dotnet publish Gateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8443
ENTRYPOINT ["dotnet", "Gateway.dll"]
