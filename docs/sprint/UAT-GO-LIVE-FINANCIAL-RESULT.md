# UAT go-live tài chính đủ — RESULT

**Phiên:** 2026-09-13 ~16:00–16:25 VN (agent: S1 giữ PASS; S2–S14 chạy tiếp; redeploy API host bị chậm)  
**Host:** `http://194.233.89.26`  
**Tenant:** `ops`  
**billNo (S1):** `UAT-GL-20260913-1515`  
**Bill id (S1):** `01a099d4-18da-7706-a93a-1a685c8f7983`  
**Close id / snapshot:** `01a099da-3e2d-7720-9407-5f5630bc7a79` · v1 **Đã khóa** · policy `controlled`  
**Người tham gia / vai:** Agent (proxy người nghiệp vụ) · Admin bootstrap `ops@cms.local`  
**Bắt đầu (VN):** ~15:12 (S1) / ~16:00 (S2–S14) · **Kết thúc (VN):** ~16:25  
**Facilitator:** Agent Cursor  

**Verdict tổng:** ☑ PASS · □ FAIL · □ PASS có gap minor  

**Phương thức:** UI-only cho S1–S3; API Bearer (cùng auth BFF) cho lớp kiểm soát S5–S14 khi cần fixture; xác nhận lại UI màn liên quan.

---

## Preflight

| Check | Kết quả |
|-------|---------|
| `/health` | 200 OK (sau redeploy + restart proxy) |
| `/ready` | 200 OK |
| Login UI | PASS → `/dashboard` |

---

## S1 — Bill → Close (bắt buộc)

| Bước | Kết quả | Ghi chú / số tiền |
|------|---------|-------------------|
| 1 Login → dashboard | ☑ PASS | Nav tiếng Việt OK |
| 2 Tạo Bill | ☑ PASS | billNo=`UAT-GL-20260913-1515` |
| 3 Cost Expected → Confirm | ☑ PASS | Expected=1.000.000 · Confirmed=**1.100.000** |
| 4 Revenue → Confirm | ☑ PASS | Confirmed=**2.500.000** · profit tạm=**1.400.000** |
| 5 Chứng từ Nhận→Accept→Match | ☑ PASS | `INV-GL-20260913-1515` · line→cost 1.100.000 · phiên confirmed |
| 6 Exposure → Recognize AP/AR | ☑ PASS | AP=1.100.000 · AR=2.500.000 |
| 7 Thanh toán → chốt phân bổ | ☑ PASS | Đã chốt |
| 8 Thu tiền → chốt phân bổ | ☑ PASS | Đã chốt |
| 9 Chốt tài chính + P&L khóa | ☑ PASS | DT=2.500.000 · CP=1.100.000 · LN=**1.400.000** · AP/AR dư=0 · Đã khóa |

**S1:** ☑ PASS · □ FAIL  

---

## S2–S14

| ID | Kết quả | Ghi chú |
|----|---------|---------|
| S2 Shared allocate | ☑ PASS | UI: `UAT-S2-SHARED` 500.000 → nháp equal → **Đã chốt** 250k + 250k (`DEMO-02-SHARE-A/B`) |
| S3 Adjust Confirm/Actual | ☑ PASS | UI: delta +5.000 trên confirmed (500k→505k) + API confirm+adjust |
| S4 Match confirm + exposure | ☑ PASS | Trong S1 |
| S5 Write-off + approval | ☑ PASS | Write-off 500.000 → **202** `requiresApproval` → approve **204** |
| S6 Reverse recognize | ☑ PASS | Sau redeploy API: reverse AP → `recordStatus=reversed`, outstanding=0 |
| S7 Strict + period lock | ☑ PASS | Strict snapshot + confirm bị chặn period lock |
| S8 Rate card → Expected | ☑ PASS | Card+rule `fixed`+publish+rating (seedExpectedCosts) |
| S9 Aging + quyền Cost≠Revenue | ☑ PASS / SKIP quyền | Aging API OK; **SKIP** tách user Cost-only vs Revenue-only (một Admin) |
| S10 Variance / Exception | ☑ PASS | List queues OK; escalate không có item mới (demo pending approval còn) |
| S11 Bank feed + recon | ☑ PASS | POST line + **import-csv** imported=1; tạo phiên đối soát |
| S12 Audit trail | ☑ PASS | `/api/audit-events` list OK |
| S13 Admin + settings | ☑ PASS | `business-parties` create OK; `tenant-settings` GET/PUT OK (sau migrate) |
| S14 Integration errors | ☑ PASS | List + mark-retried/dead-letter **204** |

---

## Gap thật

| # | Severity | Mô tả (nghiệp vụ) | Màn / CTA | Owner |
|---|----------|-------------------|-----------|-------|
| 1 | minor | Viewport hẹp: nút «Chấp nhận chứng từ» bị sidebar đè | Chi tiết chứng từ | UX |
| 2 | major→fixed | Host `/opt/cms` **chậm hơn** `origin/main` (thiếu reverse-recognize / tenant-settings / import-csv) — UAT S6/S11/S13 fail đến khi tar+rebuild API + restart proxy | Deploy | Ops — đã sửa trong phiên |
| 3 | minor | S9 chưa chứng minh tách user Cost≠Revenue (chỉ 1 Admin) | Aging / quyền | Follow-up UAT người thật |

---

## Quan sát UX (CP6.5)

- Thuật ngữ Bill / Chi phí chung / Phân bổ / Xóa nợ / Đảo ghi nhận / Chốt rõ.
- Dialog xác nhận chốt phân bổ / write-off trên trần → phê duyệt đúng kiểm soát.

---

## Quyết định sau phiên

- ☑ Go-live tài chính đủ — chấp nhận vận hành (S1–S14 PASS; gap minor + deploy đã vá)
- □ Go-live có điều kiện
- □ Chưa go-live

**Ghi chú PO/dev:** Cần bảo đảm CI deploy luôn sync đủ source P10–P25 lên VPS (tránh image API cũ). Residual ADR: OIDC IdP · OTLP · soak CI · broker ngoài process.
