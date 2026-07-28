# Build context, çözümün kökü (Frontend/) olmalı — client/Dockerfile'daki aynı gerekçe
# (shared-ui kaynağına doğrudan bağımlılık): `docker build -f projects/backoffice/Dockerfile .`
# (Frontend/ içinden).

FROM node:24-slim AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npx ng build backoffice --configuration production

FROM node:24-slim AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV SSR_ALLOWED_HOSTS=""
ENV PORT=4000
EXPOSE 4000

COPY --from=build /app/dist/backoffice ./dist/backoffice
CMD ["node", "dist/backoffice/server/server.mjs"]
