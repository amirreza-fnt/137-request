#!/bin/bash
# روی سرور .12 اجرا شود: bash fix-apis-5007-asterisk-wss.sh
# بلوک server پورت 5007 (apiweb-137request) را تمیز می‌نویسد + /asterisk-wss

set -euo pipefail
CONF=/etc/nginx/conf.d/apis.conf
STAMP=$(date +%Y%m%d%H%M%S)

if [[ ! -f "$CONF" ]]; then
  echo "ERROR: $CONF not found"
  exit 1
fi

cp -a "$CONF" "${CONF}.broken-${STAMP}"
echo "Backup of current (broken) file: ${CONF}.broken-${STAMP}"

# اگر بکاپ سالم قبل از خراب‌کاری هست، از آن شروع کن
GOOD=""
for f in "${CONF}.bak-"* "${CONF}.bak" ; do
  if [[ -f "$f" ]] && nginx -t -c /dev/stdin <<<"$(cat /etc/nginx/nginx.conf)" 2>/dev/null; then
    :
  fi
  if [[ -f "$f" ]]; then
    # تست ساده: نباید location تو در تو داشته باشد
    if ! grep -q 'asterisk-wss/.*location /' "$f" 2>/dev/null; then
      if grep -q 'listen 5007' "$f"; then
        GOOD="$f"
      fi
    fi
  fi
done

if [[ -n "$GOOD" ]]; then
  echo "Restoring from good backup: $GOOD"
  cp -a "$GOOD" "$CONF"
else
  echo "No known-good bak found; will patch current file in place."
fi

python3 - <<'PY'
from pathlib import Path
import re

path = Path("/etc/nginx/conf.d/apis.conf")
text = path.read_text(encoding="utf-8", errors="replace")

block = """
# ===========================================
# apiweb-137request.sabzevar.ir
# ===========================================
server {
    listen 5007 ssl;
    http2 on;

    server_name apiweb-137request.sabzevar.ir;

    ssl_certificate     /etc/nginx/ssl/fullchain.crt;
    ssl_certificate_key /etc/nginx/ssl/private.key;

    # WebRTC WSS → Issabel Asterisk (same-origin proxy)
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
"""

# حذف هر server که listen 5007 دارد (حتی اگر خراب باشد)
# الگوی غیرحریصانه بین server { ... } که داخلش listen 5007 باشد
pattern = re.compile(
    r"(?ms)^[ \t]*#[^\n]*137request[^\n]*\n)?^[ \t]*server\s*\{(?:[^{}]|\{[^{}]*\})*listen\s+5007(?:[^{}]|\{[^{}]*\})*\}",
)

# ساده‌تر: از اولین خطی که listen 5007 دارد تا قبل از بلوک بعدی referral/5009
lines = text.splitlines(keepends=True)
out = []
i = 0
replaced = False
while i < len(lines):
    line = lines[i]
    # شروع کاندید: کامنت 137request یا خود listen 5007 داخل server
    if (not replaced) and (
        "apiweb-137request" in line and "server_name" in line
        or re.search(r"listen\s+5007\b", line)
        or (line.strip().startswith("#") and "137request" in line)
    ):
        # عقب برو تا ابتدای server یا کامنت بخش
        start = i
        # اگر خودمان وسط server هستیم، به عقب تا "server {"
        j = i
        while j > 0 and not re.match(r"^\s*server\s*\{", lines[j]):
            if lines[j].strip().startswith("# ===") or "137request" in lines[j]:
                start = j
            j -= 1
            if j < i - 40:
                break
        # پیدا کردن server {
        while start > 0 and not re.match(r"^\s*server\s*\{", lines[start]) and "137request" not in lines[start]:
            start -= 1
        # اگر روی کامنت بخش هستیم، از همان؛ اگر روی server، از server
        while start > 0 and lines[start - 1].strip().startswith("#") and "137" in lines[start - 1]:
            start -= 1
        # جلو برو از start تا ببندیم یا به 5009/referral برسیم
        # بهتر: از اولین server { نزدیک
        k = start
        while k < len(lines) and not re.match(r"^\s*server\s*\{", lines[k]):
            k += 1
        if k >= len(lines):
            out.append(line)
            i += 1
            continue
        # از k عمق brace را بشمار
        depth = 0
        end = k
        while end < len(lines):
            depth += lines[end].count("{") - lines[end].count("}")
            end += 1
            if depth == 0:
                break
        # کامنت‌های قبل از server را هم حذف کن اگر مربوط به 137request است
        pre = k
        while pre > start and (lines[pre - 1].strip() == "" or lines[pre - 1].strip().startswith("#")):
            pre -= 1
        # فقط اگر این server واقعاً 5007 است
        chunk = "".join(lines[k:end])
        if "listen 5007" in chunk or "listen\t5007" in chunk or re.search(r"listen\s+5007", chunk):
            # خطوط قبل از pre را نگه دار
            out.extend(lines[0:pre] if not out and i == 0 else [])
            # اگر out از قبل پر شده، فقط از pre به بعد را جایگزین می‌کنیم با رویکرد دیگر
            # ساده‌سازی: کل فایل را دوباره بساز
            replaced = True
            new_lines = lines[:pre] + [block if block.endswith("\n") else block + "\n"] + lines[end:]
            # اگر قبل از pre خطوط خالی زیاد است اوکی
            path.write_text("".join(new_lines), encoding="utf-8")
            print(f"Replaced 5007 server block (lines {pre+1}-{end})")
            break
        else:
            out.append(line)
            i += 1
            continue
    else:
        i += 1
else:
    if not replaced:
        # روش جایگزین: با regex کل server دارای 5007
        def find_server_blocks(s):
            res = []
            for m in re.finditer(r"(?m)^\s*server\s*\{", s):
                start = m.start()
                depth = 0
                i = m.end() - 1
                while i < len(s):
                    if s[i] == "{":
                        depth += 1
                    elif s[i] == "}":
                        depth -= 1
                        if depth == 0:
                            res.append((start, i + 1))
                            break
                    i += 1
            return res

        blocks = find_server_blocks(text)
        found = None
        for a, b in blocks:
            chunk = text[a:b]
            if re.search(r"listen\s+5007\b", chunk):
                found = (a, b)
                break
        if not found:
            raise SystemExit("ERROR: could not find listen 5007 server block")
        a, b = found
        # کامنت‌های بالای بلاک را هم بردار
        pre = a
        while pre > 0 and text[pre - 1] in "\n\t ":
            pre -= 1
        # چند خط کامنت قبل
        line_start = text.rfind("\n", 0, a) + 1
        header = text[max(0, a - 400):a]
        # اگر بلافاصله قبل کامنت 137request است، از اول آن
        m = re.search(r"(?ms)(?:^|\n)((?:#[^\n]*\n)+)\s*$", text[:a])
        if m and "137" in m.group(1):
            a2 = m.start(1)
            if text[a2:a2+1] == "\n":
                a2 += 1
            a = a2
        new_text = text[:a] + block.strip() + "\n\n" + text[b:]
        path.write_text(new_text, encoding="utf-8")
        print("Replaced 5007 server block via brace parser")
        replaced = True

if not replaced:
    raise SystemExit("ERROR: replace failed")
print("OK: wrote", path)
PY

echo "----- nginx -t -----"
nginx -t
echo "----- reload -----"
systemctl reload nginx
echo "----- probe WSS proxy -----"
curl -vk https://127.0.0.1:5007/asterisk-wss/ws -H "Host: apiweb-137request.sabzevar.ir" 2>&1 | grep -E 'HTTP/|426|502|404|Connected' | head -20
echo "DONE"
