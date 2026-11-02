LCMS  |  BA / DEV READY

CHUẨN HÓA CƠ CHẾ ÁP DỤNG PHỤ PHÍTRÊN TOÀN HỆ THỐNG LCMS

Tách Điều kiện áp dụng – Chính sách kích hoạt – Phương pháp tính  |  Chặn tự cộng phụ phí không có căn cứ nghiệp vụ

THUỘC TÍNH

GIÁ TRỊ

Mã yêu cầu

LCMS-CR-SURCHARGE-APPLICATION-01

Issue liên quan

SURCHARGE-APPLICABILITY-01; SURCHARGE-VERSION-DETAIL-01

Phiên bản / ngày

v1.0  /  02/11/2026

Loại tài liệu

BA Change Request + Business Rules + Acceptance / UAT Scenarios

Ưu tiên

P0 – Sửa ngay / kiểm soát rủi ro tính thừa Cost & Revenue

Quyết định PO

Đã yêu cầu xử lý ngay cho TOÀN BỘ phụ phí, không chỉ DONGGOI

Trạng thái

BA sẵn sàng giao DEV; chưa có bằng chứng DEV đã sửa; chưa nghiệm thu Production

Phạm vi

Surcharge Master & Version; Bill/Shipment/Leg Rating Context; BUY/SELL; Rating/Seed; Audit; migration; UI

NGUYÊN TẮC CỐT LÕI  Khớp bảng giá, tuyến, phương thức hoặc thời gian hiệu lực chỉ xác định một phụ phí là ứng viên hợp lệ; CHƯA đồng nghĩa khoản phí đó đã phát sinh. Muốn cộng vào Rating phải thỏa chính sách kích hoạt và có căn cứ nghiệp vụ tương ứng.

1. Mục đích và quyết định triển khai

PO quan sát thấy nhiều/hầu như mọi phụ phí trong LCMS được tự động cộng khi bấm Tính giá mua/bán. Có nguy cơ hệ thống tính cả các dịch vụ chưa được sử dụng (ví dụ đóng gói), kéo theo Expected Cost, Expected Revenue và Profitability không phản ánh thực tế. PO yêu cầu lập BA để DEV sửa NGAY, không chờ tổng hợp các issue UAT khác.

Giữ kiến trúc đã khóa: Rate Card và Surcharge là hai Pricing Object độc lập, cùng được Rating Engine đánh giá.

Tách ba tầng: (1) Điều kiện giá/phạm vi hợp lệ, (2) Chính sách kích hoạt khoản phí, (3) Công thức tính số tiền.

Không chuyển tất cả phụ phí thành tùy chọn: phụ phí thực sự bắt buộc phải tự cộng khi có đủ chính sách và điều kiện hợp lệ.

Không sửa trực tiếp Published Surcharge Version, Rating snapshot, Expected Cost/Revenue hoặc chứng từ đã phát sinh lịch sử.

2. Căn cứ và đánh giá AS-IS

Nguồn

Bằng chứng / nguyên tắc

Mức xác nhận

BA kiến trúc 05/10/2026

SUR-04: phải có Applicability Rule; SUR-10: Base Rate + applicable surcharges. Field không khai báo được hiểu là không giới hạn theo trường đó.

Baseline đã chốt

UAT 26–27/10

Phụ phí DONGGOI 300.000đ đi cùng Base Freight 1.440.000đ trong BUY Rating TESTLSS002; xử lý Cost phải trace độc lập tới Rating Detail.

Đã quan sát

Ảnh 02/11/2026

DONGGOI_SGN_HAN_MUA: v2 đã phát hành; v3 nháp. Form nháp thể hiện BUY, SGN–HAN, FIXED_RATE, 300.000 VND, phương thức không giới hạn, mọi bảng giá phù hợp, không thấy trường bắt buộc/tùy chọn.

Xác nhận UI v3; KHÔNG suy v2 giống v3

Phản ánh mới của PO

Nhiều phụ phí có biểu hiện được tự cộng khi Rating; SELL cũng cần kiểm tra riêng vì ảnh DONGGOI đang là chiều BUY.

Hiện tượng UAT; root chưa xác minh

Khả năng tra cứu version

Không thể mở v2 từ bảng phiên bản hiện tại.

UI GAP cần xác minh

PHÂN BIỆT LỖI & GIẢ THUYẾT  Chưa có code/DB của phiên bản đang chạy để kết luận engine đang bỏ qua điều kiện nào. DEV phải tái hiện với dữ liệu thực tế trước khi sửa. Tuy nhiên nguy cơ tính thừa phụ phí đã đủ để ưu tiên P0 và yêu cầu chặn áp dụng không có căn cứ.

3. Phạm vi IN / OUT

IN – Bắt buộc

OUT – Không tự ý thay đổi

Toàn bộ mã phụ phí/tất cả Tenant áp dụng chức năng; BUY và SELL; master, version, rule, scope, eligibility, activation, calculation.

Không nhập phụ phí trở lại Rate Card; không biến mọi phụ phí thành tùy chọn.

Màn hình tạo/chỉnh sửa phụ phí; xem chi tiết version; Bill/Shipment/Leg chọn dịch vụ; kết quả Rating; Seed; audit; báo cáo phụ phí.

Không viết lại Published Version hoặc kết quả Rating/Cost/Revenue đã tạo trong lịch sử.

Migration phân loại và phát hành version mới; feature flag/rollout; test hồi quy theo Tenant.

Không mở rộng sang sửa AP/AR, Financial Document hoặc chính sách Revenue Maturity nếu không bị tác động trực tiếp.

4. Mô hình nghiệp vụ TO-BE

4.1 Ba khái niệm bắt buộc tách riêng

Khái niệm

Trả lời câu hỏi

Ví dụ

Eligibility / Applicability

Phụ phí nào đủ điều kiện về đối tác, tuyến, chiều, thời gian, phương thức, phạm vi?

DONGGOI BUY SGN–HAN ở ngày hiệu lực

Activation Policy

Khoản phí có căn cứ phát sinh chưa? Tự động hay cần lựa chọn/xác nhận?

Bill có yêu cầu đóng gói: Có / Không

Calculation Method & Basis

Nếu được áp dụng, tính bao nhiêu và theo đơn vị nào?

FIXED_RATE = 300.000 VND / đơn vị tính đã công bố

4.2 Ba chính sách kích hoạt chuẩn

Mã / loại

Cơ chế kích hoạt

Yêu cầu tối thiểu

MANDATORY_BY_POLICY – Bắt buộc

Tự cộng SAU KHI match eligibility. Không cần người dùng tích chọn riêng.

Có quyết định/chính sách áp dụng rõ, người có thẩm quyền duyệt, phạm vi phù hợp; không được dùng cho mọi loại phí chỉ vì thiếu điều kiện.

AUTO_CONDITIONAL – Tự động theo dữ liệu

Tự cộng nếu predicate nghiệp vụ từ nguồn dữ liệu đáng tin cậy trả TRUE.

Ví dụ isDangerousGoods=true, deliveryOutsideHours=true; false/unknown không tính; lưu nguồn dữ liệu và phiên bản rule.

OPTIONAL_OPT_IN – Tùy chọn có xác nhận

Chỉ cộng khi có lựa chọn yêu cầu dịch vụ hoặc sự kiện xác nhận hợp lệ liên kết đúng Rating Context.

Ví dụ hasPackagingService=true / service order; chọn bởi vai trò có quyền; lưu thời gian, người chọn, nguồn, phạm vi & số lượng.

4.3 Trạng thái chuyển đổi dữ liệu cũ

UNCLASSIFIED_LEGACY là trạng thái TẠM THỜI phục vụ migration, không phải chính sách có thể phát hành mới. Một phụ phí chưa được phân loại không được tự động cộng vào Rating mới. Phải hiển thị cảnh báo/exception và đưa vào danh sách quản trị bắt buộc xử lý; tránh âm thầm giảm giá hoặc cộng thừa.

5. Quy tắc nghiệp vụ (Business Rules)

BR-SUR-001 [MUST]  Tất cả Surcharge Rules đang phát hành và dùng cho Rating mới phải có activation_policy hợp lệ; các bản legacy chưa phân loại không được coi là bắt buộc mặc định.

BR-SUR-002 [MUST]  Một phụ phí được áp dụng khi và chỉ khi: published/effective/tenant/direction/scope/eligibility đều hợp lệ VÀ activation được xác minh TRUE. Phương pháp tính chỉ chạy sau bước này.

BR-SUR-003 [MUST]  Các điều kiện giá bỏ trống có thể không giới hạn phạm vi giá, nhưng TUYỆT ĐỐI không được suy ra activation=true đối với phí tùy chọn hoặc tự động có điều kiện.

BR-SUR-004 [MUST]  MANDATORY_BY_POLICY phải có căn cứ chính sách được lưu, người phê duyệt, phạm vi, ngày hiệu lực, version; không cho bật bắt buộc chỉ bằng cách để trống mọi điều kiện.

BR-SUR-005 [MUST]  AUTO_CONDITIONAL yêu cầu ít nhất một predicate nghiệp vụ có nguồn dữ liệu, operator, giá trị mong đợi; thiếu dữ liệu = UNKNOWN, không được chuyển thành TRUE ngầm.

BR-SUR-006 [MUST]  OPTIONAL_OPT_IN chỉ áp dụng khi có service selection/evidence đã lưu hợp lệ, khớp Bill/Shipment/Leg và còn hiệu lực; preview chọn tạm nhưng không lưu không được trở thành sự thật tài chính.

BR-SUR-007 [MUST]  Giá mua và giá bán áp dụng độc lập; BUY → Expected Cost, SELL → Expected Revenue; cùng một dịch vụ lựa chọn không tự suy ra SELL có giá nếu chưa có surcharge SELL tương ứng.

BR-SUR-008 [MUST]  Nếu sự kiện dịch vụ áp dụng cho một Shipment gồm nhiều Bill, phải có rõ scope và quy tắc allocation; không tự nhân phí một lần của Shipment cho mọi Bill.

BR-SUR-009 [MUST]  Tách cách tính FIXED_RATE khỏi cơ sở tính (per Bill/per Shipment/per Leg/per Unit). 300.000đ cố định không có nghĩa luôn tính 300.000đ một lần trong mọi ngữ cảnh.

BR-SUR-010 [MUST]  Một economic surcharge item chỉ xuất hiện một lần trong cùng tập kết quả hợp lệ sau precedence/deduplication; tránh cộng hai rule/version cùng mã khi cùng scope.

BR-SUR-011 [MUST]  Nếu có nhiều phiên bản/rule hoặc giá cùng ưu tiên gây mơ hồ, Rating hiển thị AMBIGUOUS và không tự cộng các dòng xung đột.

BR-SUR-012 [MUST]  Rating preview trả rõ 3 tập: Applied / Eligible but not activated / Excluded, kèm lý do machine-readable và giải thích tiếng Việt.

BR-SUR-013 [MUST]  Seed chỉ cho các dòng Rating đã được áp dụng hợp lệ; retry idempotent; thay đổi selection phải rerate/tạo Rating mới, không sửa Rating snapshot đã tồn tại.

BR-SUR-014 [MUST]  Không sửa Published Surcharge Version; thay đổi activation/scope/calculation phải phát hành version mới theo effective time và preserve historical audit.

BR-SUR-015 [MUST]  Bản ghi selection/activation phải truy xuất ai/chọn khi nào, nguồn liên kết, số lượng, actor/tenant và revision; thao tác gỡ chọn không xóa audit.

BR-SUR-016 [MUST]  Nếu dữ liệu activation thiếu/sai/unknown: không âm thầm cộng phụ phí; hiển thị cảnh báo khi tính giá và báo trường dữ liệu cần hoàn thiện.

BR-SUR-017 [MUST]  Tùy chọn dịch vụ không có giá phù hợp vẫn lưu yêu cầu dịch vụ nhưng phải cảnh báo UNPRICED; không tự ghép BUY→SELL hoặc dựng giá bằng 0 một cách thiếu giải thích.

BR-SUR-018 [MUST]  Không tạo “mandatory=true” hàng loạt từ dữ liệu legacy có điều kiện trống. Mỗi mã/tenant/direction cần quyết định phân loại có căn cứ và kiểm soát phê duyệt.

BR-SUR-019 [MUST]  Các bộ lọc cargo/route/partner/weight từ baseline vẫn là eligibility; thêm activation không được vô hiệu hóa bất kỳ ràng buộc tồn tại nào.

BR-SUR-020 [MUST]  Khi nguồn khác nhau (Bill/Shipment/Leg) có trạng thái dịch vụ xung đột, engine áp precedence theo source-of-truth policy, hoặc báo DATA_CONFLICT; không tự đoán.

BR-SUR-021 [MUST]  Đối với phụ phí theo tỷ lệ (PERCENTAGE), chỉ tính trên base được cấu hình; không tính phần trăm trên các phụ phí khác nếu policy không cho phép; phòng vòng lặp/stacking sai.

BR-SUR-022 [MUST]  Kết quả phản ánh FX, VAT, rounding và maturity theo baseline hiện hành; thay đổi activation không được phát sinh Actual Cost/Revenue.

BR-SUR-023 [MUST]  Phải có quyền cấu hình/duyệt MANDATORY_BY_POLICY, quyền chọn/tắt optional; người dùng thường không được chuyển policy đã published.

BR-SUR-024 [MUST]  Báo cáo impact phải thể hiện trước/sau theo Bill/Shipment/Leg/tenant, tổng số phụ phí tự áp sai tiềm năng và số rule UNCLASSIFIED trước khi rollout.

6. Luồng người dùng và Rating Engine

6.1 Cấu hình và phát hành phụ phí

1.  Quản trị tạo bản Draft phụ phí, chọn direction, scope, calculation method/basis và activation_policy; hệ thống validate các trường bắt buộc theo policy.

2.  Nếu MANDATORY: nhập căn cứ và quy tắc rõ ràng; nếu AUTO: khai báo predicate dữ liệu; nếu OPTIONAL: gắn mã dịch vụ/đối tượng chọn và phạm vi selection.

3.  Chạy “Thử điều kiện” trên bộ Rating Context mẫu; hiển thị nguyên nhân bao gồm/exclude và amount dự kiến; reviewer phê duyệt phát hành version mới.

4.  Phiên bản Published chỉ có chức năng Xem chi tiết/Xem lịch sử; không sửa trực tiếp, không nhấn bản version để sửa Draft nhầm.

6.2 Lập Bill và lựa chọn dịch vụ

1.  Người dùng tạo Bill/Shipment/Leg với ngữ cảnh vận hành. Khu “Dịch vụ/phụ phí tùy chọn” trình bày lựa chọn có tên, scope, đơn vị, số lượng, trạng thái giá khả dụng.

2.  Người dùng đánh dấu cần đóng gói khi thực sự có yêu cầu dịch vụ; LCMS lưu selection/evidence; không mặc định tích sẵn phụ phí OPTIONAL.

3.  Khi bấm Tính giá BUY/SELL, Rating Engine đọc ngữ cảnh đã lưu + selection + các sự kiện nghiệp vụ có hiệu lực và phiên bản Published.

4.  Kết quả chia cước chính, phụ phí đã cộng, phụ phí chưa được cộng với lý do; chọn thiếu điều kiện không được tạo Cost/Revenue.

5.  Khi người dùng đổi selection hoặc thời gian dịch vụ, phải tính lại giá và tạo snapshot mới; không ghi đè các bản đã Seed trước đó.

6.3 Thuật toán chuẩn

Bước

Kiểm tra

Nếu không đạt

1

Chọn Surcharge Published + valid time + tenant + BUY/SELL

EXCLUDED: INACTIVE / WRONG_DIRECTION

2

Match mode/service/route/party/cargo/qty/optional rate version scope

EXCLUDED: SCOPE_MISMATCH / MISSING_CONTEXT

3

Đánh giá policy: Mandatory / Auto conditional / Optional opt-in

NOT_ACTIVATED / DATA_UNKNOWN / UNCLASSIFIED

4

Giải quyết priority và trùng economic item

AMBIGUOUS / DUPLICATE_SKIPPED

5

Tính amount theo method+basis, thuế/FX/rounding baseline

CALCULATION_INVALID / MISSING_BASIS

6

Lưu Rating Detail + source/version/rule/selection snapshot + reason

TRACE_INCOMPLETE; chặn Seed nếu không xác thực

7. Dữ liệu, API, giao diện (DEV implementation contract)

7.1 Data model delta đề xuất

Object

Thuộc tính bổ sung/chuẩn hóa

Ràng buộc

surcharge_versions hoặc surcharge_rules

activation_policy (MANDATORY_BY_POLICY / AUTO_CONDITIONAL / OPTIONAL_OPT_IN), activation_config, approval_ref, effective_from/to

Required trước publish; Published immutable

surcharge_conditions

condition_type (eligibility/activation), source_field, operator, value, unknown_behavior

Không suy diễn eligibility=true thành activation=true

service_selections / activation_events

tenant_id, context_type, context_id, service_code, scope, qty, is_selected, source_ref, confirmed_by/at, revision

Unique/precedence; auditable; không xóa lịch sử

rating_details

activation_policy_snapshot, activation_evidence_ref, matched_conditions, reason_codes, source_type/id/version/rule, basis, amount

Đủ lineage Base/Surcharge tách biệt

migration_audit

old_version, new_version, mapping_policy, approver, executed_at, impact_count, errors

Không update dữ liệu lịch sử im lặng

Tên bảng/cột là đề xuất logic để DEV map vào schema AS-IS, KHÔNG ép tạo cấu trúc trùng nếu hệ thống đã có equivalent capability.

7.2 API / Service Contract tối thiểu

Chức năng

Input/output bắt buộc

Validation

Surcharge Draft/Publish

Create/update draft policy + config + scope; publish phiên bản mới

Không publish UNCLASSIFIED hoặc optional thiếu service code

Read Version

Get exact version id: toàn bộ field, rule, condition, audit ở chế độ read-only

Không fallback sang latest draft khi xem published v2

Select Service

Upsert event/selection theo context id + service code + revision

Authorization, optimistic locking, audit

Preview Rating

Applied/EligibleNotActivated/Excluded, reason codes + source snapshots

Thiếu activation không được tạo dòng charged

Seed Rating

Rating id/version + output direction + idempotency key

Chỉ valid applied line; no cross BUY/SELL

7.3 UI bắt buộc

Quản lý phụ phí: bổ sung trường “Chính sách áp dụng” và khu form tùy policy; điều kiện hiển thị riêng với “Phương pháp tính” và “Cơ sở tính”.

Danh sách phiên bản: nút “Xem chi tiết” ở mỗi v1/v2/... đã phát hành; Draft có “Chỉnh sửa”. Hiển thị thời gian hiệu lực, trạng thái, người phát hành, lịch sử thay đổi.

Màn hình Bill/Shipment/Leg: khu dịch vụ/phụ phí tùy chọn cho phép chọn, bỏ chọn và nhập quantity khi có quyền; không mặc định chọn tất cả.

Rating BUY/SELL: chia “Đã áp dụng”, “Có giá nhưng chưa phát sinh”, “Không đủ điều kiện”; cho biết mã lý do và trace tới phiên bản phụ phí thực tế.

Nếu phụ phí đang UNCLASSIFIED: cảnh báo quản trị “Chưa được phân loại chính sách áp dụng – không tự cộng” và cung cấp lối dẫn đến danh sách cấu hình cần xử lý.

8. Áp dụng riêng cho ví dụ DONGGOI (không hardcode)

Thông số

Hiện thấy / chính sách mới

Kỳ vọng

Mã

DONGGOI_SGN_HAN_MUA, chiều BUY, tuyến SGN–HAN, FIXED_RATE, 300.000 VND, version Published v2

Giữ nguyên v2 lịch sử; muốn đổi tạo v3 Published sau migration

Activation

Dịch vụ đóng gói thường là OPTIONAL_OPT_IN; chỉ chuyển MANDATORY khi có căn cứ nghiệp vụ được duyệt

Bill không chọn dịch vụ → không tính; Bill chọn & đủ eligibility → tính

Basis

Màn hình nháp ghi “Số lượng”; phải xác định đơn vị tính (per package/per Bill...) trước phát hành

Không mặc định 300.000đ/Bill nếu unit chưa xác định

BUY

Lấy giá mua từ surcharge BUY hợp lệ

Không sao chép làm giá bán

SELL

Chỉ lấy cấu hình riêng phù hợp chiều SELL (nếu có)

Nếu chưa có giá SELL, ghi rõ unpriced

Lịch sử

TESTLSS002 BUY đã Rating: FREIGHT 1.440.000 + DONGGOI 300.000 = 1.740.000 VND

Không thay đổi lịch sử đã Seed và snapshot v2

LƯU Ý KHÔNG KẾT LUẬN NHẦM  Ảnh 02/11 đang mở “Sửa phiên bản nháp” v3. Việc bảng v2 không nhấn được là vấn đề truy xuất UI; muốn xác minh điều kiện của v2, DEV phải truy vấn đúng version id và trả snapshot, không sử dụng dữ liệu v3 để giả định v2.

9. Migration và kiểm soát phát hành

9.1 Trình tự thực hiện an toàn

1.  DEV xuất inventory toàn bộ Surcharge theo Tenant × direction × version Published: code, scope, eligibility conditions, calculation, linked source, số lần áp trong Rating kỳ gần nhất; gắn nhãn đã có/thiếu activation evidence.

2.  Lập bảng đề xuất phân loại từng nhóm phí: MANDATORY / AUTO_CONDITIONAL / OPTIONAL / UNCLASSIFIED. Phân loại phải dựa trên bản chất phí và hợp đồng/chính sách Tenant, không suy từ calculation method hay điều kiện trống.

3.  Áp dụng review/approval của người có thẩm quyền cho các nhóm bị thay đổi hành vi; không auto-migrate tất cả rule cũ về MANDATORY hoặc OPTIONAL.

4.  Tạo Draft version mới cho các surcharge cần đổi; validate và publish theo effective_from; Published v1/v2 cũ giữ nguyên. Dữ liệu UAT lịch sử bảo toàn.

5.  Bật gate activation ở Staging, chạy comparison/diff trên Rating context đại diện; chặn Rating mới tự cộng UNCLASSIFIED, hiển thị cảnh báo rõ để không tạo quote sai âm thầm.

6.  Chạy hồi quy BUY/SELL, route, cargo, partner, FX/VAT, Seed, Profitability và kiểm thử multi-Tenant; có backout flag nhưng không tắt audit hoặc sửa Rating lịch sử.

7.  Chỉ đưa lên Production sau khi PO UAT đạt, có báo cáo impact và backup/restore plan. Chuyển đổi phải theo release candidate/tag cố định, không phải đẩy thẳng vào main tự deploy.

9.2 Cutover – chính sách lỗi an toàn

Tình huống

Hành vi TO-BE

Published legacy chưa phân loại

Không tự cộng trong Rating mới; trả EXCLUDED_UNCLASSIFIED + thông báo cho người dùng/quản trị; doanh nghiệp phải phân loại/phê duyệt để phát hành version mới.

Có thông tin nhưng activation UNKNOWN

Không tự tính; trả NEEDS_CONTEXT với hướng dẫn bổ sung trường/tài liệu; không âm thầm giả là TRUE.

Surcharge bắt buộc đã được duyệt

Tự cộng khi đủ eligibility và effective; trace decision ref + matched scope.

Surcharge tùy chọn đã chọn nhưng không có giá

Giữ yêu cầu dịch vụ; không tạo dòng giá 0/giá bán suy diễn; trả UNPRICED.

Đã từng Seed Rating cũ

Không rewrite; thay đổi cấu hình chỉ tác động rating mới; rerate tạo record/version mới có linkage.

10. Acceptance Criteria – điều kiện nghiệm thu chức năng

AC

Điều kiện phải PASS

AC-01

Toàn bộ phụ phí Published dùng cho Rating mới có activation policy được duyệt; không có blank→mandatory.

AC-02

Bill không yêu cầu đóng gói: BUY Rating SGN–HAN không chứa dòng DONGGOI, dù mọi điều kiện giá khác đều match.

AC-03

Bill chọn đóng gói: BUY Rating có DONGGOI đúng phiên bản, amount, basis, VAT/FX; không cộng trùng.

AC-04

FIXED_RATE / số lượng phản ánh đúng scope và basis; không tự chia/ngầm nhân theo kg.

AC-05

DG = false/unknown: phụ phí DG conditional không áp; DG=true và match đầy đủ thì được áp dụng.

AC-06

Phụ phí mandatory hợp lệ không cần tick; vẫn tự áp đúng phạm vi và có approval_ref.

AC-07

BUY và SELL tính độc lập; chỉ có BUY rule không xuất ra dòng SELL; không copy BUY amount thành SELL.

AC-08

Hai rule match chung economic item chỉ áp một lần hoặc trả ambiguity có kiểm soát.

AC-09

Phụ phí eligible nhưng inactive có lý do hiển thị đúng; user đọc được lý do không cộng.

AC-10

Xem v1/v2/v3 version đầy đủ ở chế độ chỉ đọc khi Published; v3 Draft chỉnh sửa riêng.

AC-11

Selection optional được lưu có audit; trang khác tải lại vẫn khớp; uncheck tạo revision và chỉ tác động Rating mới.

AC-12

Seed chỉ cho applied charges, retry không nhân đôi, Cost/Revenue direction đúng.

AC-13

Legacy v1/v2 Published và TESTLSS002 Rating 1.740.000 VND bất biến; thay đổi version mới chỉ áp dụng từ effective date.

AC-14

Legacy UNCLASSIFIED bị chặn tự động cộng với cảnh báo; không thể phát hành phụ phí mới thiếu policy.

AC-15

Thay đổi policy không làm sai Cost allocation, Profitability, AP/AR, chứng từ, ledger/reporting lịch sử.

AC-16

Kết quả test có link commit/PR/CI, API request/response, DB snapshots trước/sau và dữ liệu mẫu cho mỗi scenario trọng yếu.

AC-17

Bill/Shipment/Leg sử dụng chung engine nhưng scope/quantity khác nhau không bị double apply.

AC-18

Không thay đổi phương pháp tính hoặc financial maturity để che giấu lỗi activation.

11. UAT Matrix tối thiểu – kiểm thử dương/âm/hồi quy

TC

Nhóm

Dữ liệu / thao tác

Kết quả kỳ vọng

Ưu tiên

TC-01

OPTIONAL

Bill SGN–HAN không chọn đóng gói

DONGGOI không xuất hiện; thông báo NOT_SELECTED

P0

TC-02

OPTIONAL

Chọn đóng gói qty=1, BUY

Một dòng 300.000đ nếu basis là 1 đơn vị như snapshot

P0

TC-03

OPTIONAL

Chọn dịch vụ rồi bỏ chọn, rerate

Rating mới không áp; snapshot cũ bất biến

P0

TC-04

OPTIONAL

Tích trong UI nhưng chưa lưu, mở lại

Không suy activation đã xác nhận; không Seed phí

P0

TC-05

OPTIONAL

Chọn đóng gói khi khác tuyến

Không tính nếu không match route

P0

TC-06

OPTIONAL

Chọn đóng gói khi ngoài ngày hiệu lực

Không tính: phiên bản ngoài hiệu lực

P0

TC-07

AUTO

DG=false, có surcharge DG

Không cộng phí DG

P0

TC-08

AUTO

DG=true, có surcharge DG

Tự cộng đúng phí, trace context

P0

TC-09

AUTO

DG=unknown

Không cộng; NEEDS_CONTEXT

P0

TC-10

MANDATORY

Phụ phí nhiên liệu bắt buộc đã duyệt

Tự cộng khi match; không cần tick

P0

TC-11

MANDATORY

Cố gán mandatory thiếu policy ref/approval

Chặn publish/return validation error

P0

TC-12

BUY/SELL

Chỉ tồn tại BUY DONGGOI

SELL không tự cộng BUY

P0

TC-13

BUY/SELL

Có BUY & SELL độc lập

Mỗi chiều dùng đúng rule/source, amount độc lập

P0

TC-14

DOUBLE

Hai rule/versions match một mã

Dedupe/precedence hoặc AMBIGUOUS; không nhân đôi

P0

TC-15

BASIS

FIXED 300k × quantity=2 theo basis quantity

600k nếu unit/basis=per-unit; nếu per Bill giữ 300k

P1

TC-16

RATING

Rerate khi thay đổi selection

Tạo Rating mới, linked history; không overwrite

P0

TC-17

SEED

Seed BUY sau Rating hợp lệ; bấm lại

Expected Cost đúng, idempotent; không có Revenue

P0

TC-18

SEED

Seed SELL

Expected Revenue đúng, không có Cost

P0

TC-19

UI

Mở phiên bản v2 Published

Xem đầy đủ conditions/policy/formula, readonly

P1

TC-20

UI

Mở v3 Draft và thay policy

Chỉ draft thay đổi; published v2 bất biến

P1

TC-21

LEGACY

Legacy không phân loại, tính giá mới

Không tự cộng; cảnh báo/quản trị có danh sách cần duyệt

P0

TC-22

HISTORY

Xem Rating TESTLSS002 cũ

Net 1.740.000; FREIGHT 1.440.000, DONGGOI 300.000 giữ nguyên

P0

TC-23

SCOPE

Phí áp cho Shipment nhiều Bills

Không nhân nguyên phí Shipment theo từng Bill

P0

TC-24

SCOPE

Leg và Bill đồng thời đủ điều kiện

Áp đúng economic scope; không nhân đôi

P0

TC-25

CURRENCY

Surcharge khác tiền tệ báo cáo

FX snapshot đúng, trace, không rewrite

P1

TC-26

VAT

Surcharge VAT có cấu hình

Net/VAT/Gross đúng, không làm sai base charge

P1

TC-27

PERCENT

Phụ phí % trên cước chính

Chỉ dùng eligible base; không vòng lặp hoặc % trên %

P1

TC-28

PARTY

Vendor khác/Customer khác

Chỉ áp trên đúng BUY vendor/SELL customer

P1

TC-29

AUDIT

Actor đổi dịch vụ optional

Trace user/time/context/quantity/evidence và revision

P1

TC-30

RBAC

User không có quyền publish mandatory

Bị chặn cả UI và API

P0

TC-31

TENANT

Tenant A config khác Tenant B

Không rò rule/selection/amount qua Tenant

P0

TC-32

REGRESSION

Đối chiếu Cost/Revenue/Profit report sau fix

Không double count và không tự đổi maturity

P0

12. Phân công, Definition of Done và nghiệm thu

Vai trò

Đầu việc

DEV – Backend

Phân loại rule; activation resolver; validation, snapshots, idempotency, conditional data evidence, migration, bảo toàn lịch sử.

DEV – Frontend

Activation policy form; optional service selection; read-only version details; applied/excluded reasons; quyền.

DEV – QA

Tự động hóa các TC P0 và hồi quy BUY/SELL; đối chiếu current UAT snapshots; test migration và nhiều Tenant.

BA/PO

Xác nhận danh mục phí bắt buộc so với tùy chọn theo chính sách doanh nghiệp, làm UAT trên Staging; phê duyệt phát hành Production riêng.

DoD-01 [GATE]  Có inventory toàn bộ Surcharge hiện hành theo Tenant/direction, gắn loại policy và trạng thái phân loại; không còn unclassified trong phạm vi kích hoạt chính thức.

DoD-02 [GATE]  Tất cả TC P0 PASS tự động + PO UAT các luồng mẫu; các kết quả DEV tự báo không thay cho PO UAT.

DoD-03 [GATE]  Có DB migration an toàn + dry-run + impact report + backup/restore plan và link PR/commit/tag.

DoD-04 [GATE]  Có video/ảnh Staging: chưa chọn đóng gói không cộng, chọn thì cộng; mandatory hợp lệ vẫn tự cộng; BUY không lẫn SELL; xem v2 chỉ đọc.

DoD-05 [GATE]  Không deploy thẳng Production khi chưa PO phê duyệt. Mọi data repair published/historical phải có kế hoạch riêng và quyền phê duyệt.

13. Ranh giới quyết định cần quản trị

PO đã quyết định sửa ngay cơ chế toàn hệ thống. Những điểm sau được xác định là dữ liệu cấu hình cần phân loại ở mức Tenant/service chứ không được DEV tự quyết định đồng loạt:

Danh sách phụ phí nào thực sự bắt buộc theo hợp đồng/quy định; DEV phải xuất inventory và đề xuất mapping để được duyệt theo quy trình quản trị.

Dữ liệu nghiệp vụ nào xác nhận dịch vụ tùy chọn ở từng context; nếu hệ thống chưa có trường nghiệp vụ tương ứng, triển khai selection event với provenance thay vì hardcode “đóng gói” thành một cờ duy nhất.

Đơn vị tính chuẩn của từng phụ phí (Bill, Shipment, Chặng, package, kg, container...) để tránh nhân tiền sai.

Ngày cutover/effective time và cách xử lý bản ghi đang chờ Rating; không đổi lịch sử, không ghi đè phiên bản đã phát hành.

YÊU CẦU HÀNH ĐỘNG NGAY CHO DEV  Bắt đầu inventory, xác minh AS-IS, thực hiện gate activation + UI/DB/API và test trên Staging ngay. Nếu cần phê duyệt danh sách phí bắt buộc của Tenant, DEV chuẩn bị báo cáo phân loại; không trì hoãn triển khai các hạng mục kỹ thuật độc lập, nhưng không tự suy diễn chính sách bắt buộc.

14. Tài liệu baseline và bằng chứng đối chiếu

LCMS_DEV_Architecture_Change_Independent_RateCard_Surcharge_v1.0.docx – 05/10/2026 (SUR-01…SUR-15, applicability và versioning độc lập).

LCMS_BA_UAT_Followup_Change_v1.0_20261026.docx + ma trận kiểm thử Rating/Seed, nguồn BUY/SELL.

LCMS_BA_UAT_Correction_v1.0_20261027.docx + ma trận UAT: DONGGOI FIXED 300.000đ, FREIGHT 1.440.000đ, trace.

Ảnh UAT ngày 02/11/2026 – phiên bản DONGGOI_SGN_HAN_MUA v1/v2/v3; form đang sửa nháp v3, v2 Published chỉ hiển thị hàng.

Phản ánh trực tiếp của PO ngày 02/11/2026: nhiều/hầu như mọi phụ phí tự cộng khi bấm Rating, yêu cầu sửa ngay ở phạm vi toàn module.