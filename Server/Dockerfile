FROM node:20-alpine

WORKDIR /app

COPY package*.json ./
RUN npm ci --omit=dev

COPY . .

RUN mkdir -p logs data backups

EXPOSE 8080 8081

CMD ["sh", "-c", "npm run migrate:mysql && node server.js"]
