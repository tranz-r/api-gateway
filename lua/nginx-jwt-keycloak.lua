local jwt = require("resty.jwt")
local http = require("resty.http")

local _M = {}

function _M.auth(config)
    local token = ngx.var.http_Authorization and ngx.var.http_Authorization:match("Bearer%s+(.+)")
    if not token then return false, "Missing token" end

    -- Verify JWT signature, expiry, audience, etc.
    local jwt_obj = jwt:verify_jwt_obj(
        ngx.shared.jwt_key_cache,
        token,
        nil,  -- JWKS fetched dynamically
        {
            audience = config.client_id,
            issuer = "https://iam.labgrid.net/realms/tranzr" .. config.realm
        }
    )
    
    if not jwt_obj.verified then
        return false, jwt_obj.reason
    end
    
    return true, jwt_obj.payload
end

return _M