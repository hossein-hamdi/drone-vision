FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /src
COPY Checker.API/Checker.API.csproj Checker.API/
COPY Checker.Application/Checker.Application.csproj Checker.Application/
COPY Checker.Domain/Checker.Domain.csproj Checker.Domain/
COPY Checker.Infrastructure/Checker.Infrastructure.csproj Checker.Infrastructure/
RUN dotnet restore Checker.API/Checker.API.csproj
COPY . .
ENV DOTNET_USE_POLLING_FILE_WATCHER=1 DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true
CMD ["dotnet", "watch", "--non-interactive", "--project", "Checker.API/Checker.API.csproj", "run", "--no-launch-profile"]

FROM development AS build
RUN dotnet publish Checker.API/Checker.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Checker.API.dll"]
