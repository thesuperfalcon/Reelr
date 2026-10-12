FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend.csproj dotnet-tools.json ./
RUN dotnet restore backend.csproj && dotnet tool restore

COPY . ./
RUN dotnet publish backend.csproj --configuration Release --no-restore --output /app

# The migrations as one program that brings a database up to date and then exits. Already applied
# migrations are skipped, so it is safe to run on every deploy.
FROM build AS bundle
RUN dotnet ef migrations bundle --configuration Release --output /efbundle

# Run before the app on each deploy (the "migrate" service in Docker Compose). Its connection string,
# ConnectionStrings__ReelrContext, is the one account allowed to change the schema.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations
WORKDIR /migrations
COPY --from=bundle /efbundle ./efbundle
USER app
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# The aspnet image listens on port 8080 and runs as the non-root "app" user.
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "backend.dll"]
