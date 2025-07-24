# Builder Container
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /app
ARG PROJECT_PATH="./Src/APIGateway.Proxy/APIGateway.Proxy.csproj"

ARG BUILD_CONFIGURATION=Release

COPY . .
RUN dotnet restore $PROJECT_PATH --no-cache --verbosity normal --runtime linux-musl-x64
RUN dotnet publish $PROJECT_PATH --configuration $BUILD_CONFIGURATION --output /app/publish --no-self-contained --no-restore --runtime linux-musl-x64 /p:DebugTyp="None" /p:DebugSymbol=false

# Runtime Container
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Install Timezone and culture to alpine
RUN apk add --no-cache tzdata

# Install cultures (same approach as Alpine SDK image)
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false


WORKDIR /app
COPY --from=builder /app/publish ./

# Add a readonly user and set it as default
RUN adduser \
  --disabled-password \
  --home /app \
  --gecos '' 1000 \
  && chown -R 1000 /app
USER 1000

# Expose ports
ENV ASPNETCORE_URLS="http://+:80;http://+:8080"
EXPOSE 80
EXPOSE 8080

ENTRYPOINT ["dotnet", "APIGateway.Proxy.dll"]