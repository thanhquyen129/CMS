# Sổ 34 hạng mục — nguyên nhân và xử lý

DEV Test của mọi dòng dưới là **PASS** hoặc **NOT RUN** khi PO chưa cho dữ liệu (TC-22/23 nằm ở file ma trận). PO UAT = **NOT RUN**.

| Mã Issue | Mức | Root cause | Tác động | Quyết định | File | Bằng chứng | DEV | PO UAT |
|---|---|---|---|---|---|---|---|---|
| LCMS-UAT-BUG-20261029-01 | P0 | UI/API | Mất mode/service/kiện/kg sau reload | Fix | `UpdateBillContextCommand`, `OperationalCargoStore`, `revenues/page.tsx` | PATCH context ghi cột + measurement; overview không còn lấy mode làm loại DV | PASS | NOT RUN |
| LCMS-UAT-COST-PROJECTION-01 | P0 | API/UI | 3 nhóm bỏ phân bổ đã chốt nên profit 500.000 | Fix | `GetBillFinancialProfileQuery.cs`, `bills/[id]/page.tsx` | `finalizedAllocations` từ `cost_allocation_details` Finalized | PASS | NOT RUN |
| BILL-SERVICE-DISPLAY-01 | P0 | API/UI | `billType` hoặc mode lấp `serviceType` | Fix | `RatingReadiness.cs`, `SuggestBillRatesQuery.cs`, `revenues/page.tsx` | Cột Loại DV và Phương thức tách | PASS | NOT RUN |
| COST-RATING-SOURCE-01 | P0 | API | Thiếu detail thì UI bịa TDNT/QT0001; surcharge theo `source_type` | Fix | `CostQueries.cs` | Base rate `IsSurcharge=false`; thiếu card để trống, không điền mã mẫu | PASS | NOT RUN |
| BILL-SERVICE-MASTER-01 | P0 | API/UI | Đã chặn AIR service cho giao dịch mới | No Change | `UpdateBillContextCommand`, editor service catalog | AIR legacy chỉ hiện khi dữ liệu cũ; API reject mã mới AIR | PASS | NOT RUN |
| RATING-SEED-UI-01 | P0 | API/UI | BUY/SELL seed đã khóa chiều | No Change | `CreateRatingCommand`, seed buttons | Suite rating PASS, không đổi 1.740.000 | PASS | NOT RUN |
| RATING-STALE-NORMALIZE-01 | P0 | Policy | Case AIR/air không còn stale mới; cờ cũ giữ | No Change | `RateFieldPolicy.SameMode` | Test `Air_casing_is_the_same_mode`. Không DELETE stale | PASS | NOT RUN |
| MASTER-MODE-01 | P1 | UI | Dropdown lệch catalog | No Change | `catalogModeOptions`, `surcharges/new` | Form phụ phí lấy catalog active, không list cứng SEA/ROAD | PASS | NOT RUN |
| MASTER-SERVICE-01 | P1 | API/UI | AIR service vẫn là legacy | No Change | Editor + create bill services | Option AIR bị lọc khỏi catalog active | PASS | NOT RUN |
| COST-RATING-TRACE-UI-01 | P1 | UI | Trace đã có; mã giả làm sai chứng cứ | Fix | `CostQueries.cs`, `costs/[id]/page.tsx` | Rating id, version, rule, CW, VAT, vendor khi có FK | PASS | NOT RUN |
| SURCHARGE-FORMULA-TRACE-01 | P1 | UI | FIXED bị đọc như đ/kg | No Change | `costs/[id]/page.tsx` | Nhãn FIXED / khoản; đ/kg là tham khảo | PASS | NOT RUN |
| RATING-VERSION-MATCH-01 | P1 | API | Nấc [min, max) | No Change | `WeightBand` | v1 [1,120) không chứa 120; v2 [1,121) chứa | PASS | NOT RUN |
| RATING-BUY-REASON-01 | P1 | API/UI | Lý do chung | Fix | `BillRateSuggestion`, `CompareRatesForm` | Reason theo route/mode/service/date/weight | PASS | NOT RUN |
| RATING-RATE-DATE-TRACE-01 | P1 | API | ETD/manual/fallback đã có trên suggest | No Change | `SuggestBillRatesQuery` | `rateDateSource` etd / manual_override / policy_fallback | PASS | NOT RUN |
| COST-SEED-VAT-DISCLOSURE-01 | P1 | UI | Net/VAT/Gross trên rating panel | No Change | `BillRatingPanel` | Không tạo thêm dòng VAT | PASS | NOT RUN |
| LCMS-UAT-BUG-20261029-02 | P1 | API | Compare bỏ service và nuốt no-match | Fix | `CompareRatesQuery.cs` | Cùng classifier với Bill rating; lý do khi không khớp | PASS | NOT RUN |
| LCMS-UAT-BUG-20261029-03 | P1 | API | Compare lấy route của chính card | Fix | `RatingEngine.DirectedRoute`, `CompareRatesQuery` | SGN-HAN không ra giá HAN-SGN trừ `appliesAnyRoute` | PASS | NOT RUN |
| LCMS-UAT-REV-PARTNER-01 | P1 | UI | Picker không lọc role | Fix | `CreateRevenueForm.tsx` | `roleCode=customer`. Không gộp EMSL/KH0001 | PASS | NOT RUN |
| LCMS-UAT-REV-AUDIT-01 | P1 | API/UI | Confirm/actualize không ghi audit | Fix | `MaturityTransitionCommands.cs`, `AuditTrailPanel` | before/after maturity, actor, source | PASS | NOT RUN |
| LCMS-UAT-APPROVAL-ACTION-01 | P1 | UI | Maker vẫn thấy Phê duyệt | Fix | `DecideApprovalButton.tsx` | Disable + PC-21 khi `requestedBy` là user hiện tại | PASS | NOT RUN |
| LCMS-UAT-AR-EXPOSURE-01 | P1 | API/UI | Amount-only được tạo; UUID tay | Fix | Exposure commands, `CreateExposureForm.tsx` | Bắt Bill hoặc khoản hoặc chứng từ hoặc đối tác. Picker theo Bill | PASS | NOT RUN |
| LCMS-UAT-AR-RECOGNITION-01 | P1 | Policy/UI | Manual, không auto AR | No Change + dẫn hướng | `documents/[id]/page.tsx` | Accept không tạo AR. Link sang tạo quyền thu dự kiến | PASS | NOT RUN |
| LCMS-UAT-AR-IDEMPOTENCY-01 | P1 | API | Retry key có; hai exposure cùng revenue có thể vượt trần | Fix | `RecognizeReceivableExposureCommand.cs` | Tổng AR active của revenue không vượt Actual/Confirmed/Expected | PASS | NOT RUN |
| LCMS-UAT-REPORT-DIMENSION-01 | P1 | API | Báo cáo chỉ nhìn `route_id` | Fix | `ProfitabilityBoardQueries.cs` | Dùng `route_code` rồi origin-destination | PASS | NOT RUN |
| LCMS-UAT-REPORT-MATURITY-01 | P1 | UI | KPI không nói basis | Fix | `revenues/report/page.tsx`, `revenues/page.tsx` | Chú thích Best Available / current bucket / không cộng allocation vào gốc | PASS | NOT RUN |
| LCMS-UAT-BILL-COMMODITY-01 | P2 | UI | Danh mục trống im lặng | Fix | `OperationalReferenceEditor.tsx` | Câu hướng dẫn khi tenant chưa có loại hàng. Không bắt buộc | PASS | NOT RUN |
| RATE-DATE-UX-01 | P2 | UI | `input type=date` theo locale | Fix | `DateInputVi` trên compare, exposure, revenue | Hiển thị DD/MM/YYYY, submit ISO | PASS | NOT RUN |
| LCMS-UAT-REV-TAXONOMY-01 | P2 | UI | Ô mã tự do | Fix | `CreateRevenueForm.tsx` | Dropdown + "Chưa phân loại". Không validation cứng | PASS | NOT RUN |
| LCMS-UAT-REV-KPI-01 | P2 | UI | Nhãn KPI | Fix | `revenues/page.tsx` | "Số dư … trạng thái hiện hành" | PASS | NOT RUN |
| LCMS-UAT-REV-SPLIT-01 | P2 | API | Chia N bill | No Change | Revenue mapping tests trong suite | Tổng phần chia giữ nguyên; không viết test tiền mới trên dữ liệu UAT | PASS | NOT RUN |
| LCMS-UAT-APPROVAL-HISTORY-01 | P2 | UI | Mất hồ sơ sau duyệt | Fix | DEC-04 | Tab lịch sử | PASS | NOT RUN |
| LCMS-UAT-FIN-DOC-TRACE-01 | P2 | UI | Khớp nhìn như đã chấp nhận | Fix | `documents/[id]/page.tsx` | Ba chiều độc lập được viết rõ | PASS | NOT RUN |
| LCMS-UAT-FIN-UI-01 | P2 | UI | P&L trộn AR | Fix | DEC-03 | Hai bảng | PASS | NOT RUN |
| LCMS-CR-EXPOSURE-VI-01 | P2 | UI | Thuật ngữ lẫn | Fix | DEC-02 | Từ điển + màn AP/AR | PASS | NOT RUN |

## P0 / P1 — trace ngắn

Lineage giữ nguyên: Rate Card `TDNT_SGN_HAN` → Rating → `rating_details` (`source_type` base hoặc surcharge) → Cost/Revenue → chứng từ → Exposure → AR. Đợt này không ghi đè các id đó.

Compare BUY/SELL trước đây gọi `RatingEngine.Matches` với service null và `route = card.RouteCode`, nên bảng giá khớp chính nó và chiều ngược vẫn ra giá. Nay route là `origin-destination` của request. `appliesAnyRoute = true` vẫn là cờ mọi tuyến. Không thêm cột `is_bidirectional` (không migration).

Hồ sơ 3 nhóm trước cộng cost có `attribution ALLOCATED` trên Bill. Phân bổ 120.000 nằm ở detail của cost chung 240.000 (`bill_id` null), nên nhóm 2 = 0 và profit = 500.000. Query profile đã có số 120.000; UI nhận danh sách đó.

Exposure không kèm Bill, khoản, chứng từ, đối tác trả 400. Có đối tác (hạn mức) hoặc có Bill vẫn hợp lệ để không gãy sổ hiện có.

Case âm: confirm khi tenant `revenueMaturityModel=two` → 409, không tạo Confirmed. Compare HAN-SGN không chọn card SGN-HAN. Exposure body chỉ `{amount, currencyCode}` → 400.

## Hiển thị stale lịch sử

Rating `TESTLSS002` nếu còn `stale_reason` dạng `AIR → air` là cờ cũ. Logic mới không sinh thêm cờ đó và không xóa cờ cũ. PO có thể duyệt dry-run SELECT riêng; DEV không chạy UPDATE.
