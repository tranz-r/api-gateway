# api-gateway

YARP reverse proxy in front of `tranzr-moves-services`. Owns CORS for browser clients.

## Local development

Preferred: start via the moves-services compose stack (builds this repo from `../api-gateway`):

```bash
# from tranzr-moves-services
docker compose up -d gateway
```

Gateway listens on **http://localhost:5209** and proxies to the API on the host at **http://localhost:5247**.

Point frontends at the gateway:

- `NEXT_PUBLIC_API_BASE_URL=http://localhost:5209`
- `NEXT_PUBLIC_TRANZR_API_URL=http://localhost:5209`

Requires `SUPABASE_JWT_ISSUER` (typically `{SUPABASE_URL}/auth/v1`).

Or run the gateway directly:

```bash
dotnet run --project Src/APIGateway.Proxy
```

Development defaults (`appsettings.Development.json`) allow localhost frontend origins and proxy to `http://localhost:5247/`.
