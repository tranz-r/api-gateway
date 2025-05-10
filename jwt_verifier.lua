local jwt = require "resty.jwt"
local cjson = require "cjson"
local http = require "resty.http"

local jwks_url = "https://iam.labgrid.net/realms/tranzr/protocol/openid-connect/certs"
local jwks_cache = ngx.shared.jwt_cache

-- Define allowed audience values for all Tranzr clients
local allowed_aud = {
  ["mobile-app"] = true,
  ["web-app"] = true,
  ["tranzr-api"] = true
}

local function get_jwk_pem(kid)
  local cached = jwks_cache:get(kid)
  if cached then return cached end

  local httpc = http.new()
  local res, err = httpc:request_uri(jwks_url, { method = "GET" })
  if not res or res.status ~= 200 then
    return nil, "Failed to fetch JWKS: " .. (err or res.status)
  end

  local jwks = cjson.decode(res.body)
  for _, key in ipairs(jwks.keys) do
    if key.kid == kid then
      local x5c = key.x5c[1]
      local pem = "-----BEGIN CERTIFICATE-----\n"
                  .. x5c:gsub(".{64}", "%0\n")
                  .. "\n-----END CERTIFICATE-----"
      jwks_cache:set(kid, pem, 3600)
      return pem
    end
  end

  return nil, "Key ID not found in JWKS"
end

local function validate()
  local auth_header = ngx.var.http_Authorization
  if not auth_header then return false, "Missing Authorization header" end

  local _, _, jwt_token = string.find(auth_header, "Bearer%s+(.+)")
  if not jwt_token then return false, "Malformed Authorization header" end

  local jwt_obj = jwt:load_jwt(jwt_token)
  if not jwt_obj or not jwt_obj.valid then
    return false, "Invalid JWT format"
  end

  local kid = jwt_obj.header.kid
  if not kid then return false, "Missing kid in JWT header" end

  local pem, err = get_jwk_pem(kid)
  if not pem then return false, err end

  local verified = jwt:verify(pem, jwt_token)
  if not verified.verified then return false, "JWT verification failed" end

  local claims = verified.payload

  -- Validate issuer
  if claims.iss ~= "https://iam.labgrid.net/realms/tranzr" then
    return false, "Invalid issuer: " .. (claims.iss or "none")
  end

  -- Validate audience
  if not allowed_aud[claims.aud] then
    return false, "Invalid audience: " .. (claims.aud or "none")
  end

  return true
end

return { validate = validate }