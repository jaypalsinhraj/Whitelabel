# syntax=docker/dockerfile:1
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Optional overrides (e.g. GitHub Actions). Empty/unset args must not become `VAR=""`
# in the shell or Vite's loadEnv() will overwrite `frontend/.env` with blanks.
ARG VITE_MSAL_CLIENT_ID
ARG VITE_MSAL_AUTHORITY
ARG VITE_MSAL_REDIRECT_URI
ARG VITE_API_SCOPE
ARG VITE_API_BASE_URL

RUN sh -c '\
  [ -z "$$VITE_MSAL_CLIENT_ID" ] && unset VITE_MSAL_CLIENT_ID || true; \
  [ -z "$$VITE_MSAL_AUTHORITY" ] && unset VITE_MSAL_AUTHORITY || true; \
  [ -z "$$VITE_MSAL_REDIRECT_URI" ] && unset VITE_MSAL_REDIRECT_URI || true; \
  [ -z "$$VITE_API_SCOPE" ] && unset VITE_API_SCOPE || true; \
  [ -z "$$VITE_API_BASE_URL" ] && unset VITE_API_BASE_URL || true; \
  exec npm run build'

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 3000
