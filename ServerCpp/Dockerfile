# ========================================================
# UnityRTS C++ Native Server - Multi-stage Linux Build
# ========================================================

# Stage 1: Build binaries
FROM debian:bookworm-slim AS builder

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    cmake \
    ca-certificates \
    libmariadb-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY . .

# Configure and compile using native Linux Release flags
RUN cmake -B build/linux -DCMAKE_BUILD_TYPE=Release && \
    cmake --build build/linux --config Release -j$(nproc)

# Stage 2: Minimal, secure runtime
FROM debian:bookworm-slim AS runtime

ENV DEBIAN_FRONTEND=noninteractive \
    TZ=UTC

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    mariadb-client \
    libmariadb3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Run as non-root user
RUN groupadd -r unityrts && useradd -r -g unityrts -d /app -s /usr/sbin/nologin unityrts

# Copy compiled binaries from builder stage
COPY --from=builder /src/build/linux/unity_rts_server_cpp /app/bin/unity_rts_server_cpp
COPY --from=builder /src/build/linux/unity_rts_guardian_cpp /app/bin/unity_rts_guardian_cpp

# Copy default configurations, scripts and docs
COPY config/server.env.example /app/.env.example
COPY scripts /app/scripts
COPY README.md DEPLOY.md /app/

# Setup directories and permissions
RUN mkdir -p /app/logs /app/data /app/backups && \
    cp /app/.env.example /app/.env && \
    chown -R unityrts:unityrts /app && \
    chmod +x /app/bin/* /app/scripts/*.sh

USER unityrts

# Ports:
# 8080: HTTP API (Login, Register, Matchmaking, Rooms, Leaderboard, Payments)
# 8081: WebSocket Battle Relay (Realtime lockstep frame and command synchronization)
EXPOSE 8080 8081

HEALTHCHECK --interval=15s --timeout=5s --start-period=5s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["/app/bin/unity_rts_guardian_cpp"]
CMD ["--env", "/app/.env", "--server", "/app/bin/unity_rts_server_cpp"]
