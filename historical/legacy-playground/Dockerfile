FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

# Copy everything
COPY . .

# Restore
RUN dotnet restore src/RetailStoreMonolith.fsproj

# Run CanonFlow (generate types from DB schema)
RUN dotnet fsi canonflow.fsx

# Run FsAssay (scan all files)
# Assuming FsAssay.Runner is in the parent repo, for Docker we might need to mount it or skip it if it's not self-contained. 
# For now we will just build the project to ensure CanonFlow output compiles.
RUN dotnet build src/RetailStoreMonolith.fsproj -c Release

# Publish
RUN dotnet publish src/RetailStoreMonolith.fsproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RetailStoreMonolith.dll"]
