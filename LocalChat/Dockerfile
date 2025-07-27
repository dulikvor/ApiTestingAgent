# Build React app
FROM node:20 as build
WORKDIR /usr/src/app
COPY . .
RUN npm install && npm run build

# Serve static build with a lightweight server
FROM node:20-slim
WORKDIR /usr/src/app
COPY --from=build /usr/src/app/dist ./dist
RUN npm install -g serve
EXPOSE 3000
CMD ["serve", "-s", "dist", "-l", "3000"]
