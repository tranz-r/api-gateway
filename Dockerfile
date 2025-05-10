FROM openresty/openresty:alpine

# Install JWT dependency
RUN opm get ledgetech/lua-resty-jwt

# Copy configurations
COPY nginx.conf /usr/local/openresty/nginx/conf/nginx.conf
COPY api-gateway.conf /etc/nginx/conf.d/
COPY lua/ /etc/nginx/lua/

# Health check
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost/healthz || exit 1

EXPOSE 80