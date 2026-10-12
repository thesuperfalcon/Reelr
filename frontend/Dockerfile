FROM node:26-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . ./
RUN npm run build

# The unprivileged image runs nginx as a non-root user, so it listens on 8080 instead of 80.
FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime
# On start the image's entrypoint fills in the BACKEND_ variables and writes the result to conf.d/default.conf.
# BACKEND_URL is the API: the backend service in Docker Compose, the backend app's internal address in Azure.
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
ENV BACKEND_URL=http://backend:8080 \
    NGINX_ENVSUBST_FILTER=^BACKEND_
COPY security-headers.conf /etc/nginx/security-headers.conf
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 8080
