# Build context, çözümün kökü (Frontend/) olmalı — client, workspace'teki shared-ui
# kütüphanesinin KAYNAĞINA (dist'ine değil, bkz. tsconfig.json paths) doğrudan bağımlı:
# `docker build -f projects/client/Dockerfile .` (Frontend/ içinden).
#
# Angular Universal (SSR) kullanıldığı için statik bir nginx sunumu YETERLİ DEĞİL —
# runtime aşaması da Node çalıştırıyor (dist/client/server/server.mjs).

FROM node:24-slim AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npx ng build client --configuration production

FROM node:24-slim AS runtime
WORKDIR /app
ENV NODE_ENV=production
# server.ts'teki (SSRF/Host-header savunması) allowedHosts kontrolü için — üretimde
# gerçek host adı(ları) burada verilmeli, yoksa sadece kendi dinleme portu kabul edilir.
ENV SSR_ALLOWED_HOSTS=""
ENV PORT=4000
EXPOSE 4000

COPY --from=build /app/dist/client ./dist/client
CMD ["node", "dist/client/server/server.mjs"]
