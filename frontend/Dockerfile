FROM node:20-alpine
WORKDIR /app

COPY frontend/package*.json ./
RUN npm install --no-audit --no-fund

COPY frontend ./

ENV VITE_API_BASE_URL=http://localhost:8080
EXPOSE 5173

CMD ["npm", "run", "dev"]
