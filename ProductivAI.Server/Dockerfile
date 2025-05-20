# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ProductivAI.sln .
COPY ProductivAI.Server/ProductivAI.Server.csproj ProductivAI.Server/
COPY ProductivAI-Blazor/ProductivAI-Blazor.csproj ProductivAI-Blazor/
# Add other project files if they are dependencies of ProductivAI.Server

# Restore dependencies for all projects in the solution context
# This is beneficial if the server project has dependencies on other projects in the solution
# that aren't directly referenced but might be needed for the build process or if you build the whole solution.
# However, for a more targeted restore specifically for the server:
RUN dotnet restore ProductivAI.Server/ProductivAI.Server.csproj

# Copy the rest of the server project files
COPY ProductivAI.Server/. ProductivAI.Server/
# Copy other necessary project files if they are direct dependencies
# COPY ProductivAI-Blazor/. ProductivAI-Blazor/ # Only if server directly references Blazor project (unlikely for API)

WORKDIR "/src/ProductivAI.Server"
RUN dotnet build "ProductivAI.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProductivAI.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose port 8080 for the ASP.NET Core Kestrel server (default for ASP.NET Core in containers)
# If your app is configured to listen on a different port internally, adjust this.
EXPOSE 8080
# EXPOSE 8081 # If using HTTPS internally, but usually TLS termination is handled by a reverse proxy

# Define environment variables that might be needed (can be overridden in docker-compose)
ENV ASPNETCORE_URLS=http://+:8080
# Add other environment variables like ASPNETCORE_ENVIRONMENT=Development or Production as needed

ENTRYPOINT ["dotnet", "ProductivAI.Server.dll"] 