# CMS Ledger — Theme design brief

Bộ theme gốc cho **Cost Management System**: lớp kiểm soát tài chính logistics (Bill-centric), không phải TMS và không phải dashboard hóa đơn SaaS.

## Tiêu chí thiết kế (vì sao làm vậy)

1. **Tin cậy trước “đẹp lung linh”**  
   Người dùng là kế toán / controller / điều vận tài chính. Màu quá vui (tím neon, teal marketing, thẻ tín dụng) làm hệ thống trông như demo — giảm niềm tin khi xác nhận chi phí, đối soát, chốt kỳ.

2. **Số tiền và trạng thái đọc lướt được 8 giờ/ngày**  
   Nền giấy lạnh, chữ mực đậm, bảng có sọc nhẹ, bán kính vừa phải. Tránh pill-full và shadow dày — chúng làm nhiễu khi quét nhiều cột số.

3. **Accent = hành động tài chính, không = trang trí**  
   Jade sâu dùng cho CTA chính (xác nhận, tất toán, hoàn tất đối soát). Đỏ chỉ cho rủi ro/ngoại lệ. Vàng đất cho chờ xử lý — không dùng màu “cảnh báo” cho mọi badge.

4. **Phản ánh nghiệp vụ CMS, không copy Invoika/Kubayar**  
   CMS có Received ≠ Accepted ≠ Matched ≠ Settled ≠ Closed. Theme phải trung tính để status pill mang nghĩa nghiệp vụ, không bị primary color “nuốt” mọi trạng thái.

5. **Sidebar dọc là mặc định vận hành**  
   Nav dài (Bill, chứng từ, AP/AR, thanh toán, sao kê, hàng đợi, chốt…). Horizontal phù hợp marketing template; vertical phù hợp bàn làm việc tài chính.

6. **Tương phản & a11y bàn làm việc**  
   Mục tiêu tương phản chữ chính ≥ WCAG AA trên nền sáng; focus ring rõ; không phụ thuộc màu đơn thuần để phân biệt trạng thái (vẫn có chữ trên pill).

7. **Một skin sản phẩm, nhiều preset phụ**  
   Ledger = chuẩn sản phẩm. Invoika / Soft Purple / Classic giữ làm tùy chọn — không đảo chuẩn vận hành vì template tham khảo.

## Token cốt lõi

| Token | Giá trị | Vai trò |
|-------|---------|---------|
| Ink | `#15202b` | Chữ chính, số tiền |
| Muted | `#5c6b7a` | Nhãn phụ |
| BG | `#f4f6f8` | Nền giấy làm việc |
| Surface / chrome | `#ffffff` | Panel + vùng nội dung sáng |
| Sidebar (vertical) | `#123047` | Nav cấp 1 theo PO baseline |
| Line | `#d8dee6` | Viền sổ |
| Accent | `#0f6b58` | CTA / link / active |
| Danger | `#a61b1b` | Ngoại lệ / âm |
| Warning | `#9a5b12` | Chờ / lệch |
| Radius | `8px` | Chuyên nghiệp, không “toy UI” |

**Ghi chú (2026-09-16):** Sidebar dọc Ledger đổi lại **navy `#123047`** theo PO UI/UX Baseline v1.0 (UI-01…UI-15). Vùng nội dung vẫn nền giấy sáng; accent jade giữ cho CTA tài chính.

## Non-goals

- Dark mode toàn app (lát sau, cần audit contrast riêng)
- Gradient hero / glassmorphism / badge “NEW!”
- Đồng nhất success color với accent marketing teal Invoika
