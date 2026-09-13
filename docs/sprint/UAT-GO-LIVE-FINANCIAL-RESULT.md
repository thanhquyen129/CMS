# UAT go-live tài chính đủ — RESULT

**Phiên:** _(vd. 2026-09-13 buổi chiều)_  
**Host:** `http://194.233.89.26`  
**Tenant:**  
**billNo:** `UAT-GL-`  
**Bill id:** _(sau khi tạo)_  
**Close id / snapshot:** _(nếu có)_  
**Người tham gia / vai:**  
**Bắt đầu (VN):** · **Kết thúc (VN):**  
**Facilitator:**  

**Verdict tổng:** □ PASS · □ FAIL · □ PASS có gap minor  

---

## Preflight

| Check | Kết quả |
|-------|---------|
| `/health` | |
| `/ready` | |
| Login UI | |

---

## S1 — Bill → Close (bắt buộc)

| Bước | Kết quả | Ghi chú / số tiền |
|------|---------|-------------------|
| 1 Login → dashboard | □ PASS □ FAIL | |
| 2 Tạo Bill | □ PASS □ FAIL | billNo= |
| 3 Cost Expected → Confirm | □ PASS □ FAIL | Expected= · Confirmed= |
| 4 Revenue → Confirm | □ PASS □ FAIL | Confirmed= · profit tạm= |
| 5 Chứng từ Nhận→Accept→Match | □ PASS □ FAIL | |
| 6 Exposure → Recognize AP/AR | □ PASS □ FAIL | AP= · AR= |
| 7 Thanh toán → chốt phân bổ | □ PASS □ FAIL | |
| 8 Thu tiền → chốt phân bổ | □ PASS □ FAIL | |
| 9 Chốt tài chính + P&L khóa | □ PASS □ FAIL | DT= · CP= · LN= · policy= |

**S1:** □ PASS · □ FAIL  

---

## S2–S14

| ID | Kết quả | Ghi chú |
|----|---------|---------|
| S2 Shared allocate | □ PASS □ FAIL □ SKIP | |
| S3 Adjust Confirm/Actual | □ PASS □ FAIL □ SKIP | |
| S4 Match confirm + exposure | □ PASS □ FAIL □ SKIP | |
| S5 Write-off + approval | □ PASS □ FAIL □ SKIP | |
| S6 Reverse recognize | □ PASS □ FAIL □ SKIP | |
| S7 Strict + period lock | □ PASS □ FAIL □ SKIP | |
| S8 Rate card → Expected | □ PASS □ FAIL □ SKIP | |
| S9 Aging + quyền Cost≠Revenue | □ PASS □ FAIL □ SKIP | |
| S10 Variance / Exception | □ PASS □ FAIL □ SKIP | |
| S11 Bank feed + recon | □ PASS □ FAIL □ SKIP | |
| S12 Audit trail | □ PASS □ FAIL □ SKIP | |
| S13 Admin + settings | □ PASS □ FAIL □ SKIP | |
| S14 Integration errors | □ PASS □ FAIL □ SKIP | |

---

## Gap thật

| # | Severity | Mô tả (nghiệp vụ) | Màn / CTA | Owner |
|---|----------|-------------------|-----------|-------|
| 1 | blocker / major / minor | | | |

---

## Quan sát UX (CP6.5)

- Thuật ngữ có chỗ nào lệch / khó hiểu?
- Hành động chính mỗi màn có rõ không?
- Trạng thái trống / lỗi có hướng dẫn đủ không?

---

## Quyết định sau phiên

- □ Go-live tài chính đủ — chấp nhận vận hành
- □ Go-live có điều kiện (chỉ gap minor + ngày sửa)
- □ Chưa go-live — cần sửa blocker/major trước

**Ghi chú PO/dev:**
