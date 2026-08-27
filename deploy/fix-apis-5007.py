#!/usr/bin/env python3
"""Replace the broken listen 5007 server block in /etc/nginx/conf.d/apis.conf"""
from pathlib import Path
import re
import shutil
from datetime import datetime

CONF = Path("/etc/nginx/conf.d/apis.conf")
stamp = datetime.now().strftime("%Y%m%d%H%M%S")
bak = CONF.with_name(f"apis.conf.broken-{stamp}")
shutil.copy2(CONF, bak)
print(f"Backup: {bak}")

NEW = """
# ===========================================
# apiweb-137request.sabzevar.ir
# ===========================================
server {
    listen 5007 ssl;
    http2 on;

    server_name apiweb-137request.sabzevar.ir;

    ssl_certificate     /etc/nginx/ssl/fullchain.crt;
    ssl_certificate_key /etc/nginx/ssl/private.key;

    location /asterisk-wss/ {
        proxy_pass https://192.168.1.70:8089/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_ssl_verify off;
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
    }

    location / {
        proxy_pass http://127.0.0.1:5006;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
""".strip() + "\n\n"

text = CONF.read_text(encoding="utf-8", errors="replace")

# Find all top-level server { ... } blocks by brace depth
blocks = []
for m in re.finditer(r"(?m)^[ \t]*server[ \t]*\{", text):
    start = m.start()
    depth = 0
    i = m.end() - 1
    while i < len(text):
        ch = text[i]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                blocks.append((start, i + 1))
                break
        i += 1

target = None
for a, b in blocks:
    chunk = text[a:b]
    if re.search(r"listen[ \t]+5007\b", chunk):
        target = (a, b)
        break

if target is None:
    raise SystemExit("ERROR: no server block with listen 5007 found")

a, b = target
# Also drop nearby section comments immediately above this server
pre = a
# include blank lines and # comments right above
while pre > 0:
    # find previous line start
    prev_nl = text.rfind("\n", 0, pre - 1)
    line_start = 0 if prev_nl < 0 else prev_nl + 1
    line = text[line_start:pre]
    if line.strip() == "" or line.strip().startswith("#"):
        pre = line_start
        if prev_nl < 0:
            break
        continue
    break

new_text = text[:pre] + NEW + text[b:]
CONF.write_text(new_text, encoding="utf-8")
print(f"Replaced listen 5007 block (chars {a}-{b}, with header from {pre})")
print("Wrote", CONF)
