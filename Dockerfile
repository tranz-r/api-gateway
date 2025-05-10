FROM openresty/openresty:alpine

# Install dependencies as root
USER root

# Method 1: Using luarocks (most reliable)
RUN apk add --no-cache lua5.1 luarocks5.1 && \
    luarocks-5.1 install lua-resty-jwt && \
    apk del lua5.1 luarocks5.1

# Create nginx user and set permissions
RUN addgroup -S nginx && \
    adduser -S -G nginx nginx && \
    mkdir -p /var/log/nginx && \
    chown -R nginx:nginx /var/log/nginx

# Copy configurations
COPY --chown=nginx:nginx nginx.conf /usr/local/openresty/nginx/conf/nginx.conf
COPY --chown=nginx:nginx api-gateway.conf /etc/nginx/conf.d/
COPY --chown=nginx:nginx lua/ /etc/nginx/lua/

# Switch to non-root user
USER nginx

# Health check
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost/healthz || exit 1

EXPOSE 80