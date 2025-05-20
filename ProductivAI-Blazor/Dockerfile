# Stage 1: Build the Blazor WebAssembly app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ProductivAI.sln .
COPY ProductivAI-Blazor/ProductivAI-Blazor.csproj ProductivAI-Blazor/
COPY ProductivAI.Server/ProductivAI.Server.csproj ProductivAI.Server/

# Restore dependencies for the Blazor project
RUN dotnet restore ProductivAI-Blazor/ProductivAI-Blazor.csproj

# Copy the rest of the Blazor project files and any dependent projects
COPY ProductivAI-Blazor/. ProductivAI-Blazor/
COPY ProductivAI.Server/. ProductivAI.Server/

WORKDIR /src/ProductivAI-Blazor
RUN dotnet publish ProductivAI-Blazor.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Serve the Blazor app with Nginx
FROM nginx:alpine AS final

# Install gettext for envsubst utility
RUN apk add --no-cache gettext

# Copy build output from the publish stage
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Copy custom Nginx configuration
COPY ProductivAI-Blazor/nginx.conf /etc/nginx/conf.d/default.conf

# Copy the Blazor appsettings template (it will be in wwwroot, so copied to /usr/share/nginx/html with the rest of wwwroot)
# We rename it to .template so the original placeholder file isn't served directly.
RUN mv /usr/share/nginx/html/appsettings.Blazor.json /usr/share/nginx/html/appsettings.Blazor.json.template

# Copy the entrypoint script and make it executable
COPY ProductivAI-Blazor/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

# Expose port 80 for Nginx
EXPOSE 80

ENTRYPOINT ["/docker-entrypoint.sh"] 