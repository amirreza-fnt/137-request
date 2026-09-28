# خلاصهٔ کامل مکالمه — سرویس ۱۳۷ (Request Service) و کارتابل

**هدف این سند:** انتقال تمام زمینهٔ چت به یک گفتگوی جدید — بدون نیاز به خواندن تاریخچهٔ قبلی.

**تاریخ تقریبی کار:** ۲۴–۲۸ سپتامبر ۲۰۲۶  
**سرور اصلی API:** `192.168.1.12` (AlmaLinux، nginx، systemd `requestservice`)  
**Issabel / Asterisk:** `192.168.1.70`  
**Files service:** `https://192.168.1.13:6001` (همچنین `https://storage.sabzevar.ir`)  
**دامنهٔ جدید API:** `https://apisrv-137service.sabzevar.ir` (پورت عمومی 443 از طریق گیت‌وی)  
**دامنهٔ قدیمی (هنوز در برخی فایل‌ها):** `apiweb-137request.sabzevar.ir` (قبلاً اغلب `:5007`)  
**SSO:** `https://apiweb-loginsso.sabzevar.ir`  
**Kestrel:** `http://127.0.0.1:5006`  
**کلید API داخلی (dev):** `dev-internal-key-137`

---

## ۱. معماری و جریان‌ها

### ۱.۱ Request Service (این ریپو)

| مورد | مقدار |
|------|--------|
| مسیر deploy | `/opt/requestservice` |
| سورس روی سرور | `/opt/137-request-src` |
| systemd | `requestservice.service` |
| env | `/etc/requestservice.env` |
| محیط | `ASPNETCORE_ENVIRONMENT=Development` (Swagger فعال) |

**احراز هویت API:**

- `X-Api-Key` — سرویس داخلی / تلفنی / کارتابل / Issabel bridge
- `Authorization: Bearer <token>` — شهروند/اپراتور؛ اعتبارسنجی با `GET {Sso:BaseUrl}/api/auth/me`

**کانال `PhoneCall`:** ثبت درخواست از Issabel bridge با `POST /api/v1/requests` + `fileIds` از سرویس فایل.

### ۱.۲ کارتابل (`/kartabl/`)

- UI: `src/RequestService.Api/wwwroot/kartabl/index.html`
- Api-Key هاردکد در JS: `dev-internal-key-137`
- **سابقه تماس:** `GET /api/v1/phone-calls` → فقط رکوردهای `PhoneCall` در DB
- **صف زنده:** `GET /api/v1/phone-calls/live` → IssabelBridge (AMI)
- **WebRTC:** JsSIP به `wss://{origin}/asterisk-wss/ws` (ترجیح same-origin)؛ nginx باید به `https://192.168.1.70:8089/ws` پروکسی کند
- اگر WebRTC قطع باشد → جواب تماس به **MicroSIP 2001** (fallback)، نه 2101 مرورگر

### ۱.۳ Issabel Bridge (ریپو جدا: `137-IssabelBridge`)

| مورد | مسیر |
|------|------|
| کانفیگ | `/etc/asterisk/137-bridge.php` |
| کد AGI | `/var/lib/asterisk/agi-bin/137_bridge.php` |
| لاگ submit | `/var/log/asterisk/137-submit.log` |

**جریان ثبت تماس (مهم برای کارتابل):**

1. ضبط MixMonitor → `137-on-record-end.php`
2. `sox` → آپلود به files (`prepare-upload` + `upload`) → **۲۰۰**
3. `POST http://192.168.1.12:5006/api/v1/requests` با `channel: PhoneCall`, `fileIds`, `citizen`
4. request-service → coding-service برای `trackingCode` → ذخیره DB
5. کارتابل در جدول سابقه نمایش می‌دهد

**نکته:** آپلود موفق فایل ≠ ثبت درخواست در DB. بدون POST موفق، کارتابل خالی می‌ماند.

### ۱.۴ پخش/دانلود صوت در کارتابل

- URL نسبی: `/api/v1/demo/files/{fileId}/audio?key=dev-internal-key-137`
- کنترلر: `DemoFilesController` — چک Api-Key، چک `RequestFiles` در DB، سپس stream از files-service با JWT admin (`Files:JwtKey`)

---

## ۲. خطای اولیه کاربر (بعد از حذف پورت دامنه)

**علائم:** خطاهای `X-Api-Key` / `Bearer` / `UNAUTHORIZED` / `DEPENDENCY_UNAVAILABLE`

**تشخیص‌ها:**

| تست | نتیجه |
|-----|--------|
| `/health` روی دامنه | `Healthy` |
| SSO با `TOKEN_TEST` | 401 (طبیعی) |
| Bearer اشتباه | `Invalid or expired token` |
| `Sso__BaseUrl` خالی در env اول | SSO از appsettings قدیم |
| ویرایش فقط در `/opt/137-request-src` | بی‌اثر تا publish |

**اصلاحات انجام‌شده (روی `.12`):**

- `/etc/requestservice.env`: `Sso__BaseUrl=https://apiweb-loginsso.sabzevar.ir`, `InternalAuth__*`, `Files__*`, `Coding__*`
- بازنویسی تمیز `/opt/requestservice/appsettings.Development.json` (Python/json)
- **مشکل nginx:** `local` با `X-Api-Key` → **200**؛ دامنه → **401** (هدر به backend نمی‌رسید / بلوک اشتباه)

**در `apis.conf` اول فقط `apiweb-137request` بود، نه `apisrv-137service`.**

- اضافه شدن بلوک `server` برای `apisrv-137service.sabzevar.ir` روی 443 با:
  - `location /asterisk-wss/` → `https://192.168.1.70:8089/`
  - `location /` → `http://127.0.0.1:5006` + `Authorization` + `X-Api-Key`
- تست محلی WSS: `curl` با `Host: apisrv-137service` → **426** (درست، مثل Asterisk مستقیم)
- از مرورگر قبلاً **404** روی WSS → ترافیک گیت‌وی به `:5006` بدون مسیر `/asterisk-wss/` (API 404)

**گیت‌وی (پنل):** دامنه `apisrv-137service.sabzevar.ir`, مقصد `192.168.1.12:5006`, مسیر مستقیم `/asterisk-wss/` → `https://192.168.1.70:8089/`, WebSocket فعال.  
**راه پایدار پیشنهاد شده:** مقصد اصلی `https://192.168.1.12:443` (nginx) به‌جای مستقیم 5006.

**فایروال:** پورت **443** روی `.12` برای دسترسی LAN از ویندوز باز نبود؛ `5006` باز بود → `http://192.168.1.12:5006` از PC کار می‌کند.

**`Telephony:WssUrl` درست (بدون پورت قدیم):**

```json
"WssUrl": "wss://apisrv-137service.sabzevar.ir/asterisk-wss/ws"
```

---

## ۳. توکن SSO / Bearer

- توکن ثابت در repo نیست؛ JWT از `sso-login-service` بعد از login/OTP
- Issuer نمونه: `ShahrdariCentralAuth`, Audience: `ShahrdariCentralAuth.Client`
- برای تست روی سرور بدون `sqlcmd`/`pip`: اسکریپت `dotnet` + `Microsoft.Data.SqlClient` + `System.IdentityModel.Tokens.Jwt` برای ساخت accessToken از کاربر DB
- **Bearer با `JwtKey` فایل‌ها ≠ توکن SSO**

---

## ۴. مشکل کارتابل — درخواست جدید نمی‌آید

### ۴.۱ `Could not resolve host: apisrv-137service.sabzevar.ir` (Issabel)

- `request_base_url` روی PBX به دامنه عمومی بود؛ Issabel DNS ندارد
- **فیکس:** `/etc/asterisk/137-bridge.php`:

```php
'request_base_url' => 'http://192.168.1.12:5006',
'files_base_url'   => 'https://192.168.1.13:6001',
'api_key'          => 'dev-internal-key-137',
```

### ۴.۲ HTTP 500 روی `create request`

- علت در request-service (journal با `traceId`) — اغلب coding یا exception داخلی؛ گاهی قبل از fix کد ملی

### ۴.۳ HTTP 400 — `Citizen.NationalCode is required to allocate a tracking code`

- `CreateRequestValidator` برای `PhoneCall` کد ملی ۱۰ رقمی اجباری است
- `137_bridge.php` فقط `phoneNumber` می‌فرستاد

**فیکس در `createRequest` (بدون بلوک اضافی):**

```php
$citizen = ['nationalCode' => '0000000000'];
if ($callerPhone) {
    $citizen['phoneNumber'] = $callerPhone;
}
$body = [
    'channel'     => 'PhoneCall',
    'description' => $description,
    'fileIds'     => array_values($fileIds),
    'citizen'     => $citizen,
];
```

**خطای رایج کاربر:** بعد از اضافه کردن کد بالا، بلوک قدیمی مانده بود:

```php
if ($callerPhone) {
    $body['citizen'] = ['phoneNumber' => $callerPhone]; // حذف شود — nationalCode را پاک می‌کند
}
```

این باعث 400 متناوب و درخواست «خالی» (بدون شماره) می‌شد.

### ۴.۴ موفقیت در لاگ

```
tracking=00001 requestId=443b554b-...
tracking=00005 ...
tracking=00006 ...  (همان فایل wav — duplicate submit)
```

- `already submitted {uniqueId}` = قفل درست برای همان UID
- duplicate: چند trigger (on-record-end + cron/watch) یا UID متفاوت برای یک wav

### ۴.۵ `no usable wav yet`

- فایل wav زیر ۱KB یا هنوز flush نشده — submit رد می‌شود (مربوط به API نیست)

---

## ۵. وضعیت فعلی (انتهای چت)

| بخش | وضعیت |
|-----|--------|
| API health روی دامنه جدید | OK |
| SSO BaseUrl | `https://apiweb-loginsso.sabzevar.ir` |
| X-Api-Key از nginx محلی با Host درست | OK |
| WSS از nginx `.12` با Host apisrv | 426 OK |
| Issabel → request `5006` | OK پس از fix config |
| ثبت تماس با nationalCode | OK (مثلاً 00005) |
| WebRTC / گیت‌وی | بسته به deploy پنل؛ مسیر `/asterisk-wss` حیاتی |
| دانلود demo audio | **باز — 500 INTERNAL_ERROR** (آخرین سوال؛ نیاز به journal + تست files) |

---

## ۶. مشکل باز: `/api/v1/demo/files/{id}/audio` → 500

**مثال:**

```
https://apisrv-137service.sabzevar.ir/api/v1/demo/files/1c449b87-7959-4eb4-96ee-eedaadfbbf12/audio?key=dev-internal-key-137&download=true
→ {"code":"INTERNAL_ERROR","traceId":"0HNORMTKGNNPK:0000001A"}
```

**مسیر کد:** `DemoFilesController` → `RequestFileExistsAsync` → `FileServiceClient.StreamFileAsync` (metadata + `/download` یا `/i/{shortCode}` + JWT).

**دستورات تشخیص (روی `.12`):**

```bash
FILE=1c449b87-7959-4eb4-96ee-eedaadfbbf12

journalctl -u requestservice --since "30 min ago" --no-pager | grep -E "0HNORMTKGNNPK|Unhandled|1c449b87"

grep -E '^Files__' /etc/requestservice.env

curl -s "http://127.0.0.1:5006/api/v1/phone-calls" -H "X-Api-Key: dev-internal-key-137" | grep -i "$FILE"

BASE=https://192.168.1.13:6001
curl -sk -w "meta:%{http_code}\n" "$BASE/api/files/$FILE"
curl -sk -w "dl:%{http_code}\n" "$BASE/api/files/$FILE/download"

curl -s -D- -o /tmp/t.wav "http://127.0.0.1:5006/api/v1/demo/files/$FILE/audio?key=dev-internal-key-137&download=true" | head -20
```

**علل محتمل:** `Files__JwtKey` با files-service ناهماهنگ؛ `Files__BaseUrl` اشتباه؛ فایل در DB لینک نیست (404 نه 500)； OOM برای فایل بزرگ (MemoryStream)； خطای SQL.

---

## ۷. فایل‌ها و تنظیمات کلیدی در ریپو

| فایل | نقش |
|------|-----|
| `deploy/deploy.sh` | publish به `/opt/requestservice` |
| `deploy/nginx.conf` | نمونه apiweb + asterisk-wss (پورت 5007 در نمونه) |
| `deploy/requestservice.env.example` | الگوی env |
| `src/RequestService.Api/Controllers/PhoneCallsController.cs` | کارتابل API |
| `src/RequestService.Api/Controllers/DemoFilesController.cs` | پروکسی صوت |
| `src/RequestService.Application/Validation/CreateRequestValidator.cs` | nationalCode اجباری برای PhoneCall |
| `src/RequestService.Infrastructure/Clients/FileServiceClient.cs` | stream + JWT |
| `src/RequestService.Api/wwwroot/kartabl/index.html` | UI + WebRTC |

**ریپو Issabel (جدا):** `137-IssabelBridge/agi-bin/137_bridge.php` — در workspace فعلی ممکن است فقط روی سرور Issabel ویرایش شده باشد؛ نسخهٔ درست `createRequest` بدون overwrite `citizen` در همان ریپو اعمال شده است.

---

## ۸. دستورات مفید تکراری

```bash
# سرویس
systemctl restart requestservice
journalctl -u requestservice -f

# health
curl -s http://127.0.0.1:5006/health

# لیست تماس‌ها
curl -s http://127.0.0.1:5006/api/v1/phone-calls -H "X-Api-Key: dev-internal-key-137"

# nginx
nginx -t && systemctl reload nginx
grep -n "apisrv-137service\|asterisk-wss\|5006" /etc/nginx/conf.d/apis.conf

# Issabel
tail -f /var/log/asterisk/137-submit.log
php -r 'print_r(require "/etc/asterisk/137-bridge.php");'

# publish
cd /opt/137-request-src && sudo bash deploy/deploy.sh
```

---

## ۹. چک‌لیست برای چت جدید

1. آیا مشکل **احراز هویت** است؟ → local `:5006` vs دامنه؛ env `InternalAuth`؛ nginx هدرها
2. آیا **WebRTC/WSS** است؟ → 426/404/502 روی `/asterisk-wss/ws`؛ گیت‌وی vs nginx `.12`
3. آیا **کارتابل خالی** است؟ → `137-submit.log`؛ `request_base_url`؛ `nationalCode` در bridge؛ coding `5021`
4. آیا **صوت پخش/دانلود** است؟ → `DemoFilesController` + `Files__*` + journal traceId
5. آیا **duplicate tracking** است؟ → چند AGI/cron برای یک تماس

---

## ۱۰. IP/سرویس‌های وابسته

| سرویس | آدرس |
|--------|------|
| request-service | `127.0.0.1:5006` / `apisrv-137service.sabzevar.ir` |
| SSO | `apiweb-loginsso.sabzevar.ir` |
| coding | `http://192.168.1.12:5021` |
| files | `https://192.168.1.13:6001`, `storage.sabzevar.ir` |
| Issabel AMI/WSS | `192.168.1.70` (8089 WSS) |
| SQL | `185.255.91.242,2019` DB `apiweb-137request` |
| Bridge HTTP | `https://192.168.1.70/137-bridge` (Telephony:BridgeBaseUrl) |

---

*این سند خلاصهٔ عملیاتی چت پشتیبانی/دیپلوی است؛ برای جزئیات API به `SERVICE_CATALOG.md` و `PROJECT_DOCUMENTATION.md` رجوع کنید.*
