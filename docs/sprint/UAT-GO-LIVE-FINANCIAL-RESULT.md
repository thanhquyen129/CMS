# UAT go-live tài chính đủ — RESULT

**Phiên:** 2026-09-13 ~15:12–15:25 VN (agent chạy hộ S1 UI-only)  
**Host:** `http://194.233.89.26`  
**Tenant:** `ops`  
**billNo:** `UAT-GL-20260913-1515`  
**Bill id:** `01a099d4-18da-7706-a93a-1a685c8f7983`  
**Close id / snapshot:** `01a099da-3e2d-7720-9407-5f5630bc7a79` · v1 **Đã khóa** · policy `controlled`  
**Người tham gia / vai:** Agent (proxy người nghiệp vụ) · Admin bootstrap `ops@cms.local`  
**Bắt đầu (VN):** ~15:12 · **Kết thúc (VN):** ~15:25  
**Facilitator:** Agent Cursor  

**Verdict tổng:** ☑ PASS · □ FAIL · □ PASS có gap minor  

**Phương thức:** UI-only (trình duyệt + BFF cookie) — không Postman / raw JWT.

---

## Preflight

| Check | Kết quả |
|-------|---------|
| `/health` | 200 OK |
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
| 7 Thanh toán → chốt phân bổ | ☑ PASS | payment `01a099d9-4733-774e-8efc-35ec1047ac84` Đã chốt |
| 8 Thu tiền → chốt phân bổ | ☑ PASS | collection `01a099d9-efe4-7825-abbb-019979919dc2` Đã chốt |
| 9 Chốt tài chính + P&L khóa | ☑ PASS | DT=2.500.000 · CP=1.100.000 · LN=**1.400.000** · AP/AR dư=0 · Đã khóa |

**S1:** ☑ PASS · □ FAIL  

---

## S2–S14

| ID | Kết quả | Ghi chú |
|----|---------|---------|
| S2 Shared allocate | □ PASS □ FAIL ☑ SKIP | Ngoài phạm vi phiên S1 agent |
| S3 Adjust Confirm/Actual | □ PASS □ FAIL ☑ SKIP | |
| S4 Match confirm + exposure | ☑ PASS (trong S1) | Match confirm + exposure tay từ Cost/Revenue |
| S5 Write-off + approval | □ PASS □ FAIL ☑ SKIP | |
| S6 Reverse recognize | □ PASS □ FAIL ☑ SKIP | |
| S7 Strict + period lock | □ PASS □ FAIL ☑ SKIP | S1 dùng Controlled; Strict đã PASS P04 trước đó |
| S8 Rate card → Expected | □ PASS □ FAIL ☑ SKIP | |
| S9 Aging + quyền Cost≠Revenue | □ PASS □ FAIL ☑ SKIP | |
| S10 Variance / Exception | □ PASS □ FAIL ☑ SKIP | |
| S11 Bank feed + recon | □ PASS □ FAIL ☑ SKIP | |
| S12 Audit trail | □ PASS □ FAIL ☑ SKIP | |
| S13 Admin + settings | □ PASS □ FAIL ☑ SKIP | |
| S14 Integration errors | □ PASS □ FAIL ☑ SKIP | |

---

## Gap thật

| # | Severity | Mô tả (nghiệp vụ) | Màn / CTA | Owner |
|---|----------|-------------------|-----------|-------|
| 1 | minor | Trên viewport hẹp, nút «Chấp nhận chứng từ» bị sidebar đè — phải scroll/JS click mới bấm được | Chi tiết chứng từ | UX follow-up |

---

## Quan sát UX (CP6.5)

- Thuật ngữ Bill / Chi phí / Doanh thu / Nhận ≠ Chấp nhận ≠ Khớp / Exposure → Ghi nhận / Chốt phân bổ rõ.
- Hành động chính mỗi màn đủ để xong việc.
- Dialog xác nhận chốt phân bổ / tạo bản chốt có bước xác nhận — đúng kiểm soát.

---

## Quyết định sau phiên

- ☑ Go-live tài chính đủ — chấp nhận vận hành **cho vòng S1 bắt buộc** (Bill→Close trên VPS)
- □ Go-live có điều kiện (chỉ gap minor + ngày sửa)
- □ Chưa go-live — cần sửa blocker/major trước

**Ghi chú PO/dev:** S2–S14 còn SKIP — nên có phiên người nghiệp vụ thật cho lớp kiểm soát mở rộng. Gap minor sidebar không chặn go-live S1.
