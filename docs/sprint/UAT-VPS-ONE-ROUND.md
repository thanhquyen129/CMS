# UAT VPS — một vòng Bill → Close (2026-09-12)

**Host:** `http://194.233.89.26` · **Tenant:** `ops` · **Bill:** `UAT-20260912-111445`  
**Kết quả API raw (đã redact token):** `UAT-VPS-ONE-ROUND-RESULT.json`  
**Script:** `scripts/uat-vps-one-round.ps1` (cần `CMS_UAT_EMAIL` / `CMS_UAT_PASSWORD` từ host env — không commit secret)

## Mục tiêu vòng này

Chứng minh (hoặc phủ định) go-live tài chính: một Bill đi hết **Cost/Revenue → chứng từ/AP-AR → Settlement → Close**, rồi **ghi gap thật** (API vs UI), không giả định từ DoD sprint.

## Kết quả tóm tắt

| Lớp | Kết quả |
|-----|---------|
| **API (JWT login)** | **PASS** — đủ vòng đến snapshot + P&L |
| **UI (browser)** | **PARTIAL** — đọc/confirm/settle/close OK; **không** tự seed Bill→Cost→Recognize từ UI |

### Chuỗi API đã chạy (đều OK)

1. `POST /api/auth/login`
2. `POST /api/bills` → Cost create+confirm → Revenue create+confirm
3. `GET …/financial-profile` → costBA=1.100.000 · revBA=2.500.000 · profit=1.400.000
4. Document receive → line → accept → match `line_to_cost`
5. Payable/Receivable exposure → recognize
6. Payment + Collection allocate → finalize (outstanding → 0)
7. `POST /api/financial-closes` (bill, controlled) → snapshot → `GET …/pnl`

Close id `01a093d3-41a3-7501-87a9-3d15fd6a1b12` — status **locked**, snapshot v1.

### UI đã mở / quan sát

| Màn | Quan sát |
|-----|----------|
| `/login` → `/dashboard` | Login BFF OK; dashboard có totals VND sau khi có data |
| `/bills` → Bill detail | Profile + maturity + profit đúng; CTA Actualize; CTA chứng từ / thanh toán / chốt |
| `/documents` | INV UAT hiện **Đã khớp** |
| `/ap-ar` | Empty outstanding (đúng vì đã settle hết) |
| `/settlements` | Payment 1.100.000 ₫ unapplied=0 |
| `/financial-closes` | Bản chốt Bill **Đã khóa**, 1 snapshot |

---

## Gap thật (ưu tiên)

| # | Severity | Gap | Bằng chứng | Hướng xử lý đề xuất |
|---|----------|-----|------------|---------------------|
| G1 | **blocker (UI-only go-live)** → **Done** (`UI-G1-DOD.md`) | Operator **không** hoàn tất vòng từ UI: thiếu tạo Bill / Cost / Revenue / Exposure+Recognize | `/bills` không form tạo; Bill panel chỉ confirm/actualize; `/ap-ar` chỉ đọc outstanding | Thin create forms (Bill, Cost, Revenue, Exposure→Recognize) — **shipped** |
| G2 | **major** | **Khớp chứng từ** không có trên UI | Copy document detail: “Khớp… chưa có trên UI U4” | UI match thin (`/api/document-matches`) |
| G3 | **major** → **Done** (`UI-G3-DOD.md`) | **Thêm dòng chứng từ** không có trên UI | Empty state: “Thêm dòng qua API” | Form add line trên `/documents/[id]` — **shipped** |
| G4 | **minor** | List documents **không lọc `billId`** | Bill panel copy thừa nhận | API `?billId=` + UI filter |
| G5 | **minor** | `/ap-ar` **ẩn đã tất toán** (`isOutstanding` only) | Sau settle: “Không có khoản… còn dư” — khó audit lịch sử trên desk | Tab “Đã tất toán” / filter status |
| G6 | **minor** | Settlement list cột Bill = “Mở Bill”, **không hiện `billNo`** | `/settlements` text | Join/resolve billNo khi list |

### Không phải gap (đã verify)

- Money path API: confirm / match / recognize / finalize / close immutability — vòng UAT này xanh.
- Bill financial profile & profitability UI khớp số API.
- Close locked + P&L readable.
- `/health` `/ready` OK; proxy UI + `/api` OK.

---

## Definition of “vòng đủ” sau khi vá G1–G3

Người vận hành (không Postman): login → tạo/mở Bill → tạo+xác nhận Cost/Revenue → nhận+accept+match chứng từ → recognize AP/AR → thanh toán/thu tiền chốt phân bổ → chốt Bill + đọc P&L.

## Follow-up ngoài phạm vi vòng này

Write-off UI · reverse allocation UI · bank feed · period close Strict policy stress · demo seeder trên VPS nếu chưa bật (`Demo__SeedOnStartup`).
