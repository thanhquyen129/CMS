# -*- coding: utf-8 -*-
"""
Module trích xuất và định nghĩa dữ liệu kiểm kê trường toàn hệ thống (Sheet 1 - Full Field Inventory).
Đạt chuẩn vét cạn 100% (Exhaustive Full-System Inventory):
Bao gồm đầy đủ 5 phân hệ, mọi màn hình tạo mới/chỉnh sửa/khai báo, với 0 ô trống (NaN).
"""

def get_field_inventory_rows():
    rows = []

    # =========================================================================
    # PHÂN HỆ 1: VẬN HÀNH (OPERATIONAL CONTEXT)
    # =========================================================================

    # 1.1 ORDER (/orders/new, /orders/[id])
    orders_screen = "Tạo mới & Chi tiết Đơn hàng (/orders/new, /orders/[id])"
    order_fields = [
        ("Vận hành (Operations)", orders_screen, "Thông tin chung (Header)",
         "Mã đơn hàng *", "orderNo", "orders.order_no", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do / Tự sinh", "Không (Prefix ORD)", "Hệ thống vận hành (Ops SoT)",
         "Nhận diện đơn hàng, làm anchor đối soát", "Có (khi Draft); Khóa khi Locked/Closed", "bills.bill_no, shipments.shipment_no",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest, CreateOrderCommand.cs; DB: orders.order_no; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thông tin chung (Header)",
         "Ngày tạo đơn *", "orderDate", "orders.order_date", "DateTimeOffset", "Required (UI & API/DB)",
         "Nhập tự do (Date picker)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Xác định kỳ tài chính, tính hạn thanh toán", "Có (khi Draft)", "bills.bill_date, transport_movements.movement_on",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.order_date; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thông tin chung (Header)",
         "Trạng thái vận hành", "operationalStatus", "orders.operational_status", "String(32)", "Optional (Default 'draft')",
         "Dropdown", "Lookup ('draft', 'active', 'completed', 'cancelled')", "Hệ thống vận hành (Ops SoT)",
         "Kiểm soát luồng xử lý, cho phép link Bill", "Có (theo workflow)", "bills.operational_status, shipments.operational_status",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.operational_status; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thông tin chung (Header)",
         "Hệ thống nguồn", "sourceSystem", "orders.source_system", "String(64)", "Required (API/DB)",
         "Nhập tự do / Tự sinh ('lcms_manual')", "Lookup hệ thống tích hợp (TMS/WMS/Manual)", "Hệ thống nguồn phát sinh",
         "Kiểm soát quyền ghi đè C-002, Field Ownership", "Không thể sửa (Khóa SoT)", "bills.source_system, shipments.source_system",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.source_system; Test: HardeningPackageHTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thông tin chung (Header)",
         "ID tham chiếu hệ thống nguồn", "externalId", "orders.external_id", "String(128)", "Required (API/DB)",
         "Nhập tự do / Tự sinh", "Không", "Hệ thống nguồn phát sinh",
         "Idempotency tích hợp, chống tạo trùng bản ghi", "Không thể sửa", "bills.external_id, shipments.external_id",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.external_id; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thông tin chung (Header)",
         "Nhân viên phụ trách", "assignedTo", "orders.assigned_user_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "users.id", "LCMS User Directory",
         "Phân quyền theo dõi đơn và chia hoa hồng bán hàng", "Có (khi chưa đóng kỳ)", "bills.assigned_user_id",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.assigned_user_id; Test: AdminSettingsFullTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thông tin chung (Header)",
         "Người liên hệ", "contactName", "orders.contact_name", "String(128)", "Optional",
         "Nhập tự do", "Không", "Hệ thống vận hành (Ops SoT)",
         "Liên hệ giao nhận hiện trường", "Có (khi Draft)", "party_contacts.contact_name",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.contact_name; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thông tin chung (Header)",
         "Điện thoại / Email liên hệ", "contactPhoneEmail", "orders.contact_phone_email", "String(128)", "Optional",
         "Nhập tự do", "Không", "Hệ thống vận hành (Ops SoT)",
         "Gửi thông báo trạng thái vận đơn tự động", "Có (khi Draft)", "party_contacts.phone",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.contact_phone_email; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thông tin chung (Header)",
         "Mô tả / Ghi chú đơn hàng", "description", "orders.description", "String(512)", "Optional",
         "Nhập tự do (Textarea)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Lưu ý đặc biệt cho tài xế và kế toán", "Có (khi Draft)", "bills.notes, shipments.notes",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.description; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Vận chuyển & Tuyến đường",
         "Phương thức vận chuyển *", "transportMode", "orders.transport_mode", "String(16)", "Required (UI & API/DB)",
         "Dropdown ('air', 'sea', 'road', 'rail')", "Master Data / Enum Mode", "Hệ thống vận hành (Ops SoT)",
         "Điều kiện tiên quyết Rating (Air/Sea factor)", "Có (khi Draft)", "bills.transport_mode, shipments.transport_mode",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.transport_mode; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Vận chuyển & Tuyến đường",
         "Loại dịch vụ *", "serviceTypeCode", "orders.service_type_code", "String(64)", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "MasterCatalogItem (Category 'service_type')", "Hệ thống vận hành (Ops SoT)",
         "Matching quy tắc bảng giá (Pricing Rule matching)", "Có (khi Draft)", "bills.service_type_code, rate_cards.service_type",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.service_type_code; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Vận chuyển & Tuyến đường",
         "Điều kiện thương mại (Incoterms)", "incoterms", "orders.incoterms", "String(16)", "Optional",
         "Dropdown ('EXW', 'FOB', 'CIF', 'DDP', ...)", "Incoterms Master", "Hợp đồng thương mại",
         "Xác định nghĩa vụ chi phí BUY / SELL", "Có (khi Draft)", "bills.incoterms",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.incoterms; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Vận chuyển & Tuyến đường",
         "Địa điểm lấy hàng (Origin)", "originCode", "orders.origin_code", "String(32)", "Optional (Conditional)",
         "Dropdown / Autocomplete", "locations.code", "Hệ thống vận hành (Ops SoT)",
         "Matching điểm đi bảng giá Rating", "Có (khi Draft)", "bills.origin_code, shipments.origin_code",
         "UI: LocationField.tsx, CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.origin_code; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Vận chuyển & Tuyến đường",
         "Địa điểm giao hàng (Destination)", "destinationCode", "orders.destination_code", "String(32)", "Optional (Conditional)",
         "Dropdown / Autocomplete", "locations.code", "Hệ thống vận hành (Ops SoT)",
         "Matching điểm đến bảng giá Rating", "Có (khi Draft)", "bills.destination_code, shipments.destination_code",
         "UI: LocationField.tsx, CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.destination_code; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Vận chuyển & Tuyến đường",
         "Ngày dự kiến đi (ETD)", "etd", "orders.etd", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Xác định ngày áp dụng bảng giá (Rate Date fallback)", "Có (khi Draft)", "bills.etd, shipments.etd",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.etd; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Vận chuyển & Tuyến đường",
         "Ngày dự kiến đến (ETA)", "eta", "orders.eta", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Theo dõi tiến độ, xác định kỳ phát sinh", "Có (khi Draft)", "bills.eta, shipments.eta",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.eta; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Vận chuyển & Tuyến đường",
         "Ngày khách hàng yêu cầu", "requestedDeliveryDate", "orders.requested_delivery_date", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Hợp đồng dịch vụ khách hàng",
         "Đo lường SLA giao nhận", "Có (khi Draft)", "orders.requested_delivery_date",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.requested_delivery_date; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thương mại & Đối tác",
         "Reference khách hàng", "customerRef", "orders.customer_ref", "String(64)", "Optional",
         "Nhập tự do", "Không", "Khách hàng",
         "In trên chứng từ đối soát cho khách", "Có (khi Draft)", "bills.customer_ref",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.customer_ref; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thương mại & Đối tác",
         "Số báo giá / Rate Reference", "quotationRef", "orders.quotation_ref", "String(64)", "Optional",
         "Nhập tự do", "Không", "Báo giá thương mại",
         "Gợi ý bảng giá SELL cho nhân viên Rating", "Có (khi Draft)", "rate_cards.code",
         "UI: CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.quotation_ref; Test: RateCardCommercialReadyTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thương mại & Đối tác",
         "Khách hàng thanh toán", "customerPartyId", "orders.customer_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties (Role: 'customer')", "Hợp đồng dịch vụ",
         "Ghi nhận công nợ AR, đề xuất bảng giá SELL", "Có (khi Draft)", "bills.customer_party_id, revenues.customer_party_id",
         "UI: PartyTypeahead.tsx, CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.customer_party_id; Test: BusinessPartyMasterFullTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thương mại & Đối tác",
         "Người gửi hàng (Shipper)", "shipperPartyId", "orders.shipper_party_id", "Guid", "Optional (Conditional)",
         "Dropdown / Autocomplete", "business_parties (Role: 'shipper')", "Hợp đồng vận chuyển",
         "Đối tượng phát sinh nghiệp vụ, in chứng từ vận đơn", "Có (khi Draft)", "bills.shipper_party_id",
         "UI: PartyTypeahead.tsx, CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.shipper_party_id; Test: BusinessPartyDirectoryFullTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Thương mại & Đối tác",
         "Người nhận hàng (Consignee)", "consigneePartyId", "orders.consignee_party_id", "Guid", "Optional (Conditional)",
         "Dropdown / Autocomplete", "business_parties (Role: 'consignee')", "Hợp đồng vận chuyển",
         "Đối tượng nhận hàng, thu tiền COD/Freight Collect", "Có (khi Draft)", "bills.consignee_party_id",
         "UI: PartyTypeahead.tsx, CreateOrderForm.tsx; API: UpsertOrderRequest; DB: orders.consignee_party_id; Test: BusinessPartyDirectoryFullTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Hàng hóa & Kích thước (Cargo)",
         "Số kiện (Package Count)", "packageCount", "operational_measurements.package_count", "Int32", "Optional",
         "Nhập tự do (Number)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Rating (chế độ tính giá per_package)", "Có (khi Draft)", "bills.package_count, cargo_packages.count",
         "UI: CreateCargoFields.tsx; API: UpsertOrderRequest; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Hàng hóa & Kích thước (Cargo)",
         "Trọng lượng thực (Gross Weight kg)", "grossWeightKg", "operational_measurements.gross_weight_kg", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Tính chargeable weight, so sánh cước", "Có (khi Draft)", "bills.gross_weight_kg, shipments.gross_weight_kg",
         "UI: CreateCargoFields.tsx; API: UpsertOrderRequest; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Hàng hóa & Kích thước (Cargo)",
         "Thể tích (CBM)", "volumeCbm", "operational_measurements.volume_cbm", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Tính thể tích quy đổi cước (Volumetric weight)", "Có (khi Draft)", "bills.volume_cbm, shipments.volume_cbm",
         "UI: CreateCargoFields.tsx; API: UpsertOrderRequest; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Hàng hóa & Kích thước (Cargo)",
         "Trọng lượng tính cước (Chargeable Weight)", "chargeableWeightKg", "operational_measurements.chargeable_weight_kg", "Decimal(18,4)", "Optional",
         "Tự tính / Ghi đè", "Công thức RatingEngine.Chargeable(mode, gross, cbm)", "Hệ thống vận hành / LCMS Override",
         "Cốt lõi tính cước Rating (Weight break, Weight step)", "Có (kèm lý do ghi đè)", "ratings.chargeable_weight_kg, bills.chargeable_weight_kg",
         "UI: CreateCargoFields.tsx, OperationalReferenceEditor.tsx; API: ConfirmChargeableWeightCommand; DB: operational_field_overrides; Test: OperationalReferenceEditRatingContextTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Hàng hóa & Kích thước (Cargo)",
         "Loại hàng hóa (Commodity)", "commodityTypeId", "orders.commodity_type_id", "Guid", "Optional",
         "Dropdown", "commodity_types.id", "Hệ thống vận hành (Ops SoT)",
         "Matching phụ phí hoặc đơn giá theo mặt hàng", "Có (khi Draft)", "bills.commodity_type_id, transport_legs.commodity_type_id",
         "UI: CreateCargoFields.tsx; API: UpsertOrderRequest; DB: orders.commodity_type_id; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Hàng hóa & Kích thước (Cargo)",
         "Số lượng Container (Container Count)", "containerCount", "operational_measurements.container_count", "Int32", "Optional",
         "Nhập tự do (Number)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Tính giá vận tải biển nguyên container (FCL)", "Có (khi Draft)", "bills.container_count, shipments.container_count",
         "UI: CreateCargoFields.tsx; API: UpsertOrderRequest; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Hàng hóa & Kích thước (Cargo)",
         "Số TEU quy đổi", "teuCount", "operational_measurements.teu_count", "Decimal(8,2)", "Optional",
         "Nhập tự do / Tự tính", "Không", "Hệ thống vận hành (Ops SoT)",
         "Phân bổ chi phí vận tải container", "Có (khi Draft)", "bills.teu_count, shipments.teu_count",
         "UI: CreateCargoFields.tsx; API: UpsertOrderRequest; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", orders_screen, "Liên kết nghiệp vụ",
         "Bill liên quan", "bills", "order_bill_links", "Array<Guid>", "Optional",
         "Autocomplete / Tag picker", "bills.id", "Liên kết M:N (LCMS)",
         "Phân bổ chi phí, đối soát tài chính", "Có (thêm/xóa liên kết)", "order_bill_links.bill_id",
         "UI: RefTagPicker.tsx; API: LinkOrderToBillCommand.cs; DB: order_bill_links; Test: Ui02OperationalCreateContextTests.cs")
    ]
    rows.extend(order_fields)

    # 1.2 BILL (/bills/new, /bills/[id])
    bills_screen = "Tạo mới & Chi tiết Bill (/bills/new, /bills/[id])"
    bill_fields = [
        ("Vận hành (Operations)", bills_screen, "Thông tin cơ bản",
         "Số Bill *", "billNo", "bills.bill_no", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do / Tự sinh", "Không (Prefix BILL)", "Financial Anchor (LCMS SoT)",
         "Mỏ neo tài chính H-003, định danh Cost/Revenue", "Không thể sửa số Bill sau khi tạo", "costs.bill_id, revenues.bill_id",
         "UI: CreateBillWorkspaceForm.tsx; API: CreateBillCommand.cs; DB: bills.bill_no; Test: BillCreateTotalsTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thông tin cơ bản",
         "Loại Bill *", "billType", "bills.bill_type", "String(32)", "Required (UI & API/DB)",
         "Dropdown ('master', 'house', 'direct')", "Master Catalog / Hardcoded Enum", "LCMS Financial Anchor",
         "Phân cấp hạch toán, Rating context", "Có (khi Draft)", "rate_cards.service_type",
         "UI: CreateBillWorkspaceForm.tsx; API: CreateBillCommand.cs; DB: bills.bill_type; Test: TenantBillApiTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thông tin cơ bản",
         "Trạng thái vận hành Bill", "operationalStatus", "bills.operational_status", "String(32)", "Optional (Default 'active')",
         "Dropdown ('draft', 'active', 'closed')", "Lookup Status", "LCMS Financial Anchor",
         "Kiểm soát vòng đời Bill, cho phép gắn Cost/Revenue", "Có (qua workflow)", "orders.operational_status",
         "UI: CreateBillWorkspaceForm.tsx; API: CreateBillCommand.cs; DB: bills.operational_status; Test: TenantBillApiTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thông tin cơ bản",
         "Phương thức vận chuyển *", "transportMode", "bills.transport_mode", "String(16)", "Required (UI & API/DB)",
         "Dropdown ('air', 'sea', 'road', 'rail')", "Master Data / Enum Mode", "LCMS Context",
         "Điều kiện tiên quyết Rating (Air/Sea factor)", "Có (khi Draft)", "orders.transport_mode, ratings.transport_mode",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.transport_mode; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thông tin cơ bản",
         "Ngày tạo Bill *", "billDate", "bills.bill_date", "DateTimeOffset", "Required (UI & API/DB)",
         "Nhập tự do (Date picker)", "Không", "LCMS Financial Anchor",
         "Kỳ kế toán, xác định tỷ giá Fx AsOf", "Có (khi Draft)", "costs.cost_date, revenues.revenue_date",
         "UI: CreateBillWorkspaceForm.tsx; API: CreateBillCommand.cs; DB: bills.bill_date; Test: BillCreateTotalsTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thông tin cơ bản",
         "Nhân viên phụ trách Bill", "assignedTo", "bills.assigned_user_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "users.id", "LCMS User Directory",
         "Theo dõi trách nhiệm tài chính của Bill", "Có (khi Draft)", "orders.assigned_user_id",
         "UI: CreateBillWorkspaceForm.tsx; API: CreateBillCommand.cs; DB: bills.assigned_user_id; Test: AdminSettingsFullTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thông tin cơ bản",
         "Ghi chú / Diễn giải Bill", "notes", "bills.notes", "String(512)", "Optional",
         "Nhập tự do (Textarea)", "Không", "LCMS Financial Anchor",
         "Lưu vết nghiệp vụ xử lý của kế toán chi phí", "Có (khi Draft)", "orders.description",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.notes; Test: TenantBillApiTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Tuyến & Thời gian",
         "Tuyến danh mục", "routeId", "bills.route_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "routes.id", "Master Data Routes",
         "Matching Rate Card Route", "Có (khi Draft)", "pricing_rules.route_id",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.route_id; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Tuyến & Thời gian",
         "Mã tuyến hiển thị", "routeCode", "bills.route_code", "String(64)", "Optional",
         "Nhập tự do / Tự điền từ routeId", "routes.code", "Master Data Routes",
         "Rating matching routeCode", "Có (khi Draft)", "pricing_rules.route_code",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.route_code; Test: RateRatingBaselineTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Tuyến & Thời gian",
         "Điểm đi (Origin Code)", "originCode", "bills.origin_code", "String(32)", "Optional (Conditional)",
         "Dropdown / Autocomplete", "locations.code", "Master Data Locations",
         "Matching bảng giá Rating điểm đi", "Có (khi Draft)", "orders.origin_code, ratings.origin_code",
         "UI: LocationField.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.origin_code; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Tuyến & Thời gian",
         "Điểm đến (Destination Code)", "destinationCode", "bills.destination_code", "String(32)", "Optional (Conditional)",
         "Dropdown / Autocomplete", "locations.code", "Master Data Locations",
         "Matching bảng giá Rating điểm đến", "Có (khi Draft)", "orders.destination_code, ratings.destination_code",
         "UI: LocationField.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.destination_code; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Tuyến & Thời gian",
         "Ngày ETD", "etd", "bills.etd", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Ops/LCMS",
         "Dự kiến khởi hành, kiểm tra hiệu lực giá", "Có (khi Draft)", "orders.etd, shipments.etd",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.etd; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Tuyến & Thời gian",
         "Ngày ETA", "eta", "bills.eta", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Ops/LCMS",
         "Dự kiến kết thúc dịch vụ, ghi nhận doanh thu", "Có (khi Draft)", "orders.eta, shipments.eta",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.eta; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Khách hàng (Customer Party)", "customerPartyId", "bills.customer_party_id", "Guid", "Optional (Conditional)",
         "Dropdown / Autocomplete", "business_parties (Role: 'customer')", "Hợp đồng thương mại",
         "Ghi nhận doanh thu Revenue, AR, Rating SELL", "Có (khi Draft)", "orders.customer_party_id, revenues.customer_party_id",
         "UI: PartyTypeahead.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.customer_party_id; Test: BusinessPartyMasterFullTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Bên thanh toán (Payer Party)", "payerPartyId", "bills.payer_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties", "Hợp đồng thương mại",
         "Đối tượng phát hành Debit Note, AR Exposure", "Có (khi Draft)", "receivable_exposures.counterparty_id",
         "UI: PartyTypeahead.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.payer_party_id; Test: Sprint7ExposureApArTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Người gửi (Shipper Party)", "shipperPartyId", "bills.shipper_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties (Role: 'shipper')", "Vận đơn Bill of Lading",
         "In ấn chứng từ, phân loại luồng hàng", "Có (khi Draft)", "orders.shipper_party_id",
         "UI: PartyTypeahead.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.shipper_party_id; Test: BusinessPartyDirectoryFullTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Người nhận (Consignee Party)", "consigneePartyId", "bills.consignee_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties (Role: 'consignee')", "Vận đơn Bill of Lading",
         "In ấn chứng từ, thủ tục giải phóng hàng", "Có (khi Draft)", "orders.consignee_party_id",
         "UI: PartyTypeahead.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.consignee_party_id; Test: BusinessPartyDirectoryFullTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Bên nhận hóa đơn (Bill-to Party)", "billToPartyId", "bills.bill_to_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties", "Hợp đồng tài chính",
         "Phát hành hóa đơn tài chính VAT Invoice", "Có (khi Draft)", "financial_documents.counterparty_id",
         "UI: PartyTypeahead.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.bill_to_party_id; Test: Sprint6FinancialDocumentTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Hãng vận chuyển dự kiến (Carrier)", "carrierPartyId", "bills.carrier_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties (Role: 'carrier')", "Thỏa thuận cước hãng",
         "Gợi ý hãng vận chuyển khi tạo Shipment và tính giá BUY", "Có (khi Draft)", "shipments.carrier_party_id",
         "UI: PartyTypeahead.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.carrier_party_id; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Tiền tệ ưu tiên (Currency)", "currencyCode", "bills.currency_code", "String(3)", "Required (Default 'VND')",
         "Dropdown", "currencies.code", "Chính sách tiền tệ LCMS",
         "Xác định đồng tiền tính toán cơ sở và Fx conversion", "Có (khi Draft)", "ratings.currency_code, costs.currency_code",
         "UI: CurrencySelect.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.currency_code; Test: ReportingCurrencyFxTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Loại dịch vụ Bill", "serviceTypeCode", "bills.service_type_code", "String(64)", "Optional",
         "Dropdown / Autocomplete", "master_catalog_items (Category 'service_type')", "LCMS Context",
         "Matching điều kiện giá Rate Card", "Có (khi Draft)", "orders.service_type_code, rate_cards.service_type",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.service_type_code; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Điều kiện thương mại (Incoterms)", "incoterms", "bills.incoterms", "String(16)", "Optional",
         "Dropdown", "Incoterms Master", "Thương mại",
         "Phân định nghĩa vụ cước", "Có (khi Draft)", "orders.incoterms",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.incoterms; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Reference khách hàng (Customer Ref)", "customerRef", "bills.customer_ref", "String(64)", "Optional",
         "Nhập tự do", "Không", "Khách hàng",
         "In trên chứng từ đối soát cho khách", "Có (khi Draft)", "orders.customer_ref",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.customer_ref; Test: Ui02OperationalCreateContextTests.cs"),
        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Ngày áp dụng giá (Rate Date)", "rateEffectiveDate", "bills.rate_effective_date", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Thỏa thuận thương mại",
         "Tra cứu hiệu lực bảng giá Rate Card Version", "Có (khi Draft)", "ratings.rate_date",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.rate_effective_date; Test: RateRatingBaselineTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Hàng hóa & Kích thước (Cargo)",
         "Số kiện (Package Count)", "packageCount", "operational_measurements.package_count", "Int32", "Optional",
         "Nhập tự do (Number)", "Không", "Ops/LCMS Measurement",
         "Rating per_package, kiểm tra khớp chứng từ", "Có (khi Draft)", "orders.package_count",
         "UI: CreateCargoFields.tsx; API: UpdateBillContextCommand.cs; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Hàng hóa & Kích thước (Cargo)",
         "Trọng lượng thực (Gross Weight kg)", "grossWeightKg", "operational_measurements.gross_weight_kg", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Ops/LCMS Measurement",
         "Đầu vào tính Chargeable Weight", "Có (khi Draft)", "orders.gross_weight_kg",
         "UI: CreateCargoFields.tsx; API: UpdateBillContextCommand.cs; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Hàng hóa & Kích thước (Cargo)",
         "Thể tích (CBM)", "volumeCbm", "operational_measurements.volume_cbm", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Ops/LCMS Measurement",
         "Đầu vào tính Chargeable Weight quy đổi", "Có (khi Draft)", "orders.volume_cbm",
         "UI: CreateCargoFields.tsx; API: UpdateBillContextCommand.cs; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Hàng hóa & Kích thước (Cargo)",
         "Trọng lượng tính cước (Chargeable Weight)", "chargeableWeightKg", "operational_measurements.chargeable_weight_kg", "Decimal(18,4)", "Optional",
         "Tự tính / Ghi đè", "RatingEngine.Chargeable(mode, gross, cbm)", "LCMS SoT (Rating Context)",
         "Đại lượng quyết định tiền cước chính (Base Freight)", "Có (kèm lý do ghi đè)", "ratings.chargeable_weight_kg",
         "UI: CreateCargoFields.tsx, OperationalReferenceEditor.tsx; API: ConfirmChargeableWeightCommand; DB: operational_field_overrides; Test: OperationalReferenceEditRatingContextTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Hàng hóa & Kích thước (Cargo)",
         "Loại hàng (Commodity)", "commodityTypeId", "bills.commodity_type_id", "Guid", "Optional",
         "Dropdown", "commodity_types.id", "Master Data Commodity",
         "Matching đơn giá đặc thù (DG, Reefer)", "Có (khi Draft)", "orders.commodity_type_id",
         "UI: CreateCargoFields.tsx; API: UpdateBillContextCommand.cs; DB: bills.commodity_type_id; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Ghi đè thông số vận hành",
         "Lý do ghi đè thông số *", "overrideReason", "operational_field_overrides.reason", "String(512)", "Required (khi Override)",
         "Nhập tự do (Textarea)", "Không", "LCMS User Audit",
         "Lưu vết lý do tài chính điều chỉnh số liệu của Ops SoT", "Không thể xóa sau khi lưu", "audit_events.reason",
         "UI: OperationalReferenceEditor.tsx; API: ConfirmChargeableWeightCommand.cs; DB: operational_field_overrides.reason; Test: OperationalReferenceEditRatingContextTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Liên kết nghiệp vụ",
         "Đơn hàng liên quan (Orders)", "orders", "order_bill_links", "Array<Guid>", "Optional",
         "Autocomplete / Tag Picker", "orders.id", "Liên kết M:N (LCMS)",
         "Đối soát đơn hàng và phân tích lãi gộp Order Margin", "Có (thêm/xóa link)", "order_bill_links.order_id",
         "UI: RefTagPicker.tsx; API: LinkOrderToBillCommand.cs; DB: order_bill_links; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", bills_screen, "Liên kết nghiệp vụ",
         "Lô hàng liên quan (Shipments)", "shipments", "bill_shipment_links", "Array<Guid>", "Optional",
         "Autocomplete / Tag Picker", "shipments.id", "Liên kết M:N (LCMS)",
         "Kế thừa chi phí vận hành lô hàng vào Bill", "Có (thêm/xóa link)", "bill_shipment_links.shipment_id",
         "UI: RefTagPicker.tsx; API: LinkBillToShipmentCommand.cs; DB: bill_shipment_links; Test: Ui02OperationalCreateContextTests.cs")
    ]
    rows.extend(bill_fields)

    # 1.3 SHIPMENT (/shipments/new, QuickCreateShipmentDialog.tsx)
    shipments_screen = "Tạo mới & Chi tiết Lô hàng (/shipments/new, QuickCreateShipmentDialog.tsx)"
    shipment_fields = [
        ("Vận hành (Operations)", shipments_screen, "Thông tin chung",
         "Mã Shipment *", "shipmentNo", "shipments.shipment_no", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do / Tự sinh", "Không (Prefix SHP)", "Hệ thống vận hành (Ops SoT)",
         "Định danh lô hàng gom/vận chuyển, anchor chi phí BUY", "Không thể sửa sau khi tạo", "shipments.shipment_no",
         "UI: CreateShipmentForm.tsx, QuickCreateShipmentDialog.tsx; API: UpsertShipmentCommand.cs; DB: shipments.shipment_no; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Thông tin chung",
         "Phương thức vận chuyển *", "transportMode", "shipments.transport_mode", "String(16)", "Required (UI & API/DB)",
         "Dropdown ('air', 'sea', 'road', 'rail')", "Master Data / Enum Mode", "Hệ thống vận hành (Ops SoT)",
         "Rating Context mua theo chặng (Leg Mode Policy)", "Có (khi Draft)", "transport_legs.transport_mode",
         "UI: CreateShipmentForm.tsx, QuickCreateShipmentDialog.tsx; API: UpsertShipmentCommand.cs; DB: shipments.transport_mode; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Thông tin chung",
         "Trạng thái vận hành", "operationalStatus", "shipments.operational_status", "String(32)", "Optional (Default 'active')",
         "Dropdown ('active', 'completed', 'cancelled')", "Lookup Status", "Hệ thống vận hành (Ops SoT)",
         "Kiểm soát vòng đời lô hàng, cho phép phân bổ chi phí", "Có (theo workflow)", "transport_legs.operational_status",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.operational_status; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Thông tin chung",
         "Hệ thống nguồn", "sourceSystem", "shipments.source_system", "String(64)", "Required (API/DB)",
         "Nhập tự do / Tự sinh ('lcms_manual')", "Lookup hệ thống", "Hệ thống nguồn phát sinh",
         "Kiểm soát quyền ghi đè C-002, Field Ownership", "Không thể sửa", "bills.source_system",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.source_system; Test: HardeningPackageHTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Thông tin chung",
         "ID tham chiếu nguồn", "externalId", "shipments.external_id", "String(128)", "Required (API/DB)",
         "Nhập tự do / Tự sinh", "Không", "Hệ thống nguồn phát sinh",
         "Idempotency tích hợp với TMS", "Không thể sửa", "bills.external_id",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.external_id; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Thông tin chung",
         "Loại dịch vụ gom hàng", "serviceTypeCode", "shipments.service_type_code", "String(64)", "Optional",
         "Dropdown / Autocomplete", "master_catalog_items (Category 'service_type')", "Hệ thống vận hành (Ops SoT)",
         "Phân loại hình thức vận chuyển chặng chính", "Có (khi Draft)", "transport_legs.service_type_code",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.service_type_code; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Thông tin chung",
         "Hãng vận chuyển (Carrier)", "carrierPartyId", "shipments.carrier_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties (Role: 'carrier')", "Hệ thống vận hành (Ops SoT)",
         "Xác định vendor tính giá mua, phát hành AP", "Có (khi Draft)", "costs.vendor_party_id, rate_cards.supplier_party_id",
         "UI: PartyTypeahead.tsx, CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.carrier_party_id; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Tuyến & Thời gian",
         "Điểm đi lô hàng (Origin Code)", "originCode", "shipments.origin_code", "String(32)", "Optional (Conditional)",
         "Dropdown / Autocomplete", "locations.code", "Hệ thống vận hành (Ops SoT)",
         "Điểm gom hàng / Cảng xuất khẩu chính", "Có (khi Draft)", "transport_legs.origin_code",
         "UI: LocationField.tsx, CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.origin_code; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Tuyến & Thời gian",
         "Điểm đến lô hàng (Destination Code)", "destinationCode", "shipments.destination_code", "String(32)", "Optional (Conditional)",
         "Dropdown / Autocomplete", "locations.code", "Hệ thống vận hành (Ops SoT)",
         "Điểm dỡ hàng / Cảng nhập khẩu chính", "Có (khi Draft)", "transport_legs.destination_code",
         "UI: LocationField.tsx, CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.destination_code; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Tuyến & Thời gian",
         "Mã tuyến vận chuyển", "routeCode", "shipments.route_code", "String(64)", "Optional",
         "Nhập tự do / Autocomplete", "routes.code", "Hệ thống vận hành (Ops SoT)",
         "Matching bảng giá mua BUY", "Có (khi Draft)", "transport_legs.route_code",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.route_code; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Tuyến & Thời gian",
         "ETD dự kiến lô hàng", "etd", "shipments.etd", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Kế hoạch khởi hành của lô hàng gom", "Có (khi Draft)", "transport_legs.etd",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.etd; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Tuyến & Thời gian",
         "ETA dự kiến lô hàng", "eta", "shipments.eta", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Kế hoạch cập bến của lô hàng gom", "Có (khi Draft)", "transport_legs.eta",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.eta; Test: Ui02OperationalCreateContextTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Hàng hóa & Kích thước (Cargo)",
         "Tổng số kiện gom (Package Count)", "packageCount", "operational_measurements.package_count", "Int32", "Optional",
         "Nhập tự do (Number)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Kiểm tra khớp số lượng kiện khai manifest", "Có (khi Draft)", "bills.package_count",
         "UI: CreateCargoFields.tsx; API: UpsertShipmentCommand.cs; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Hàng hóa & Kích thước (Cargo)",
         "Tổng trọng lượng thực (Gross Weight kg)", "grossWeightKg", "operational_measurements.gross_weight_kg", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Cơ sở phân bổ chi phí vận tải theo trọng lượng", "Có (khi Draft)", "cost_allocations.basis",
         "UI: CreateCargoFields.tsx; API: UpsertShipmentCommand.cs; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Hàng hóa & Kích thước (Cargo)",
         "Tổng thể tích gom (Volume CBM)", "volumeCbm", "operational_measurements.volume_cbm", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Cơ sở phân bổ chi phí vận tải đường biển LCL", "Có (khi Draft)", "cost_allocations.basis",
         "UI: CreateCargoFields.tsx; API: UpsertShipmentCommand.cs; DB: operational_measurements; Test: OperationalCargoTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Hàng hóa & Kích thước (Cargo)",
         "Trọng lượng tính cước lô hàng (Chargeable Weight)", "chargeableWeightKg", "operational_measurements.chargeable_weight_kg", "Decimal(18,4)", "Optional",
         "Tự tính / Ghi đè", "RatingEngine.Chargeable", "LCMS / Ops SoT",
         "Tính giá cước mua chặng gom", "Có (kèm lý do)", "ratings.chargeable_weight_kg",
         "UI: CreateCargoFields.tsx; API: UpsertShipmentCommand.cs; DB: operational_measurements; Test: OperationalReferenceEditRatingContextTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Hàng hóa & Kích thước (Cargo)",
         "Mặt hàng chính (Commodity)", "commodityTypeId", "shipments.commodity_type_id", "Guid", "Optional",
         "Dropdown", "commodity_types.id", "Master Data Commodity",
         "Áp dụng phụ phí hàng đặc thù lô gom", "Có (khi Draft)", "bills.commodity_type_id",
         "UI: CreateCargoFields.tsx; API: UpsertShipmentCommand.cs; DB: shipments.commodity_type_id; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", shipments_screen, "Liên kết nghiệp vụ",
         "Bill liên quan (Gom Bills)", "bills", "bill_shipment_links", "Array<Guid>", "Optional",
         "Autocomplete / Tag Picker", "bills.id", "Liên kết M:N (LCMS)",
         "Tập hợp các Bill thụ hưởng chi phí của Shipment", "Có (thêm/xóa link)", "bill_shipment_links.bill_id",
         "UI: RefTagPicker.tsx; API: LinkBillToShipmentCommand.cs; DB: bill_shipment_links; Test: Ui02OperationalCreateContextTests.cs")
    ]
    rows.extend(shipment_fields)

    # 1.4 TRANSPORT LEG (/operations/legs/new)
    legs_screen = "Tạo mới & Chi tiết Chặng (/operations/legs/new, /operations/[kind]/[id])"
    leg_fields = [
        ("Vận hành (Operations)", legs_screen, "Thông tin chặng",
         "Số chặng *", "legNo", "transport_legs.leg_no", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do / Tự sinh", "Không (Prefix LEG)", "Hệ thống vận hành (Ops SoT)",
         "Định danh phân đoạn vận tải 1:N với Shipment", "Không thể sửa", "ratings.transport_leg_id",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.leg_no; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Thông tin chặng",
         "Lô hàng sở hữu *", "shipmentId", "transport_legs.shipment_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "shipments.id", "Hệ thống vận hành (CONFIRMED 1:N)",
         "Liên kết lô hàng cha để kế thừa điểm đi/đến/chế độ", "Khóa sau khi tạo", "shipments.id",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.shipment_id; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Thông tin chặng",
         "Thứ tự chặng (Sequence No)", "sequenceNo", "transport_legs.sequence_no", "Int32", "Required (Default 1)",
         "Nhập tự do (Number)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Sắp xếp thứ tự hành trình (Pick-up, Main-leg, Delivery)", "Có (khi Draft)", "transport_legs.sequence_no",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.sequence_no; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Thông tin chặng",
         "Chế độ vận tải chặng", "transportMode", "transport_legs.transport_mode", "String(16)", "Optional (Fallback từ Shipment)",
         "Dropdown ('air', 'sea', 'road', 'rail')", "Master Data / Enum Mode", "Hệ thống vận hành (Ops SoT)",
         "Ưu tiên cao hơn Shipment khi Rating theo Chặng", "Có (khi Draft)", "ratings.transport_mode",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.transport_mode; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Thông tin chặng",
         "Loại dịch vụ chặng", "serviceTypeCode", "transport_legs.service_type_code", "String(64)", "Optional",
         "Dropdown / Autocomplete", "master_catalog_items (Category 'service_type')", "Hệ thống vận hành (Ops SoT)",
         "Matching đơn giá mua của chặng cụ thể", "Có (khi Draft)", "pricing_rules.service_type_code",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.service_type_code; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Tuyến đường chặng",
         "Điểm bắt đầu chặng (Origin Code)", "originCode", "transport_legs.origin_code", "String(32)", "Optional (Conditional)",
         "Dropdown / Autocomplete", "locations.code", "Hệ thống vận hành (Ops SoT)",
         "Matching bảng giá mua điểm đi của chặng", "Có (khi Draft)", "ratings.origin_code",
         "UI: LocationField.tsx, CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.origin_code; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Tuyến đường chặng",
         "Điểm kết thúc chặng (Destination Code)", "destinationCode", "transport_legs.destination_code", "String(32)", "Optional (Conditional)",
         "Dropdown / Autocomplete", "locations.code", "Hệ thống vận hành (Ops SoT)",
         "Matching bảng giá mua điểm đến của chặng", "Có (khi Draft)", "ratings.destination_code",
         "UI: LocationField.tsx, CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.destination_code; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Tuyến đường chặng",
         "Mã tuyến chặng", "routeCode", "transport_legs.route_code", "String(64)", "Optional",
         "Nhập tự do / Autocomplete", "routes.code", "Hệ thống vận hành (Ops SoT)",
         "Matching quy tắc giá theo tuyến chặng", "Có (khi Draft)", "pricing_rules.route_code",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.route_code; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Đối tác & Hàng hóa",
         "Loại hàng hóa chặng", "commodityTypeId", "transport_legs.commodity_type_id", "Guid", "Optional",
         "Dropdown", "commodity_types.id", "Master Data Commodity",
         "Phân định loại hàng nguy hiểm/lạnh trên chặng", "Có (khi Draft)", "pricing_rules.commodity_code",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.commodity_type_id; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Đối tác & Hàng hóa",
         "Nhà cung cấp vận tải chặng", "carrierPartyId", "transport_legs.carrier_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties (Role: 'vendor'/'carrier')", "Hệ thống vận hành (Ops SoT)",
         "Chủ nợ AP phát sinh từ cước chặng này", "Có (khi Draft)", "costs.vendor_party_id",
         "UI: PartyTypeahead.tsx, CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.carrier_party_id; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Thời gian & Kích thước",
         "ETD chặng", "etd", "transport_legs.etd", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Thời gian áp dụng bảng giá chặng", "Có (khi Draft)", "ratings.rate_date",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.etd; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Thời gian & Kích thước",
         "ETA chặng", "eta", "transport_legs.eta", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Thời gian hoàn thành chặng", "Có (khi Draft)", "transport_legs.eta",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.eta; Test: BuyRatingLegTests.cs"),

        ("Vận hành (Operations)", legs_screen, "Liên kết nghiệp vụ",
         "Bill liên quan chặng", "bills", "bill_leg_links", "Array<Guid>", "Optional",
         "Autocomplete / Tag Picker", "bills.id", "Liên kết M:N (LCMS)",
         "Điều kiện tiên quyết để tính giá mua theo Chặng (ContextType=leg)", "Có (thêm/xóa link)", "bill_leg_links.bill_id",
         "UI: RefTagPicker.tsx; API: LinkBillToLegCommand.cs; DB: bill_leg_links; Test: BuyRatingLegTests.cs")
    ]
    rows.extend(leg_fields)

    # 1.5 TRANSPORT MOVEMENT (/operations/movements/new)
    movements_screen = "Tạo mới & Chi tiết Chuyến (/operations/movements/new, /operations/[kind]/[id])"
    movement_fields = [
        ("Vận hành (Operations)", movements_screen, "Thông tin chuyến",
         "Số chuyến *", "movementNo", "transport_movements.movement_no", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do / Tự sinh", "Không (Prefix MOV)", "Hệ thống vận hành (Ops SoT)",
         "Thực tế thực hiện chuyến (xe/tàu/máy bay cụ thể)", "Không thể sửa", "transport_movements.movement_no",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.movement_no; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", movements_screen, "Thông tin chuyến",
         "Ngày thực hiện chuyến", "movementOn", "transport_movements.movement_on", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Hệ thống vận hành (Ops SoT)",
         "Thời điểm phát sinh chi phí thực tế (Actual Cost)", "Có (khi Draft)", "costs.cost_date",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.movement_on; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", movements_screen, "Thông tin chuyến",
         "Chế độ vận chuyển chuyến", "transportMode", "transport_movements.transport_mode", "String(16)", "Optional",
         "Dropdown ('air', 'sea', 'road', 'rail')", "Master Data / Enum Mode", "Hệ thống vận hành (Ops SoT)",
         "Phân loại hình thức phương tiện", "Có (khi Draft)", "transport_legs.transport_mode",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.transport_mode; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", movements_screen, "Thông tin chuyến",
         "Đơn vị vận tải (Carrier / Trucker)", "carrierPartyId", "transport_movements.carrier_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties (Role: 'carrier')", "Hệ thống vận hành (Ops SoT)",
         "Đơn vị thụ hưởng chi phí chuyến", "Có (khi Draft)", "costs.vendor_party_id",
         "UI: PartyTypeahead.tsx, CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.carrier_party_id; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", movements_screen, "Hành trình & Phương tiện",
         "Địa điểm xuất phát", "originLocationId", "transport_movements.origin_location_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "locations.id", "Master Data Locations",
         "Xác định lộ trình chuyến đi thực tế", "Có (khi Draft)", "transport_legs.origin_location_id",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.origin_location_id; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", movements_screen, "Hành trình & Phương tiện",
         "Địa điểm kết thúc", "destinationLocationId", "transport_movements.destination_location_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "locations.id", "Master Data Locations",
         "Xác định lộ trình chuyến đến thực tế", "Có (khi Draft)", "transport_legs.destination_location_id",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.destination_location_id; Test: ReferenceMasterTests.cs"),

        ("Vận hành (Operations)", movements_screen, "Hành trình & Phương tiện",
         "Số hiệu phương tiện (Biển số xe / Số hiệu chuyến bay)", "vehicleNo", "transport_movements.vehicle_no", "String(64)", "Optional",
         "Nhập tự do", "Không", "Hệ thống vận hành (Ops SoT)",
         "Kiểm soát tài sản, định mức xăng dầu", "Có (khi Draft)", "costs.description",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.vehicle_no; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", movements_screen, "Thông tin chung",
         "Trạng thái vận hành chuyến", "operationalStatus", "transport_movements.operational_status", "String(32)", "Optional (Default 'active')",
         "Dropdown ('active', 'completed', 'cancelled')", "Lookup Status", "Hệ thống vận hành (Ops SoT)",
         "Xác nhận chuyến xe hoàn tất để actualize chi phí", "Có (theo workflow)", "costs.maturity_state",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.operational_status; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", movements_screen, "Liên kết nghiệp vụ",
         "Chặng vận chuyển liên quan", "legs", "leg_movement_links", "Array<Guid>", "Optional",
         "Autocomplete / Tag Picker", "transport_legs.id", "Liên kết M:N (LCMS)",
         "Đối soát thực hiện chặng và phân bổ chi phí chuyến", "Có (thêm/xóa link)", "leg_movement_links.transport_leg_id",
         "UI: CreateMovementForm.tsx; API: LegMovementLinkCommands.cs; DB: leg_movement_links; Test: Sprint2OperationalReferenceTests.cs"),

        ("Vận hành (Operations)", movements_screen, "Liên kết nghiệp vụ",
         "Bill liên quan chuyến", "bills", "bill_movement_links", "Array<Guid>", "Optional",
         "Autocomplete / Tag Picker", "bills.id", "Liên kết M:N (LCMS)",
         "Gắn chi phí trực tiếp của chuyến vào Bill", "Có (thêm/xóa link)", "bill_movement_links.bill_id",
         "UI: CreateMovementForm.tsx; API: LegMovementLinkCommands.cs; DB: bill_movement_links; Test: Sprint2OperationalReferenceTests.cs")
    ]
    rows.extend(movement_fields)

    # 1.6 WAYBILL CAPTURE (WaybillCaptureForm.tsx)
    waybill_screen = "Khai báo Vận đơn Chi tiết (/bills/[id]/waybill, WaybillCaptureForm.tsx)"
    waybill_fields = [
        ("Vận hành (Operations)", waybill_screen, "Bên gửi (Shipper)",
         "Họ tên người gửi *", "senderName", "bill_waybills.sender_name", "String(128)", "Required",
         "Nhập tự do", "Không", "Vận đơn pháp lý",
         "In vận đơn phát hành cho khách", "Có (khi Draft)", "orders.contact_name",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.sender_name; Test: BillWaybillApiTests.cs"),

        ("Vận hành (Operations)", waybill_screen, "Bên gửi (Shipper)",
         "Điện thoại người gửi *", "senderPhone", "bill_waybills.sender_phone", "String(32)", "Required",
         "Nhập tự do", "Không", "Vận đơn pháp lý",
         "Liên hệ giao nhận bưu kiện", "Có (khi Draft)", "party_contacts.phone",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.sender_phone; Test: BillWaybillApiTests.cs"),

        ("Vận hành (Operations)", waybill_screen, "Bên gửi (Shipper)",
         "Địa chỉ người gửi *", "senderAddress", "bill_waybills.sender_address", "String(256)", "Required",
         "Nhập tự do", "Không", "Vận đơn pháp lý",
         "Điểm lấy hàng bưu chính", "Có (khi Draft)", "business_parties.address_line1",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.sender_address; Test: BillWaybillApiTests.cs"),

        ("Vận hành (Operations)", waybill_screen, "Bên nhận (Consignee)",
         "Họ tên người nhận *", "recipientName", "bill_waybills.recipient_name", "String(128)", "Required",
         "Nhập tự do", "Không", "Vận đơn pháp lý",
         "Người ký nhận bưu kiện", "Có (khi Draft)", "orders.contact_name",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.recipient_name; Test: BillWaybillApiTests.cs"),

        ("Vận hành (Operations)", waybill_screen, "Bên nhận (Consignee)",
         "Điện thoại người nhận *", "recipientPhone", "bill_waybills.recipient_phone", "String(32)", "Required",
         "Nhập tự do", "Không", "Vận đơn pháp lý",
         "Liên hệ phát hàng bưu chính", "Có (khi Draft)", "party_contacts.phone",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.recipient_phone; Test: BillWaybillApiTests.cs"),

        ("Vận hành (Operations)", waybill_screen, "Bên nhận (Consignee)",
         "Địa chỉ người nhận *", "recipientAddress", "bill_waybills.recipient_address", "String(256)", "Required",
         "Nhập tự do", "Không", "Vận đơn pháp lý",
         "Địa điểm giao phát bưu chính", "Có (khi Draft)", "business_parties.address_line1",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.recipient_address; Test: BillWaybillApiTests.cs"),

        ("Vận hành (Operations)", waybill_screen, "Cước phí & Thu hộ",
         "Cước chính *", "basePostage", "bill_waybills.base_postage", "Decimal(18,4)", "Required",
         "Nhập tự do (Number)", "Không", "Bảng giá bưu chính",
         "Doanh thu cước chính bưu kiện", "Có (khi Draft)", "revenues.amount",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.base_postage; Test: BillWaybillApiTests.cs"),

        ("Vận hành (Operations)", waybill_screen, "Cước phí & Thu hộ",
         "Phụ cước", "surcharge", "bill_waybills.surcharge", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Phụ cước bưu chính",
         "Doanh thu phụ phí phát tận tay/vùng sâu", "Có (khi Draft)", "revenues.amount",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.surcharge; Test: BillWaybillApiTests.cs"),

        ("Vận hành (Operations)", waybill_screen, "Cước phí & Thu hộ",
         "Tiền thu hộ COD", "codFee", "bill_waybills.cod_fee", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Tiền thu hộ thương mại",
         "Theo dõi công nợ thu hộ phải trả lại khách hàng", "Có (khi Draft)", "accounts_payable.amount",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.cod_fee; Test: BillWaybillApiTests.cs"),

        ("Vận hành (Operations)", waybill_screen, "Cước phí & Thu hộ",
         "Tổng cước thu *", "totalCollect", "bill_waybills.total_collect", "Decimal(18,4)", "Required (Tự tính)",
         "Tự động tính tổng", "Công thức sum", "Vận đơn tài chính",
         "Phát hành chứng từ thu tiền khách", "Tự động cập nhật theo dòng", "receivable_exposures.amount",
         "UI: WaybillCaptureForm.tsx; API: UpsertBillWaybillCommand.cs; DB: bill_waybills.total_collect; Test: BillWaybillApiTests.cs")
    ]
    rows.extend(waybill_fields)

    # =========================================================================
    # PHÂN HỆ 2: BẢNG GIÁ & TÍNH GIÁ (PRICING & RATING)
    # =========================================================================

    # 2.1 RATE CARD HEADER (/rate-cards/new)
    rc_screen = "Khai báo Bảng giá (/rate-cards/new, /rate-cards/[id])"
    rc_fields = [
        ("Bảng giá & Tính giá", rc_screen, "Thông tin định danh",
         "Mã bảng giá *", "code", "rate_cards.code", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Bảng giá LCMS",
         "Định danh hợp đồng bảng giá", "Không thể sửa sau khi lưu", "rate_cards.code",
         "UI: RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.code; Test: RateCardCommercialReadyTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Thông tin định danh",
         "Tên bảng giá *", "name", "rate_cards.name", "String(256)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Bảng giá LCMS",
         "Hiển thị tra cứu gợi ý bảng giá (Rate Suggestion)", "Có (khi chưa khóa)", "rate_cards.name",
         "UI: RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.name; Test: RateCardCommercialReadyTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Thông tin định danh",
         "Chiều bảng giá *", "partyType", "rate_cards.party_type", "String(16)", "Required (UI & API/DB)",
         "Dropdown ('customer' = Bán / 'vendor' = Mua)", "Lookup ('customer', 'vendor')", "Bảng giá LCMS",
         "Phân định nguồn sinh Doanh thu (SELL) hay Chi phí (BUY)", "Không thể sửa sau khi lưu", "ratings.party_type_code",
         "UI: RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.party_type; Test: RateCardCommercialReadyTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Thông tin định danh",
         "Chế độ khai báo *", "declarationMode", "rate_cards.declaration_mode", "String(32)", "Required (Default 'basic')",
         "Radio ('basic' = Đơn tuyến / 'advanced' = Đa tuyến)", "Lookup ('basic', 'advanced')", "Kiến trúc Bảng giá LCMS",
         "Quyết định hiển thị Form cơ bản hay Ma trận TariffMatrix", "Không thể sửa sau khi tạo quy tắc", "rate_cards.declaration_mode",
         "UI: RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.declaration_mode; Test: RateCardCommercialReadyTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Phạm vi áp dụng",
         "Phương thức vận chuyển *", "transportMode", "rate_cards.transport_mode", "String(16)", "Required (UI & API/DB)",
         "Dropdown ('air', 'sea', 'road', 'rail')", "Master Data / Enum Mode", "Bảng giá LCMS",
         "Lọc bảng giá áp dụng cho Bill/Leg", "Có (khi chưa có version)", "ratings.transport_mode",
         "UI: RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.transport_mode; Test: RateCardCommercialReadyTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Phạm vi áp dụng",
         "Loại dịch vụ áp dụng", "serviceType", "rate_cards.service_type", "String(64)", "Optional",
         "Dropdown / Autocomplete", "master_catalog_items (Category 'service_type')", "Bảng giá LCMS",
         "Matching loại hình vận chuyển", "Có (khi Draft)", "bills.service_type_code",
         "UI: RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.service_type; Test: RateCardCommercialReadyTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Phạm vi áp dụng",
         "Nhà cung cấp / Hãng (Bảng giá Mua)", "supplierPartyId", "rate_cards.supplier_party_id", "Guid", "Conditional (Required khi BUY)",
         "Dropdown / Autocomplete", "business_parties (Role: 'vendor'/'carrier')", "Bảng giá LCMS",
         "Xác định đối tượng thụ hưởng chi phí khi tính giá mua", "Có (qua API /partner)", "costs.vendor_party_id",
         "UI: PartyTypeahead.tsx, RateCardDeclarationForm.tsx; API: SetRateCardPartnerCommand.cs; DB: rate_cards.supplier_party_id; Test: BuyRatingLegTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Phạm vi áp dụng",
         "Khách hàng áp dụng (Bảng giá Bán)", "customerPartyId", "rate_cards.customer_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties (Role: 'customer')", "Bảng giá LCMS",
         "Bảng giá riêng theo khách hàng (Contract Rate)", "Có (khi chưa khóa)", "revenues.customer_party_id",
         "UI: PartyTypeahead.tsx, RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.customer_party_id; Test: RateCardCommercialReadyTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Phạm vi áp dụng",
         "Tiền tệ biểu giá *", "currencyCode", "rate_cards.currency_code", "String(3)", "Required (UI & API/DB)",
         "Dropdown", "currencies.code", "Bảng giá LCMS",
         "Quy định đồng tiền định giá gốc (Original Currency)", "Không thể sửa sau khi lưu version", "ratings.original_currency",
         "UI: CurrencySelect.tsx, RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.currency_code; Test: ReportingCurrencyFxTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Phân loại kinh tế",
         "Khoản mục kinh tế mặc định", "chargeCode", "rate_cards.charge_code", "String(64)", "Optional",
         "Dropdown / Autocomplete", "economic_charge_types.code", "Master Khoản mục kinh tế",
         "Kế thừa charge_code vào các quy tắc giá bên dưới", "Có (khi Draft)", "pricing_rules.charge_code",
         "UI: RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.charge_code; Test: ChargeProfitVatTests.cs"),

        ("Bảng giá & Tính giá", rc_screen, "Phân loại kinh tế",
         "Vai trò dòng chi phí (Line Role)", "lineRole", "rate_cards.line_role", "String(32)", "Optional (Default 'base_freight')",
         "Dropdown ('base_freight', 'surcharge', 'local_charge')", "Lookup Line Roles", "Chuẩn mực H-003",
         "Phân loại cước chính vs phụ phí trong PnL Margin", "Có (khi Draft)", "rating_details.financial_nature",
         "UI: RateCardDeclarationForm.tsx; API: SaveRateCardDeclarationCommand.cs; DB: rate_cards.line_role; Test: ChargeProfitVatTests.cs")
    ]
    rows.extend(rc_fields)

    # 2.2 RATE VERSION & VAT (/rate-cards/[id])
    rv_screen = "Phiên bản Bảng giá & Thuế (/rate-cards/[id], CreateRateVersionForm.tsx, DraftRateVersionVatForm.tsx)"
    rv_fields = [
        ("Bảng giá & Tính giá", rv_screen, "Phiên bản (Version)",
         "Số phiên bản *", "versionNumber", "rate_versions.version_number", "Int32", "Tự sinh (1, 2, ...)",
         "Tự sinh", "Không", "Phiên bản LCMS",
         "Quản lý lịch sử hiệu lực, bất biến khi đã phát hành", "Không thể sửa", "rate_versions.version_number",
         "UI: CreateRateVersionForm.tsx; API: CreateRateVersionCommand.cs; DB: rate_versions.version_number; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rv_screen, "Phiên bản (Version)",
         "Hiệu lực từ ngày *", "effectiveFrom", "rate_versions.effective_from", "DateTimeOffset", "Required (UI & API/DB)",
         "Nhập tự do (Date picker)", "Không", "Phiên bản LCMS",
         "Chặn tính giá nếu ngày áp dụng trước ngày này", "Chỉ sửa khi Draft; Khóa khi Published", "ratings.rate_date",
         "UI: CreateRateVersionForm.tsx; API: CreateRateVersionCommand.cs; DB: rate_versions.effective_from; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rv_screen, "Phiên bản (Version)",
         "Hiệu lực đến ngày", "effectiveTo", "rate_versions.effective_to", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Phiên bản LCMS",
         "Chặn tính giá nếu ngày áp dụng sau ngày này", "Chỉ sửa khi Draft; Khóa khi Published", "ratings.rate_date",
         "UI: CreateRateVersionForm.tsx; API: CreateRateVersionCommand.cs; DB: rate_versions.effective_to; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rv_screen, "Phiên bản (Version)",
         "Ghi chú phiên bản", "note", "rate_versions.note", "String(512)", "Optional",
         "Nhập tự do (Textarea)", "Không", "Phiên bản LCMS",
         "Lưu lý do điều chỉnh cước kỳ này", "Có (khi Draft)", "rate_versions.note",
         "UI: CreateRateVersionForm.tsx; API: CreateRateVersionCommand.cs; DB: rate_versions.note; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rv_screen, "Phiên bản (Version)",
         "Trạng thái phát hành", "isPublished", "rate_versions.is_published", "Boolean", "Tự sinh (Default false)",
         "Nút bấm phát hành (Publish)", "Không", "Phiên bản LCMS",
         "Đóng băng phiên bản, chỉ phiên bản Published mới được dùng để tính giá", "Một chiều (Draft -> Published)", "ratings.rate_version_id",
         "UI: PublishRateVersionButton.tsx; API: PublishRateVersionCommand.cs; DB: rate_versions.is_published; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rv_screen, "Cấu hình Thuế VAT",
         "Thuế suất VAT (%) *", "vatRate", "rate_versions.vat_rate", "Decimal(5,2)", "Optional (Default 0)",
         "Nhập tự do (Number)", "Không", "Chính sách thuế GTGT",
         "Tự động tính thuế VAT khi tính giá và seed Cost/Revenue", "Chỉ sửa khi Draft; Khóa khi Published", "costs.tax_rate, revenues.tax_rate",
         "UI: DraftRateVersionVatForm.tsx; API: PutRateVersionVatCommand.cs; DB: rate_versions.vat_rate; Test: ChargeProfitVatTests.cs"),

        ("Bảng giá & Tính giá", rv_screen, "Cấu hình Thuế VAT",
         "Phương pháp xử lý thuế VAT", "vatTreatment", "rate_versions.vat_treatment", "String(32)", "Optional (Default 'exclusive')",
         "Dropdown ('exclusive' = Chưa gồm thuế / 'inclusive' = Đã gồm thuế)", "Lookup VAT Treatment", "Kế toán thuế",
         "Quy định số tiền đơn giá có bao gồm VAT hay chưa", "Chỉ sửa khi Draft", "rating_details.vat_amount",
         "UI: DraftRateVersionVatForm.tsx; API: PutRateVersionVatCommand.cs; DB: rate_versions.vat_treatment; Test: ChargeProfitVatTests.cs")
    ]
    rows.extend(rv_fields)

    # 2.3 PRICING RULES - BASIC & ADVANCED MATRIX (/rate-cards/[id])
    pr_screen = "Quy tắc Tính giá Basic & Advanced (/rate-cards/[id], AddPricingRuleForm.tsx, TariffMatrix.tsx)"
    pr_fields = [
        ("Bảng giá & Tính giá", pr_screen, "Định danh quy tắc",
         "Mã quy tắc tính giá *", "code", "pricing_rules.code", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Bảng giá LCMS",
         "Định danh dòng giá trong phiên bản", "Chỉ sửa khi Version Draft", "pricing_rules.code",
         "UI: AddPricingRuleForm.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.code; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Định danh quy tắc",
         "Tên quy tắc tính giá *", "name", "pricing_rules.name", "String(256)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Bảng giá LCMS",
         "Hiển thị chi tiết dòng cước trên bảng kê đối soát", "Chỉ sửa khi Version Draft", "pricing_rules.name",
         "UI: AddPricingRuleForm.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.name; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Công thức & Khoản mục",
         "Loại công thức tính *", "formulaType", "pricing_rules.formula_type", "String(32)", "Required (UI & API/DB)",
         "Dropdown ('weight_break', 'weight_step', 'unit_rate', 'flat_rate', 'container_rate', 'composite')", "Lookup 6 Formula Types", "Engine LCMS",
         "Quyết định thuật toán tính toán của RatingEngine", "Chỉ sửa khi Version Draft", "ratings.context_json",
         "UI: AddPricingRuleForm.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.formula_type; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Công thức & Khoản mục",
         "Khoản mục kinh tế *", "economicChargeTypeId", "pricing_rules.economic_charge_type_id", "Guid", "Required (UI & API/DB)",
         "Dropdown", "economic_charge_types.id", "Master Data Charge Types",
         "Ánh xạ sang Cost/Revenue charge_code khi seed", "Chỉ sửa khi Version Draft", "costs.charge_code, revenues.charge_code",
         "UI: AddPricingRuleForm.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.economic_charge_type_id; Test: ChargeProfitVatTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Điều kiện áp dụng",
         "Tuyến áp dụng (Route)", "routeId", "pricing_rules.route_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "routes.id", "Master Data Routes",
         "Matching tuyến đường cụ thể của Bill", "Chỉ sửa khi Version Draft", "bills.route_id",
         "UI: AddPricingRuleForm.tsx, TariffMatrix.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.route_id; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Điều kiện áp dụng",
         "Áp dụng cho mọi tuyến", "appliesAnyRoute", "pricing_rules.applies_any_route", "Boolean", "Optional (Default false)",
         "Checkbox", "Không", "Quy tắc cước",
         "Cho phép quy tắc khớp với bất kỳ tuyến nào nếu không có tuyến riêng", "Chỉ sửa khi Version Draft", "pricing_rules.applies_any_route",
         "UI: AddPricingRuleForm.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.applies_any_route; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Điều kiện áp dụng",
         "Hãng vận chuyển (Carrier)", "carrierPartyId", "pricing_rules.carrier_party_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "business_parties (Role: 'carrier')", "Thỏa thuận hãng",
         "Chỉ áp dụng khi vận chuyển qua hãng này", "Chỉ sửa khi Version Draft", "shipments.carrier_party_id",
         "UI: AddPricingRuleForm.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.carrier_party_id; Test: BuyRatingLegTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Điều kiện áp dụng",
         "Mặt hàng áp dụng (Commodity Code)", "commodityCode", "pricing_rules.commodity_code", "String(64)", "Optional",
         "Dropdown / Autocomplete", "commodity_types.code", "Master Data Commodity",
         "Chỉ áp dụng khi hàng hóa có mã này", "Chỉ sửa khi Version Draft", "bills.commodity_type_id",
         "UI: AddPricingRuleForm.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.commodity_code; Test: ReferenceMasterTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Thông số tính toán",
         "Cơ sở trọng lượng (Weight Basis)", "weightBasis", "pricing_rules.weight_basis", "String(32)", "Optional (Default 'chargeable')",
         "Dropdown ('gross', 'volumetric', 'chargeable')", "Lookup Weight Basis", "Rating Engine",
         "Quyết định dùng Gross Weight hay Chargeable Weight để tra cước", "Chỉ sửa khi Version Draft", "ratings.chargeable_basis",
         "UI: AddPricingRuleForm.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.weight_basis; Test: RateWeightBandTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Thông số tính toán",
         "Mức cước tối thiểu (Min Charge)", "minCharge", "pricing_rules.min_charge", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Hợp đồng cước",
         "Bảo đảm cước không thấp hơn mức sàn", "Chỉ sửa khi Version Draft", "rating_details.amount",
         "UI: AddPricingRuleForm.tsx; API: CreatePricingRuleCommand.cs; DB: pricing_rules.min_charge; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Bậc giá (Rate Breaks)",
         "Bậc tối thiểu (Break Min) *", "weightMin", "rate_breaks.min_quantity", "Decimal(18,4)", "Required",
         "Nhập tự do (Number)", "Không", "Biểu giá lũy tiến",
         "Mốc dưới của bậc thang tính cước (VD: 0, 45, 100, 300, 500 kg)", "Chỉ sửa khi Version Draft", "rate_breaks.min_quantity",
         "UI: AddPricingRuleForm.tsx, TariffMatrix.tsx; API: AddRateBreakCommand.cs; DB: rate_breaks.min_quantity; Test: RateWeightBandTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Bậc giá (Rate Breaks)",
         "Bậc tối đa (Break Max)", "weightMax", "rate_breaks.max_quantity", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Biểu giá lũy tiến",
         "Mốc trên của bậc thang tính cước", "Chỉ sửa khi Version Draft", "rate_breaks.max_quantity",
         "UI: AddPricingRuleForm.tsx, TariffMatrix.tsx; API: AddRateBreakCommand.cs; DB: rate_breaks.max_quantity; Test: RateWeightBandTests.cs"),

        ("Bảng giá & Tính giá", pr_screen, "Bậc giá (Rate Breaks)",
         "Đơn giá bậc (Unit Rate) *", "unitAmount", "rate_breaks.unit_amount", "Decimal(18,4)", "Required",
         "Nhập tự do (Number)", "Không", "Biểu giá lũy tiến",
         "Đơn giá trên 1 kg/CBM tương ứng với bậc sản lượng", "Chỉ sửa khi Version Draft", "rate_breaks.unit_amount",
         "UI: AddPricingRuleForm.tsx, TariffMatrix.tsx; API: AddRateBreakCommand.cs; DB: rate_breaks.unit_amount; Test: RateWeightBandTests.cs")
    ]
    rows.extend(pr_fields)

    # 2.4 SURCHARGES (/rate-cards/surcharges/new, SurchargeForm.tsx)
    sc_screen = "Khai báo Phụ phí (/rate-cards/surcharges/new, SurchargeForm.tsx)"
    sc_fields = [
        ("Bảng giá & Tính giá", sc_screen, "Thông tin chung",
         "Mã phụ phí *", "code", "surcharges.code", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Master Phụ phí LCMS",
         "Định danh mã phụ phí (THC, CIC, EBS, DDC, ...)", "Khóa sau khi tạo", "surcharge_rules.charge_code",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharges.code; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Thông tin chung",
         "Tên phụ phí *", "name", "surcharges.name", "String(256)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Master Phụ phí LCMS",
         "Hiển thị tên phụ phí trên hóa đơn và đối soát", "Có (khi Draft)", "surcharges.name",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharges.name; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Thông tin chung",
         "Chiều phụ phí *", "direction", "surcharge_rules.direction", "String(16)", "Required ('buy' | 'sell' | 'both')",
         "Dropdown", "Lookup ('buy', 'sell', 'both')", "Chính sách phụ phí",
         "Quy định phụ phí là chi phí mua hay doanh thu bán", "Có (khi Draft)", "surcharge_rules.direction",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.direction; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Thông tin chung",
         "Khoản mục kinh tế *", "economicChargeTypeId", "surcharge_rules.economic_charge_type_id", "Guid", "Required (UI & API/DB)",
         "Dropdown", "economic_charge_types.id", "Master Data Charge Types",
         "Định danh kinh tế duy nhất cho phụ phí", "Có (khi Draft)", "costs.charge_code",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.economic_charge_type_id; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Phạm vi & Điều kiện",
         "Bảng giá liên kết", "rateCardId", "surcharge_rules.rate_card_id", "Guid", "Optional",
         "Dropdown", "rate_cards.id", "Bảng giá LCMS",
         "Ràng buộc phụ phí gắn kèm Rate Card cụ thể hay phụ phí độc lập", "Có (khi Draft)", "surcharge_rules.rate_card_id",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.rate_card_id; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Phạm vi & Điều kiện",
         "Tiền tệ phụ phí *", "currencyCode", "surcharge_rules.currency_code", "String(3)", "Required (Default 'VND')",
         "Dropdown", "currencies.code", "Bảng giá LCMS",
         "Đồng tiền thanh toán của phụ phí", "Có (khi Draft)", "surcharge_rules.currency_code",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.currency_code; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Phạm vi & Điều kiện",
         "Phương thức vận tải áp dụng", "transportMode", "surcharge_rules.transport_mode", "String(16)", "Optional",
         "Dropdown ('air', 'sea', 'road', 'rail')", "Master Data / Enum Mode", "Bảng giá LCMS",
         "Điều kiện kích hoạt phụ phí theo chế độ vận tải", "Có (khi Draft)", "surcharge_rules.transport_mode",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.transport_mode; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Phạm vi & Điều kiện",
         "Tuyến áp dụng (Route Code)", "routeCode", "surcharge_rules.route_code", "String(64)", "Optional",
         "Nhập tự do / Autocomplete", "routes.code", "Master Data Routes",
         "Điều kiện kích hoạt phụ phí theo tuyến", "Có (khi Draft)", "surcharge_rules.route_code",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.route_code; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Cách tính & Mức giá",
         "Chế độ tính cước *", "calculationMode", "surcharge_rules.calculation_mode", "String(32)", "Required (Default 'unit_rate')",
         "Dropdown ('unit_rate', 'flat_rate', 'percent_of_freight')", "Lookup Calc Mode", "Rating Engine",
         "Công thức tính tiền phụ phí", "Có (khi Draft)", "surcharge_rules.calculation_mode",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.calculation_mode; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Cách tính & Mức giá",
         "Mức giá / Tỷ lệ *", "rateAmountPercent", "surcharge_rules.rate_amount_percent", "Decimal(18,4)", "Required",
         "Nhập tự do (Number)", "Không", "Biểu giá phụ phí",
         "Số tiền cố định, đơn giá trên kg, hoặc tỷ lệ % cước chính", "Có (khi Draft)", "rating_details.amount",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.rate_amount_percent; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Cách tính & Mức giá",
         "Hiệu lực từ ngày *", "validFrom", "surcharge_versions.valid_from", "DateTimeOffset", "Required",
         "Nhập tự do (Date picker)", "Không", "Phiên bản phụ phí",
         "Khoảng thời gian bắt đầu áp dụng phụ phí", "Có (khi Draft)", "surcharge_versions.valid_from",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_versions.valid_from; Test: IndependentSurchargeTests.cs"),

        ("Bảng giá & Tính giá", sc_screen, "Cách tính & Mức giá",
         "Hiệu lực đến ngày", "validTo", "surcharge_versions.valid_to", "DateTimeOffset", "Optional",
         "Nhập tự do (Date picker)", "Không", "Phiên bản phụ phí",
         "Khoảng thời gian kết thúc áp dụng phụ phí", "Có (khi Draft)", "surcharge_versions.valid_to",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_versions.valid_to; Test: IndependentSurchargeTests.cs")
    ]
    rows.extend(sc_fields)

    # 2.5 RATING SCREEN (/rate-cards/rate, RateBillForm.tsx)
    rt_screen = "Màn hình Tính giá Bill & Chặng (/rate-cards/rate, RateBillForm.tsx)"
    rt_fields = [
        ("Bảng giá & Tính giá", rt_screen, "Ngữ cảnh tính giá",
         "Bill cần tính giá *", "billId", "ratings.bill_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "bills.id", "LCMS Financial Anchor",
         "Gốc tài chính của lần tính giá", "Không thể sửa sau khi lưu rating", "ratings.bill_id",
         "UI: RateBillForm.tsx, BillTypeahead.tsx; API: CreateRatingCommand.cs; DB: ratings.bill_id; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Ngữ cảnh tính giá",
         "Ngữ cảnh tính giá *", "contextType", "ratings.context_type", "String(16)", "Required ('bill' | 'leg')",
         "Radio / Select", "Lookup ('bill', 'leg')", "LCMS Rating Context",
         "Xác định nguồn kế thừa thông số vận hành (Theo Bill hay Chặng)", "Bất biến sau khi ghi nhận rating", "ratings.context_type",
         "UI: RateBillForm.tsx, BillRatingPanel.tsx; API: CreateRatingCommand.cs; DB: ratings.context_type; Test: BuyRatingLegTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Ngữ cảnh tính giá",
         "Chặng liên kết (khi chọn Leg)", "transportLegId", "ratings.transport_leg_id", "Guid", "Conditional (Required khi Leg)",
         "Dropdown", "transport_legs.id (kết nối Bill qua bill_leg_links)", "Vận hành Ops",
         "Kế thừa trọng lượng, điểm đi/đến, mode của chặng", "Bất biến sau khi tính", "ratings.transport_leg_id",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.transport_leg_id; Test: BuyRatingLegTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Ngữ cảnh tính giá",
         "Phiên bản bảng giá áp dụng *", "rateVersionId", "ratings.rate_version_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Rate Suggestion", "rate_versions.id (Published)", "Bảng giá LCMS",
         "Chỉ định bộ quy tắc giá được dùng để tính cước", "Bất biến sau khi tính", "ratings.rate_version_id",
         "UI: RateBillForm.tsx, RateCalculatorForm.tsx; API: CreateRatingCommand.cs; DB: ratings.rate_version_id; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Phương thức vận tải (Override)", "transportMode", "ratings.context_json (Override Mode)", "String(16)", "Optional",
         "Dropdown", "Master Data Mode", "User Input / Ops Fallback",
         "Ghi đè mode để tính giá thử nghiệm", "Lưu vào snapshot ContextJson", "ratings.context_json",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.context_json; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Tuyến vận chuyển (Override)", "routeCode", "ratings.route_code", "String(64)", "Optional",
         "Nhập tự do", "routes.code", "User Input / Bill Fallback",
         "Ghi đè tuyến tính giá", "Lưu vào snapshot ContextJson", "ratings.route_code",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.route_code; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Trọng lượng tính cước (Override CW)", "weight", "ratings.chargeable_weight_kg", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "User Input / Ops Fallback",
         "Trọng lượng thực tế áp dụng để tra bảng giá", "Lưu vào snapshot ContextJson", "ratings.chargeable_weight_kg",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.chargeable_weight_kg; Test: OperationalReferenceEditRatingContextTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Lý do ghi đè trọng lượng tính cước", "chargeableOverrideReason", "ratings.context_json (Reason)", "String(512)", "Conditional",
         "Nhập tự do", "Không", "LCMS Audit",
         "Giải trình nguyên nhân số kg tính giá khác với vận hành", "Lưu vào audit trail", "audit_events.reason",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.context_json; Test: OperationalReferenceEditRatingContextTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Ngày áp dụng giá (Rate Date)", "rateDate", "ratings.rate_date", "DateTimeOffset", "Optional (Default Now)",
         "Nhập tự do (Date picker)", "Không", "User Input / Bill Fallback",
         "Kiểm tra hiệu lực bảng giá và tra cứu tỷ giá Fx", "Lưu vào snapshot ContextJson", "ratings.rate_date",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.rate_date; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Chế độ vận chuyển matching", "transportMode", "ratings.transport_mode", "String(16)", "Optional",
         "Dropdown / Tự điền", "Master Data Mode", "LCMS Context Resolver",
         "Matching mode với quy tắc bảng giá", "Lưu vào snapshot ContextJson", "ratings.transport_mode",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.transport_mode; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Cảng đi matching", "originCode", "ratings.origin_code", "String(32)", "Optional",
         "Dropdown / Tự điền", "locations.code", "LCMS Context Resolver",
         "Matching điểm đi với quy tắc bảng giá", "Lưu vào snapshot ContextJson", "ratings.origin_code",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.origin_code; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Cảng đến matching", "destinationCode", "ratings.destination_code", "String(32)", "Optional",
         "Dropdown / Tự điền", "locations.code", "LCMS Context Resolver",
         "Matching điểm đến với quy tắc bảng giá", "Lưu vào snapshot ContextJson", "ratings.destination_code",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.destination_code; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Loại dịch vụ matching", "serviceTypeCode", "ratings.service_type_code", "String(64)", "Optional",
         "Dropdown / Tự điền", "master_catalog_items", "LCMS Context Resolver",
         "Matching loại dịch vụ với quy tắc bảng giá", "Lưu vào snapshot ContextJson", "ratings.service_type_code",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.service_type_code; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
         "Mã hàng hóa matching", "commodityCode", "ratings.commodity_code", "String(64)", "Optional",
         "Dropdown / Tự điền", "commodity_types.code", "LCMS Context Resolver",
         "Matching hàng hóa với quy tắc bảng giá", "Lưu vào snapshot ContextJson", "ratings.commodity_code",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: ratings.commodity_code; Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Dòng chi tiết tính cước",
         "Số tiền thành phần cước *", "detailAmount", "rating_details.amount", "Decimal(18,4)", "Tự tính",
         "RatingEngine tính toán", "Không", "Rating Engine LCMS",
         "Số tiền chi tiết từng thành phần để seed sang Cost/Revenue", "Bất biến sau khi tính", "rating_details.amount",
         "UI: BillRatingPanel.tsx; API: CreateRatingCommand.cs; DB: rating_details.amount; Test: RateRatingBaselineTests.cs"),
        ("Bảng giá & Tính giá", rt_screen, "Tự động sinh Chi phí / Doanh thu",
         "Tự động sinh Chi phí dự kiến", "seedExpectedCosts", "N/A (Command Flag)", "Boolean", "Checkbox (Default false)",
         "Checkbox trên UI", "Không", "User Action Flag",
         "Tự động gọi SeedExpectedCostsFromRatingCommand tạo Cost", "Chỉ thực thi lúc chạy lệnh", "costs.source_type, costs.source_id",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: costs (Expected); Test: RateRatingBaselineTests.cs"),

        ("Bảng giá & Tính giá", rt_screen, "Tự động sinh Chi phí / Doanh thu",
         "Tự động sinh Doanh thu dự kiến", "seedExpectedRevenues", "N/A (Command Flag)", "Boolean", "Checkbox (Default false)",
         "Checkbox trên UI", "Không", "User Action Flag",
         "Tự động gọi SeedExpectedRevenuesFromRatingCommand tạo Revenue", "Chỉ thực thi lúc chạy lệnh", "revenues.source_type, revenues.source_id",
         "UI: RateBillForm.tsx; API: CreateRatingCommand.cs; DB: revenues (Expected); Test: RateRatingBaselineTests.cs")
    ]
    rows.extend(rt_fields)

    # =========================================================================
    # PHÂN HỆ 3: CHI PHÍ, DOANH THU & CHỨNG TỪ (COST, REVENUE & DOCUMENTS)
    # =========================================================================

    # 3.1 DIRECT COST (/bills/[id]/costs/new, CreateCostForm.tsx)
    costs_screen = "Khai báo & Điều chỉnh Chi phí (/bills/[id]/costs/new, /costs/[id], CreateCostForm.tsx)"
    cost_fields = [
        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Định danh & Gốc tài chính",
         "Mã Bill gán chi phí *", "billId", "costs.bill_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / URL Param", "bills.id", "LCMS Financial Anchor",
         "Mỏ neo ghi nhận chi phí và tính Margin", "Không thể sửa sau khi lưu", "costs.bill_id",
         "UI: CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.bill_id; Test: Sprint4CostTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Định danh & Gốc tài chính",
         "Chặng phát sinh (Leg)", "transportLegId", "costs.transport_leg_id", "Guid", "Optional",
         "Dropdown", "transport_legs.id", "Ops Chặng",
         "Chi tiết hóa chi phí mua theo từng chặng vận tải", "Có (khi Expected)", "costs.transport_leg_id",
         "UI: CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.transport_leg_id; Test: BuyRatingLegTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Định danh & Gốc tài chính",
         "Khoản mục chi phí *", "chargeCode", "costs.charge_code", "String(64)", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "economic_charge_types.code", "Master Khoản mục kinh tế",
         "Phân loại chi phí kinh tế duy nhất (Single Economic Cost)", "Có (khi Expected/Draft)", "costs.charge_code",
         "UI: CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.charge_code; Test: Sprint4CostTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Định danh & Gốc tài chính",
         "Mã loại chi phí kế toán", "costTypeCode", "costs.cost_type_code", "String(64)", "Optional",
         "Dropdown / Autocomplete", "master_catalog_items (Category 'cost_type')", "Master Kế toán",
         "Phục vụ hạch toán vào phần mềm kế toán (ERP)", "Có (khi Expected)", "costs.cost_type_code",
         "UI: CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.cost_type_code; Test: Sprint4CostTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Đối tác & Tiền tệ",
         "Nhà cung cấp / Đối tác *", "vendorPartyId", "costs.vendor_party_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "business_parties (Role: 'vendor'/'carrier')", "Master Business Parties",
         "Chủ nợ AP, đối soát chứng từ đầu vào", "Có (qua API /partner)", "accounts_payable.vendor_party_id",
         "UI: PartyTypeahead.tsx, CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.vendor_party_id; Test: Sprint4CostTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Đối tác & Tiền tệ",
         "Số tiền nguyên tệ *", "amount", "costs.amount", "Decimal(18,4)", "Required (UI & API/DB)",
         "Nhập tự do (Number)", "Không", "Khai báo / Rating Seed",
         "Đại lượng kinh tế gốc, tính lợi nhuận Margin", "Có (khi Expected); Khóa khi Confirmed/Actual", "costs.amount",
         "UI: CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.amount; Test: Sprint4CostTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Đối tác & Tiền tệ",
         "Tiền tệ *", "currencyCode", "costs.currency_code", "String(3)", "Required (UI & API/DB)",
         "Dropdown", "currencies.code", "Khai báo / Rating Seed",
         "Quy đổi Fx sang tiền tệ báo cáo (Base Amount)", "Có (khi Expected)", "costs.currency_code",
         "UI: CurrencySelect.tsx, CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.currency_code; Test: Sprint4CostTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Đối tác & Tiền tệ",
         "Tỷ giá Fx áp dụng", "fxRate", "costs.fx_rate", "Decimal(18,6)", "Optional (Default 1.0)",
         "Nhập tự do / Tra cứu VCB", "fx_rates.rate", "LCMS Fx Engine",
         "Quy đổi số tiền sang Base Currency (VND)", "Có (khi Expected)", "costs.fx_rate",
         "UI: CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.fx_rate; Test: ReportingCurrencyFxTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Thuế & Ngày phát sinh",
         "Thuế suất VAT (%)", "taxRate", "costs.tax_rate", "Decimal(5,2)", "Optional (Default 0)",
         "Nhập tự do (Number)", "Không", "Chính sách thuế",
         "Tính số tiền thuế VAT đầu vào được khấu trừ", "Có (khi Expected)", "costs.tax_rate",
         "UI: CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.tax_rate; Test: ChargeProfitVatTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Thuế & Ngày phát sinh",
         "Số tiền thuế VAT", "vatAmount", "costs.vat_amount", "Decimal(18,4)", "Optional (Tự tính)",
         "Tự tính theo taxRate", "Không", "Kế toán thuế",
         "Ghi nhận nghĩa vụ thuế đầu vào", "Tự động cập nhật theo amount", "costs.vat_amount",
         "UI: CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.vat_amount; Test: ChargeProfitVatTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Thuế & Ngày phát sinh",
         "Ngày phát sinh chi phí *", "incurredDate", "costs.cost_date", "DateTimeOffset", "Required",
         "Nhập tự do (Date picker)", "Không", "Thời điểm dịch vụ hoàn thành",
         "Xác định kỳ kế toán phát sinh chi phí", "Có (khi Expected)", "costs.cost_date",
         "UI: CreateCostForm.tsx; API: CreateCostCommand.cs; DB: costs.cost_date; Test: Sprint4CostTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Trạng thái & Điều chỉnh",
         "Trạng thái trưởng thành (Maturity) *", "maturityState", "costs.maturity_state", "String(16)", "Required (UI & API/DB)",
         "Dropdown / Workflow State", "Lookup ('expected', 'confirmed', 'actual')", "Vòng đời chi phí LCMS",
         "Kiểm soát đối soát, tạo AP Exposure, chốt kỳ Financial Close", "Chuyển trạng thái qua Command có kiểm soát", "costs.maturity_state",
         "UI: MaturityTransitionButton.tsx; API: MaturityTransitionCommands.cs; DB: costs.maturity_state; Test: Sprint4FullCostTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", costs_screen, "Trạng thái & Điều chỉnh",
         "Lý do điều chỉnh chi phí", "adjustmentReason", "cost_adjustments.reason", "String(512)", "Conditional",
         "Nhập tự do (Textarea)", "Không", "Kế toán chi phí",
         "Bắt buộc nhập khi điều chỉnh chi phí đã Confirmed/Actual", "Không thể xóa sau khi lưu", "cost_adjustments.reason",
         "UI: AdjustCostRevenueButton.tsx; API: AdjustCostCommand.cs; DB: cost_adjustments.reason; Test: Sprint4CostTests.cs")
    ]
    rows.extend(cost_fields)

    # 3.2 SHARED COST & ALLOCATION (/costs/shared/new, AllocateSharedCostForm.tsx)
    shared_screen = "Khai báo & Phân bổ Chi phí dùng chung (/costs/shared/new, AllocateSharedCostForm.tsx)"
    shared_fields = [
        ("Chi phí, Doanh thu & Chứng từ", shared_screen, "Chi phí dùng chung gốc",
         "Khoản mục chi phí dùng chung *", "chargeCode", "costs.charge_code", "String(64)", "Required",
         "Dropdown / Autocomplete", "economic_charge_types.code", "Master Khoản mục kinh tế",
         "Định danh khoản chi phí mua chung cho nhiều Bill (VD: Cước xe gom, Thuê nguyên chuyến)", "Có (khi Draft)", "costs.charge_code",
         "UI: CreateSharedCostForm.tsx; API: CreateCostCommand.cs; DB: costs.charge_code; Test: AllocationBasisTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", shared_screen, "Chi phí dùng chung gốc",
         "Nhà cung cấp / Đối tác *", "vendorPartyId", "costs.vendor_party_id", "Guid", "Required",
         "Dropdown / Autocomplete", "business_parties (Role: 'vendor')", "Master Business Parties",
         "Chủ nợ AP cho toàn bộ khoản chi dùng chung", "Có (khi Draft)", "costs.vendor_party_id",
         "UI: CreateSharedCostForm.tsx; API: CreateCostCommand.cs; DB: costs.vendor_party_id; Test: AllocationBasisTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", shared_screen, "Chi phí dùng chung gốc",
         "Tổng số tiền chi phí cần phân bổ *", "amount", "costs.amount", "Decimal(18,4)", "Required",
         "Nhập tự do (Number)", "Không", "Tổng chi phí mua gốc",
         "Số tiền gốc đem đi chia nhỏ cho các Bill", "Khóa sau khi Finalize", "cost_allocations.total_amount",
         "UI: CreateSharedCostForm.tsx; API: CreateCostCommand.cs; DB: costs.amount; Test: AllocationBasisTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", shared_screen, "Cấu hình phân bổ",
         "Tiêu chí phân bổ (Allocation Basis) *", "allocationBasis", "cost_allocations.basis", "String(32)", "Required",
         "Dropdown ('gross_weight', 'chargeable_weight', 'cbm', 'package_count', 'revenue_ratio', 'equal')", "Lookup Basis", "Chính sách phân bổ LCMS",
         "Thuật toán chia tỷ lệ chi phí cho từng Bill", "Có (khi chưa chốt phiên)", "cost_allocations.basis",
         "UI: AllocateSharedCostForm.tsx; API: AllocationCommands.cs; DB: cost_allocations.basis; Test: AllocationBasisTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", shared_screen, "Cấu hình phân bổ",
         "Danh sách Bill thụ hưởng *", "eligibleBillIds", "cost_allocation_details.bill_id", "Array<Guid>", "Required",
         "Checkbox chọn từ danh sách Bill", "bills.id", "LCMS Financial Anchor",
         "Các Bill gánh chịu chi phí phân bổ này", "Có (khi chưa chốt phiên)", "cost_allocation_details.bill_id",
         "UI: AllocateSharedCostForm.tsx; API: AllocationCommands.cs; DB: cost_allocation_details.bill_id; Test: AllocationBasisTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", shared_screen, "Cấu hình phân bổ",
         "Tỷ lệ phân bổ tính toán (%)", "allocationRatio", "cost_allocation_details.ratio", "Decimal(8,6)", "Tự tính",
         "Tự động tính từ số đo vận hành của Bill", "Công thức chia tỷ lệ theo basis", "Engine phân bổ",
         "Tỷ trọng chi phí Bill phải gánh", "Tự động cập nhật theo basis", "cost_allocation_details.ratio",
         "UI: AllocateSharedCostForm.tsx; API: AllocationCommands.cs; DB: cost_allocation_details.ratio; Test: AllocationBasisTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", shared_screen, "Cấu hình phân bổ",
         "Số tiền phân bổ vào Bill *", "allocatedAmount", "cost_allocation_details.allocated_amount", "Decimal(18,4)", "Tự tính / Điều chỉnh",
         "Tự tính (có thể làm tròn)", "Không", "Chi phí trực tiếp của Bill",
         "Cộng trực tiếp vào chi phí thực tế của Bill để tính Margin", "Khóa sau khi Finalize", "costs.allocated_from_id",
         "UI: AllocateSharedCostForm.tsx; API: AllocationCommands.cs; DB: cost_allocation_details.allocated_amount; Test: AllocationBasisTests.cs")
    ]
    rows.extend(shared_fields)

    # 3.3 DIRECT REVENUE (/bills/[id]/revenues/new, CreateRevenueForm.tsx)
    revenues_screen = "Khai báo & Điều chỉnh Doanh thu (/bills/[id]/revenues/new, /revenues/[id], CreateRevenueForm.tsx)"
    rev_fields = [
        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Định danh & Gốc tài chính",
         "Mã Bill gán doanh thu *", "billId", "revenues.bill_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / URL Param", "bills.id", "LCMS Financial Anchor",
         "Mỏ neo ghi nhận doanh thu và tính Margin Bill", "Không thể sửa sau khi lưu", "revenues.bill_id",
         "UI: CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.bill_id; Test: Sprint5RevenueTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Định danh & Gốc tài chính",
         "Khoản mục doanh thu *", "chargeCode", "revenues.charge_code", "String(64)", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "economic_charge_types.code", "Master Khoản mục kinh tế",
         "Phân loại doanh thu kinh tế duy nhất (Single Economic Revenue)", "Có (khi Expected)", "revenues.charge_code",
         "UI: CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.charge_code; Test: Sprint5RevenueTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Định danh & Gốc tài chính",
         "Mã loại doanh thu kế toán", "revenueTypeCode", "revenues.revenue_type_code", "String(64)", "Optional",
         "Dropdown / Autocomplete", "master_catalog_items (Category 'revenue_type')", "Master Kế toán",
         "Hạch toán tài khoản doanh thu kế toán", "Có (khi Expected)", "revenues.revenue_type_code",
         "UI: CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.revenue_type_code; Test: Sprint5RevenueTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Đối tác & Tiền tệ",
         "Khách hàng *", "customerPartyId", "revenues.customer_party_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "business_parties (Role: 'customer')", "Master Business Parties",
         "Con nợ AR, đối tượng xuất hóa đơn thu tiền", "Có (qua API /partner)", "accounts_receivable.customer_party_id",
         "UI: PartyTypeahead.tsx, CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.customer_party_id; Test: Sprint5RevenueTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Đối tác & Tiền tệ",
         "Số tiền nguyên tệ *", "amount", "revenues.amount", "Decimal(18,4)", "Required (UI & API/DB)",
         "Nhập tự do (Number)", "Không", "Khai báo / Rating Seed",
         "Doanh thu dịch vụ ghi nhận cho Bill", "Có (khi Expected); Khóa khi Confirmed/Actual", "revenues.amount",
         "UI: CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.amount; Test: Sprint5RevenueTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Đối tác & Tiền tệ",
         "Tiền tệ doanh thu *", "currencyCode", "revenues.currency_code", "String(3)", "Required (UI & API/DB)",
         "Dropdown", "currencies.code", "Khai báo / Rating Seed",
         "Đồng tiền thu khách hàng, quy đổi Fx ra VND", "Có (khi Expected)", "revenues.currency_code",
         "UI: CurrencySelect.tsx, CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.currency_code; Test: Sprint5RevenueTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Đối tác & Tiền tệ",
         "Tỷ giá Fx doanh thu", "fxRate", "revenues.fx_rate", "Decimal(18,6)", "Optional (Default 1.0)",
         "Nhập tự do / Tra cứu VCB", "fx_rates.rate", "LCMS Fx Engine",
         "Quy đổi doanh thu sang Base Currency", "Có (khi Expected)", "revenues.fx_rate",
         "UI: CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.fx_rate; Test: ReportingCurrencyFxTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Thuế & Ngày ghi nhận",
         "Thuế suất VAT (%)", "taxRate", "revenues.tax_rate", "Decimal(5,2)", "Optional (Default 0)",
         "Nhập tự do (Number)", "Không", "Chính sách thuế",
         "Tính số tiền thuế VAT đầu ra phải nộp", "Có (khi Expected)", "revenues.tax_rate",
         "UI: CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.tax_rate; Test: ChargeProfitVatTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Thuế & Ngày ghi nhận",
         "Số tiền thuế VAT đầu ra", "vatAmount", "revenues.vat_amount", "Decimal(18,4)", "Optional (Tự tính)",
         "Tự tính theo taxRate", "Không", "Kế toán thuế",
         "Nghĩa vụ thuế VAT ghi nhận trên hóa đơn xuất khách", "Tự động cập nhật theo amount", "revenues.vat_amount",
         "UI: CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.vat_amount; Test: ChargeProfitVatTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Thuế & Ngày ghi nhận",
         "Ngày ghi nhận doanh thu *", "revenueDate", "revenues.revenue_date", "DateTimeOffset", "Required",
         "Nhập tự do (Date picker)", "Không", "Thời điểm dịch vụ hoàn thành",
         "Kỳ kế toán ghi nhận doanh thu", "Có (khi Expected)", "revenues.revenue_date",
         "UI: CreateRevenueForm.tsx; API: CreateRevenueCommand.cs; DB: revenues.revenue_date; Test: Sprint5RevenueTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Trạng thái & Điều chỉnh",
         "Trạng thái trưởng thành (Maturity) *", "maturityState", "revenues.maturity_state", "String(16)", "Required (UI & API/DB)",
         "Dropdown / Workflow State", "Lookup ('expected', 'confirmed', 'actual')", "Vòng đời doanh thu LCMS",
         "Kiểm soát tạo AR Exposure, đối soát và chốt sổ", "Chuyển trạng thái qua Command có kiểm soát", "revenues.maturity_state",
         "UI: MaturityTransitionButton.tsx; API: MaturityTransitionCommands.cs; DB: revenues.maturity_state; Test: Sprint5FullRevenueProfitabilityTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", revenues_screen, "Trạng thái & Điều chỉnh",
         "Lý do điều chỉnh doanh thu", "adjustmentReason", "revenue_adjustments.reason", "String(512)", "Conditional",
         "Nhập tự do (Textarea)", "Không", "Kế toán doanh thu",
         "Bắt buộc nhập khi điều chỉnh doanh thu đã Confirmed/Actual", "Không thể xóa sau khi lưu", "revenue_adjustments.reason",
         "UI: AdjustCostRevenueButton.tsx; API: AdjustRevenueCommand.cs; DB: revenue_adjustments.reason; Test: Sprint5RevenueTests.cs")
    ]
    rows.extend(rev_fields)

    # 3.4 FINANCIAL DOCUMENTS & MATCHING (/documents/receive, /documents/[id]/match)
    docs_screen = "Tiếp nhận & Khớp Chứng từ (/documents/receive, /documents/[id]/match, ReceiveDocumentForm.tsx)"
    doc_fields = [
        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Thông tin chứng từ",
         "Chiều chứng từ *", "direction", "financial_documents.direction", "String(16)", "Required ('payable' | 'receivable')",
         "Radio Button", "Lookup ('payable', 'receivable')", "Nghiệp vụ tài chính LCMS",
         "Xác định hóa đơn đầu vào (AP) hay đầu ra (AR)", "Không thể sửa sau khi lưu", "financial_documents.direction",
         "UI: ReceiveDocumentForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.direction; Test: Sprint6FinancialDocumentTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Thông tin chứng từ",
         "Chế độ tiếp nhận *", "mode", "financial_documents.reception_mode", "String(32)", "Required ('lcms_generated' | 'external_received')",
         "Radio Button", "Workflow Mode", "Giao diện tiếp nhận LCMS",
         "Nếu lcms_generated: Tự động kế thừa dòng từ Cost/Revenue đã chọn. Nếu external: Nhập thủ công", "Không thể sửa sau khi tiếp nhận", "financial_documents.reception_mode",
         "UI: ReceiveDocumentForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.reception_mode; Test: Sprint6FullFinancialDocumentTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Thông tin chứng từ",
         "Loại chứng từ *", "documentType", "financial_documents.document_type", "String(32)", "Required ('invoice' | 'debit_note' | 'credit_note')",
         "Dropdown", "Lookup ('invoice', 'debit_note', 'credit_note')", "Hồ sơ tài chính",
         "Phân loại hình thức hóa đơn GTGT hay Giấy báo nợ", "Có (qua CorrectDocumentHeaderForm)", "financial_documents.document_type",
         "UI: ReceiveDocumentForm.tsx, CorrectDocumentHeaderForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.document_type; Test: Sprint6FinancialDocumentTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Thông tin chứng từ",
         "Số hóa đơn / Chứng từ *", "documentNo", "financial_documents.document_no", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Đối tác cung cấp / Cục thuế",
         "Định danh chứng từ pháp lý, đối soát AP/AR", "Sửa qua CorrectDocumentHeaderForm", "financial_documents.document_no",
         "UI: ReceiveDocumentForm.tsx, CorrectDocumentHeaderForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.document_no; Test: Sprint6FinancialDocumentTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Thông tin chứng từ",
         "Đối tác chứng từ *", "counterpartyId", "financial_documents.counterparty_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "business_parties.id", "Master Business Parties",
         "Xác định pháp nhân xuất hoặc nhận hóa đơn", "Có (khi chưa khóa/khớp)", "financial_documents.counterparty_id",
         "UI: PartyTypeahead.tsx, ReceiveDocumentForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.counterparty_id; Test: Sprint6FinancialDocumentTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Thông tin chứng từ",
         "Bill liên quan (nếu có)", "billId", "financial_documents.bill_id", "Guid", "Optional",
         "Dropdown / Autocomplete", "bills.id", "LCMS Financial Anchor",
         "Lọc danh sách dòng chi phí/doanh thu để khớp nhanh", "Có (khi Draft)", "financial_documents.bill_id",
         "UI: BillTypeahead.tsx, ReceiveDocumentForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.bill_id; Test: DocumentBillIdFilterTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Thông tin chứng từ",
         "Ngày phát hành chứng từ *", "documentDate", "financial_documents.issue_date", "DateTimeOffset", "Required",
         "Nhập tự do (Date picker)", "Không", "Ngày ghi trên hóa đơn",
         "Kỳ kê khai thuế GTGT", "Có (khi Draft)", "financial_documents.issue_date",
         "UI: ReceiveDocumentForm.tsx, CorrectDocumentHeaderForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.issue_date; Test: Sprint6FinancialDocumentTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Thông tin chứng từ",
         "Hạn thanh toán hóa đơn", "dueDate", "financial_documents.due_date", "DateOnly", "Optional",
         "Nhập tự do (Date picker)", "Không", "Hợp đồng đối tác",
         "Kế thừa vào hạn nợ AP/AR khi ghi nhận", "Có (khi Draft)", "accounts_payable.due_date, accounts_receivable.due_date",
         "UI: ReceiveDocumentForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.due_date; Test: Sprint6FinancialDocumentTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Thông tin chứng từ",
         "Tiền tệ chứng từ *", "currencyCode", "financial_documents.currency_code", "String(3)", "Required (Default 'VND')",
         "Dropdown", "currencies.code", "Hóa đơn pháp lý",
         "Đồng tiền thanh toán của chứng từ", "Có (khi Draft)", "financial_documents.currency_code",
         "UI: CurrencySelect.tsx, ReceiveDocumentForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.currency_code; Test: ReportingCurrencyFxTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Số tiền & Dòng chi tiết",
         "Tổng tiền chứng từ *", "totalAmount", "financial_documents.total_amount", "Decimal(18,4)", "Required (UI & API/DB)",
         "Nhập tự do hoặc Tổng dòng được chọn", "Không", "Đối tác / Hệ thống",
         "Đối soát khớp dòng (3-way match) với chi phí/doanh thu", "Có (khi Draft)", "financial_documents.total_amount",
         "UI: ReceiveDocumentForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_documents.total_amount; Test: Sprint6FinancialDocumentTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Số tiền & Dòng chi tiết",
         "Diễn giải dòng hóa đơn *", "lineDescription", "financial_document_lines.description", "String(256)", "Required",
         "Nhập tự do", "Không", "Nội dung hóa đơn",
         "Lưu vết chi tiết từng dòng dịch vụ", "Có (qua EditDocumentLineForm)", "financial_document_lines.description",
         "UI: AddDocumentLineForm.tsx, EditDocumentLineForm.tsx; API: DocumentLineCommands; DB: financial_document_lines.description; Test: DocumentLineIntegrityTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Số tiền & Dòng chi tiết",
         "Số tiền dòng hóa đơn *", "lineAmount", "financial_document_lines.line_total", "Decimal(18,4)", "Required",
         "Nhập tự do (Number)", "Không", "Nội dung hóa đơn",
         "Số tiền chi tiết đối soát với Cost/Revenue", "Có (qua EditDocumentLineForm)", "financial_document_lines.line_total",
         "UI: AddDocumentLineForm.tsx; API: DocumentLineCommands; DB: financial_document_lines.line_total; Test: DocumentLineIntegrityTests.cs"),

        ("Chi phí, Doanh thu & Chứng từ", docs_screen, "Khớp chứng từ 3 bên",
         "Khoản chi phí / Doanh thu khớp *", "matchedCostRevenueId", "document_match_details.cost_id", "Guid", "Required",
         "Checkbox / Click chọn từ danh sách đủ điều kiện", "costs.id / revenues.id", "LCMS Financial Engine",
         "Khớp kinh tế (Economic Matching) và chuyển trạng thái Matched", "Có thể hủy khớp (Reverse Match)", "document_match_details",
         "UI: AddMatchDetailForm.tsx, StartDocumentMatchForm.tsx; API: AddDocumentMatchDetailCommand.cs; DB: document_match_details; Test: FinancialDocumentSourceMatchingTests.cs")
    ]
    rows.extend(doc_fields)

    # =========================================================================
    # PHÂN HỆ 4: CÔNG NỢ & DÒNG TIỀN (DEBT & CASH FLOW)
    # =========================================================================

    # 4.1 PAYABLE (AP) & PAYMENT (/ap-ar, /settlements/payments/new)
    ap_screen = "Quản lý Phải trả & Chi tiền (/ap-ar/exposures/new, /settlements/payments/new, CreateExposureForm.tsx)"
    ap_fields = [
        ("Công nợ & Dòng tiền", ap_screen, "Phải trả (Payable Exposure)",
         "Đối tác phải trả (Vendor) *", "vendorPartyId", "payable_exposures.vendor_party_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "business_parties.id", "Master Business Parties",
         "Chủ nợ AP", "Khóa sau khi tạo", "payable_exposures.vendor_party_id",
         "UI: CreateExposureForm.tsx; API: CreatePayableExposureCommand.cs; DB: payable_exposures.vendor_party_id; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Phải trả (Payable Exposure)",
         "Số tiền phải trả *", "amount", "payable_exposures.amount", "Decimal(18,4)", "Required (UI & API/DB)",
         "Nhập tự do (hoặc tự sinh từ Match Detail)", "Không", "LCMS Exposure Engine",
         "Xác định độ phơi nhiễm nợ phải trả", "Khóa sau khi Recognize thành AP chính thức", "payable_exposures.amount",
         "UI: CreateExposureForm.tsx; API: CreatePayableExposureCommand.cs; DB: payable_exposures.amount; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Phải trả (Payable Exposure)",
         "Tiền tệ phải trả *", "currencyCode", "payable_exposures.currency_code", "String(3)", "Required (Default 'VND')",
         "Dropdown", "currencies.code", "Tiền tệ LCMS",
         "Đồng tiền thanh toán công nợ", "Khóa sau khi tạo", "payable_exposures.currency_code",
         "UI: CurrencySelect.tsx, CreateExposureForm.tsx; API: CreatePayableExposureCommand.cs; DB: payable_exposures.currency_code; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Phải trả (Payable Exposure)",
         "Hạn thanh toán *", "dueDate", "accounts_payable.due_date", "DateOnly", "Required (UI & API/DB)",
         "Nhập tự do (Date picker)", "Không", "Thỏa thuận thanh toán",
         "Báo cáo tuổi nợ Aging Dashboard (0-30, 31-60, 61-90, >90 ngày)", "Có (qua Adjust AP)", "accounts_payable.due_date",
         "UI: RecognizeExposureForm.tsx; API: RecognizePayableExposureCommand.cs; DB: accounts_payable.due_date; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Sổ cái AP",
         "Nhà cung cấp chủ nợ AP *", "vendorPartyId", "accounts_payable.vendor_party_id", "Guid", "Required",
         "Dropdown / Kế thừa từ Exposure", "business_parties.id", "Sổ cái AP",
         "Định danh chủ thể được trả tiền", "Khóa sau khi tạo", "accounts_payable.vendor_party_id",
         "UI: ApArLedgerPanel.tsx; API: RecognizePayableExposureCommand.cs; DB: accounts_payable.vendor_party_id; Test: Sprint7ExposureApArTests.cs"),
        ("Công nợ & Dòng tiền", ap_screen, "Sổ cái AP",
         "Số tiền nợ gốc ban đầu", "originalAmount", "accounts_payable.original_amount", "Decimal(18,4)", "Tự sinh từ Exposure",
         "Tự sinh khi Recognize", "Không", "Sổ cái AP",
         "Ghi nhận nợ phải trả ban đầu", "Bất biến (Chỉ điều chỉnh qua Adjustment)", "accounts_payable.original_amount",
         "UI: ApArLedgerPanel.tsx; API: RecognizePayableExposureCommand.cs; DB: accounts_payable.original_amount; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Sổ cái AP",
         "Số tiền nợ còn lại (Remaining)", "remainingAmount", "accounts_payable.remaining_amount", "Decimal(18,4)", "Tự tính",
         "Tự động trừ lùi khi phân bổ thanh toán", "Công thức original - paid", "Sổ cái AP",
         "Theo dõi dư nợ chưa thanh toán cho nhà cung cấp", "Tự động cập nhật theo giao dịch", "accounts_payable.remaining_amount",
         "UI: ApArLedgerPanel.tsx; API: AllocatePaymentCommand.cs; DB: accounts_payable.remaining_amount; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Lập lệnh Chi tiền (Payment)",
         "Đối tác nhận tiền (Payee) *", "partyId", "payments.party_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "business_parties.id", "Master Business Parties",
         "Kiểm tra khớp với chủ nợ AP khi phân bổ", "Khóa sau khi tạo", "payments.party_id",
         "UI: CreateCashTxnForm.tsx; API: CreatePaymentCommand.cs; DB: payments.party_id; Test: Sprint8SettlementTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Lập lệnh Chi tiền (Payment)",
         "Số tiền chi *", "amount", "payments.amount", "Decimal(18,4)", "Required (UI & API/DB)",
         "Nhập tự do (Number)", "Không", "Giao dịch tiền mặt / Ủy nhiệm chi",
         "Quyết toán công nợ AP, đối soát Bank Feed", "Khóa sau khi Finalize", "payments.amount",
         "UI: CreateCashTxnForm.tsx; API: CreatePaymentCommand.cs; DB: payments.amount; Test: Sprint8SettlementTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Lập lệnh Chi tiền (Payment)",
         "Phương thức chi *", "paymentMethod", "payments.payment_method", "String(32)", "Required ('bank_transfer' | 'cash')",
         "Dropdown", "Lookup ('bank_transfer', 'cash')", "Kế toán thanh toán",
         "Phân định tiền gửi ngân hàng hay tiền mặt tại quỹ", "Có (khi Draft)", "payments.payment_method",
         "UI: CreateCashTxnForm.tsx; API: CreatePaymentCommand.cs; DB: payments.payment_method; Test: Sprint8SettlementTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Lập lệnh Chi tiền (Payment)",
         "Ngày chi tiền *", "paymentDate", "payments.payment_date", "DateTimeOffset", "Required",
         "Nhập tự do (Date picker)", "Không", "Thời điểm xuất quỹ",
         "Kỳ kế toán chi tiền, đối soát sao kê", "Có (khi Draft)", "payments.payment_date",
         "UI: CreateCashTxnForm.tsx; API: CreatePaymentCommand.cs; DB: payments.payment_date; Test: Sprint8SettlementTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Phân bổ Chi tiền (Payment Allocation)",
         "Khoản nợ AP được cấn trừ *", "accountsPayableId", "payment_allocations.accounts_payable_id", "Guid", "Required",
         "Dropdown chọn từ danh sách nợ mở của đối tác", "accounts_payable (Open/Partially Paid)", "Sổ cái công nợ AP",
         "Trừ giảm số dư nợ còn lại (Remaining Amount), đổi trạng thái Settled", "Có thể Reverse trước khi chốt kỳ", "payment_allocations.accounts_payable_id",
         "UI: AllocateCashForm.tsx; API: AllocatePaymentCommand.cs; DB: payment_allocations.accounts_payable_id; Test: Sprint8SettlementTests.cs"),

        ("Công nợ & Dòng tiền", ap_screen, "Phân bổ Chi tiền (Payment Allocation)",
         "Số tiền cấn trừ nợ *", "allocatedAmount", "payment_allocations.amount", "Decimal(18,4)", "Required",
         "Nhập tự do (Number)", "Không", "Thực tế cấn trừ",
         "Xác nhận số tiền thanh toán thực tế cho khoản nợ", "Có thể Reverse", "payment_allocations.amount",
         "UI: AllocateCashForm.tsx; API: AllocatePaymentCommand.cs; DB: payment_allocations.amount; Test: Sprint8SettlementTests.cs")
    ]
    rows.extend(ap_fields)

    # 4.2 RECEIVABLE (AR) & COLLECTION (/ap-ar, /settlements/collections/new)
    ar_screen = "Quản lý Phải thu & Thu tiền (/ap-ar/exposures/new, /settlements/collections/new, CreateExposureForm.tsx)"
    ar_fields = [
        ("Công nợ & Dòng tiền", ar_screen, "Phải thu (Receivable Exposure)",
         "Khách hàng phải thu (Customer) *", "customerPartyId", "receivable_exposures.customer_party_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "business_parties.id", "Master Business Parties",
         "Con nợ AR", "Khóa sau khi tạo", "receivable_exposures.customer_party_id",
         "UI: CreateExposureForm.tsx; API: CreateReceivableExposureCommand.cs; DB: receivable_exposures.customer_party_id; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Phải thu (Receivable Exposure)",
         "Số tiền phải thu *", "amount", "receivable_exposures.amount", "Decimal(18,4)", "Required (UI & API/DB)",
         "Nhập tự do (hoặc tự sinh từ Match Detail)", "Không", "LCMS Exposure Engine",
         "Xác định độ phơi nhiễm nợ phải thu từ khách", "Khóa sau khi Recognize thành AR chính thức", "receivable_exposures.amount",
         "UI: CreateExposureForm.tsx; API: CreateReceivableExposureCommand.cs; DB: receivable_exposures.amount; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Phải thu (Receivable Exposure)",
         "Tiền tệ phải thu *", "currencyCode", "receivable_exposures.currency_code", "String(3)", "Required (Default 'VND')",
         "Dropdown", "currencies.code", "Tiền tệ LCMS",
         "Đồng tiền thu nợ từ khách", "Khóa sau khi tạo", "receivable_exposures.currency_code",
         "UI: CurrencySelect.tsx, CreateExposureForm.tsx; API: CreateReceivableExposureCommand.cs; DB: receivable_exposures.currency_code; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Phải thu (Receivable Exposure)",
         "Hạn thu tiền *", "dueDate", "accounts_receivable.due_date", "DateOnly", "Required (UI & API/DB)",
         "Nhập tự do (Date picker)", "Không", "Hợp đồng dịch vụ khách hàng",
         "Theo dõi hạn nợ khách hàng trên Aging Dashboard", "Có (qua Adjust AR)", "accounts_receivable.due_date",
         "UI: RecognizeExposureForm.tsx; API: RecognizeReceivableExposureCommand.cs; DB: accounts_receivable.due_date; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Sổ cái AR",
         "Số tiền phải thu gốc ban đầu", "originalAmount", "accounts_receivable.original_amount", "Decimal(18,4)", "Tự sinh từ Exposure",
         "Tự sinh khi Recognize", "Không", "Sổ cái AR",
         "Ghi nhận quyền thu tiền chính thức", "Bất biến (Chỉ điều chỉnh qua Adjustment)", "accounts_receivable.original_amount",
         "UI: ApArLedgerPanel.tsx; API: RecognizeReceivableExposureCommand.cs; DB: accounts_receivable.original_amount; Test: Sprint7ExposureApArTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Sổ cái AR",
         "Số tiền còn phải thu (Remaining)", "remainingAmount", "accounts_receivable.remaining_amount", "Decimal(18,4)", "Tự tính",
         "Tự động trừ lùi khi phân bổ thu tiền", "Công thức original - collected", "Sổ cái AR",
         "Theo dõi dư nợ quá hạn của khách hàng", "Tự động cập nhật theo giao dịch", "accounts_receivable.remaining_amount",
         "UI: ApArLedgerPanel.tsx; API: AllocateCollectionCommand.cs; DB: accounts_receivable.remaining_amount; Test: ArLedgerRegressionTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Lập lệnh Thu tiền (Collection)",
         "Khách hàng nộp tiền (Payer) *", "partyId", "collections.party_id", "Guid", "Required (UI & API/DB)",
         "Dropdown / Autocomplete", "business_parties.id", "Master Business Parties",
         "Kiểm tra khớp với con nợ AR khi phân bổ", "Khóa sau khi tạo", "collections.party_id",
         "UI: CreateCashTxnForm.tsx; API: CreateCollectionCommand.cs; DB: collections.party_id; Test: Sprint8SettlementTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Lập lệnh Thu tiền (Collection)",
         "Số tiền thu *", "amount", "collections.amount", "Decimal(18,4)", "Required (UI & API/DB)",
         "Nhập tự do (Number)", "Không", "Giao dịch báo có ngân hàng / Phiếu thu",
         "Quyết toán công nợ AR, đối soát Bank Feed", "Khóa sau khi Finalize", "collections.amount",
         "UI: CreateCashTxnForm.tsx; API: CreateCollectionCommand.cs; DB: collections.amount; Test: Sprint8SettlementTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Lập lệnh Thu tiền (Collection)",
         "Phương thức thu *", "collectionMethod", "collections.collection_method", "String(32)", "Required ('bank_transfer' | 'cash')",
         "Dropdown", "Lookup ('bank_transfer', 'cash')", "Kế toán thu tiền",
         "Phân định tiền gửi ngân hàng hay tiền mặt tại quỹ", "Có (khi Draft)", "collections.collection_method",
         "UI: CreateCashTxnForm.tsx; API: CreateCollectionCommand.cs; DB: collections.collection_method; Test: Sprint8SettlementTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Lập lệnh Thu tiền (Collection)",
         "Ngày thu tiền *", "collectionDate", "collections.collection_date", "DateTimeOffset", "Required",
         "Nhập tự do (Date picker)", "Không", "Thời điểm tiền vào tài khoản",
         "Kỳ kế toán thu tiền, đối soát sao kê ngân hàng", "Có (khi Draft)", "collections.collection_date",
         "UI: CreateCashTxnForm.tsx; API: CreateCollectionCommand.cs; DB: collections.collection_date; Test: Sprint8SettlementTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Phân bổ Thu tiền (Collection Allocation)",
         "Khoản nợ AR được thu *", "accountsReceivableId", "collection_allocations.accounts_receivable_id", "Guid", "Required",
         "Dropdown chọn từ danh sách nợ mở của khách", "accounts_receivable (Open/Partially Paid)", "Sổ cái công nợ AR",
         "Trừ giảm số dư nợ còn lại (Remaining Amount), đổi trạng thái Settled", "Có thể Reverse trước khi chốt kỳ", "collection_allocations.accounts_receivable_id",
         "UI: AllocateCashForm.tsx; API: AllocateCollectionCommand.cs; DB: collection_allocations.accounts_receivable_id; Test: ArLedgerRegressionTests.cs"),

        ("Công nợ & Dòng tiền", ar_screen, "Phân bổ Thu tiền (Collection Allocation)",
         "Số tiền phân bổ thu nợ *", "allocatedAmount", "collection_allocations.amount", "Decimal(18,4)", "Required",
         "Nhập tự do (Number)", "Không", "Thực tế cấn trừ",
         "Xác nhận số tiền thu nợ thực tế cho hóa đơn", "Có thể Reverse", "collection_allocations.amount",
         "UI: AllocateCashForm.tsx; API: AllocateCollectionCommand.cs; DB: collection_allocations.amount; Test: ArLedgerRegressionTests.cs")
    ]
    rows.extend(ar_fields)

    # 4.3 BANK FEED, RECONCILIATION & FINANCIAL CLOSE (/bank-feed, /reconciliations/new, /financial-closes/new)
    recon_screen = "Đối soát & Chốt kỳ (/bank-feed, /reconciliations/new, /financial-closes/new)"
    recon_fields = [
        ("Công nợ & Dòng tiền", recon_screen, "Sổ phụ Ngân hàng (Bank Feed)",
         "Số tài khoản ngân hàng *", "bankAccountNumber", "bank_feed_lines.bank_account_number", "String(64)", "Required",
         "Nhập tự do hoặc Import CSV", "Không", "Ngân hàng thương mại",
         "Đối chiếu dòng tiền thực tế tài khoản công ty", "Không sửa sau khi import", "bank_feed_lines.bank_account_number",
         "UI: ImportBankFeedCsvForm.tsx, CreateBankFeedLineForm.tsx; API: ImportBankFeedCsvCommand.cs; DB: bank_feed_lines.bank_account_number; Test: BankFeedAndCompleteReconciliationTests.cs"),

        ("Công nợ & Dòng tiền", recon_screen, "Sổ phụ Ngân hàng (Bank Feed)",
         "Ngày hạch toán sao kê *", "bookingDate", "bank_feed_lines.booking_date", "DateOnly", "Required",
         "Nhập tự do hoặc Import CSV", "Không", "Sao kê ngân hàng",
         "Xác định ngày tiền vào/ra tài khoản ngân hàng", "Không sửa sau khi import", "bank_feed_lines.booking_date",
         "UI: ImportBankFeedCsvForm.tsx; API: ImportBankFeedCsvCommand.cs; DB: bank_feed_lines.booking_date; Test: BankFeedAndCompleteReconciliationTests.cs"),

        ("Công nợ & Dòng tiền", recon_screen, "Sổ phụ Ngân hàng (Bank Feed)",
         "Số tiền sao kê *", "amount", "bank_feed_lines.amount", "Decimal(18,4)", "Required",
         "Nhập tự do hoặc Import CSV", "Không", "Sao kê ngân hàng",
         "Đối chiếu khớp với Payment hoặc Collection", "Không sửa sau khi import", "bank_feed_lines.amount",
         "UI: ImportBankFeedCsvForm.tsx; API: ImportBankFeedCsvCommand.cs; DB: bank_feed_lines.amount; Test: BankFeedAndCompleteReconciliationTests.cs"),

        ("Công nợ & Dòng tiền", recon_screen, "Phiên Đối soát (Reconciliation)",
         "Kỳ đối soát sổ phụ *", "reconciliationPeriod", "reconciliations.period_to", "DateOnly", "Required",
         "Nhập tự do (Date picker)", "Không", "Kỳ tài chính LCMS",
         "Chốt sai lệch giữa Sổ quỹ và Sổ phụ ngân hàng", "Khóa khi Complete", "reconciliations.status",
         "UI: StartReconciliationForm.tsx; API: StartReconciliationCommand.cs; DB: reconciliations.period_to; Test: BankFeedAndCompleteReconciliationTests.cs"),

        ("Công nợ & Dòng tiền", recon_screen, "Phiên Đối soát (Reconciliation)",
         "Giao dịch khớp đối soát *", "settlementId", "reconciliation_details.settlement_id", "Guid", "Required",
         "Dropdown chọn từ danh sách Payment/Collection", "payments / collections", "Sổ quỹ LCMS",
         "Ghi nhận khớp dòng tiền sổ sách với dòng tiền ngân hàng", "Khóa sau khi hoàn thành", "reconciliation_details.settlement_id",
         "UI: AddReconciliationDetailForm.tsx; API: AddReconciliationDetailCommand.cs; DB: reconciliation_details.settlement_id; Test: BankFeedAndCompleteReconciliationTests.cs"),

        ("Công nợ & Dòng tiền", recon_screen, "Chốt kỳ Tài chính (Financial Close)",
         "Phạm vi chốt (Scope) *", "scopeType", "financial_closes.scope_type", "String(16)", "Required ('period' | 'bill' | 'tenant')",
         "Dropdown", "Lookup ('period', 'bill', 'tenant')", "Chính sách chốt tài chính C-010",
         "Quyết định đóng băng phạm vi dữ liệu tài chính", "Khóa sau khi chốt", "financial_closes.scope_type",
         "UI: StartFinancialCloseForm.tsx; API: CreateFinancialCloseSnapshotCommand.cs; DB: financial_closes.scope_type; Test: Sprint10FinancialCloseTests.cs"),

        ("Công nợ & Dòng tiền", recon_screen, "Chốt kỳ Tài chính (Financial Close)",
         "Từ ngày *", "periodFrom", "financial_closes.period_from", "DateOnly", "Conditional (Required khi Scope=period)",
         "Nhập tự do (Date picker)", "Không", "Kỳ tài chính LCMS",
         "Mốc bắt đầu kỳ kế toán được chốt", "Khóa sau khi chốt", "financial_closes.period_from",
         "UI: StartFinancialCloseForm.tsx; API: CreateFinancialCloseSnapshotCommand.cs; DB: financial_closes.period_from; Test: Sprint10FullFinancialCloseTests.cs"),

        ("Công nợ & Dòng tiền", recon_screen, "Chốt kỳ Tài chính (Financial Close)",
         "Đến ngày *", "periodTo", "financial_closes.period_to", "DateOnly", "Conditional (Required khi Scope=period)",
         "Nhập tự do (Date picker)", "Không", "Kỳ tài chính LCMS",
         "Mốc kết thúc kỳ kế toán: Chặn sửa toàn bộ Cost/Revenue trong kỳ", "Khóa sau khi chốt", "financial_closes.period_to",
         "UI: StartFinancialCloseForm.tsx; API: CreateFinancialCloseSnapshotCommand.cs; DB: financial_closes.period_to; Test: Sprint10FullFinancialCloseTests.cs"),

        ("Công nợ & Dòng tiền", recon_screen, "Chốt kỳ Tài chính (Financial Close)",
         "Chính sách kiểm soát chốt kỳ *", "policyVersion", "financial_closes.policy_version", "String(32)", "Required ('controlled' | 'strict')",
         "Dropdown", "Lookup ('controlled', 'strict')", "Chính sách quản trị rủi ro",
         "Chế độ Strict bắt buộc kiểm tra điều kiện Eligibility và chặn mọi ngoại lệ", "Khóa sau khi chốt", "financial_closes.policy_version",
         "UI: StartFinancialCloseForm.tsx; API: CreateFinancialCloseSnapshotCommand.cs; DB: financial_closes.policy_version; Test: ClosePackageTests.cs"),

        ("Công nợ & Dòng tiền", recon_screen, "Chốt kỳ Tài chính (Financial Close)",
         "Tiền tệ báo cáo chốt sổ *", "baseCurrency", "financial_closes.base_currency", "String(3)", "Required (Default 'VND')",
         "Dropdown", "currencies.code", "Tenant Config",
         "Đồng tiền chuẩn quy đổi toàn bộ báo cáo PnL", "Khóa sau khi chốt", "financial_closes.base_currency",
         "UI: StartFinancialCloseForm.tsx; API: CreateFinancialCloseSnapshotCommand.cs; DB: financial_closes.base_currency; Test: ClosePackageTests.cs")
    ]
    rows.extend(recon_fields)

    # =========================================================================
    # PHÂN HỆ 5: DANH MỤC & CẤU HÌNH (MASTER DATA & CONFIGURATION)
    # =========================================================================

    # 5.1 BUSINESS PARTIES (/admin/parties/new, /admin/parties/[id])
    parties_screen = "Quản lý Đối tác kinh doanh (/admin/parties/new, /admin/parties/[id])"
    party_fields = [
        ("Danh mục & Cấu hình", parties_screen, "Thông tin đối tác",
         "Mã đối tác *", "code", "business_parties.code", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Master Đối tác LCMS",
         "Định danh đối tác xuyên suốt hệ thống", "Không thể sửa mã", "business_parties.code",
         "UI: CreateBusinessPartyForm.tsx; API: BusinessPartyCommands.cs; DB: business_parties.code; Test: BusinessPartyMasterFullTests.cs"),

        ("Danh mục & Cấu hình", parties_screen, "Thông tin đối tác",
         "Tên pháp lý *", "legalName", "business_parties.legal_name", "String(256)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Cơ quan thuế / Giấy phép ĐKKD",
         "Xuất hóa đơn tài chính, in hợp đồng", "Có thể sửa", "business_parties.legal_name",
         "UI: CreateBusinessPartyForm.tsx, EditBusinessPartyForm.tsx; API: BusinessPartyCommands.cs; DB: business_parties.legal_name; Test: BusinessPartyMasterFullTests.cs"),

        ("Danh mục & Cấu hình", parties_screen, "Thông tin đối tác",
         "Tên giao dịch (Trade Name)", "tradeName", "business_parties.trade_name", "String(256)", "Optional",
         "Nhập tự do", "Không", "Thương mại",
         "Hiển thị tra cứu nhanh trên Typeahead", "Có thể sửa", "business_parties.trade_name",
         "UI: CreateBusinessPartyForm.tsx; API: BusinessPartyCommands.cs; DB: business_parties.trade_name; Test: BusinessPartyMasterFullTests.cs"),

        ("Danh mục & Cấu hình", parties_screen, "Thông tin đối tác",
         "Mã số thuế", "taxId", "business_parties.tax_id", "String(32)", "Optional (Khuyên dùng)",
         "Nhập tự do", "Không", "Cơ quan thuế",
         "Xác thực hóa đơn điện tử e-Invoice, chống trùng lặp đối tác", "Có thể sửa", "business_parties.tax_id",
         "UI: CreateBusinessPartyForm.tsx; API: BusinessPartyCommands.cs; DB: business_parties.tax_id; Test: BusinessPartyMasterFullTests.cs"),

        ("Danh mục & Cấu hình", parties_screen, "Thông tin đối tác",
         "Địa chỉ trụ sở *", "addressLine1", "business_parties.address_line1", "String(256)", "Required",
         "Nhập tự do", "Không", "Giấy phép kinh doanh",
         "In địa chỉ trên hóa đơn tài chính VAT", "Có thể sửa", "business_parties.address_line1",
         "UI: CreateBusinessPartyForm.tsx, VnAddressFields.tsx; API: BusinessPartyCommands.cs; DB: business_parties.address_line1; Test: BusinessPartyMasterFullTests.cs"),

        ("Danh mục & Cấu hình", parties_screen, "Thông tin đối tác",
         "Tỉnh / Thành phố *", "city", "business_parties.city", "String(128)", "Required",
         "Dropdown / Autocomplete", "Danh mục Tỉnh thành VN", "Địa chính",
         "Phân vùng địa lý giao nhận", "Có thể sửa", "business_parties.city",
         "UI: VnAddressFields.tsx; API: BusinessPartyCommands.cs; DB: business_parties.city; Test: BusinessPartyMasterFullTests.cs"),

        ("Danh mục & Cấu hình", parties_screen, "Thông tin đối tác",
         "Quốc gia *", "countryCode", "business_parties.country_code", "String(2)", "Required (Default 'VN')",
         "Dropdown (ISO 3166-1 alpha-2)", "Chuẩn ISO 3166", "Địa chính quốc tế",
         "Phân định đối tác nội địa hay quốc tế", "Có thể sửa", "business_parties.country_code",
         "UI: CreateBusinessPartyForm.tsx; API: BusinessPartyCommands.cs; DB: business_parties.country_code; Test: BusinessPartyMasterFullTests.cs"),

        ("Danh mục & Cấu hình", parties_screen, "Vai trò đối tác (Party Roles)",
         "Vai trò đảm nhiệm *", "roleCodes", "party_roles.role_code", "Array<String>", "Required",
         "Checkbox đa chọn ('customer', 'vendor', 'carrier', 'shipper', 'consignee')", "Lookup Roles", "Phân loại đối tác LCMS",
         "Quyết định đối tác được xuất hiện ở dropdown nào trên toàn hệ thống", "Có thể bật/tắt vai trò", "party_roles.role_code",
         "UI: PartyRolesPanel.tsx; API: PartyRoleCommands.cs; DB: party_roles; Test: BusinessPartyMasterFullTests.cs"),

        ("Danh mục & Cấu hình", parties_screen, "Tài khoản ngân hàng",
         "Số tài khoản ngân hàng *", "accountNumber", "party_bank_accounts.account_number", "String(64)", "Required",
         "Nhập tự do", "Không", "Ngân hàng",
         "Tự động điền tài khoản thụ hưởng khi lập lệnh chi tiền (Payment)", "Có thể thêm/sửa", "party_bank_accounts.account_number",
         "UI: PartyBankAccountsPanel.tsx, VnBankAutocomplete.tsx; API: BusinessPartyCommands.cs; DB: party_bank_accounts; Test: BusinessPartyDirectoryFullTests.cs"),

        ("Danh mục & Cấu hình", parties_screen, "Tài khoản ngân hàng",
         "Tên ngân hàng *", "bankName", "party_bank_accounts.bank_name", "String(128)", "Required",
         "Dropdown / Autocomplete", "Danh mục Ngân hàng VN (VietQR/Napas)", "Ngân hàng",
         "Đích chuyển tiền thanh toán", "Có thể sửa", "party_bank_accounts.bank_name",
         "UI: PartyBankAccountsPanel.tsx, VnBankAutocomplete.tsx; API: BusinessPartyCommands.cs; DB: party_bank_accounts.bank_name; Test: BusinessPartyDirectoryFullTests.cs")
    ]
    rows.extend(party_fields)

    # 5.2 LOCATIONS & ROUTES (/admin/locations, /admin/routes)
    loc_screen = "Quản lý Địa điểm & Tuyến đường (/admin/locations, /admin/routes)"
    loc_fields = [
        ("Danh mục & Cấu hình", loc_screen, "Địa điểm (Locations)",
         "Mã địa điểm *", "code", "locations.code", "String(32)", "Required (UI & API/DB)",
         "Nhập tự do (IATA/UN/LOCODE)", "Không (Chuẩn IATA/LOCODE)", "Master Địa điểm LCMS",
         "Matching tuyến đường và bảng giá", "Khóa sau khi tạo", "locations.code",
         "UI: UpsertLocationForm.tsx; API: LocationCommands.cs; DB: locations.code; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", loc_screen, "Địa điểm (Locations)",
         "Tên địa điểm *", "name", "locations.name", "String(128)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Master Địa điểm LCMS",
         "Hiển thị tên sân bay/cảng biển/kho bãi", "Có thể sửa", "locations.name",
         "UI: UpsertLocationForm.tsx; API: LocationCommands.cs; DB: locations.name; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", loc_screen, "Địa điểm (Locations)",
         "Loại địa điểm *", "locationType", "locations.location_type", "String(32)", "Required ('airport' | 'seaport' | 'warehouse' | 'city')",
         "Dropdown", "Lookup Type", "Master Địa điểm LCMS",
         "Kiểm tra tính tương thích với phương thức vận tải", "Có thể sửa", "locations.location_type",
         "UI: UpsertLocationForm.tsx; API: LocationCommands.cs; DB: locations.location_type; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", loc_screen, "Địa điểm (Locations)",
         "Mã UN/LOCODE", "unLocode", "locations.un_locode", "String(5)", "Optional",
         "Nhập tự do (5 ký tự chuẩn UN)", "Chuẩn UN/CEFACT", "Địa chính quốc tế",
         "Tích hợp hệ thống hàng hải quốc tế", "Có thể sửa", "locations.un_locode",
         "UI: UpsertLocationForm.tsx; API: LocationCommands.cs; DB: locations.un_locode; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", loc_screen, "Tuyến đường (Routes)",
         "Mã tuyến đường *", "code", "routes.code", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do (VD: SGN-HAN, SGN-SIN)", "Không", "Master Tuyến đường LCMS",
         "Matching quy tắc bảng giá Pricing Rules", "Khóa sau khi tạo", "routes.code",
         "UI: UpsertRouteForm.tsx; API: RouteCommands.cs; DB: routes.code; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", loc_screen, "Tuyến đường (Routes)",
         "Tên tuyến đường *", "name", "routes.name", "String(256)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Master Tuyến đường LCMS",
         "Hiển thị mô tả lộ trình vận chuyển", "Có thể sửa", "routes.name",
         "UI: UpsertRouteForm.tsx; API: RouteCommands.cs; DB: routes.name; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", loc_screen, "Tuyến đường (Routes)",
         "Điểm bắt đầu tuyến *", "originLocationId", "routes.origin_location_id", "Guid", "Required",
         "Dropdown / Autocomplete", "locations.id", "Master Địa điểm",
         "Ràng buộc địa lý điểm đi", "Có thể sửa", "routes.origin_location_id",
         "UI: UpsertRouteForm.tsx; API: RouteCommands.cs; DB: routes.origin_location_id; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", loc_screen, "Tuyến đường (Routes)",
         "Điểm kết thúc tuyến *", "destinationLocationId", "routes.destination_location_id", "Guid", "Required",
         "Dropdown / Autocomplete", "locations.id", "Master Địa điểm",
         "Ràng buộc địa lý điểm đến", "Có thể sửa", "routes.destination_location_id",
         "UI: UpsertRouteForm.tsx; API: RouteCommands.cs; DB: routes.destination_location_id; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", loc_screen, "Tuyến đường (Routes)",
         "Khoảng cách tuyến (Km)", "distanceKm", "routes.distance_km", "Decimal(10,2)", "Optional",
         "Nhập tự do (Number)", "Không", "Thông số vận tải",
         "Tính định mức cước đường bộ theo km", "Có thể sửa", "routes.distance_km",
         "UI: UpsertRouteForm.tsx; API: RouteCommands.cs; DB: routes.distance_km; Test: ReferenceMasterTests.cs")
    ]
    rows.extend(loc_fields)

    # 5.3 COMMODITIES, CURRENCIES, FX & ECONOMIC CHARGES
    master_screen = "Hàng hóa, Tiền tệ, Tỷ giá & Khoản mục kinh tế (/admin/commodities, /admin/currencies, /admin/fx-rates, /rate-cards/charge-types)"
    master_fields = [
        ("Danh mục & Cấu hình", master_screen, "Loại hàng hóa (Commodity Types)",
         "Mã loại hàng hóa *", "code", "commodity_types.code", "String(32)", "Required (UI & API/DB)",
         "Nhập tự do (GEN, DG, PER, VAL, ...)", "Không", "Master Loại hàng LCMS",
         "Matching phụ phí và đơn giá hàng đặc thù", "Khóa sau khi tạo", "commodity_types.code",
         "UI: UpsertCommodityForm.tsx; API: CommodityCommands.cs; DB: commodity_types.code; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Loại hàng hóa (Commodity Types)",
         "Tên loại hàng hóa *", "name", "commodity_types.name", "String(128)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Master Loại hàng LCMS",
         "Hiển thị tên hàng hóa trên form", "Có thể sửa", "commodity_types.name",
         "UI: UpsertCommodityForm.tsx; API: CommodityCommands.cs; DB: commodity_types.name; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Loại hàng hóa (Commodity Types)",
         "Hàng nguy hiểm (DG Flag)", "isHazardous", "commodity_types.is_hazardous", "Boolean", "Optional (Default false)",
         "Checkbox", "Không", "Quy định an toàn hàng không/hàng hải",
         "Tự động kích hoạt phụ phí hàng nguy hiểm khi tính giá", "Có thể sửa", "surcharge_rules.dangerous_goods",
         "UI: UpsertCommodityForm.tsx; API: CommodityCommands.cs; DB: commodity_types.is_hazardous; Test: ReferenceMasterTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Tiền tệ & Tỷ giá",
         "Mã tiền tệ *", "code", "currencies.code", "String(3)", "Required (UI & API/DB)",
         "Nhập tự do (ISO 4217)", "Chuẩn ISO 4217", "Master Tiền tệ LCMS",
         "Hạch toán tiền tệ toàn hệ thống", "Khóa sau khi tạo", "currencies.code",
         "UI: CreateCurrencyForm.tsx; API: UpsertCurrencyCommand.cs; DB: currencies.code; Test: ReportingCurrencyFxTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Tiền tệ & Tỷ giá",
         "Số chữ số thập phân *", "decimalPlaces", "currencies.decimal_places", "Int32", "Required (Default 0 cho VND, 2 cho USD)",
         "Nhập tự do (Number)", "Chuẩn tiền tệ", "Quy định làm tròn",
         "Làm tròn số tiền khi tính cước và in hóa đơn", "Có thể sửa", "currencies.decimal_places",
         "UI: CreateCurrencyForm.tsx; API: UpsertCurrencyCommand.cs; DB: currencies.decimal_places; Test: ReportingCurrencyFxTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Tiền tệ & Tỷ giá",
         "Tỷ giá quy đổi *", "rate", "fx_rates.rate", "Decimal(18,6)", "Required (UI & API/DB)",
         "Nhập tự do / Đồng bộ tự động VCB", "Ngân hàng Vietcombank", "Ngân hàng / LCMS Fx",
         "Chuyển đổi nguyên tệ sang tiền tệ báo cáo Base Currency", "Có thể thêm ngày mới (bất biến theo ngày)", "fx_rates.rate",
         "UI: UpsertFxRateForm.tsx, SyncVcbFxButton.tsx; API: FxRateCommands.cs; DB: fx_rates.rate; Test: ReportingCurrencyFxTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Tiền tệ & Tỷ giá",
         "Ngày hiệu lực tỷ giá *", "effectiveDate", "fx_rates.effective_date", "DateOnly", "Required",
         "Nhập tự do (Date picker)", "Không", "Tỷ giá công bố",
         "Tra cứu tỷ giá chính xác tại ngày phát sinh hóa đơn/tính giá", "Bất biến theo ngày", "fx_rates.effective_date",
         "UI: UpsertFxRateForm.tsx; API: FxRateCommands.cs; DB: fx_rates.effective_date; Test: ReportingCurrencyFxTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Khoản mục kinh tế chuẩn (Economic Charge Types)",
         "Mã khoản mục kinh tế *", "code", "economic_charge_types.code", "String(64)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "LCMS Master Economic Charge Types",
         "Cốt lõi hạch toán: Bảng giá -> Rating -> Cost/Revenue -> Hóa đơn", "Khóa sau khi có giao dịch", "economic_charge_types.code",
         "UI: EconomicChargeTypeForm.tsx; API: EconomicChargeCommands.cs; DB: economic_charge_types.code; Test: ChargeProfitVatTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Khoản mục kinh tế chuẩn (Economic Charge Types)",
         "Tên khoản mục kinh tế *", "name", "economic_charge_types.name", "String(256)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "LCMS Master Economic Charge Types",
         "Tên diễn giải khoản mục trên báo cáo lãi gộp", "Có thể sửa", "economic_charge_types.name",
         "UI: EconomicChargeTypeForm.tsx; API: EconomicChargeCommands.cs; DB: economic_charge_types.name; Test: ChargeProfitVatTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Khoản mục kinh tế chuẩn (Economic Charge Types)",
         "Bản chất tài chính *", "financialNature", "economic_charge_types.financial_nature", "String(16)", "Required ('cost' | 'revenue' | 'both')",
         "Dropdown", "Lookup ('cost', 'revenue', 'both')", "Chuẩn mực H-003",
         "Ràng buộc khoản mục chỉ dùng cho chi phí, doanh thu hoặc cả hai", "Khóa sau khi có giao dịch", "economic_charge_types.financial_nature",
         "UI: EconomicChargeTypeForm.tsx; API: EconomicChargeCommands.cs; DB: economic_charge_types.financial_nature; Test: ChargeProfitVatTests.cs"),

        ("Danh mục & Cấu hình", master_screen, "Khoản mục kinh tế chuẩn (Economic Charge Types)",
         "Thuế suất GTGT chuẩn (%)", "standardVatRate", "economic_charge_types.standard_vat_rate", "Decimal(5,2)", "Optional (Default 0)",
         "Nhập tự do (Number)", "Không", "Chính sách thuế",
         "Gợi ý tự động mức thuế suất khi khai báo bảng giá hoặc chi phí", "Có thể sửa", "costs.tax_rate",
         "UI: EconomicChargeTypeForm.tsx; API: EconomicChargeCommands.cs; DB: economic_charge_types.standard_vat_rate; Test: ChargeProfitVatTests.cs")
    ]
    rows.extend(master_fields)

    # 5.4 TENANT SETTINGS & POLICIES (/settings/company, /settings/business)
    settings_screen = "Cấu hình Doanh nghiệp & Chính sách Tenant (/settings/company, /settings/business)"
    settings_fields = [
        ("Danh mục & Cấu hình", settings_screen, "Thông tin Tenant",
         "Tên doanh nghiệp *", "legalName", "tenants.legal_name", "String(256)", "Required (UI & API/DB)",
         "Nhập tự do", "Không", "Đăng ký kinh doanh",
         "In tiêu đề trên mọi phiếu thu/chi và chứng từ nội bộ", "Có thể sửa", "tenants.legal_name",
         "UI: CompanyProfileForm.tsx; API: TenantProfileCommands.cs; DB: tenants.legal_name; Test: AdminSettingsFullTests.cs"),

        ("Danh mục & Cấu hình", settings_screen, "Thông tin Tenant",
         "Mã số thuế doanh nghiệp *", "taxCode", "tenants.tax_code", "String(32)", "Required",
         "Nhập tự do", "Không", "Cơ quan thuế",
         "Khai báo thông tin đơn vị phát hành chứng từ", "Có thể sửa", "tenants.tax_code",
         "UI: CompanyProfileForm.tsx; API: TenantProfileCommands.cs; DB: tenants.tax_code; Test: AdminSettingsFullTests.cs"),

        ("Danh mục & Cấu hình", settings_screen, "Cấu hình Tài chính Tenant",
         "Tiền tệ báo cáo mặc định *", "baseCurrency", "tenant_settings.base_currency", "String(3)", "Required (Default 'VND')",
         "Dropdown", "currencies.code", "Tenant Configuration",
         "Đồng tiền quy đổi chuẩn cho toàn bộ PnL và Financial Close", "Hạn chế sửa sau khi đã chốt kỳ", "tenant_settings.base_currency",
         "UI: TenantFinancialSettingsForm.tsx; API: TenantSettingsEndpoints.cs; DB: tenant_settings.base_currency; Test: AdminSettingsFullTests.cs"),

        ("Danh mục & Cấu hình", settings_screen, "Cấu hình Tài chính Tenant",
         "Chính sách vai trò đối tác trên Bill (Bill Party Policy)", "billPartyPolicyJson", "tenant_settings.bill_party_policy_json", "String (JSON)", "Optional",
         "Checkbox / Cấu hình JSON", "Lookup bắt buộc role", "Chính sách Tenant",
         "Quy định các vai trò đối tác bắt buộc phải có trước khi lưu Bill", "Có thể sửa", "tenant_settings.bill_party_policy_json",
         "UI: BillPartyPolicyForm.tsx; API: ReferenceMasterEndpoints.cs; DB: tenant_settings.bill_party_policy_json; Test: AdminSettingsFullTests.cs"),

        ("Danh mục & Cấu hình", settings_screen, "Cấu hình Tài chính Tenant",
         "Cho phép đối tác vãng lai (Allow Walk-in)", "allowWalkIn", "tenant_settings.allow_walk_in", "Boolean", "Optional (Default false)",
         "Checkbox", "Không", "Chính sách Tenant",
         "Cho phép nhập tên đối tác tự do khi chưa có trong danh mục Master", "Có thể sửa", "tenant_settings.allow_walk_in",
         "UI: BillPartyPolicyForm.tsx; API: ReferenceMasterEndpoints.cs; DB: tenant_settings.allow_walk_in; Test: AdminSettingsFullTests.cs"),

        ("Danh mục & Cấu hình", settings_screen, "Cấu hình Tài chính Tenant",
         "Ngưỡng phê duyệt tài chính (Approval Threshold)", "approvalThresholdAmount", "tenant_settings.approval_threshold_amount", "Decimal(18,4)", "Optional",
         "Nhập tự do (Number)", "Không", "Chính sách kiểm soát nội bộ",
         "Các khoản chi phí hoặc điều chỉnh vượt quá mức này bắt buộc qua phê duyệt", "Có thể sửa", "approvals.amount",
         "UI: TenantFinancialSettingsForm.tsx; API: TenantSettingsEndpoints.cs; DB: tenant_settings.approval_threshold_amount; Test: ControlPackageTests.cs")
    ]
    rows.extend(settings_fields)

    return rows

if __name__ == "__main__":
    r = get_field_inventory_rows()
    print(f"Total Full Field Inventory rows: {len(r)}")
