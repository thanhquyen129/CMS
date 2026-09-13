# UAT go-live tài chính đủ — người nghiệp vụ (VPS)

**Ngày mở:** 2026-09-13  
**Host:** `http://194.233.89.26`  
**Phạm vi:** không còn checklist P-series. Mục tiêu = người làm nghề **xong việc** quanh Bill trên VPS.  
**Phương thức:** UI-only (trình duyệt + đăng nhập BFF). Không Postman / raw JWT.  
**Kết quả:** điền `UAT-GO-LIVE-FINANCIAL-RESULT.md` (một vòng / một phiên).

---

## Định nghĩa PASS (go-live đủ)

Người nghiệp vụ hoàn tất trên UI, số tiền và trạng thái đọc được bằng tiếng Việt (CP6.5):

**Bill → Chi phí/Doanh thu (Dự kiến → Đã xác nhận → Thực tế khi cần) → Chứng từ (Nhận → Chấp nhận → Khớp) → Exposure → Ghi nhận AP/AR → Thanh toán/Thu tiền (phân bổ → chốt) → Đối soát (khi có) → Chốt tài chính + P&L → Audit đọc được.**

Không silent overwrite số đã chốt. Cost ≠ Payment; Revenue ≠ Collection; Received ≠ Accepted ≠ Matched ≠ Recognized ≠ Settled.

---

## Chuẩn bị (facilitator / Admin)

| # | Việc | Ghi chú |
|---|------|---------|
| 0.1 | `/health` `/ready` = 200 | Preflight 2026-09-13: OK |
| 0.2 | Tài khoản UAT | Email bootstrap trên host `infra/.env` (`Auth__Bootstrap__*` / `ops@cms.local`) — **không** ghi password vào doc |
| 0.3 | Tenant demo | Thường `ops`. Seed catalog: `Demo__SeedOnStartup` hoặc `POST /api/dev/seed-demo` nếu bật |
| 0.4 | Vai trò thử | Ít nhất 1 user Admin đầy quyền; tùy chọn tách Cost-only vs Revenue-only để chứng minh H View Cost ≠ Revenue |
| 0.5 | Quy ước Bill | Mỗi phiên dùng `billNo` riêng: `UAT-GL-yyyyMMdd-HHmm` |

---

## Vai trò tham gia

| Vai | Việc chính trên CMS |
|-----|---------------------|
| Điều vận / Ops | Tạo Bill; nhập chi phí/doanh thu dự kiến; theo dõi profile |
| Kế toán chi phí | Xác nhận / thực tế hóa cost; phân bổ chi phí chung; khớp chứng từ AP |
| Kế toán DT / công nợ | Xác nhận revenue; ghi nhận AR; thu tiền; aging |
| Controller | Phê duyệt xóa nợ / write-off; chốt Strict; period lock; audit |
| Admin thuê bao | Danh mục (đối tác/org/tiền tệ); ngưỡng cài đặt; lỗi tích hợp |

Một người có thể đeo nhiều vai nếu thiếu người — ghi rõ trong RESULT.

---

## Kịch bản bắt buộc (S1) — một vòng Bill → Close

| Bước | Màn / CTA | Kỳ vọng |
|------|-----------|---------|
| 1 | Login → `/dashboard` | Vào được; nav tiếng Việt |
| 2 | `/bills/new` → tạo Bill | Bill mở; `billNo` theo quy ước |
| 3 | Tạo **Chi phí** (Expected) → **Xác nhận** (số có thể ≠ Expected) | Maturity Đã xác nhận; profile costBA cập nhật |
| 4 | Tạo **Doanh thu** → **Xác nhận** | revBA cập nhật; lợi nhuận tạm = rev − cost |
| 5 | Nhận chứng từ → thêm dòng → **Chấp nhận** → **Khớp** (line→cost/revenue) | Trạng thái Khớp; không invent Cost/Revenue mới |
| 6 | Tạo/đề xuất Exposure → **Ghi nhận** AP & AR | AP/AR Recognized; số khớp confirmed |
| 7 | **Thanh toán** → phân bổ nháp → **Chốt phân bổ** | AP Settled (hoặc dư đúng số còn lại) |
| 8 | **Thu tiền** → phân bổ nháp → **Chốt phân bổ** | AR Settled |
| 9 | `/financial-closes` → mở chốt Bill (Controlled hoặc Strict) → tạo bản chốt → **Khóa** | Snapshot + P&L đọc được; khóa rồi không sửa money path |

**S1 FAIL** nếu bất kỳ bước nào kẹt UI (nút giả / 4xx/5xx không hiểu được) hoặc số P&L sai so với confirmed đã settle.

---

## Kịch bản mở rộng (S2–S12) — đủ lớp kiểm soát

Làm sau S1 PASS. Tick từng dòng trong RESULT. Gap = blocker / major / minor + mô tả + ảnh/URL.

| ID | Tên | Đường đi ngắn | PASS khi |
|----|-----|---------------|----------|
| **S2** | Chi phí chung → phân bổ Bill | `/costs/shared` → basis → draft → finalize | Bill nhận allocation; không double-count khi chốt |
| **S3** | Điều chỉnh Confirm/Actual | Dialog số + lý do trên Cost/Revenue | List adjustment; profile/asOf trung thực |
| **S4** | Match confirm + auto exposure | Sau khớp → đề xuất/tạo exposure | Không invent C/R; exposure đúng chiều |
| **S5** | Xóa nợ trên trần + phê duyệt | Write-off wizard → `/queues/approvals` → decide | Dưới trần apply ngay; trên trần chờ approved |
| **S6** | Đảo ghi nhận AP/AR | `/ap-ar` → Đảo ghi nhận (chưa settle) | Exposure/restored; có lịch sử; không silent overwrite |
| **S7** | Chốt Strict + period lock | Close `strict` khi đủ điều kiện; thử confirm/allocate khi Locked | 409 / thông báo khóa kỳ; reopen không phá hash snapshot cũ |
| **S8** | Bảng giá → rate Bill → seed Expected | `/rate-cards` publish → rate trên Bill | Expected lines xuất hiện đúng card |
| **S9** | Aging + quyền tài chính | `/ap-ar/aging`; user chỉ Cost vs chỉ Revenue | Bucket đúng; không lộ số ngoài quyền |
| **S10** | Variance / Exception | `/queues/variances` → escalate; `/queues/exceptions` | Tách lớp; CTA mở được exception |
| **S11** | Bank feed + đối soát | `/bank-feed` import CSV; `/queues/reconciliations` | Line vào feed; phiên đối soát xử lý được |
| **S12** | Audit trail | Panel trên Cost / Close (và đối tượng tiền khác nếu có) | Thấy ai/làm gì/khi nào |

### Tuỳ chọn vận hành (S13–S14)

| ID | Tên | PASS khi |
|----|-----|----------|
| **S13** | Admin + settings | `/admin` CRUD tối thiểu; `/settings` đổi ngưỡng write-off / confirm / recognition policy |
| **S14** | Lỗi tích hợp | `/integration-errors` → thử lại / dead-letter |

---

## Cách ghi nhận (người nghiệp vụ)

1. Mỗi phiên: copy `UAT-GO-LIVE-FINANCIAL-RESULT.md` → đổi tên hoặc ghi đè mục «Phiên».
2. Ghi `billNo`, người tham gia, giờ bắt đầu/kết thúc (giờ VN).
3. S1: PASS/FAIL từng bước; nếu FAIL → dừng, ghi bước + thông báo lỗi + URL.
4. S2–S14: PASS / FAIL / SKIP (+ lý do SKIP).
5. Mục «Gap thật»: severity + mô tả nghiệp vụ (không jargon sprint Pxx).
6. Gửi RESULT cho dev/PO; **không** gửi password.

---

## Non-goals (vòng này)

- OIDC IdP thật, OTLP exporter, soak CI gate, broker ngoài process (residual — cần ADR).
- TMS (GPS, e-POD, xe trống).
- Viết lại API Pass 3.

---

## Liên kết

- Vòng UI mỏng đã PASS (2026-09-12): `UAT-VPS-ONE-ROUND.md`
- Board P01–P25 đóng: `PO-GAP-CHECKLIST.md`
- Template kết quả: `UAT-GO-LIVE-FINANCIAL-RESULT.md`
