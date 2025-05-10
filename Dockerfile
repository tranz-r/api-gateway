FROM openresty/openresty:1.21.4.1-4-alpine

# Install dependencies
RUN apk add --no-cache curl jq perl

# Install lua-resty-http and lua-cjson
RUN opm get ledgetech/lua-resty-http && \
    opm get openresty/lua-cjson

# Copy configs and script
COPY gateway.conf /etc/nginx/conf.d/default.conf
COPY jwt_verifier.lua /etc/nginx/lua/jwt_verifier.lua

EXPOSE 80
