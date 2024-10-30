FROM node:22-alpine AS runner

ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME/bin:$PATH"

# hadolint ignore=DL3018
RUN apk update && \
  apk upgrade && \
  apk add --update --no-cache tzdata && \
  cp /usr/share/zoneinfo/Asia/Tokyo /etc/localtime && \
  echo "Asia/Tokyo" > /etc/timezone && \
  apk del tzdata && \
  corepack enable

WORKDIR /app

COPY pnpm-lock.yaml ./

RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm fetch

COPY package.json tsconfig.json ./
COPY src src

RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm install --frozen-lockfile --offline

ENV NODE_ENV=production
ENV API_PORT=80
ENV GITHUB_USER_MAP_FILE_PATH=/data/github-user-map.json
ENV MUTE_USERS_FILE_PATH=/data/mute-users.json

VOLUME [ "/data" ]

ENTRYPOINT [ "pnpm", "start" ]
