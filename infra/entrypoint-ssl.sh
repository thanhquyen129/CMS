#!/bin/sh
CERT_FILE="/etc/letsencrypt/live/cms.lsslogistics.vn/fullchain.pem"
if [ -f "$CERT_FILE" ]; then
    echo "SSL certificate found at $CERT_FILE, enabling HTTPS config..."
    cp /etc/nginx/ssl.conf.available /etc/nginx/conf.d/cms-ssl.conf
else
    echo "No SSL certificate found at $CERT_FILE, running HTTP-only mode."
    rm -f /etc/nginx/conf.d/cms-ssl.conf
fi
