# UAT VPS — một vòng Bill → Close (UI-only re-run 2026-09-12)

**Host:** `http://194.233.89.26` · **Tenant:** `ops` · **Bill:** `UAT-UI-20260912-161500`  
**Bill id:** `01a094e5-673f-762c-b873-b6309a707772`  
**Close id:** `01a094e9-a71a-7408-b58b-11a68c461644` (locked, snapshot v1)  
**Kết quả:** `UAT-VPS-ONE-ROUND-RESULT.json`  
**Phương thức:** **UI-only** (browser + BFF cookie) — **không** Postman / raw JWT script

## Mục tiêu vòng này

Chứng minh go-live tài chính UI: operator hoàn tất **Bill → Cost/Revenue → chứng từ/AP-AR → Settlement → Close** chỉ từ UI, không giả định từ DoD sprint.

## Kết quả tóm tắt

| Lớp | Kết quả |
|-----|---------|
| **UI (browser)** | **PASS** — đủ vòng tạo/confirm/settle/close từ UI |
| **G1–G6** | **PASS thật** (không chỉ DoD) |

### Chuỗi UI đã chạy (đều OK)

1. Login BFF → `/dashboard`
2. `/bills/new` → Bill `UAT-UI-20260912-161500`
3. Tạo Cost 1.000.000 → Xác nhận **1.100.000**
4. Tạo Revenue 2.500.000 → Xác nhận **2.500.000**
5. Profile: costBA=1.100.000 · revBA=2.500.000 · profit=**1.400.000**
6. Nhận chứng từ INV-UI… → thêm dòng → chấp nhận → khớp `line_to_cost`
7. Exposure phải trả + ghi nhận AP 1.100.000; exposure phải thu + ghi nhận AR 2.500.000
8. Thanh toán 1.100.000 → phân bổ nháp → **chốt phân bổ**
9. Thu tiền 2.500.000 → phân bổ nháp → **chốt phân bổ**
10. Mở chốt Bill (controlled) → tạo bản chốt → **Đã khóa** + P&L 2.500.000 − 1.100.000 = **1.400.000**

### Verify sau vòng

| Màn | Quan sát |
|-----|----------|
| `/settlements` | Cột Bill = **`UAT-UI-20260912-161500`** (G6 — không «Mở Bill») |
| `/ap-ar?status=settled` | AP 1.100.000 **Đã tất toán**, dư 0 |
| `/ap-ar?tab=ar&status=settled` | AR 2.500.000 **Đã tất toán**, dư 0 |
| `/financial-closes` | Bill · v1 **Đã khóa chốt** (09:18 UTC) |
| Close detail | P&L: DT 2.500.000 · CP 1.100.000 · LN 1.400.000 |

---

## Gap thật (ưu tiên)

| # | Severity | Gap | Status |
|---|----------|-----|--------|
| G1 | blocker (UI-only) | Thiếu tạo Bill/Cost/Revenue/Exposure+Recognize | **PASS thật** (`UI-G1-DOD.md`) |
| G2 | major | Khớp chứng từ UI | **PASS thật** (`UI-MATCH-DOD.md`) |
| G3 | major | Thêm dòng chứng từ UI | **PASS thật** (`UI-G3-DOD.md`) |
| G4 | minor | Lọc documents `billId` | **PASS thật** (`UI-G4-DOD.md`) |
| G5 | minor | AP/AR tab Đã tất toán | **PASS thật** (`UI-G5-DOD.md`) |
| G6 | minor | Settlement list `billNo` | **PASS thật** (`UI-G6-DOD.md`) |

### Không phải gap (đã verify UI-only)

- Money path UI: confirm / match / recognize / finalize / close immutability.
- Bill financial profile & profitability khớp số chốt.
- Close locked + P&L readable.
- `/health` `/ready` OK; proxy UI + `/api` OK.

---

## Definition of “vòng đủ”

Người vận hành (không Postman): login → tạo/mở Bill → tạo+xác nhận Cost/Revenue → nhận+accept+match chứng từ → recognize AP/AR → thanh toán/thu tiền chốt phân bổ → chốt Bill + đọc P&L.

**Vòng 2026-09-12 UI-only: đạt.**

## Follow-up ngoài phạm vi vòng này

Write-off UI · reverse allocation UI · bank feed · period close Strict policy stress · demo seeder trên VPS nếu chưa bật (`Demo__SeedOnStartup`).
