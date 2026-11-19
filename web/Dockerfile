# AI-Guard web önyüzü imajı (E1b). Vite build çıktısını nginx sunuyor; /api istekleri api servisine gidiyor.
# Bağlam (context) web/ klasörüdür; docker-compose.yml bunu ayarlıyor.

# ---------- Derleme ----------
FROM node:20-alpine AS build
WORKDIR /app

COPY . .

# Vite projesi S kartlarında (Şeyma) gelecek. package.json gelene kadar geçici sayfa sunuluyor; böylece
# compose bugün de dört servisi ayağa kaldırıyor ve proje eklenince bu dosyayı değiştirmek gerekmiyor.
RUN if [ -f package.json ]; then \
      npm ci && npm run build; \
    else \
      mkdir -p dist && cp docker/placeholder.html dist/index.html; \
    fi

# ---------- Sunum ----------
FROM nginx:1.27-alpine
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
