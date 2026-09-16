# CMS — Checklist go-live tài chính (tuần tự)

Ngày lập: 2026-09-16  
Mục tiêu: người làm nghề **xong việc** quanh Bill (chi phí → doanh thu → chứng từ → AP/AR → thanh toán/thu → đối soát → chốt → lợi nhuận), UI tiếng Việt CP6.5, audit đủ.  
Cách dùng: làm **theo số thứ tự**; chỉ nhảy bước khi bước trước ✅ hoặc bị chặn ADR/PO. Đánh `[x]` khi xong; ghi bug/blocker ngay dưới bước.

---

## A. UAT luồng tiền (ưu tiên tuyệt đối)

### A1. Chuẩn bị phiên UAT
- [ ] Tài khoản có đủ quyền Action × Data Scope (không chỉ “Admin” giả định)
- [ ] Tenant có ≥1 Party NCC + ≥1 Party KH, tiền tệ VND
- [ ] Ghi URL prod + build/commit đang chạy

### A2. Bill (neo tài chính)
- [ ] Tạo Bill mới → mở hồ sơ → thấy tab Tổng quan / Chi phí / Doanh thu / Chứng từ / Tính giá / Lịch sử
- [ ] List Bill: KPI + drawer + phân trang hoạt động; số DT/CP/LN khớp hồ sơ (best available)
- [ ] **Blocker nếu:** tạo/xem Bill fail, summary sai tiền, lộ tenant khác

### A3. Bảng giá → kỳ vọng (không tạo Thực tế)
- [ ] Tạo/chọn Rate card → phiên bản → quy tắc → phát hành
- [ ] Tính giá trên Bill → seed chi phí **Dự kiến** (không nhảy thẳng Thực tế)
- [ ] **Blocker nếu:** rating ghi Actual hoặc không gắn Bill

### A4. Chi phí (Direct + Shared→Allocated)
- [ ] Thêm chi phí Direct: Dự kiến → Đã xác nhận → Thực tế (chuyển trạng thái hợp lệ)
- [ ] Chi phí Shared → phân bổ → finalize → số về Bill
- [ ] List `/costs`: drawer maturity layers + filter + phân trang
- [ ] **Blocker nếu:** nhảy maturity trái phép, Shared không allocate, Cost lộ sang Revenue UI vì cùng quyền

### A5. Doanh thu & lợi nhuận trên Bill
- [ ] Thêm doanh thu Dự kiến → Đã xác nhận → Thực tế
- [ ] Bill overview: DT / CP / LN best available đúng
- [ ] List `/revenues`: filter độ chín + phân trang
- [ ] **Blocker nếu:** Profit hiện khi thiếu quyền DT hoặc CP

### A6. Chứng từ tài chính (triad độc lập)
- [ ] Nhận chứng từ (Received) ≠ Chấp nhận (Accepted) ≠ Khớp (Matched) — ba chiều tách
- [ ] Match chứng từ ↔ cost/revenue / AP-AR theo luồng hiện có
- [ ] List `/documents`: filter loại + triad denser + drawer + phân trang
- [ ] **Blocker nếu:** một status gộp triad, hoặc match ghi nhận tiền sai

### A7. AP / AR
- [ ] Ghi nhận / đảo ghi nhận exposure (khi chưa tất toán)
- [ ] Aging đọc được; quyền AP ≠ AR
- [ ] **Blocker nếu:** nhận diện nhầm Payment = Cost hoặc Collection = Revenue

### A8. Thanh toán & Thu tiền
- [ ] Tạo Payment (AP) / Collection (AR); trạng thái mở → hoàn tất theo API
- [ ] Liên kết chứng từ / exposure đúng; không silent overwrite
- [ ] **Blocker nếu:** tất toán khi chưa đủ điều kiện nghiệp vụ

### A9. Sao kê & Đối soát
- [ ] Bank feed nhập / xem dòng
- [ ] Tạo phiên đối soát → khớp / ngoại lệ → đóng phiên
- [ ] Queue ngoại lệ ≠ queue chênh lệch (không gộp nhãn)
- [ ] **Blocker nếu:** đối soát sửa sổ tiền đã chốt

### A10. Chốt tài chính
- [ ] Tạo kỳ chốt → chạy kiểm tra → chốt
- [ ] Snapshot bất biến: không sửa CP/DT/Bill tiền trong kỳ đã chốt
- [ ] **Blocker nếu:** edit được số sau close

### A11. Kiểm soát & Báo cáo (đọc)
- [ ] Hub `/control`: số queue thật (exception / variance / approval / recon)
- [ ] Hub `/reports`: CP/DT/LN + aging có số (theo quyền)
- [ ] Dashboard UI-01: KPI + việc cần xử lý từ dữ liệu thật (không fake TMS)

### A12. Kết thúc vòng UAT A
- [ ] Ghi danh sách bug theo severity: P0 tiền/auth/tenant · P1 chặn nghề · P2 UI
- [ ] Fix **toàn bộ P0** trước khi sang B

---

## B. Sửa blocker rồi mới polish UI

### B1. Fix P0 / P1 từ vòng A
- [ ] Tiền sai / chuyển trạng thái trái / tenant leak / 403 sai / 500 đường tài chính
- [ ] Test regression đường tiền/auth liên quan
- [ ] Ship + verify `/health` `/ready`

### B2. Pixel / operability theo mockup (sau P0 sạch)
- [ ] UI-09 Thanh toán & Thu tiền — so screenshot PO
- [ ] UI-10 Kiểm soát tài chính
- [ ] UI-11 Chốt tài chính
- [ ] UI-12 Báo cáo & Phân tích
- [ ] UI-13 Danh mục (parties/org/currency/access)
- [ ] UI-14 Hệ thống & Cài đặt
- [ ] UI-01 Dashboard — gap còn lại (series tháng, user chip) nếu API đủ

### B3. Search & điều hướng
- [ ] Global Search đa entity (Bill + Party + Document + …) khi API sẵn
- [ ] UI-15 Workflow map: link đúng mọi bước A2–A10

---

## C. Nợ kỹ thuật list (không chặn go-live nếu list < vài nghìn dòng)

- [ ] API list: server `skip`/`take` + `totalCount` (bills/costs/revenues/documents/rate-cards)
- [ ] Cost list: query filter vendor + date range (rồi mới denser UI)
- [ ] Drawer: bổ sung field từ detail GET khi list DTO thiếu
- [ ] AP/AR tách route vật lý (`/ap`, `/ar`) — **chỉ nếu PO yêu cầu** (hiện `?tab=` ổn)

---

## D. Ngoài scope — cần ADR / PO trước khi code

- [ ] Order / Shipment CRUD trong CMS (mockup UI-02) — đụng H-002 SoT vận hành
- [ ] Mọi surface TMS (GPS, e-POD, xe trống) — cần ADR mới

---

## E. Definition of Done go-live

- [ ] Một user nghiệp vụ hoàn thành A2→A10 trên prod không cần DevOps can thiệp
- [ ] Không còn P0 mở; P1 có hạn hoặc workaround có văn bản
- [ ] Close snapshot immutability đã verify
- [ ] Cost/Revenue/AP/AR quyền tách server-side đã verify
- [ ] Handoff ghi ngày UAT + commit + bugs còn lại

---

## Thứ tự làm việc gợi ý theo ngày

| Ngày | Việc |
|------|------|
| D1 | A1–A5 (Bill → rate → cost → revenue) |
| D2 | A6–A8 (chứng từ → AP/AR → payment/collection) |
| D3 | A9–A12 (recon → close → control/reports + triage bug) |
| D4–D5 | B1 fix P0/P1 + ship |
| D6+ | B2–B3 pixel; C khi rảnh |

Không làm C/D song song với A nếu còn P0.
