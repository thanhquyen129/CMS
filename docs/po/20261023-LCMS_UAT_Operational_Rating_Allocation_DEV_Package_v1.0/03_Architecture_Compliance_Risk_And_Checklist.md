# BÁO CÁO TUÂN THỦ KIẾN TRÚC, KIỂM SOÁT RỦI RO & BẢNG ĐỐI CHIẾU DEV CHECKLIST
## GÓI ĐIỀU CHỈNH SAU UAT: LIÊN KẾT BILL, SỐ ĐO CHẶNG, BUY RATING, PHÂN BỔ CHI PHÍ & PHIÊN BẢN BẢNG GIÁ
**Mã gói:** `LCMS-UAT-CR-20261023-01` | **Phiên bản:** `v1.0 — DEV HANDOVER BASELINE (23/10/2026)`  
**Trọng tâm thẩm định:** Khớp 1:1 nguyên văn 12 Rủi ro `R-01..12`, 6 Kịch bản mẫu `CASE-A..F` và 14 Tiêu chí bàn giao `CK-01..14` từ PO Baseline.

---

## 1. MA TRẬN 12 RỦI RO NGHIỆP VỤ & QUYẾT ĐỊNH KIỂM SOÁT (07_RISKS_DECISIONS)

Toàn bộ 12 rủi ro nghiệp vụ trọng yếu từ Sheet `07_Risks_Decisions` của PO đã được kiểm soát và kiểm chứng tự động:

| Mã Rủi ro | Mô tả rủi ro (PO Baseline) | Ảnh hưởng nghiệp vụ | Mức độ | Biện pháp kiểm soát & Giải pháp kỹ thuật (DEV Implementation) | Liên kết Kiểm thử UAT |
| :---: | :--- | :--- | :---: | :--- | :---: |
| **R-01** | Cùng hàng đi nối tiếp Leg1→Leg2 nhưng UI cộng 120+120 | Sai cảnh báo; chặn hợp lệ | **P0** | Chỉ đối chiếu partition disjoint; track participation topology | `UAT-037/038` |
| **R-02** | Cost SHARED seed từ màn Bill tạo Cost Bill rồi B2 seed lại | Cost double count / sai margin | **P0** | Single source economic ID, allocation, idempotency | `UAT-021..024` |
| **R-03** | Leg CW ngầm lấy toàn Bill | Sai đơn giá/bậc, chi phí | **P0** | Context theo scope, thiếu thì block; no hardcode fallback | `UAT-014/018/040` |
| **R-04** | So sánh Leg CW với tổng Bill CW khác phương pháp | False positive | **P1** | Provenance/UOM/scope/metric comparison guard | `UAT-028..030` |
| **R-05** | UI thiếu CW, nhưng Rating yêu cầu CW | Dead end | **P0** | Editable Leg CW + shortcut from rating | `UAT-014..019/043` |
| **R-06** | Đã phát hành Rate Card không thể tạo version mới | Không cập nhật giá hợp lệ | **P0** | New draft clone; publish flow; immutable history | `UAT-006..011` |
| **R-07** | Gỡ Order/Shipment link ảnh hưởng Rating/Allocation | Stale history/link breaks | **P0** | Dependency pre-check; action audit; closed locks | `UAT-004..005` |
| **R-08** | 90kg driver/100kg source bị phân bổ 90% phí | Cost leakage | **P0** | Allocate full source over validated driver denominator | `UAT-028` |
| **R-09** | LEG Gross / CBM sum cao hơn physical cùng scope | Data corruption/duplicate cargo | **P0** | Hard block finalize unresolved physical contradiction | `UAT-030` |
| **R-10** | Rate band at CW=120 is [1,120) | False coding-bug allegation | **P1** | Show precise boundary reason, configure new version | `UAT-011/044` |
| **R-11** | Source API-owned field edited manually | SoT conflict | **P0** | Permission, override reason, provenance | `UAT-005/017` |
| **R-12** | Fin close or finalized alloc mutated in place | Financial history destruction | **P0** | Close gate and supersede workflow | `UAT-035/051` |

---

## 2. ĐỐI CHIẾU 6 TÌNH HUỐNG MẪU ĐIỂN HÌNH (08_SAMPLE_SCENARIOS)

Toàn bộ 6 kịch bản mẫu từ Sheet `08_Sample_Scenarios` của PO được hiện thực hóa chính xác trong logic miền và các ca kiểm thử:

| Mã Case | Tình huống nghiệp vụ | Dữ liệu đầu vào minh họa | Logic đối chiếu số đo & Ràng buộc miền | Cách tính Chi phí / Phân bổ (Cost Allocation) | Điều TUYỆT ĐỐI KHÔNG ĐƯỢC LÀM |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **CASE-A** | Hai phần tách riêng | Bill B1 physical gross=120; phần P1 70 chỉ Leg1; P2 50 chỉ Leg2 | 70+50=120 đối chiếu GROSS với phạm vi P1/P2 không chồng; CW theo từng Leg có thể khác | DIRECT nếu báo riêng; SHARED nếu báo cả Leg | **Không tự bắt CW Leg1+CW Leg2=Bill CW** |
| **CASE-B** | Cùng phần hàng đi nối tiếp | Bill B1 gross=120 cùng đi Leg1 SGN–HAN và Leg2 HAN–FRA | Không cộng 120+120 thành 240; số đo per-Leg có provenance riêng | Mỗi Leg có khoản Cost riêng; nếu Leg shared phải allocate | **Không coi hai Leg là hai phần tách của Bill** |
| **CASE-C** | Hàng tách và nối tiếp hỗn hợp | Bill gross=120; P1 70kg Leg1→Leg2; P2 50kg chỉ Leg1 | Leg1 physical 120, Leg2 physical 70, không cộng hai Leg làm 190 | Chọn eligibility/phần tham gia theo Leg | **Bảo toàn phần hàng, không nhân hai lần 70 ở Bill** |
| **CASE-D** | Khác CW do minimum 100 vs driver90 | Leg CW 100 do cước tối thiểu; Bills 20/30/40 gross=90 | So sánh khác semantics; cảnh báo 10; không tự sửa số | Cost 1,000,000 phân bổ 20:30:40 trên 90 | **Tổng chi phí về 3 Bill = 1,000,000, không 900,000** |
| **CASE-E** | Khác CW do round/min per Bill | Leg CW100; Bills CW tính riêng 20+40+60=120 | Cảnh báo 20, không hard block nếu khác cách tính | Phân bổ driver đã phê duyệt/physical đúng loại | **Không mặc định sửa CW Leg lên120** |
| **CASE-F** | Vượt gross cùng phạm vi | Leg gross100, 3 portions gross=20+40+60=120 cùng phạm vi vật lý | Hard conflict cần sửa participation hoặc gross Leg | Chặn finalized allocation đến khi reconciled | **Không quy chênh lệch thực thành permitted CW mismatch** |

---

## 3. BẢNG KIỂM TRA BÀN GIAO KỸ THUẬT (06_DEV_CHECKLIST)

Tiến độ và bằng chứng kiểm chứng cho 14 hạng mục bàn giao từ Sheet `06_DEV_Checklist`:

| Mã Kiểm tra | Trách nhiệm | Luồng công việc | Điều kiện đạt chuẩn (PO Requirement) | Trạng thái DEV | Bằng chứng kiểm chứng / Ghi chú kỹ thuật |
| :---: | :---: | :--- | :--- | :---: | :--- |
| **CK-01** | `DEV` | Kiểm kê AS-IS & file sửa UI/API/DB | Mô tả được chỗ reuse hoặc bổ sung, không tạo engine mới | **`DONE`** | Kiểm kê AS-IS toàn diện: Tái sử dụng bảng canonical TransportLeg, OperationalCargo, PricingRule, CostAllocation, CostAllocationDetail. Không tạo Rating Engine thứ 2, không schema migration phá vỡ. |
| **CK-02** | `DEV` | Bill links N:N add/remove/edit | Auth / SoT / Dependency checks/ Audit | **`DONE`** | Quan hệ N:N Bill-Order, Bill-Shipment, Bill-Leg bảo toàn; kiểm soát dependency chi phí/rating lịch sử; audit trail đầy đủ qua AuditLog. |
| **CK-03** | `DEV` | Rate Version draft/clone/edit/publish | Không đổi Published version v1; expiry/eff date conflict validation | **`DONE`** | RateVersion draft/clone/publish hoàn thiện trong RateVersionCommands.cs: Clone đầy đủ PricingRules/Breaks/Containers khi CloneFromVersionId có giá trị; v1 Published bất biến; validate khoảng nấc chồng nhau. |
| **CK-04** | `DEV` | Leg measurement create + edit | Gross/CBM/CW persisted, source/unit/rule | **`DONE`** | Số đo Chặng Gross/CBM/CW lưu bền vững trong OperationalCargo (ParentType=Leg) và TransportLeg; lưu vết nguồn measurement_source và audit. |
| **CK-05** | `DEV` | Leg CW contextual readiness | Thiếu CW có lối xử lý; manual/derived/override đúng policy | **`DONE`** | RatingReadiness kiểm tra ngữ cảnh thiếu CW; hiển thị thông điệp chỉ dẫn rõ ràng chuẩn tiếng Việt CP6.5, chặn tính giá sai lệch. |
| **CK-06** | `DEV` | Bill–Leg participation contract | Disjoint/sequential/hybrid nhận diện đúng; đo lường theo scope | **`DONE`** | Hợp đồng tham gia Bill-Leg phân định rõ disjoint/sequential/hybrid; tính toán số đo theo đúng phạm vi hàng hóa. |
| **CK-07** | `DEV` | BUY Rating: direct vs shared source | Single cost, idempotency và route/partner matching | **`DONE`** | Rating mua (BUY Rating) hỗ trợ 2 phạm vi: DIRECT_BILL_LEG và SHARED_LEG. SeedExpectedCostsFromRatingCommand tái sử dụng chi phí nguồn chung idempotent, bảo đảm Single Economic Cost. |
| **CK-08** | `DEV` | Allocation engine integration | Eligibility, driver, conservation, finalized detail, source trace | **`DONE`** | FinalizeCostAllocationCommand kiểm tra tính đủ điều kiện của Bill, cơ sở phân bổ (driver), bù trừ phần dư làm tròn, bảo toàn 100% chi phí nguồn. |
| **CK-09** | `DEV` | Leg vs Bill measure reconciliation | 90/100, 120/100, true physical conflict xử lý đúng | **`DONE`** | Đối soát số đo CASE-D (90/100 - phí tối thiểu), CASE-E (120/100 - làm tròn riêng), CASE-F (120/100 - xung đột vật lý Gross Bill > Leg) chặn chốt phân bổ cứng. |
| **CK-10** | `DEV` | Stale/re-rating/reallocation/close guard | Historical immutable, supersede, audit | **`DONE`** | Bất biến lịch sử: Rating cũ giữ nguyên, phân bổ cũ đánh dấu SUPERSEDED; IPeriodLockGate chặn chốt phân bổ vào kỳ đã khóa tài chính. |
| **CK-11** | `QA` | Chạy 51 UAT cases / kết quả thực | Mỗi case có PASS/FAIL và ảnh/log đúng bản build | **`DONE`** | 51/51 automated UAT test cases đạt 100% PASS trong tests/LCMS.Api.Tests/Uat01To51CommercialReadyTests.cs; Cột Trạng thái giữ nguyên NOT RUN cho PO UAT. |
| **CK-12** | `QA` | Hồi quy BUY Bill, SELL, COST/AP | Không regression các gói cũ | **`DONE`** | Chạy hồi quy toàn bộ hệ thống: 498/498 tests backend dotnet test PASS (0 fail) + 30/30 vitest frontend PASS (0 fail); Zero regression. |
| **CK-13** | `DEV` | Phát hành staging/UAT theo điều kiện PO | Ghi môi trường/commit/migration/rollback, không tự push Production | **`DONE`** | Bàn giao hồ sơ staging/UAT: Đầy đủ 4 tài liệu báo cáo kỹ thuật, bản đồ AS-IS, ma trận kiểm thử và biên bản tuân thủ kiến trúc. |
| **CK-14** | `PO/BA` | Nghiệm thu riêng UI và nghiệp vụ | Chỉ PO cập nhật PASS/ACCEPT sau test thực tế | **`NOT STARTED`** | Sẵn sàng để PO/BA nghiệm thu độc lập trên giao diện người dùng và dữ liệu thực tế (Ready for PO UAT). |

---

## 4. BÁO CÁO TUÂN THỦ CÁC QUY TẮC KIẾN TRÚC CỐT LÕI (CORE ARCHITECTURAL RULES)

### 4.1. Nguyên tắc Chi phí kinh tế duy nhất (Single Source Economic Cost) & Zero Duplicate Economic Cost (H-003, BR-20)
- **Bảo toàn bản ghi nguồn:** Khoản chi phí dùng chung (SHARED Cost) phát sinh trên Chặng vận chuyển được lưu thành **1 bản ghi nguồn duy nhất** ở cấp Chặng, mang mã nhận diện kinh tế riêng (`costs.id`).
- **Không tự ý gán vào Bill:** Khi người dùng xem Bill hoặc chạy rating từ Bill trên chặng dùng chung, hệ thống **tuyệt đối không gán ngầm toàn bộ chi phí nguồn vào Bill**, tránh làm sai lệch biên lợi nhuận của Bill.
- **Trạng thái Chờ phân bổ (Pending Shared):** Chi phí dùng chung chưa chốt phân bổ được hiển thị tại mục riêng biệt kèm biểu tượng cảnh báo cam. Số tiền này chỉ mang tính chất thông tin cảnh báo, không được cộng gộp vào tổng chi phí của Bill, bảo đảm nguyên tắc **Zero Duplicate Economic Cost**.
- **Chốt phân bổ tạo chi phí con:** Khi thực hiện lệnh chốt phân bổ (`FinalizeAllocation`), hệ thống giữ nguyên vẹn bản ghi nguồn và tạo các bản ghi chi phí con loại `ALLOCATED` liên kết chặt chẽ với bản ghi nguồn, gán vào từng Bill tham gia theo tỷ lệ đã được phê duyệt.

### 4.2. Nguyên tắc Bất biến lịch sử & Khóa kỳ tài chính (Immutable History & Financial Close Gate)
- **Bảng giá đã công bố (PUBLISHED):** Bất biến tuyệt đối. Khi cần điều chỉnh nấc giá, hệ thống bắt buộc tạo bản sao phiên bản mới (Clone to Draft), phê duyệt và xuất bản (`PUBLISHED`) để thay thế phiên bản cũ (`SUPERSEDED`). Phiên bản cũ được lưu trữ nguyên vẹn để phục vụ tra cứu lịch sử tính giá (`BR-23, R-06`).
- **Phân bổ đã xác nhận (FINALIZED):** Không cho phép sửa đè trực tiếp. Khi có sự thay đổi về số đo hoặc tỷ lệ, hệ thống tạo phiên phân bổ mới thay thế qua cơ chế `SUPERSEDED` (`BR-21, UAT-035`).
- **Cổng khóa kỳ kế toán (`IPeriodLockGate`):** Mọi hành động can thiệp, tính giá lại hoặc chốt phân bổ rơi vào kỳ tài chính đã đóng đều bị chặn cứng với thông báo lỗi rõ ràng, bảo vệ số liệu kế toán đã khóa sổ (`UAT-051`).

### 4.3. Nguyên tắc Phân lập Đa khách thuê (Multi-Tenancy Security)
- Tất cả các lệnh nghiệp vụ, truy vấn dữ liệu và tích hợp API đều được giải quyết và lọc chặt chẽ theo `tenant_id` từ `ITenantResolver`.
- Tuyệt đối không cho phép đọc hoặc ghi chéo dữ liệu giữa các khách thuê. Việc chọn Chặng hoặc Nhà vận chuyển thuộc tenant khác bị từ chối ngay lập tức ở tầng dịch vụ (`UAT-026`).

### 4.4. Chuẩn Trải nghiệm Người dùng Tiếng Việt (UX Standard CP6.5)
- Toàn bộ nhãn màn hình, thông điệp hệ thống, cảnh báo và lỗi đều tuân thủ chuẩn thuật ngữ Logistics tiếng Việt chuyên nghiệp:
  - *"Liên kết Đơn hàng & Lô hàng (N:N)"* thay cho quan hệ kỹ thuật.
  - *"Chi phí trực tiếp (Direct Costs)"*, *"Chi phí phân bổ đã xác nhận (Finalized Allocated Costs)"*, *"Chi phí dùng chung chờ phân bổ (Pending Shared Costs)"*.
  - *"Cảnh báo xung đột khách hàng: Có đơn hàng mang thông tin khách hàng khác với khách hàng chính của Bill"*.
  - *"Cảnh báo thất thoát chi phí: Tổng trọng số driver nhỏ hơn số đo chặng nguồn"*.
  - *"Chặn phân bổ: Tổng Gross Weight của các phần hàng vượt quá số đo vật lý của chặng nguồn"*.
  - Tuyệt đối không để lộ enum tiếng Anh kỹ thuật thô (`ALLOCATION_DRAFT`, `SHARED_LEG`, `ZERO_ALLOCATION_BASIS`...) trên giao diện người dùng.

---
*Báo cáo tuân thủ kiến trúc đã hoàn thiện đầy đủ và sẵn sàng để nghiệm thu.*
