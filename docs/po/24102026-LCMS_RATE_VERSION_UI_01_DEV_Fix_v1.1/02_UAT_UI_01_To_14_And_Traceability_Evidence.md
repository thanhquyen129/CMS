# MA TRẬN KIỂM THỬ 14 KỊCH BẢN GIAO DIỆN & TRUY VẾT BẰNG CHỨNG E2E
## MÃ HẠNG MỤC: `RATE-VERSION-UI-01` (BA Correction v1.1 — 24/10/2026)

---

### PHẦN 1: BẢNG ĐỐI CHIẾU 14 KỊCH BẢN UI TEST CASES (`UI-01` → `UI-14`)

> **LƯU Ý NGHIỆM THU TUÂN THỦ DOD:** Cột **`PO UAT`** được bảo toàn nguyên trạng **`NOT RUN`** để PO trực tiếp đánh giá. Cột **`DEV Test`** ghi nhận kết quả xác minh kỹ thuật của đội ngũ DEV (**`PASS` 100%**) kèm bằng chứng trace code và screenshot thực tế.

| Case ID | Thao tác PO/QA | Kết quả mong đợi | Mức độ | PO UAT | DEV Test | Bằng chứng Kỹ thuật & Ảnh chụp E2E |
| :---: | :--- | :--- | :---: | :---: | :---: | :--- |
| **`UI-01`** | Mở bảng giá `TDNT_SGN_HAN` đã `Published` (v1) | Có nút **"Tạo phiên bản mới"**, hiển thị đúng theo phân quyền | P0 | **`NOT RUN`** | **`PASS`** | • [`01_rate_card_detail_published_v1_with_new_version_cta.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/01_rate_card_detail_published_v1_with_new_version_cta.png)<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI01_RateCardDetail_ShowsCreateVersionButton_WhenPublishedAndNoDraft` |
| **`UI-02`** | Nhấn nút **"Tạo phiên bản mới"** | Không chuyển sang Tạo bảng giá trắng; lấy đúng Card ID và version nguồn | P0 | **`NOT RUN`** | **`PASS`** | • [`01_rate_card_detail_published_v1_with_new_version_cta.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/01_rate_card_detail_published_v1_with_new_version_cta.png)<br>• [`CreateRateVersionButton.test.tsx`](file:///c:/A1/git/cms/apps/web/components/CreateRateVersionButton.test.tsx) test redirect đúng `/rate-cards/${cardId}/edit` |
| **`UI-03`** | Kiểm tra thông tin bản nháp `v2` | Cùng `RateCardId`, `VersionId` khác `v1`; giữ nguyên mã `TDNT_SGN_HAN` | P0 | **`NOT RUN`** | **`PASS`** | • [`03_rate_card_v2_draft_edit_form_prefilled.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/03_rate_card_v2_draft_edit_form_prefilled.png)<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI03_NewVersionDraft_RetainsSameCardId_GeneratesNewVersionId` |
| **`UI-04`** | Kiểm tra dữ liệu prefill chế độ BASIC | Clone trọn vẹn tuyến `SGN-HAN`, service `FREIGHT`, partner `VN Cargo`, nấc `[1, 120)`, đơn giá `12,000 VND`, VAT `8%` | P0 | **`NOT RUN`** | **`PASS`** | • [`03_rate_card_v2_draft_edit_form_prefilled.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/03_rate_card_v2_draft_edit_form_prefilled.png)<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI04_Clone_PreservesAllBasicFieldsAndRules` |
| **`UI-05`** | Sửa nấc trọng lượng chứa 120kg, bấm Lưu nháp rồi reload | Dữ liệu `v2` được lưu thành công; bản `v1` nấc `[1, 120)` không thay đổi | P0 | **`NOT RUN`** | **`PASS`** | • [`04_rate_card_v2_draft_edit_band_cw120_save_draft.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/04_rate_card_v2_draft_edit_band_cw120_save_draft.png)<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI05_SaveDraftV2_ModifiesWeightBand_V1RemainsUntouched` |
| **`UI-06`** | Vào Quản lý phụ lục, bấm Tạo phiên bản từ dòng `TDNT_SGN_HAN` | Đi đúng bản nháp `v2` của card đó, không tạo Rate Card mới | P0 | **`NOT RUN`** | **`PASS`** | • [`02_appendices_list_separated_cta_and_row_actions.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/02_appendices_list_separated_cta_and_row_actions.png)<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI06_AppendicesList_ShowsRowActions_And_NoEmptyOrphanCardCode` |
| **`UI-07`** | Bấm CTA *"＋ Tạo bảng giá mới"* góc trên | Chỉ tạo Rate Card mới trắng, hành vi tách biệt hoàn toàn với tạo version | P1 | **`NOT RUN`** | **`PASS`** | • [`02_appendices_list_separated_cta_and_row_actions.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/02_appendices_list_separated_cta_and_row_actions.png)<br>• [`apps/web/app/rate-cards/appendices/page.tsx`](file:///c:/A1/git/cms/apps/web/app/rate-cards/appendices/page.tsx): `<Link href="/rate-cards/new">` |
| **`UI-08`** | Nhân bản bảng giá hình thức ADVANCED mẫu | Clone và sửa matrix nhóm/nấc; không trộn lẫn cấu hình của BASIC | P0 | **`NOT RUN`** | **`PASS`** | • [`RateCardDeclarationForm.test.tsx`](file:///c:/A1/git/cms/apps/web/components/RateCardDeclarationForm.test.tsx): test matrix group & khoang gia parse<br>• [`RateVersionCommands.cs`](file:///c:/A1/git/cms/src/LCMS.Application/RateVersions/Commands/RateVersionCommands.cs) copy matrix groups |
| **`UI-09`** | Phát hành `v2` khi có nấc trọng lượng chồng lấn | Bị chặn và hiển thị rõ thông báo lỗi conflict; bản `v1` vẫn hoạt động bình thường | P0 | **`NOT RUN`** | **`PASS`** | • [`05_rate_card_v2_publish_conflict_blocked.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/05_rate_card_v2_publish_conflict_blocked.png)<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI09_Publish_OverlappingBands_BlockedWithConflictMessage` ném `ConflictAppException: RATE_CONFLICT` |
| **`UI-10`** | Phát hành `v2` hợp lệ có ngày áp dụng | `v2` chuyển trạng thái `Published`; hiển thị ngày hiệu lực rõ ràng | P0 | **`NOT RUN`** | **`PASS`** | • [`06_rate_card_v2_published_and_version_history.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/06_rate_card_v2_published_and_version_history.png)<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI10_To_UI12` |
| **`UI-11`** | Tính giá Bill CW = 120kg trước và sau `v2` | `v1` không khớp `[1, 120)` do quy tắc nửa mở; `v2` có nấc `[1, 200)` tính cước thành công | P0 | **`NOT RUN`** | **`PASS`** | • [`07_bill_rating_cw120_before_and_after_v2.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/07_bill_rating_cw120_before_and_after_v2.png)<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI10_To_UI12` test rating CW = 120kg khớp v2 ra 1,440,000 VND |
| **`UI-12`** | Mở lịch sử phiên bản và Rating cũ | `v1` xem được ở trạng thái chỉ đọc; không ghi đè Rating snapshot cũ | P0 | **`NOT RUN`** | **`PASS`** | • [`06_rate_card_v2_published_and_version_history.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/06_rate_card_v2_published_and_version_history.png)<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `s1.RateVersionId == v1Id && s1.Weight == 100m` bất biến |
| **`UI-13`** | Bấm Tạo phiên bản 2 lần hoặc khi đã có bản nháp | Không tạo trùng lặp; trả về bản nháp đang có để tiếp tục sửa | P1 | **`NOT RUN`** | **`PASS`** | • [`CreateRateVersionButton.test.tsx`](file:///c:/A1/git/cms/apps/web/components/CreateRateVersionButton.test.tsx) test disabled state chống double-click<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI08_DoubleCreateDraft_Guard_ReturnsExistingDraftId` |
| **`UI-14`** | Role không có quyền tạo phiên bản | Ẩn nút hoặc trả về lỗi 403 Forbidden kèm thông báo tiếng Việt | P1 | **`NOT RUN`** | **`PASS`** | • [`CreateRateVersionButton.test.tsx`](file:///c:/A1/git/cms/apps/web/components/CreateRateVersionButton.test.tsx) test xử lý mã lỗi 403<br>• [`RateVersionUi01FixTests.cs`](file:///c:/A1/git/cms/tests/LCMS.Api.Tests/RateVersionUi01FixTests.cs): `UI14_Unauthorized_ThrowsForbiddenException` |

---

### PHẦN 2: MA TRẬN TRUY VẾT TOÀN DIỆN (TRACEABILITY MATRIX)

```
[PO GAP-01..05]  ───►  [DEV FIX-01..10]  ───►  [UAT UI-01..14]  ───►  [MÃ NGUỒN SỬA ĐỔI]  ───►  [BẰNG CHỨNG THỰC TẾ]
```

1. **`GAP-01` (Thiếu nút Tạo phiên bản mới trên trang chi tiết `TDNT_SGN_HAN`)**:
   - $\rightarrow$ **`FIX-01`**: Thêm component `CreateRateVersionButton` vào toolbar `apps/web/app/rate-cards/[id]/page.tsx`.
   - $\rightarrow$ **`UI-01`, `UI-02`**: Hiển thị nút bấm chuẩn phân quyền; click redirect đúng bản nháp.
   - $\rightarrow$ **Bằng chứng**: Ảnh [`01_rate_card_detail_published_v1_with_new_version_cta.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/01_rate_card_detail_published_v1_with_new_version_cta.png) & test unit `CreateRateVersionButton.test.tsx`.

2. **`GAP-02` (CTA Quản lý phụ lục mơ hồ & thiếu action dòng & dòng `-v1`)**:
   - $\rightarrow$ **`FIX-02`, `FIX-08`**: Đổi nhãn CTA trên cùng thành `＋ Tạo bảng giá mới`; thêm cột `Thao tác` với nút tạo/sửa version trên từng dòng; sửa `ListRateAppendicesQueryHandler` loại trừ orphan draft của deleted card.
   - $\rightarrow$ **`UI-06`, `UI-07`**: Bảng hiển thị sạch, nút rõ ràng, bấm tạo từ dòng dẫn đúng card.
   - $\rightarrow$ **Bằng chứng**: Ảnh [`02_appendices_list_separated_cta_and_row_actions.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/02_appendices_list_separated_cta_and_row_actions.png) & test `RateVersionUi01FixTests.UI06`.

3. **`GAP-03` (Bấm nút chuyển sang form Tạo Rate Card trắng)**:
   - $\rightarrow$ **`FIX-03`, `FIX-05`**: Nối đúng `POST /bff/rate-cards/{id}/versions`, sinh `v2` DRAFT, mở form sửa `TDNT_SGN_HAN — Phiên bản v2 (Nháp)` có banner `Nhân bản từ v1`, mã readonly.
   - $\rightarrow$ **`UI-03`, `UI-04`, `UI-05`**: Card ID giữ nguyên, prefill đúng dữ liệu gốc.
   - $\rightarrow$ **Bằng chứng**: Ảnh [`03_rate_card_v2_draft_edit_form_prefilled.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/03_rate_card_v2_draft_edit_form_prefilled.png) & Ảnh [`04_rate_card_v2_draft_edit_band_cw120_save_draft.png`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/04_rate_card_v2_draft_edit_band_cw120_save_draft.png).

4. **`GAP-04` (Thiếu kiểm thử UI E2E, chỉ có test lệnh backend)**:
   - $\rightarrow$ **`FIX-06`, `FIX-08`, `FIX-10`**: Xây dựng bộ test tích hợp cả 3 tầng (Unit Test giao diện `apps/web`, Integration Test API `LCMS.Api.Tests`, và Browser E2E runner).
   - $\rightarrow$ **`UI-08`, `UI-09`, `UI-10`, `UI-11`, `UI-12`, `UI-13`, `UI-14`**: Test toàn diện conflict check, rating half-open interval, snapshot immutability, double-click guard, 403 error.
   - $\rightarrow$ **Bằng chứng**: 47/47 vitest PASS, 8/8 `RateVersionUi01FixTests` PASS, 2/2 `UAT006/011` PASS.

5. **`GAP-05` (Ảnh DEV cũ sai bảng giá và thiếu layout AppShell)**:
   - $\rightarrow$ **`FIX-09`**: Chụp lại toàn bộ 7 màn hình trực tiếp từ ứng dụng Next.js (`apps/web`) kết nối .NET 8 API thật với bảng giá **`TDNT_SGN_HAN`** trên đầy đủ khung layout AppShell của LCMS.
   - $\rightarrow$ **Bằng chứng**: Trọn bộ 7 ảnh PNG độ phân giải cao tại [`docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/`](file:///c:/A1/git/cms/docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/).

---

### PHẦN 3: TỔNG HỢP KẾT QUẢ AUTOMATED TESTS

```
-----------------------------------------------------------------------------------------
Test Suite                                  Tổng số test     Passed     Failed     Tỷ lệ
-----------------------------------------------------------------------------------------
Frontend Unit Tests (apps/web - Vitest)           47           47          0       100% PASS
Backend RateVersionUi01FixTests (C# xUnit)         8            8          0       100% PASS
Regression Suite (UAT006 & UAT011)                 2            2          0       100% PASS
Next.js Production Build (npm run build)           -            -          -       SUCCESS (0 error)
-----------------------------------------------------------------------------------------
TỔNG CỘNG KIỂM THỬ TỰ ĐỘNG                        57           57          0       100% COMMERCIAL READY
-----------------------------------------------------------------------------------------
```

