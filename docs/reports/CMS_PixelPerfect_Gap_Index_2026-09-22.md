# Chỉ mục Pixel-perfect còn thiếu — CMS / LCMS

**Ngày:** 2026-09-22  
**Phạm vi:** toàn sản phẩm (UI-01…UI-15 + domain MASTER + cross-cutting)  
**Nguồn đối chiếu:** `docs/po/LCMS_UIUX_Mockup_Package_v1.0/` (PO APPROVED), HTML mockup UI-01…03, MASTER 01–09, ADR-0023…0030, handoff gói A–H, `_extract_ui_trace.txt`, ~86 route `apps/web`.

---

## 0. Định nghĩa & verdict

### Pixel-perfect (CMS) nghĩa là gì
| Tiêu chí | Bắt buộc |
|---|---|
| Bố cục / hierarchy khớp mockup PO APPROVED (không lệch khối chính) | Có |
| Nhãn tiếng Việt CP6.5; không lộ enum/Canonical English | Có |
| Data & CTA thật (API domain); empty / loading / error rõ | Có |
| Filter / cột / KPI / drawer / form đủ độ sâu vận hành | Có |
| UAT có evidence (screenshot + UX-01…14) | Có |

### Không tính “thiếu Pixel-perfect”
- Widget trang trí / % MoM / checkbox bulk / “Xuất Excel” giả khi chưa có luồng (honest UI — đã khóa trên UI-02/04/05).
- TMS (GPS, e-POD, xe trống).
- Budget/Forecast (P13) — ngoài A–H; theo dõi riêng.
- Party merge (P1) — theo dõi riêng.

### Verdict ngắn
| Lớp | Trạng thái |
|---|---|
| **Gói A–H (MASTER depth W-A…H)** | **DONE / ship production** — domain + màn thật; **không** = UAT pixel |
| **Mockup fidelity UX-01…14** | **Chưa kiểm thử** (extract UI trace) |
| **UI maturity thực tế** | ~1 `PIXEL_OK` · đa số `STRUCTURAL` · vài `SKELETON` · không thiếu module cấp 1 |
| **09B workbook** | DEV tự đánh DONE; Reviewer vẫn «Chờ review» |

> Claim “Pixel-perfect” trong chat A–H = **đủ W-item vận hành được**, không phải nghiệm thu pixel từng PNG.

---

## 1. Thang maturity

| Mã | Ý nghĩa |
|---|---|
| `PIXEL_OK` | Khớp mockup chính + data thật; lệch nhỏ/có chủ đích |
| `STRUCTURAL` | List kit / form / API đủ dùng; chưa 1:1 PNG/HTML; thiếu chrome hoặc UAT |
| `SKELETON` | Route có, bảng/form mỏng, CTA lệch mockup |
| `API_ONLY` | Backend đủ; **không** có màn UI tương ứng |
| `MISSING` | Chưa có route/luồng |
| `INTENTIONAL` | Cố ý không làm (honest UI / chưa có luồng) |
| `OUT` | Ngoài scope go-live hiện tại (ADR / backlog P) |

---

## 2. Tổng hợp theo UI-01 … UI-15

| UI | Màn chính | Route | Mockup | Maturity | Gap Pixel-perfect chính | Gói gốc |
|---|---|---|---|---|---|---|
| **01** | Dashboard V2 | `/dashboard` | PNG + HTML | `PIXEL_OK` | Chart 12 tháng chỉ tháng hiện tại có số thật; thiếu UAT UX-01 | — |
| **02** | DS Bill + Financial View | `/bills` + drawer | PNG + HTML | `STRUCTURAL` | Drawer tabs lệch nhẹ (có «Vận đơn», thiếu «Tính giá» trên drawer); không Excel; chưa UAT | B |
| **02** | Hồ sơ Bill | `/bills/[id]` | PNG panel | `STRUCTURAL` | Inline edit mockup mỏng; tab Rating có | A–C |
| **02** | Tạo Bill / Order / Shipment | `/bills/new`, `/orders/new`, `/shipments/new` | HTML riêng | `STRUCTURAL` | Không lưới kiện; picker người phụ trách mỏng | A–B |
| **02** | DS Order / Shipment | `/orders`, `/shipments` | HTML subnav | `STRUCTURAL` | Cùng kit Bill; không checkbox/% MoM (`INTENTIONAL`) | B |
| **02** | Chặng / Chuyến | `/operations`, `/operations/[kind]/[id]`, `/operations/legs|movements/new` | Không PNG riêng | `STRUCTURAL` | CreateWorkspace có; unlink K3 blocked | B |
| **03** | DS bảng giá | `/rate-cards` | PNG + HTML-01 | `STRUCTURAL` | Thiếu «Bộ lọc khác», drawer Excel-like; weight-break không trong panel list | C |
| **03** | Tính giá | `/rate-cards/rate` | HTML-02 | `STRUCTURAL` | Cần UAT 2-cột + breakdown | C |
| **03** | So sánh giá | `/rate-cards/compare` | HTML-03 | `STRUCTURAL` | Form-only vs CTA «Tạo so sánh mới» mockup | C |
| **03** | Phụ phí | `/rate-cards/surcharges` | HTML-04 | `SKELETON` | List mỏng; CTA → `/rate-cards/new` (không form phụ phí); thiếu KPI/filter | C |
| **03** | Tỷ giá | `/rate-cards/fx` (+ `/admin/fx-rates`) | HTML-05 | `STRUCTURAL` | Trùng admin FX; đủ Stat+upsert | C |
| **03** | Phụ lục giá | `/rate-cards/appendices` | HTML-06 | `SKELETON` | List version; CTA tạo → list rate-cards; thiếu wizard | C |
| **03** | Lịch sử giá | `/rate-cards/history` | HTML-07 | `STRUCTURAL` | Thiếu «Xuất lịch sử», filter thời gian dày | C |
| **03** | Import bảng giá | — | (MASTER) | `API_ONLY` | `POST /api/rate-imports/preview\|commit` — **không màn** | C |
| **04** | DS / detail chi phí | `/costs`, `/costs/[id]` | PNG | `STRUCTURAL` | Subnav Duyệt/Đối soát/theo Shipment/Chuyến/BC = `INTENTIONAL` | D |
| **04** | Phân bổ chung | `/costs/shared*` | PNG slice | `STRUCTURAL` | UI phiên đủ nút; SoD người tạo≠người chốt **chưa**; chrome list mỏng | D/F |
| **05** | DS / detail DT | `/revenues*` | PNG | `STRUCTURAL` | Không «Đối soát DT» (`INTENTIONAL`); filter mỏng hơn PNG | E |
| **05** | Báo cáo lãi | `/revenues/report` | PNG slice | `STRUCTURAL` | Bảng groupBy; không chart dày mockup | E/G |
| **06** | Chứng từ + khớp | `/documents*` | PNG | `STRUCTURAL` | Workflow F có; density drawer/chart PNG thiếu; UX-08 chưa UAT | F |
| **07** | AP | `/ap-ar?tab=ap` | PNG full-page | `STRUCTURAL` | Không route tách full-bleed như PNG | F |
| **08** | AR | `/ap-ar?tab=ar` | PNG full-page | `STRUCTURAL` | Như UI-07 | F |
| **09** | TT & Thu | `/settlements*` | PNG | `STRUCTURAL` | Allocate có; timeline/widget PNG mỏng | F |
| **10** | Hub KS + queues | `/control`, `/queues/*`, `/bank-feed`, `/reconciliations*` | PNG | `STRUCTURAL` | Không work-list / báo cáo KS giả; exception thiếu cột ngày nếu API không trả | F |
| **11** | Chốt | `/financial-closes*` | PNG | `STRUCTURAL` | Gate G có; checklist visual PNG mỏng | G |
| **12** | Báo cáo hub + tiền | `/reports`, `/reports/cash` | PNG | `STRUCTURAL` | Chỉ link màn thật; không BI multi-chart | G |
| **13** | Danh mục | `/admin/*` | PNG | `STRUCTURAL` | Location/Route/Commodity OK; **import Excel party chưa**; merge trùng `OUT` | A |
| **14** | Cài đặt | `/settings/*` | PNG | `STRUCTURAL` | Policies H có; sample-data không trên PNG; backup ≠ PITR | H |
| **15** | Workflow map | `/workflow` | PNG diagram | `SKELETON` | Text steps + link; **không** diagram visual PNG; không sidebar | — |
| — | Login | `/login` | — | `STRUCTURAL` | Không mockup | — |
| — | Shell 14 module | `AppShell` | UI-15 contract | `STRUCTURAL` | AP/AR cùng route `?tab=` | — |

### Đếm nhanh (hàng bảng trên)
| Maturity | Số gần đúng |
|---|---|
| `PIXEL_OK` | 1 |
| `STRUCTURAL` | ~28 |
| `SKELETON` | 5 |
| `API_ONLY` | 2+ (ops import, rate import; thêm ownership invisible) |
| `INTENTIONAL` | nhiều (bulk, % MoM, subnav CP/DT đối soát, …) |

---

## 3. Backend có — UI mỏng / thiếu (`API_ONLY` + depth)

| # | Capability | API / domain | UI hiện tại | Mức | Việc Pixel-perfect |
|---|---|---|---|---|---|
| B1 | Operational import preview/commit | `POST /api/operational-import/*` | **Không màn** | `API_ONLY` | Màn nhập Order/Bill/Shipment: preview dòng → commit all-or-nothing |
| B2 | Rate card import | `POST /api/rate-imports/*` | **Không màn** | `API_ONLY` | Màn nhập bảng giá (UI-03) |
| B3 | Cargo packages / containers | `POST …/packages\|containers` | Create form **không lưới** | `SKELETON` | Lưới kiện/container trên Bill/Order (mockup có thể bỏ — MASTER FR-006) |
| B4 | Field ownership matrix | `field_ownerships` | Invisible (chỉ fail) | `STRUCTURAL` | Hiển thị nguồn SoT + lý do ghi đè trên field | 
| B5 | Leg / Movement create | Upsert API | CreateWorkspace `/operations/legs|movements/new` | `STRUCTURAL` | Unlink (K3) còn blocked |
| B6 | Unlink quan hệ | — | **MISSING** | `MISSING` | API + UI unlink Order–Bill / Bill–Shipment + audit |
| B7 | Weight breaks trên list | Rating engine | Chỉ hồ sơ version | `STRUCTURAL` | Panel bậc trọng lượng trên detail list (HTML-01) |
| B8 | Allocation SoD finalize | Commands | Người tạo vẫn chốt | `STRUCTURAL` | Chặn creator finalize khi có user khác (ADR-0026 residual) |
| B9 | Exception `createdAt` | Một số list thiếu | Queue không cột ngày | `STRUCTURAL` | API trả timestamp + cột UI |
| B10 | Idempotency-Key client | Server H OK | Form không đồng đều gửi header | `STRUCTURAL` | Mọi command form gửi key + chặn double-submit (UX-12) |
| B11 | Party Excel import | Export query có | Import **chưa** | `MISSING` | Import đối tác + preview |
| B12 | Payment/Collection file import | Manual + bank CSV | Không generic file | `PARTIAL` | (P1) import chứng từ tiền nếu PO chốt |
| B13 | Profitability charts | `GET /api/profitability/groups` | Bảng `/revenues/report` | `STRUCTURAL` | Chart thật nếu PO đòi PNG UI-05/12 |
| B14 | Control work-list / KS report | — | Hub tiles only | `INTENTIONAL`→optional | Chỉ làm khi có luồng PO |

---

## 4. Chỉ mục việc Wave 2 (sau A–H) — mở rộng tối đa

Việc **đã DONE trong A–H** không lặp. Chỉ mục dưới đây = **chưa đạt Pixel-perfect / độ sâu còn PARTIAL**.

### Gói I — Import UX parity (Manual = Import = API)
| ID | Việc | UI / API | Prio | Phụ thuộc |
|---|---|---|---|---|
| W-I1 | Màn nhập vận hành (Order/Bill/Shipment): upload → preview → commit | Mới `/operations/import` hoặc hub UI-02 | P0 | B1 |
| W-I2 | Màn nhập bảng giá | `/rate-cards/import` | P0 | B2 |
| W-I3 | Import đối tác CSV + preview | `/admin/parties/import` | P1 | B11 |
| W-I4 | Empty/error/idempotency trên mọi màn import | Shared | P0 | I1–I3 |
| W-I5 | Audit + correlation ID trên lỗi import | UI toast/panel | P1 | UX-13 |

### Gói J — UI-03 Pixel (phụ phí / phụ lục / chrome)
| ID | Việc | Route | Prio |
|---|---|---|---|
| W-J1 | Phụ phí: KPI + filter + form tạo/sửa (không CTA lệch) | `/rate-cards/surcharges` | P0 |
| W-J2 | Phụ lục: wizard phiên bản mới (amendment) | `/rate-cards/appendices` | P0 |
| W-J3 | DS bảng giá: panel bậc trọng lượng / container rate | `/rate-cards`, `[id]` | P1 |
| W-J4 | Lịch sử: filter thời gian + xuất CSV (không Excel giả) | `/rate-cards/history` | P1 | DONE |
| W-J5 | Tính giá / So sánh: UAT bố cục 2 cột vs HTML | `rate`, `compare` | P1 |
| W-J6 | Gộp hoặc cắt trùng `/rate-cards/fx` vs `/admin/fx-rates` | — | P2 |

### Gói K — UI-02 / Ops depth
| ID | Việc | Route | Prio |
|---|---|---|---|
| W-K1 | Lưới kiện/container trên tạo Bill/Order (nếu PO xác nhận FR-006 UI) | `*/new` | P0 |
| W-K2 | CreateWorkspace Chặng / Chuyến | `/operations/new`… | P1 |
| W-K3 | Unlink quan hệ + audit | Bill drawer / Liên quan | P1 |
| W-K4 | Field ownership badge + lý do ghi đè hiển thị | Bill/Order forms | P1 | DONE (note VI; API chưa expose ownership) |
| W-K5 | Drawer Bill: tab «Tính giá» hoặc deep-link rõ; căn tabs vs mockup | drawer | P2 |
| W-K6 | Picker người phụ trách (user) đủ dùng | create forms | P2 |

### Gói L — Chi phí / Doanh thu / Kiểm soát polish
| ID | Việc | Route | Prio |
|---|---|---|---|
| W-L1 | SoD: người tạo phân bổ **không** tự chốt khi có approver | `/costs/shared/[id]` | P0 |
| W-L2 | Queue / cột ngày ngoại lệ | `/queues/exceptions` | P1 |
| W-L3 | AP / AR skin full-page hoặc chấp nhận `?tab=` + ghi ADR | `/ap-ar` | P2 |
| W-L4 | Settlements: timeline allocate đọc được hơn PNG | `/settlements/[id]` | P2 |
| W-L5 | Close: panel gate visual (checklist chặn chốt) | `/financial-closes/[id]` | P1 |
| W-L6 | Reports: giữ honest — chỉ thêm chart khi có số thật | `/reports*` | P2 | DONE |

### Gói M — Danh mục / Cài đặt / Workflow
| ID | Việc | Route | Prio |
|---|---|---|---|
| W-M1 | UI-15: diagram hoặc sơ đồ điều hướng visual (không fake state) | `/workflow` | P1 |
| W-M2 | Link UI-15 từ shell (prefs/help) rõ hơn | AppShell | P2 |
| W-M3 | Party: import (I3) + credit/block panels UAT | `/admin/parties` | P1 |
| W-M4 | Policies UI: edit hiệu lực đủ field MASTER | `/settings/policies` | P2 |
| W-M5 | Idempotent submit mọi form tiền (client key) | cross | P0 |

### Gói N — UAT Pixel & UX acceptance (bắt buộc trước gọi “Pixel-perfect xong”)
| ID | Việc | Evidence | Prio |
|---|---|---|---|
| W-N1 | UX-01 Mockup fidelity — screenshot UI-01…15 vs PNG/HTML | Sheet + ảnh | P0 |
| W-N2 | UX-02…06 Permission / state / tenant / VI | UAT 2-user | P0 |
| W-N3 | UX-07…10 Error / loading / empty / a11y / responsive | Checklist | P1 |
| W-N4 | UX-11…13 No fake finance / idempotent / correlation | Script + UI | P0 |
| W-N5 | UX-14 UAT evidence pack; unresolved P0/P1 = 0 | Zip/report | P0 |
| W-N6 | PO Reviewer ký 09B (không còn «Chờ review») | Workbook | P0 |

### Gói O — Ngoài scope (theo dõi, không lẫn Pixel-perfect A–H)
| ID | Việc | Status |
|---|---|---|
| W-O1 | Budget / Forecast (P13) | `OUT` / NOT STARTED |
| W-O2 | Party merge trùng | `OUT` / P1 backlog |
| W-O3 | OIDC IdP ngoài | Residual ADR |
| W-O4 | OTLP / soak CI / broker ngoài process | Residual |
| W-O5 | Checkbox bulk / % MoM / Excel giả / subnav CP không có luồng | `INTENTIONAL` — không làm trừ PO đổi |

---

## 5. Đối chiếu FR / AC residual (sau A–H)

| ID | Post A–H | Còn thiếu Pixel / depth |
|---|---|---|
| FR-001…005, 007, 010, 011 | Mostly DONE | UAT + chrome |
| FR-006 Package lines | API DONE | **UI lưới** (K1) |
| FR-008 Deep-link 5 đối tượng | PARTIAL+ | Create Leg/Movement + unlink (K2–K3) |
| FR-009 Field ownership | PARTIAL | Matrix rộng + UI (K4) |
| AC-010 Concurrency | PARTIAL | Đồng đều mọi command |
| AC-012 SoD | PARTIAL | Allocation finalize (L1) |
| AC-SCP-03/06/07/09 Import | PARTIAL | Gói I |
| UX-01…14 | Chưa kiểm thử | Gói N |

---

## 6. Ma trận route × maturity (toàn `apps/web` ~86 page)

Ghi chú: mọi route dưới đây **có page**; cột = mức Pixel so mockup/MASTER.

| Route | Maturity | Gói Wave 2 |
|---|---|---|
| `/dashboard` | PIXEL_OK | N1 |
| `/login` | STRUCTURAL | — |
| `/bills`, `/bills/[id]`, `/bills/new` | STRUCTURAL | K*, N1 |
| `/bills/[id]/costs/new`, `…/revenues/new` | STRUCTURAL | — |
| `/orders`, `/orders/new` | STRUCTURAL | K1, N1 |
| `/shipments`, `/shipments/new` | STRUCTURAL | K1 |
| `/operations`, `/operations/[kind]/[id]`, `…/new` | STRUCTURAL | K3 unlink |
| `/rate-cards`, `/new`, `/[id]` | STRUCTURAL | J3, I2 |
| `/rate-cards/rate`, `/compare`, `/fx`, `/history` | STRUCTURAL | J4–J5 |
| `/rate-cards/surcharges`, `/appendices` | SKELETON | J1–J2 |
| `/costs`, `/costs/[id]` | STRUCTURAL | N1 |
| `/costs/shared`, `/new`, `/[id]` | STRUCTURAL | L1 |
| `/revenues`, `/[id]`, `/new`, `/report` | STRUCTURAL | L6, N1 |
| `/documents*` | STRUCTURAL | N* |
| `/ap-ar*` | STRUCTURAL | L3 |
| `/settlements*` | STRUCTURAL | L4 |
| `/control`, `/queues/*` | STRUCTURAL | L2 |
| `/bank-feed`, `/reconciliations*` | STRUCTURAL | N1 |
| `/financial-closes*` | STRUCTURAL | L5 |
| `/reports`, `/reports/cash` | STRUCTURAL | L6 |
| `/admin` + parties/catalog/locations/routes/commodities/currencies/fx/orgs/access | STRUCTURAL | I3, M3 |
| `/settings*` | STRUCTURAL | M4–M5 |
| `/workflow` | SKELETON | M1 |
| `/integration-errors` | STRUCTURAL | — (ops) |

---

## 7. Thứ tự làm đề xuất (unlock)

```
N0  Đăng nhập UAT + baseline screenshot (mở khóa N1)
I1  Import vận hành UI          ─┐
I2  Import bảng giá UI          ─┼─ AC-SCP parity
J1  Phụ phí pixel               ─┤
J2  Phụ lục pixel               ─┘
L1  SoD allocation finalize
K1  Lưới kiện (nếu PO YES) / hoặc khóa ADR «totals only»
K2–K3  Operations create + unlink
M1  Workflow map visual
M5  Idempotent client forms
N1–N6  UX acceptance + 09B Reviewer
O*  Chỉ khi PO mở scope
```

**Không mở lại:** bulk checkbox, % MoM bịa, Excel giả, nav «Đối soát» khi chưa có luồng (`INTENTIONAL`).

---

## 8. Evidence & con trỏ

| Tài liệu | Vai trò |
|---|---|
| `docs/po/LCMS_UIUX_Mockup_Package_v1.0/` | PNG UI-01…15 PO APPROVED |
| `docs/po/Mockup html/`, `Mockup Bảng giá_Tính giá/` | HTML chi tiết UI-02/03 |
| `docs/handoff.md` (2026-09-22 A–H) | Claim ship + residual ghi chú |
| ADR-0023…0030 | Quyết định gói A–H |
| `docs/reports/_extract_ui_trace.txt` | UX-01…14 = Chưa kiểm thử |
| `docs/reports/_chat_answers_full.json` | FR/W-index gốc 55 việc |
| Canvas (IDE) | `pixel-perfect-gap-index.canvas.tsx` — lọc theo gói/maturity |

---

## 9. Tóm tắt số

| Hạng mục | Số |
|---|---|
| Module UI cấp 1 (UI-01…15) | 15 — **0 MISSING** |
| Đạt `PIXEL_OK` (đã UAT) | **~0–1** (UI-01 gần; chưa UX-01 signed) |
| `STRUCTURAL` cần polish/UAT | **~28+** |
| `SKELETON` ưu tiên | **5** (surcharges, appendices, operations, workflow, …) |
| `API_ONLY` import | **2** P0 (ops + rate) |
| Việc Wave 2 đánh số (I–N) | **~35** |
| `OUT` / `INTENTIONAL` theo dõi | **~10** |

**Kết luận vận hành (cập nhật đợt Wave 2):** Lõi tài chính đã ship. Đợt này đã đóng P0 chính: Import UI (I1/I2), phụ phí/phụ lục (J1/J2), SoD phân bổ (L1), lưới kiện (K1), workflow nav (M1/M2), CreateWorkspace Chặng/Chuyến (K2). Còn: I3 party import, K3 ops unlink, L2–L6 polish, M5 đồng đều client, **N UAT evidence**.

---

## 10. Tiến độ Wave 2 (2026-09-22)

| ID | Trạng thái | Ghi chú |
|---|---|---|
| W-I1 | **DONE** | `/operations/import` + BFF preview/commit |
| W-I2 | **DONE** | `/rate-cards/import` + BFF preview/commit |
| W-I3 | DONE | Import CSV đối tác preview→commit (`/admin/parties/import`) |
| W-I4–I5 | PARTIAL | Preview/error trên I1/I2 |
| W-J1 | **DONE** | KPI + filter + CTA trung thực |
| W-J2 | **DONE** | KPI + filter + quy trình phụ lục |
| W-J3–J6 | **J3/J4 DONE** | J5/J6 UAT HTML pixel còn (cần login) |
| W-K1 | **DONE** | Lưới kiện/container trên tạo Bill/Order |
| W-K2 | **DONE** | CreateWorkspace Chặng/Chuyến |
| W-K3 | **DONE** | Soft-delete unlink + `linkId` graph + drawer CTA · ADR-0032 |
| W-J3 | **DONE** | Breaks + container rates trên `/rate-cards/[id]` |
| W-N1–N6 | TEMPLATE | `docs/uat/UAT-PIXEL-PERFECT-WAVE2.md` — chờ runner + login |
| W-L3 | **DONE** | ADR-0031 giữ `/ap-ar?tab=` |
| W-K4 | **DONE** | Note VI điểm đi/đến (API ownership chưa expose) |
| W-K5–K6 | PENDING | Drawer tabs, picker |
| W-L1 | **DONE** | PC-21 SoD finalize + `AllocationSodTests` |
| W-L2–L5 | PENDING | Queue ngày, AP skin, close gate visual… |
| W-L6 | **DONE** | Copy honest API trên `/reports` + `/revenues/report` |
| W-M1 | **DONE** | Workflow strip + bước import + nav |
| W-M2 | **DONE** | Link từ Cài đặt |
| W-M5 | PARTIAL | `idempotency.ts` + finalize alloc / import |
| W-N1–N6 | TEMPLATE | `docs/uat/UAT-PIXEL-PERFECT-WAVE2.md` — chờ runner + login |
| W-O* | OUT / INTENTIONAL | Không mở |
