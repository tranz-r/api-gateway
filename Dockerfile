FROM nginx:1.25-alpine

# Install Lua dependencies
RUN apk add --no-cache lua5.1 lua5.1-dev openssl-dev

# Copy configurations
COPY nginx.conf /etc/nginx/nginx.conf
COPY api-gateway.conf /etc/nginx/conf.d/
COPY lua/ /etc/nginx/lua/

# Health check
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost/healthz || exit 1
EXPOSE 80