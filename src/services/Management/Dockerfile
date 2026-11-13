FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Management.API/Management.API.csproj Management.API/
COPY Management.Application/Management.Application.csproj Management.Application/
COPY Management.Domain/Management.Domain.csproj Management.Domain/
COPY Management.Infrastructure/Management.Infrastructure.csproj Management.Infrastructure/
RUN dotnet restore Management.API/Management.API.csproj
COPY . .
RUN dotnet publish Management.API/Management.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Management.API.dll"]
