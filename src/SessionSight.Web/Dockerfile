# SessionSight Web Frontend Dockerfile
# Multi-stage build for React + Vite app

FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY src/SessionSight.Web/package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY src/SessionSight.Web/ ./

# Build for production
RUN npm run build

# Production stage with nginx
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Remove default nginx static files
RUN rm -rf ./*

# Copy built app
COPY --from=build /app/dist ./

# Copy custom nginx config template for SPA routing
COPY src/SessionSight.Web/nginx.conf /etc/nginx/templates/default.conf.template

# Default API URL (override via environment variable in Container Apps)
ENV API_URL=http://localhost:8080

EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:80/health || exit 1

# nginx:alpine uses envsubst to process templates in /etc/nginx/templates/
CMD ["nginx", "-g", "daemon off;"]
