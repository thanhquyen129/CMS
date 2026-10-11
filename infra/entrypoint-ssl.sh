#!/bin/sh
enable_site() {
    cert="$1"
    src="$2"
    dest="$3"
    if [ -f "$cert" ]; then
        echo "SSL certificate found at $cert, enabling HTTPS config..."
        cp "$src" "$dest"
    else
        echo "No SSL certificate found at $cert."
        rm -f "$dest"
    fi
}

enable_site \
    /etc/letsencrypt/live/cms.lsslogistics.vn/fullchain.pem \
    /etc/nginx/ssl.conf.available \
    /etc/nginx/conf.d/cms-ssl.conf

enable_site \
    /etc/letsencrypt/live/cms.a1lplatform.com/fullchain.pem \
    /etc/nginx/ssl-a1lplatform.conf \
    /etc/nginx/conf.d/cms-a1lplatform.conf
