#!/bin/sh
# Renew Let's Encrypt certs via webroot, then reload nginx if it is running.
set -eu
docker run --rm \
  -v /etc/letsencrypt:/etc/letsencrypt \
  -v /var/www/certbot:/var/www/certbot \
  certbot/certbot renew --webroot -w /var/www/certbot --quiet
if docker ps --format '{{.Names}}' | grep -qx cms-proxy; then
  docker exec cms-proxy nginx -s reload
fi
