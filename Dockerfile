FROM openresty/openresty:alpine

RUN apk add --no-cache lua5.1 luarocks5.1 && \
    luarocks-5.1 install lua-resty-jwt && \
    apk del lua5.1 luarocks5.1

# Copy configurations
COPY nginx.conf /usr/local/openresty/nginx/conf/nginx.conf
COPY api-gateway.conf /etc/nginx/conf.d/
COPY lua/ /etc/nginx/lua/

# Health check
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost/healthz || exit 1

EXPOSE 80