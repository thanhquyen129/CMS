# LCMS-CR-SURCHARGE-APPLICATION-01 — Executive Handover (DEV → PO)

**Gói:** `Docs/po/02112026-LCMS_Surcharge_Application_P0_DEV_Handover_v1.0_20261102/`  
**Commit SHA:** `__GIT_SHA__`  
**Ngày DEV:** 2026-11-02  
**PO UAT:** **NOT RUN** (cột PO giữ nguyên 100%)

## Trả lời PO (tóm tắt 1 trang)

| Câu hỏi PO | Trả lời DEV |
|---|---|
| Vì sao Bill SGN–HAN tự cộng Đóng gói 300.000 dù không chọn? | AS-IS: Eligibility ≡ Activation. P0 tách 3 tầng; OPTIONAL chỉ cộng khi `service_selections` đã **Lưu**. |
| Published v2 có bị sửa không? | Không. Exact version GET + UI read-only. Draft v3 riêng (TC-19/20). |
| Rating lịch sử TESTLSS002? | Giữ **1.740.000** (FREIGHT 1.440.000 + DONGGOI 300.000) — TC-22 + SQL Prod. |
| Production đã cutover? | **Chưa.** MG-18 chờ PO. Migration sẵn; inventory đã xuất từ `cms-db`. |

## DoD gates

| Gate | Trạng thái |
|---|---|
| DoD-01 Inventory | PASS DEV — 9 version / 3 header từ Prod SELECT → sheet `Kiểm kê phụ phí` + `04_*.md` |
| DoD-02 Engine 3 tầng | PASS DEV — `SurchargeRating` / `SurchargeActivation` |
| DoD-03 24BR / 18AC / 32TC | PASS DEV — Excel + MD; PO UAT = NOT RUN |
| DoD-04 Evidence | PASS DEV — JSON `evidence/api_samples.json`, SQL snapshot, `screenshots/01–05`, SHA |
| DoD-05 Immutability | PASS DEV — Published/Rating/Cost lịch sử không rewrite |

## File đã đổi (chính)

**Backend:** `SurchargeActivation.cs`, `SurchargeRating.cs`, `SurchargeCommands.cs`, `ServiceSelectionCommands.cs`, `SurchargeQueries.cs`, `SurchargeEndpoints.cs`, entities + migration `20261102170000_*`  
**Frontend:** `SurchargeForm.tsx`, version read-only page, `ServiceSelectionPanel.tsx`, `RatingSurchargeBuckets.tsx`, `BillRatingPanel.tsx`, BFF `service-selections`  
**Tests:** `SurchargeApplicationP0Tests.cs` (TC01–TC32 + API helper)

## Kết quả kiểm thử

| Suite | Kết quả |
|---|---|
| `dotnet test --filter FullyQualifiedName~SurchargeApplicationP0Tests` | **32/32 (+1 API) PASS** |
| Frontend `npm test` (vitest) | **47/47 PASS** |
| `npx tsc --noEmit` | PASS |
| Full `LCMS.Api.Tests` | Chạy lại sau compat publish stamp; xem log đính kèm phiên làm việc |

## Hướng dẫn PO UAT (UI)

1. Mở Bill SGN–HAN → panel **Dịch vụ / Phụ phí tùy chọn** — mặc định **không** tích; bấm **Lưu lựa chọn dịch vụ**.  
2. Tính giá BUY **chưa chọn** → Nhóm 2 `NOT_SELECTED` (screenshot 03).  
3. Chọn + Lưu qty=1 → Tính lại → Nhóm 1 có DONGGOI 300.000 (screenshot 04).  
4. Mở `/rate-cards/surcharges/.../versions/{v2}` — Chỉ đọc (screenshot 01).  
5. Sửa v3 Draft — 3 tầng (screenshot 02).  
6. Kiểm lịch sử TESTLSS002 = 1.740.000 (screenshot 05).  
7. Đánh dấu cột **PO UAT** trên Excel khi xong — hiện **NOT RUN**.
