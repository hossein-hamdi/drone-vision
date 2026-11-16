FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /src
COPY Management.API/Management.API.csproj Management.API/
COPY Management.Application/Management.Application.csproj Management.Application/
COPY Management.Domain/Management.Domain.csproj Management.Domain/
COPY Management.Infrastructure/Management.Infrastructure.csproj Management.Infrastructure/
RUN dotnet restore Management.API/Management.API.csproj
COPY . .
ENV DOTNET_USE_POLLING_FILE_WATCHER=1 DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true
CMD ["dotnet", "watch", "--non-interactive", "--project", "Management.API/Management.API.csproj", "run", "--no-launch-profile"]

FROM development AS build
RUN dotnet publish Management.API/Management.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Management.API.dll"]
