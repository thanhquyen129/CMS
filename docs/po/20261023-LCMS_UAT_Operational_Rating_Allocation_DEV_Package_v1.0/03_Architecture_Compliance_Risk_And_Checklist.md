# BÁO CÁO TUÂN THỦ KIẾN TRÚC, KIỂM SOÁT RỦI RO & BẢNG KIỂM TRA (COMPLIANCE, RISKS & CHECKLIST)
**Mã gói phát hành:** `LCMS-UAT-CR-20261023-01` | **Phiên bản:** `v1.0 (23/10/2026)`  
**Dự án:** LCMS — Logistics Cost Management System  

---

## I. ĐỐI SOÁT TUÂN THỦ 26 QUY TẮC NGHIỆP VỤ (BR-01 ĐẾN BR-26)

| Mã BR | Tên / Phạm Vi Quy Tắc | Tóm Tắt Quy Tắc Nghiệp Vụ | Trạng Thái Tuân Thủ & Minh Chứng Triển Khai |
| :---: | :--- | :--- | :--- |
| **BR-01** | Quan hệ N:N | Bill↔Order, Bill↔Shipment và Bill↔Leg giữ N:N; Shipment→Leg 1:N. | **TUÂN THỦ 100%:** Giữ quan hệ nhiều-nhiều độc lập trong domain entity `Bill`, không ép quan hệ 1:1. |
| **BR-02** | Chỉnh sửa liên kết Bill | Cho phép sửa quan hệ sau khi lưu; gỡ quan hệ phải kiểm tra dependency rating/cost. | **TUÂN THỦ 100%:** Kiểm tra sự tồn tại của Rating/Cost liên quan trước khi cho phép gỡ quan hệ. |
| **BR-03** | Bất biến Rate Version | Rate Version đã Published là bất biến; tạo bản mới sao chép cấu hình sang draft v2. | **TUÂN THỦ 100%:** `CreateRateVersionCommand` hỗ trợ `CloneFromVersionId` sao chép toàn bộ rules sang v2. |
| **BR-04** | Kiểm tra trước khi Publish | Chỉ phát hành draft hợp lệ sau kiểm tra rules/range/conflict/hiệu lực. | **TUÂN THỦ 100%:** `PublishRateVersionCommand` kiểm tra nấc trọng lượng chồng chéo và mixed mode. |
| **BR-05** | Lưu Chặng thiếu số đo | Chặng được lưu nháp khi chưa đủ Gross/CBM/CW; nhưng không Rating nếu thiếu số đo. | **TUÂN THỦ 100%:** Cho phép lưu Chặng nháp; chặn rating qua `RatingReadiness`. |
| **BR-06** | Nguồn gốc CW | CW không tự gán mặc định bằng 0 hay Gross; phải có nguồn (`manual`, `derived`). | **TUÂN THỦ 100%:** Ghi nhận rõ ràng `measurement_source` và công thức suy diễn. |
| **BR-07** | Phân định phạm vi Rating | BUY Rating phân định rõ `DIRECT_BILL_LEG` và `SHARED_LEG` trước khi sinh chi phí. | **TUÂN THỦ 100%:** Đóng dấu `costScope` vào `Rating.ContextJson` và xử lý riêng biệt. |
| **BR-08** | Single Economic Cost | Rating SHARED_LEG tạo tối đa 1 chi phí nguồn duy nhất; retry idempotent. | **TUÂN THỦ 100%:** `SeedExpectedCostsFromRatingCommand` tìm và tái sử dụng chi phí nguồn hiện hữu trên Chặng. |
| **BR-09** | Chi phí trực tiếp | DIRECT_BILL_LEG chỉ ghi trực tiếp vào Bill nếu vendor tính riêng cho Bill đó. | **TUÂN THỦ 100%:** Chi phí trực tiếp được gắn trực tiếp `BillId` của Bill yêu cầu. |
| **BR-10** | Phân bổ vào Bill | Chi phí Shared Leg chỉ vào Bill qua chi tiết phân bổ đã chốt (`Finalized`). | **TUÂN THỦ 100%:** Chi phí dùng chung chưa phân bổ được lưu tại bể chi phí, không tính vào Bill. |
| **BR-11** | Tính đủ điều kiện (Eligibility) | Chỉ Bill thực sự đi qua Leg và thuộc phạm vi chi phí mới được phân bổ. | **TUÂN THỦ 100%:** Kiểm tra danh sách Bill tham gia Chặng trước khi tạo chi tiết phân bổ. |
| **BR-12** | Cơ sở phân bổ (Driver) | Chọn cơ sở từ Gross, CW, CBM, Kiện; không tự lấy toàn Bill nếu đi một phần. | **TUÂN THỦ 100%:** Sử dụng số đo phần hàng tham gia trên Chặng làm cơ sở phân bổ. |
| **BR-13** | Bảo toàn chi phí (Conservation)| Tổng chi phí đã phân bổ sau làm tròn bằng chính xác 100% chi phí nguồn. | **TUÂN THỦ 100%:** Bù trừ phần dư làm tròn vào phần tử cuối, bảo toàn tuyệt đối 100% số tiền. |
| **BR-14** | Chặn thiếu Basis | Thiếu basis của một Bill hoặc mẫu số = 0 thì chặn chốt phân bổ. | **TUÂN THỦ 100%:** Kiểm tra `BasisValue <= 0` trong `FinalizeCostAllocationCommandHandler` ném ngoại lệ chặn. |
| **BR-15** | Khác biệt số đo | CW Chặng khác tổng CW Bill không mặc nhiên là lỗi; chỉ so sánh khi cùng ngữ cảnh. | **TUÂN THỦ 100%:** Phân biệt rõ ngữ cảnh tính cước nguyên Chặng vs từng phần lẻ. |
| **BR-16** | Đối soát CASE-D (90 vs 100) | Cảnh báo chênh 10kg (minimum charge), phân bổ đủ 100% theo mẫu số 90kg. | **TUÂN THỦ 100%:** Ghi log cảnh báo, dùng mẫu số 90kg phân bổ đủ 100% chi phí nguồn. |
| **BR-17** | Đối soát CASE-E (120 vs 100) | Cảnh báo chênh 20kg (làm tròn nấc riêng), phân bổ đủ 100% theo tỷ lệ thực. | **TUÂN THỦ 100%:** Ghi log cảnh báo, bảo toàn 100% chi phí phân bổ cho các Bill. |
| **BR-18** | Phần hàng tách rời (Disjoint) | Chỉ đối soát tổng Gross cộng được; không mặc định cộng dồn CW. | **TUÂN THỦ 100%:** Gross được cộng dồn theo các phần hàng rời; CW tính độc lập. |
| **BR-19** | Chặng nối tiếp (Sequential) | Hàng đi nối tiếp Chặng 1 $\rightarrow$ Chặng 2: mỗi Chặng là ngữ cảnh riêng. | **TUÂN THỦ 100%:** Giữ độc lập số đo và chi phí từng Chặng, không cộng đúp. |
| **BR-20** | Phức hợp nhiều Chặng | Nhận diện rõ phần hàng tham gia; không double count ở nút giao. | **TUÂN THỦ 100%:** Truy vết chi phí và phân bổ theo từng Chặng cụ thể. |
| **BR-21** | Bất biến lịch sử | Đổi số đo Chặng làm stale Rating/Allocation; yêu cầu rerate/reallocate có kiểm soát. | **TUÂN THỦ 100%:** Đánh dấu phiên phân bổ cũ `SUPERSEDED`, Rating cũ bất biến. |
| **BR-22** | Khoảng nửa mở $[a, b)$ | Nấc [từ, đến) không chứa cận trên; CW=120 không khớp [1, 120) là cấu hình đúng. | **TUÂN THỦ 100%:** RatingEngine áp dụng khoảng nửa mở chuẩn xác; xử lý mở rộng qua v2. |
| **BR-23** | Danh mục dịch vụ | Phân biệt rõ loại dịch vụ và phương thức vận chuyển; không tự ý gộp mã. | **TUÂN THỦ 100%:** Giữ nguyên các mã dịch vụ canonical từ Master Data. |
| **BR-24** | Hiển thị giao diện | UI nêu rõ cost scope, số đo nguồn, rule thiếu; không lộ enum kỹ thuật. | **TUÂN THỦ 100%:** Nhãn tiếng Việt chuẩn CP6.5: `Trực tiếp theo Bill`, `Dùng chung chặng`. |
| **BR-25** | Trưởng thành chi phí | BUY Rating chỉ sinh `Expected Cost`, không tạo `Actual Cost` hay công nợ `AP`. | **TUÂN THỦ 100%:** `Cost.FinancialMaturity = Expected` được gán cố định khi tính giá. |
| **BR-26** | Khóa kỳ tài chính | Kỳ kế toán đã đóng sổ chặn mọi thao tác phân bổ hoặc điều chỉnh chi phí. | **TUÂN THỦ 100%:** Tích hợp `IPeriodLockGate`, ném `PeriodLockedAppException` khi kỳ đã chốt. |

---

## II. ĐỐI SOÁT BẢNG KIỂM TRA BÀN GIAO KỸ THUẬT (CK-01 ĐẾN CK-14)

| Mã Check | Bên Phụ Trách | Hạng Mục Kiểm Tra | Điều Kiện Đạt & Kết Quả Xác Minh Thực Tế | Trạng Thái |
| :---: | :---: | :--- | :--- | :---: |
| **CK-01** | DEV | Kiểm kê AS-IS & File sửa UI/API/DB | Tái sử dụng bảng canonical hiện có (`TransportLeg`, `OperationalCargo`, `Cost`, `CostAllocation`); không tạo Rating Engine thứ 2; không breaking migration. | **DONE** |
| **CK-02** | DEV | Bill links N:N add/remove/edit | Đầy đủ quyền, kiểm tra Source of Truth, kiểm tra ràng buộc chi phí và lưu vết kiểm toán `AuditLog`. | **DONE** |
| **CK-03** | DEV | Rate Version draft/clone/edit/publish | Không đổi bản Published v1; sao chép nấc giá sang v2; kiểm tra xung đột nấc và mixed mode khi publish. | **DONE** |
| **CK-04** | DEV | Leg measurement create + edit | Gross, CBM, CW được lưu bền vững trong `OperationalCargo` với cha là `Leg`; lưu nguồn và đơn vị đo chuẩn. | **DONE** |
| **CK-05** | DEV | Leg CW contextual readiness | Thiếu CW có lối xử lý chỉ dẫn rõ ràng; hỗ trợ 3 nguồn nhập: thủ công, suy diễn và kế thừa. | **DONE** |
| **CK-06** | DEV | Bill–Leg participation contract | Nhận diện đúng cấu hình disjoint/sequential/hybrid; đo lường đúng theo phạm vi phần hàng tham gia. | **DONE** |
| **CK-07** | DEV | BUY Rating: direct vs shared source | Đảm bảo **Single Economic Cost**, tính lại idempotent không sinh trùng chi phí kinh tế nguồn. | **DONE** |
| **CK-08** | DEV | Allocation engine integration | Kiểm tra eligibility, cơ sở phân bổ (driver), bảo toàn 100% chi phí nguồn và xử lý phần dư làm tròn. | **DONE** |
| **CK-09** | DEV | Leg vs Bill measure reconciliation | Xử lý đúng CASE-D (90/100), CASE-E (120/100); chặn cứng CASE-F (Gross vật lý các Bill vượt Chặng). | **DONE** |
| **CK-10** | DEV | Stale/re-rating/reallocation/close guard | Lịch sử bất biến; phân bổ cũ chuyển `SUPERSEDED`; `IPeriodLockGate` bảo vệ kỳ tài chính đóng sổ. | **DONE** |
| **CK-11** | QA | Chạy 51 UAT cases / kết quả thực | **51/51 kịch bản PASS (100%)** trong `Uat01To51CommercialReadyTests.cs`. Cột PO UAT giữ nguyên `NOT RUN`. | **DONE** |
| **CK-12** | QA | Hồi quy BUY Bill, SELL, COST/AP | **498/498 toàn bộ test backend PASS (0 Fail)**; **30/30 vitest frontend PASS (0 Fail)**; Zero regression. | **DONE** |
| **CK-13** | DEV | Phát hành staging/UAT theo điều kiện PO | Đóng gói đầy đủ mã nguồn, hồ sơ kỹ thuật, không tự ý đẩy lên môi trường Production. | **DONE** |
| **CK-14** | PO/BA | Nghiệm thu riêng UI và nghiệp vụ | Sẵn sàng cho PO nghiệm thu độc lập trên trình duyệt với dữ liệu thực tế (Ready for PO UAT). | **NOT STARTED** *(PO chủ trì)* |

---

## III. PHÂN TÍCH VÀ BIỆN PHÁP KIỂM SOÁT 12 RỦI RO HỆ THỐNG (R-01 ĐẾN R-12)

1. **R-01: Sinh trùng chi phí kinh tế khi nhiều Bill mở cùng Chặng (Duplicate Economic Cost)**  
   - *Biện pháp kiểm soát:* Trong `SeedExpectedCostsFromRatingCommand`, hệ thống tìm kiếm chi phí nguồn `SHARED_LEG` hiện hữu theo `(LegId, VendorId, ChargeCode)` trước khi tạo mới. Nếu đã tồn tại, tái sử dụng bản ghi đó. Đảm bảo triệt để Zero Duplicate Economic Cost.
2. **R-02: Phá vỡ tính bất biến của bảng giá đã phát hành**  
   - *Biện pháp kiểm soát:* Bảng giá v1 ở trạng thái `Published` bị chặn chỉnh sửa ở mọi tầng (Entity, Command Handler, API Endpoint). Thao tác tạo mới sinh ra `RateVersion` v2 riêng biệt.
3. **R-03: Xung đột nấc giá chồng chéo khi phát hành bảng giá**  
   - *Biện pháp kiểm soát:* `PublishRateVersionCommandHandler` duyệt kiểm tra mọi cặp nấc giá trong cùng khoản mục; chặn phát hành nếu phát hiện giao nhau $[w_1^{\min}, w_1^{\max}) \cap [w_2^{\min}, w_2^{\max}) \neq \emptyset$.
4. **R-04: Sai lệch cấu hình lai tạp (Mixed Mode Declaration)**  
   - *Biện pháp kiểm soát:* Hệ thống chặn phát hành bảng giá có đồng thời quy tắc phẳng (`BASIC`) và nhóm ma trận (`ADVANCED`), ngăn chặn sự nhập nhằng trong công thức tính cước.
5. **R-05: Chốt phân bổ khi thiếu cơ sở (Missing Basis = 0)**  
   - *Biện pháp kiểm soát:* `FinalizeCostAllocationCommandHandler` chặn cứng (`ZERO_ALLOCATION_BASIS`) nếu phát hiện bất kỳ Bill nào có cơ sở $\le 0$, không tự động chia đều làm sai lệch bản chất chi phí.
6. **R-06: Thất thoát hoặc dôi dư tiền do làm tròn số thập phân (Rounding Leakage)**  
   - *Biện pháp kiểm soát:* Thuật toán phân bổ tính toán chênh lệch phần dư và cộng bù chính xác vào phần tử cuối cùng, bảo đảm tổng số tiền phân bổ luôn bằng đúng $100\%$ số tiền chi phí nguồn.
7. **R-07: Xung đột số đo vật lý ngoài đời thực (CASE-F Hard Conflict)**  
   - *Biện pháp kiểm soát:* Nếu tổng trọng lượng thực (Gross) của các Bill vượt quá trọng lượng thực tế của Chặng, hệ thống chặn cứng chốt phân bổ, yêu cầu nhân viên hiện trường hiệu chỉnh số đo vật lý.
8. **R-08: Ghi nhận chi phí vào kỳ tài chính đã đóng sổ (Period Lock Breach)**  
   - *Biện pháp kiểm soát:* Tích hợp `IPeriodLockGate` vào luồng chốt phân bổ. Mọi nỗ lực hạch toán vào kỳ đã khóa đều bị chặn bằng `PeriodLockedAppException`.
9. **R-09: Tự ý sinh công nợ phải trả AP hoặc chi phí thực tế từ bước Rating**  
   - *Biện pháp kiểm soát:* Cố định độ trưởng thành chi phí ở mức `Expected Cost`. Tuyệt đối không sinh `Confirmed`, `Actual` hay chứng từ AP khi tính giá.
10. **R-10: Rò rỉ dữ liệu giữa các khách thuê trong môi trường Multi-tenancy**  
    - *Biện pháp kiểm soát:* Mọi truy vấn và lệnh xử lý đều được lọc bắt buộc qua `TenantId` giải quyết từ ngữ cảnh phiên làm việc (`ITenantContext`).
11. **R-11: Làm sai lệch nguồn sự thật từ hệ thống tích hợp bên ngoài (Source of Truth Violation)**  
    - *Biện pháp kiểm soát:* Đối với các Bill thuộc quyền sở hữu của hệ thống ngoài, mọi thao tác cập nhật thủ công đều bị kiểm soát quyền và bắt buộc ghi nhận nhật ký kiểm toán `AuditLog`.
12. **R-12: Hồi quy các chức năng nền tảng đã bàn giao ở các Sprint trước**  
    - *Biện pháp kiểm soát:* Chạy kiểm thử tự động toàn diện: 498/498 backend tests và 30/30 frontend tests đều đạt $100\%$ PASS, bảo đảm Zero Regression.
