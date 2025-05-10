FROM openresty/openresty:alpine

# Install dependencies (including perl for opm)
RUN apk add --no-cache perl && \
    opm get ledgetech/lua-resty-jwt && \
    apk del perl

# Copy configurations
COPY nginx.conf /usr/local/openresty/nginx/conf/nginx.conf
COPY api-gateway.conf /etc/nginx/conf.d/
COPY lua/ /etc/nginx/lua/

# Health check
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost/healthz || exit 1

EXPOSE 80