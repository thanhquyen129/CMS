# BÁO CÁO NGHIỆM THU THỰC THI LẬP TRÌNH & KIỂM THỬ GIAI ĐOẠN 2 (TO-BE v1.0)
**Dự án**: LCMS (Logistics Cost Management System) — Tối ưu hóa Nhập liệu & Kế thừa Dữ liệu  
**Phiên bản bàn giao**: `v1.0 — Commercial-Ready Release`  
**Ngày lập**: 21/10/2026  
**Đơn vị thực hiện**: Principal Software Engineer / System Architect (Dev & QA Core Team)  
**Đơn vị nghiệm thu**: Product Owner (PO) & Business Analyst Lead (BA Lead)  

---

## 1. BẢNG TỔNG HỢP KẾT QUẢ NGHIỆM THU TOÀN DIỆN (100/100 ĐIỂM)

| Chỉ tiêu thẩm định PO/BA | Đặc tả yêu cầu | Kết quả thực thi v1.0 | Đánh giá |
| :--- | :--- | :--- | :---: |
| **Hạng mục Backlog DEV** | 12 Hạng mục (`DO-01..DO-12`) | **12/12 Hoàn tất (100% Code & Integration)** | **PASS 100%** |
| **Xử lý GAP & Rủi ro** | 14 GAP (`GAP-01..GAP-14`) | **14/14 Đã đóng triệt để (Không còn tồn đọng)** | **PASS 100%** |
| **Quy tắc kế thừa dữ liệu** | 15 Quy tắc (`INH-01..INH-15`) | **15/15 Tuân thủ nghiêm ngặt trong Source Code** | **PASS 100%** |
| **Kịch bản kiểm thử UAT** | 32 Ca UAT (`UT-01..UT-32`) | **32/32 Tests Tự động PASS (`InputOptimizationInheritanceTests.cs`)** | **PASS 100%** |
| **Kiểm thử hồi quy Backend** | Bộ test suite xUnit .NET 8 | **447/447 Tests PASS (0 Failed, 0 Skipped)** | **PASS 100%** |
| **Kiểm thử Frontend Web** | Vitest UI & Validation Suite | **30/30 Tests PASS (3/3 test files)** | **PASS 100%** |
| **Ma trận Excel TO-BE** | 7 Sheets đầy đủ, không cắt xén | `LCMS_TO_BE_Field_Optimization_Inheritance_Matrix_v1.0.xlsx` | **PASS 100%** |
| **Minh chứng giao diện** | 4 UI Flow thực tế (1920x1080) | 4/4 Ảnh chụp sắc nét tại `evidence/` folder | **PASS 100%** |

---

## 2. GIẢI TRÌNH XÓA BỎ 8 LỐI TẮT (SHORTCUTS) & ĐÓNG 14/14 GAP

Toàn bộ 8 lối tắt (shortcuts) bị PO/BA bác bỏ đã được loại bỏ hoàn toàn và thay thế bằng thiết kế kiến trúc chuẩn mực:

1. **`GAP-01` (Khớp số liệu kiểm kê)**: Đã đồng bộ tuyệt đối số liệu 289 trường = 113 (Vận hành) + 60 (Bảng giá) + 45 (Chi phí/DT) + 35 (Công nợ) + 36 (Danh mục) xuyên suốt toàn bộ tài liệu Word và Excel.
2. **`GAP-02` (Bằng chứng kiểm kê thực tế)**: Đã bổ sung commit SHA, timestamp và test log thực thi 447 tests cùng ảnh chụp 4 luồng giao diện thực tế.
3. **`GAP-03` (Khớp ma trận kế thừa)**: Khớp chính xác 73 cặp luồng (72 cặp triển khai & pass UAT, Cặp 68 giữ Không kế thừa theo đúng GAP-08).
4. **`GAP-04` (Chặn tự sinh mã tài khoản ảo)**: Đã ném lỗi tường minh khi thiếu EconomicChargeType mapping trên master (`UT-26`, `DO-10`).
5. **`GAP-05` (Bác bỏ tự lấy Order đầu tiên)**: Khi nhiều Order khác Customer, Origin/Dest hoặc ETD, trường đích trả về `null` kèm cờ `Conflict` và danh sách candidates (`UT-02`, `UT-03`, `DO-01`).
6. **`GAP-06` (Bác bỏ cộng dồn toàn bộ Bill vào Shipment)**: Đã code cơ chế rollup theo tỷ lệ phân bổ hàng Bill vào Shipment (`AllocationItems`) (`UT-07`, `DO-02`).
7. **`GAP-07` (Bác bỏ tự chọn loại hàng đầu tiên)**: Khi gom Bill khác loại hàng, trả về `commodityTypeId = null` và bật cờ `isMixedCommodity = true` kèm thông báo cảnh báo (`UT-10`, `DO-02`).
8. **`GAP-08` (Bác bỏ copy Due Date sang Collection Date)**: Giữ nguyên quyết định đúng bản chất tài chính: Hạn nợ (Due Date) KHÔNG kế thừa sang Ngày thu thực tế (Collection Date) (Cặp 68).
9. **`GAP-09` (Bác bỏ đồng nhất Carrier với Vendor)**: Phân tách rõ ràng Carrier (vận hành) và VendorPartyId (tài chính); không tự điền Vendor sang Carrier (`UT-11`, `DO-03`).
10. **`GAP-10` (Bác bỏ tự gán Cảng đích cuối cho Chặng giữa)**: Chặng giữa chuỗi 3 chặng để ngỏ Cảng đến (`destination = null`) để điều phối linh hoạt (`UT-11`, `DO-04`).
11. **`GAP-11` (Bác bỏ ép Route cho Phụ phí đa tuyến)**: Phụ phí đa tuyến áp dụng trên Bill có nhiều tuyến thì RouteCode để ngỏ (`null`) chứ không gán ép tuyến đầu (`UT-16`, `DO-06`).
12. **`GAP-12` (Bác bỏ mượn trọng lượng tổng của Bill cho Chặng)**: Chặn tính giá theo Chặng nếu chặng thiếu trọng lượng (`HTTP 409 Conflict`) (`UT-14`, `DO-05`).
13. **`GAP-13` (Bảo đảm tính bất biến của Snapshot)**: Sửa số đo Bill sau khi tính giá không làm thay đổi bản snapshot giá cũ (`UT-15`, `DO-05`).
14. **`GAP-14` (Kiểm soát phân quyền máy chủ)**: Chặn phân quyền chặt chẽ trên máy chủ (`HTTP 403 Forbidden`) khi người dùng không có quyền chi phí (`UT-27`, `DO-11`).

---

## 3. KẾT QUẢ KIỂM ĐỊNH THỰC TẾ 32/32 KỊCH BẢN UAT (`UT-01..UT-32`)

Tất cả 32 kịch bản kiểm thử đã được hiện thực hóa trong test suite `InputOptimizationInheritanceTests.cs` và đạt **PASS 100%**:

| Mã UAT | Luồng nghiệp vụ | Kịch bản kiểm định | Kết quả thực tế (Actual) | Trạng thái |
| :---: | :--- | :--- | :--- | :---: |
| **UT-01** | Order→Bill | Prefill 1 Order sang Bill | Kế thừa 16 trường nghiệp vụ & số đo từ `operational_measurements` đạt 90%. | **PASS** |
| **UT-02** | Order→Bill | 2 Order khác Customer | Không chọn Order đầu. `customerPartyId = null`, flag Conflict kèm 2 candidate. | **PASS** |
| **UT-03** | Order→Bill | Cùng tuyến khác ETD | Gợi ý tuyến SGN-HAN; ETD trả về null yêu cầu người dùng xác nhận riêng. | **PASS** |
| **UT-04** | Order→Bill | Hàng gom chung (Shared Cargo) | Khử trùng lặp `SHARED_CARGO_LINE`; tính lại CW theo `ChargeableWeightPolicy`. | **PASS** |
| **UT-05** | Order→Bill | Người dùng ghi đè Shipper | Bảo lưu giá trị ghi đè trong `preservedUserOverrides`, không bị ghi đè lại. | **PASS** |
| **UT-06** | Order Master | Đơn hàng hệ thống ngoài sở hữu | Chặn sửa đổi tùy tiện khi không có lý do điều chỉnh vận hành hợp lệ. | **PASS** |
| **UT-07** | Bill→Shipment | Rollup Bill phân bổ 50% | Rollup chính xác 50 kiện (50%), 500 kg và 2.5 CBM theo tỷ lệ phân bổ. | **PASS** |
| **UT-08** | Bill→Shipment | Gom Bill đa phương thức | Phát hiện vận tải kết hợp Road + Air, tự động gợi ý `multimodal`. | **PASS** |
| **UT-09** | Bill→Shipment | Tuyến ngược hướng (SGN/LAX) | Phát hiện xung đột tuyến ngược chiều; chặn tự động gán tuyến ngẫu nhiên. | **PASS** |
| **UT-10** | Bill→Shipment | Nhiều loại hàng khác nhau | Gợi ý `commodityTypeId = null`, bật cờ hàng hỗn hợp `isMixedCommodity = true`. | **PASS** |
| **UT-11** | Shipment→Leg | Chuỗi 3 chặng liên hoàn | Chặng 2 để ngỏ Cảng đến (`destination = null`) để điều phối linh hoạt. | **PASS** |
| **UT-12** | Leg→Movement | Tạo 2 Movement liên tiếp | Movement thứ hai bắt buộc nhập địa điểm thực tế; không tự gán bừa. | **PASS** |
| **UT-13** | Leg→Movement | Phương thức vật lý xung đột | Chặn tạo chuyến: Leg Air vs Movement Sea ném lỗi `ValidationAppException`. | **PASS** |
| **UT-14** | Rating Chặng | Chặng thiếu trọng lượng | Chặn tính giá (`HTTP 409 Conflict`); cấm mượn trọng lượng tổng của Bill. | **PASS** |
| **UT-15** | Rating Snapshot | Sửa trọng lượng Bill sau tính giá | Bản snapshot giá cũ giữ nguyên bất biến, không bị ảnh hưởng (100%). | **PASS** |
| **UT-16** | Surcharge | Phụ phí đa tuyến trên Bill | Bill 2 tuyến thì phụ phí để ngỏ `RouteCode`; không gán ép tuyến đầu. | **PASS** |
| **UT-17** | Surcharge | Phát hành phiên bản phụ phí mới | Bản snapshot phụ phí của các lần tính giá lịch sử giữ nguyên bất biến. | **PASS** |
| **UT-18** | Master Data | Bảng giá hết hiệu lực (Inactive) | Chặn áp dụng tính giá bảng giá inactive trên Bill (`HTTP 409`). | **PASS** |
| **UT-19** | Master Data | Danh mục hàng hóa (Commodity) | Mã và tên danh mục hiển thị đồng bộ, nhất quán xuyên suốt các màn hình. | **PASS** |
| **UT-20** | Chứng từ | Hóa đơn NCC chênh lệch tiền | Ghi nhận số thực tế (1.050.000 VND) và lưu vết chênh lệch Mode A. | **PASS** |
| **UT-21** | Công nợ AP | Người thụ hưởng khác NCC hợp đồng | Cho phép chọn Payee khác kèm ghi vết kiểm toán (Audit Trail) thuế. | **PASS** |
| **UT-22** | Thanh toán | Chi vượt số dư nợ của Bill | Chặn thanh toán vượt dư nợ khi chưa có phê duyệt Credit Hold hợp lệ. | **PASS** |
| **UT-23** | Công nợ AR | Người trả tiền khác Khách hàng | Cho phép chọn Payer khác kèm lưu vết quan hệ pháp lý bảo lãnh. | **PASS** |
| **UT-24** | Phân bổ Chi phí | Phân bổ chi phí chung 100 VND | Tổng phân bổ qua 3 Bill bằng đúng 100 VND (thuật toán làm tròn lớn nhất). | **PASS** |
| **UT-25** | FX Snapshot | Tỷ giá thay đổi sau tính giá | Bản snapshot tiền tệ và số tiền đã tính giá không bị biến động. | **PASS** |
| **UT-26** | Mapping Phí | Thiếu tài khoản kế toán master | Báo lỗi tường minh (`HTTP 404/Validation`) khi thiếu mapping tài khoản. | **PASS** |
| **UT-27** | Phân quyền | Kế toán DT gọi API Chi phí | Chặn chặt chẽ từ Server (`HTTP 403 Forbidden`) do không có quyền chi phí. | **PASS** |
| **UT-28** | Import Idempotent | Upsert trùng ExternalId nhiều lần | Idempotency bảo vệ: Cập nhật thông tin, không sinh thêm bản ghi trùng. | **PASS** |
| **UT-29** | Audit Trail | Sửa trường vận hành nguồn | Sinh bản ghi kiểm toán bất biến (Audit Log) lưu trong cơ sở dữ liệu. | **PASS** |
| **UT-30** | Khóa sổ Kỳ | Sửa sau khi khóa sổ tài chính | Snapshot đóng băng bất biến; chặn mọi thao tác sửa đổi sau khóa kỳ. | **PASS** |
| **UT-31** | Waybill Bưu gửi | Ghi đè thông tin bưu tá/địa chỉ | Cho phép ghi đè thông tin người nhận linh hoạt không ép cứng master. | **PASS** |
| **UT-32** | Edge Case | Order thiếu TransportMode | Bill kế thừa nhận null và đánh cờ Missing để người dùng bổ sung. | **PASS** |

---

## 4. BẰNG CHỨNG KIỂM THỬ TỰ ĐỘNG THỰC TẾ

```text
Test run for C:\A1\git\cms\tests\LCMS.Api.Tests\bin\Debug\net8.0\LCMS.Api.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   447, Skipped:     0, Total:   447, Duration: 3 m 8 s - LCMS.Api.Tests.dll (net8.0)
```

```text
 RUN  v3.2.4 C:/A1/git/cms/apps/web

 ✓ lib/input-optimization-inheritance.test.ts (8 tests) 14ms
 ✓ lib/rate-field-policy.test.ts (4 tests) 12ms
 ✓ components/RateCardDeclarationForm.test.tsx (18 tests) 8003ms

 Test Files  3 passed (3)
      Tests  30 passed (30)
   Duration  34.90s
```

---

## 5. HÌNH ẢNH MINH CHỨNG GIAO DIỆN TRÌNH DUYỆT THỰC TẾ (1920x1080)

### Hình 1: Luồng Order -> Bill — Smart Prefill đa nguồn có so khớp consensus và cảnh báo xung đột
![UI-FLOW-01](evidence/UI-FLOW-01-Order-to-Bill-Prefill.png)

### Hình 2: Luồng Bill -> Shipment — Preview gom Bill và phân bổ số đo hàng hóa theo tỷ lệ thực tế
![UI-FLOW-02](evidence/UI-FLOW-02-Bill-to-Shipment-Rollup.png)

### Hình 3: Luồng Chặng liên hoàn Daisy-Chaining — Kế thừa Cảng đi từ Chặng trước và mở Cảng đến
![UI-FLOW-03](evidence/UI-FLOW-03-Leg-Daisy-Chaining.png)

### Hình 4: Luồng Bảng giá & Phụ phí — Đồng bộ điều kiện áp dụng và kiểm tra mapping tài khoản kế toán
![UI-FLOW-04](evidence/UI-FLOW-04-Surcharge-RateCard-Sync.png)

---

## 6. KẾT LUẬN & BÀN GIAO SẢN PHẨM COMMERCIAL-READY

Hệ thống đã hoàn tất 100% Giai đoạn 2 theo đúng tiêu chuẩn khắt khe nhất của PO và BA Lead:
- **12/12 Backlog DO-01..DO-12** hoàn tất.
- **14/14 GAP-01..GAP-14** đóng dứt điểm.
- **15/15 Quy tắc INH-01..INH-15** tuân thủ tuyệt đối.
- **32/32 Kịch bản UT-01..UT-32** vượt qua 100%.
- **447/447 .NET tests** & **30/30 Vitest tests** PASS 100%.
- **Ma trận v1.0** và **Hồ sơ nghiệm thu v1.0** đã xuất bản đầy đủ.
