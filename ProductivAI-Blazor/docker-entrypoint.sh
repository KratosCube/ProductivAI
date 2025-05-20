#!/bin/sh
set -e

# Default to an empty string if OPENROUTER_API_KEY is not set, to avoid envsubst errors and allow Blazor to handle missing key
export OPENROUTER_API_KEY=${OPENROUTER_API_KEY:-""}

# Substitute environment variables in the Blazor appsettings
# Corrected path for template and output to be within /usr/share/nginx/html which is the WORKDIR for wwwroot contents
envsubst '${OPENROUTER_API_KEY} ${BLAZOR_API_BASE_URL}' < /usr/share/nginx/html/appsettings.Blazor.json.template > /usr/share/nginx/html/appsettings.Blazor.json

echo "Substituted OPENROUTER_API_KEY into appsettings.Blazor.json"

# Execute the original Nginx command
exec nginx -g 'daemon off;' 