FROM node:20-alpine AS build
WORKDIR /app

COPY frontend/package*.json ./
RUN npm install --no-audit --no-fund

COPY frontend ./
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build

FROM nginx:1.27-alpine AS runtime
WORKDIR /usr/share/nginx/html

RUN apk add --no-cache wget

COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist ./

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
