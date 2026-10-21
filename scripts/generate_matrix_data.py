# -*- coding: utf-8 -*-
"""
Module trích xuất và định nghĩa Ma trận Đối chiếu Trùng lặp & Kế thừa dữ liệu (Sheet 2 - Cross-Module Duplicate & Inheritance Matrix).
Bao gồm đầy đủ 7 chuỗi luồng nghiệp vụ với cả 2 nhánh AP/Chi tiền và AR/Thu tiền,
đồng thời bảo đảm 100% tính toàn vẹn tham chiếu với Sheet 1.
"""

def get_matrix_rows():
    rows = []

    # -------------------------------------------------------------
    # LUỒNG 1: ORDER -> BILL
    # -------------------------------------------------------------
    flow1 = [
        ("Luồng 1: Order -> Bill", "orders.customer_party_id", "bills.customer_party_id",
         "Định danh khách hàng thương mại của dịch vụ", "Nhập lại thủ công (Manual Re-entry)",
         "100% Trùng lặp. Khi tạo Bill liên kết Order qua RefTagPicker, UI chỉ lưu link M:N vào order_bill_links sau khi tạo; không có logic prefill khách hàng từ Order sang form Bill.",
         "Rủi ro chọn nhầm khách hàng giữa Bill và Order; tốn thao tác tìm kiếm lại đối tác.",
         "UI: CreateBillWorkspaceForm.tsx (RefTagPicker); API: LinkOrderToBillCommand.cs; DB: order_bill_links; Test: Ui02OperationalCreateContextTests.cs",
         "Tự động điền (Auto-fill) customer_party_id khi người dùng chọn Order đầu tiên trong Bill; cho phép sửa đổi nếu cố tình tách pháp nhân thanh toán."),

        ("Luồng 1: Order -> Bill", "orders.transport_mode", "bills.transport_mode",
         "Chế độ vận tải (air, sea, road, rail) quyết định cách tính cước", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp hoàn toàn. Người dùng vừa chọn mode trên Order, sang form Bill vẫn phải chọn lại dropdown mode.",
         "Sai lệch phương thức vận chuyển dẫn đến sai lệch hệ số quy đổi thể tích tính cước (Air factor 167 vs Sea 1:1 CBM).",
         "UI: CreateBillWorkspaceForm.tsx; API: CreateBillCommand.cs; DB: bills.transport_mode; Test: Ui02OperationalCreateContextTests.cs",
         "Kế thừa tự động từ Order được liên kết; khóa hoặc cảnh báo nếu cố tình chọn mode khác với Order."),

        ("Luồng 1: Order -> Bill", "orders.origin_code", "bills.origin_code",
         "Địa điểm lấy hàng / Cảng xuất phát", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp 100%. Người dùng phải gõ lại hoặc chọn lại Location ở form Bill dù Order đã khai báo cụ thể.",
         "Sai lệch địa điểm gây lỗi 'NO_APPLICABLE_RATE' khi tính giá tự động do không match quy tắc bảng giá.",
         "UI: LocationField.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.origin_code",
         "Tự động điền origin_code từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "orders.destination_code", "bills.destination_code",
         "Địa điểm giao hàng / Cảng đến", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp 100%. Người dùng phải gõ lại Destination trên form Bill.",
         "Sai lệch địa điểm giao nhận, tính sai giá cước chặng trả hàng.",
         "UI: LocationField.tsx, CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.destination_code",
         "Tự động điền destination_code từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "orders.etd", "bills.etd",
         "Thời gian dự kiến khởi hành (ETD)", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải mở lịch chọn lại ETD trên form Bill.",
         "Lệch kỳ áp dụng bảng giá (rate_effective_date) và lệch kỳ kế toán dự thu.",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.etd",
         "Kế thừa mặc định ETD từ Order sang Bill; cho phép sửa khi có cập nhật lịch trình thực tế."),

        ("Luồng 1: Order -> Bill", "orders.eta", "bills.eta",
         "Thời gian dự kiến cập cảng/đến nơi (ETA)", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải mở lịch chọn lại ETA trên form Bill.",
         "Sai lệch kỳ ghi nhận doanh thu và đối soát tiến độ giao hàng.",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.eta",
         "Kế thừa mặc định ETA từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "orders.shipper_party_id", "bills.shipper_party_id",
         "Chủ thể gửi hàng trên vận đơn", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp hoàn toàn. Cả hai màn hình đều dùng PartyTypeahead để tìm lại shipper.",
         "Sai lệch thông tin pháp lý trên hóa đơn và vận đơn vận chuyển (Waybill).",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.shipper_party_id",
         "Kế thừa tự động từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "orders.consignee_party_id", "bills.consignee_party_id",
         "Chủ thể nhận hàng trên vận đơn", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp hoàn toàn. Người dùng phải tìm kiếm lại consignee.",
         "Sai lệch người nhận trên chứng từ giải phóng hàng và thu cước trả sau.",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.consignee_party_id",
         "Kế thừa tự động consignee_party_id từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "orders.service_type_code", "bills.service_type_code",
         "Loại hình dịch vụ vận chuyển", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải chọn lại loại dịch vụ trên form Bill.",
         "Không xác định đúng điều kiện áp dụng cước, gây tranh chấp nghĩa vụ thanh toán.",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.service_type_code",
         "Tự động điền service_type_code từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "orders.incoterms", "bills.incoterms",
         "Điều kiện thương mại quốc tế (Incoterms)", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải chọn lại Incoterms (EXW, FOB, CIF, DDP, ...).",
         "Sai sót trong việc phân định cước trả trước (Prepaid) hay cước trả sau (Collect).",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.incoterms",
         "Tự động điền incoterms từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "orders.customer_ref", "bills.customer_ref",
         "Mã tham chiếu riêng của khách hàng", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải copy-paste mã ref của khách từ Order sang Bill.",
         "Thiếu thông tin đối chiếu trên Debit Note gửi cho khách hàng, dẫn đến bị từ chối thanh toán.",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.customer_ref",
         "Tự động kế thừa customer_ref từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "orders.commodity_type_id", "bills.commodity_type_id",
         "Mặt hàng chuyên chở (General, DG, Reefer, ...)", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Chọn lại dropdown Commodity ở cả 2 màn hình.",
         "Bỏ sót tính chất hàng nguy hiểm hoặc hàng lạnh, dẫn đến tính thiếu phụ phí DG/Reefer.",
         "UI: CreateCargoFields.tsx; API: UpdateBillContextCommand.cs; DB: bills.commodity_type_id",
         "Kế thừa tự động mặt hàng chính từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "operational_measurements.package_count", "operational_measurements.package_count",
         "Số kiện hàng hóa", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. CreateCargoFields xuất hiện ở cả hai form nhưng dữ liệu không liên thông.",
         "Sai lệch số kiện trên vận đơn so với hợp đồng đơn hàng.",
         "UI: CreateCargoFields.tsx; API: UpdateBillContextCommand.cs; DB: operational_measurements.package_count; Test: OperationalCargoTests.cs",
         "Tự động kế thừa tổng số kiện từ Order sang Bill (hoặc sum nếu nhiều Order)."),

        ("Luồng 1: Order -> Bill", "operational_measurements.gross_weight_kg", "operational_measurements.gross_weight_kg",
         "Trọng lượng thực tế (Gross Weight kg)", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải gõ lại số cân nặng thô trên form Bill.",
         "Sai lệch khối lượng tính cước giữa đơn hàng thương mại và hồ sơ tài chính của Bill.",
         "UI: CreateCargoFields.tsx; API: UpdateBillContextCommand.cs; DB: operational_measurements.gross_weight_kg; Test: OperationalCargoTests.cs",
         "Tự động kế thừa gross_weight_kg từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "operational_measurements.volume_cbm", "operational_measurements.volume_cbm",
         "Thể tích hàng hóa (Volume CBM)", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải gõ lại số khối CBM trên form Bill.",
         "Sai lệch thể tích quy đổi cước đường biển LCL hoặc hàng không cồng kềnh.",
         "UI: CreateCargoFields.tsx; API: UpdateBillContextCommand.cs; DB: operational_measurements.volume_cbm; Test: OperationalCargoTests.cs",
         "Tự động kế thừa volume_cbm từ Order sang Bill."),

        ("Luồng 1: Order -> Bill", "operational_measurements.chargeable_weight_kg", "operational_measurements.chargeable_weight_kg",
         "Trọng lượng tính cước chuẩn (Chargeable Weight)", "Nhập lại thủ công (Manual Re-entry)",
         "Người dùng phải nhập lại hoặc bấm tính lại trên form Bill.",
         "Không đồng bộ trọng lượng tính cước báo giá với trọng lượng tính cước hạch toán.",
         "UI: CreateCargoFields.tsx; API: UpdateBillContextCommand.cs; DB: operational_measurements.chargeable_weight_kg; Test: OperationalCargoTests.cs",
         "Tự động kế thừa hoặc chạy lại công thức RatingEngine.Chargeable từ số đo kế thừa.")
    ]
    rows.extend(flow1)

    # -------------------------------------------------------------
    # LUỒNG 2: BILL -> SHIPMENT
    # -------------------------------------------------------------
    flow2 = [
        ("Luồng 2: Bill -> Shipment", "bills.transport_mode", "shipments.transport_mode",
         "Chế độ vận tải của lô hàng gom", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Màn hình tạo Shipment bắt buộc chọn transportMode, không kế thừa từ các Bill được liên kết.",
         "Sai lệch giữa phương thức ghi nhận tài chính (Bill) và phương thức vận hành thực tế (Shipment).",
         "UI: CreateShipmentForm.tsx, QuickCreateShipmentDialog.tsx; API: UpsertShipmentCommand.cs; DB: shipments.transport_mode",
         "Khi tạo Shipment từ màn hình Bill hoặc gom các Bill cùng phương thức: Tự động gán mode từ Bill sang Shipment."),

        ("Luồng 2: Bill -> Shipment", "bills.origin_code", "shipments.origin_code",
         "Cảng đi / Cảng xuất phát lô hàng gom", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải nhập lại điểm đi ở form Shipment.",
         "Lô hàng gom đi lệch cảng/sân bay so với chứng từ Bill của khách hàng.",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.origin_code",
         "Kế thừa origin_code từ Bill (với lô hàng đơn) hoặc gợi ý cảng gom chung."),

        ("Luồng 2: Bill -> Shipment", "bills.destination_code", "shipments.destination_code",
         "Cảng đến / Cảng đích lô hàng gom", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải nhập lại điểm đến ở form Shipment.",
         "Lô hàng gom dỡ lệch cảng/sân bay so với chứng từ Bill.",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.destination_code",
         "Kế thừa destination_code từ Bill sang Shipment."),

        ("Luồng 2: Bill -> Shipment", "bills.route_code", "shipments.route_code",
         "Mã tuyến vận chuyển lô gom", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Nhập lại tuyến đường ở form Shipment.",
         "Không match được bảng giá mua BUY theo tuyến.",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.route_code",
         "Tự động điền route_code từ Bill sang Shipment."),

        ("Luồng 2: Bill -> Shipment", "bills.carrier_party_id", "shipments.carrier_party_id",
         "Hãng vận chuyển (Carrier)", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Form Bill có trường 'Hãng dự kiến', form Shipment lại bắt chọn lại Carrier qua PartyTypeahead.",
         "Không đồng bộ giữa dự toán chi phí mua và đối tác vận tải thực tế phát hành Master Bill.",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.carrier_party_id",
         "Tự động điền Carrier dự kiến từ Bill sang Shipment; cho phép cập nhật khi hãng thực tế thay đổi."),

        ("Luồng 2: Bill -> Shipment", "bills.service_type_code", "shipments.service_type_code",
         "Loại dịch vụ vận chuyển", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải chọn lại dịch vụ gom hàng.",
         "Không đồng bộ điều kiện dịch vụ giữa bán ra (Bill) và mua vào (Shipment).",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.service_type_code",
         "Kế thừa service_type_code từ Bill sang Shipment."),

        ("Luồng 2: Bill -> Shipment", "bills.etd", "shipments.etd",
         "Ngày khởi hành dự kiến", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải mở lịch chọn lại ETD.",
         "Lệch lịch trình giữa vận đơn khách hàng và lịch tàu/bay thực tế.",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.etd",
         "Tự động lấy ETD sớm nhất của các Bill liên kết làm ETD dự kiến cho Shipment."),

        ("Luồng 2: Bill -> Shipment", "bills.eta", "shipments.eta",
         "Ngày đến nơi dự kiến", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Người dùng phải mở lịch chọn lại ETA.",
         "Sai lệch cam kết tiến độ giao hàng cho khách.",
         "UI: CreateShipmentForm.tsx; API: UpsertShipmentCommand.cs; DB: shipments.eta",
         "Tự động lấy ETA của các Bill liên kết làm căn cứ cho Shipment."),

        ("Luồng 2: Bill -> Shipment", "bills.commodity_type_id", "shipments.commodity_type_id",
         "Mặt hàng chuyên chở chính", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Chọn lại dropdown Commodity ở form Shipment.",
         "Bỏ sót khai báo loại hàng nguy hiểm/lạnh trên hồ sơ gửi hãng vận chuyển.",
         "UI: CreateCargoFields.tsx; API: UpsertShipmentCommand.cs; DB: shipments.commodity_type_id",
         "Kế thừa commodity_type_id từ Bill sang Shipment."),

        ("Luồng 2: Bill -> Shipment", "operational_measurements.gross_weight_kg", "operational_measurements.gross_weight_kg",
         "Tổng trọng lượng lô hàng gom", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Nhân viên vận hành phải tự bấm máy tính cộng dồn trọng lượng các Bill để gõ vào Shipment.",
         "Sai lệch số liệu manifest, quá tải phương tiện vận chuyển, sai lệch phân bổ chi phí BUY theo tỷ lệ trọng lượng.",
         "UI: CreateCargoFields.tsx; API: UpsertShipmentCommand.cs; DB: operational_measurements.gross_weight_kg; Test: OperationalCargoTests.cs",
         "Tự động tính tổng cộng dồn (Auto-rollup / Aggregate Sum) gross_weight_kg từ tất cả các Bill liên kết vào Shipment."),

        ("Luồng 2: Bill -> Shipment", "operational_measurements.volume_cbm", "operational_measurements.volume_cbm",
         "Tổng thể tích lô hàng gom", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Phải tự cộng dồn CBM ngoài Excel rồi gõ vào Shipment.",
         "Sai lệch book chỗ (Booking capacity) với hãng hàng không hoặc hãng tàu.",
         "UI: CreateCargoFields.tsx; API: UpsertShipmentCommand.cs; DB: operational_measurements.volume_cbm; Test: OperationalCargoTests.cs",
         "Tự động tính tổng cộng dồn volume_cbm từ các Bill gom vào Shipment."),

        ("Luồng 2: Bill -> Shipment", "operational_measurements.package_count", "operational_measurements.package_count",
         "Tổng số kiện lô hàng gom", "Nhập lại thủ công (Manual Re-entry)",
         "Trùng lặp. Gõ lại tổng số kiện trên Shipment.",
         "Lệch số kiện khi làm thủ tục hải quan xuất nhập khẩu lô hàng gom.",
         "UI: CreateCargoFields.tsx; API: UpsertShipmentCommand.cs; DB: operational_measurements.package_count; Test: OperationalCargoTests.cs",
         "Tự động tính tổng số kiện từ các Bill vào Shipment.")
    ]
    rows.extend(flow2)

    # -------------------------------------------------------------
    # LUỒNG 3: SHIPMENT -> CHẶNG (LEG) -> CHUYẾN (MOVEMENT)
    # -------------------------------------------------------------
    flow3 = [
        ("Luồng 3: Shipment -> Leg -> Movement", "shipments.transport_mode", "transport_legs.transport_mode",
         "Chế độ vận tải của chặng con", "Kế thừa một phần (Fallback logic tại Backend)",
         "Trên UI người dùng vẫn phải chọn hoặc để trống. Tại Backend, RatingEngine và ShipmentModePolicy thực hiện fallback: Nếu Leg không có mode thì lấy mode của Shipment.",
         "Người dùng nhập thừa thãi trên UI; nếu nhập mâu thuẫn giữa chặng và lô hàng thì gây hiểu lầm.",
         "Code: ShipmentModePolicy.cs (LegRatingMode), RatingReadiness.cs; UI: CreateLegForm.tsx; DB: transport_legs.transport_mode; Test: BuyRatingLegTests.cs",
         "Mặc định tự điền mode của Shipment vào Leg; chỉ cho phép sửa khi lô hàng là đa phương thức (Multimodal)."),

        ("Luồng 3: Shipment -> Leg -> Movement", "shipments.origin_code", "transport_legs.origin_code",
         "Điểm bắt đầu của chặng", "Nhập lại thủ công (Manual Re-entry)",
         "Với chặng đơn (1 Shipment : 1 Leg), người dùng vẫn phải gõ lại cặp cảng đi trên form Leg.",
         "Lãng phí thời gian nhập liệu, nguy cơ sai sót chính tả mã cảng.",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.origin_code",
         "Với chặng đầu tiên: Tự động kế thừa origin_code từ Shipment. Với các chặng nối tiếp: Tự động gán origin của chặng N = destination của chặng N-1."),

        ("Luồng 3: Shipment -> Leg -> Movement", "shipments.destination_code", "transport_legs.destination_code",
         "Điểm kết thúc của chặng", "Nhập lại thủ công (Manual Re-entry)",
         "Người dùng phải nhập lại cảng đến trên form Leg.",
         "Sai lệch cảng dỡ hàng của chặng con.",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.destination_code",
         "Tự động điền destination_code từ Shipment sang Leg cuối cùng."),

        ("Luồng 3: Shipment -> Leg -> Movement", "shipments.carrier_party_id", "transport_legs.carrier_party_id",
         "Nhà cung cấp vận tải chặng", "Nhập lại thủ công (Manual Re-entry)",
         "Người dùng phải chọn lại Carrier trên form Leg.",
         "Không đối soát được AP phát sinh theo chặng với hãng vận tải của Shipment.",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.carrier_party_id",
         "Kế thừa tự động carrier_party_id từ Shipment sang Chặng."),

        ("Luồng 3: Shipment -> Leg -> Movement", "shipments.service_type_code", "transport_legs.service_type_code",
         "Loại dịch vụ vận chuyển của chặng", "Nhập lại thủ công (Manual Re-entry)",
         "Phải chọn lại loại dịch vụ trên form Chặng.",
         "Sai lệch quy tắc tính giá mua BUY theo chặng.",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.service_type_code",
         "Kế thừa tự động service_type_code từ Shipment sang Chặng."),

        ("Luồng 3: Shipment -> Leg -> Movement", "shipments.commodity_type_id", "transport_legs.commodity_type_id",
         "Loại hàng hóa vận chuyển trên chặng", "Nhập lại thủ công (Manual Re-entry)",
         "Chọn lại dropdown Commodity trên form Chặng.",
         "Không match được bảng giá mua hàng lạnh/nguy hiểm theo chặng.",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.commodity_type_id",
         "Kế thừa tự động commodity_type_id từ Shipment sang Chặng."),

        ("Luồng 3: Shipment -> Leg -> Movement", "shipments.etd", "transport_legs.etd",
         "Thời gian khởi hành chặng", "Nhập lại thủ công (Manual Re-entry)",
         "Người dùng phải mở lịch chọn lại ETD chặng.",
         "Lệch ngày áp dụng bảng giá mua theo chặng.",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.etd",
         "Kế thừa mặc định ETD từ Shipment sang Chặng đầu tiên."),

        ("Luồng 3: Shipment -> Leg -> Movement", "shipments.eta", "transport_legs.eta",
         "Thời gian kết thúc chặng", "Nhập lại thủ công (Manual Re-entry)",
         "Người dùng phải mở lịch chọn lại ETA chặng.",
         "Lệch tiến độ vận chuyển của chặng con.",
         "UI: CreateLegForm.tsx; API: UpsertTransportLegCommand.cs; DB: transport_legs.eta",
         "Kế thừa ETA từ Shipment sang Chặng cuối."),

        ("Luồng 3: Shipment -> Leg -> Movement", "transport_legs.origin_code", "transport_movements.origin_location_id",
         "Hành trình xuất phát thực tế của chuyến xe/tàu", "Nhập lại thủ công (Manual Re-entry)",
         "Form CreateMovementForm bắt nhập điểm đi hoàn toàn độc lập với Leg.",
         "Sai lệch lộ trình thực tế so với hợp đồng vận tải.",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.origin_location_id",
         "Tự động kế thừa địa điểm đi từ Chặng được gán sang Chuyến."),

        ("Luồng 3: Shipment -> Leg -> Movement", "transport_legs.destination_code", "transport_movements.destination_location_id",
         "Hành trình kết thúc thực tế của chuyến xe/tàu", "Nhập lại thủ công (Manual Re-entry)",
         "Form CreateMovementForm bắt nhập điểm đến độc lập.",
         "Sai lệch lộ trình thực tế của xe chở hàng.",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.destination_location_id",
         "Tự động kế thừa địa điểm đến từ Chặng sang Chuyến."),

        ("Luồng 3: Shipment -> Leg -> Movement", "transport_legs.carrier_party_id", "transport_movements.carrier_party_id",
         "Nhà xe / Hãng thực hiện chuyến vận chuyển", "Nhập lại thủ công (Manual Re-entry)",
         "Không kế thừa tự động. Người dùng phải chọn lại Carrier trên Movement.",
         "Không đối soát được AP phát sinh theo chuyến với hãng vận chuyển đã ký hợp đồng ở Chặng.",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.carrier_party_id",
         "Tự động điền Carrier của Chặng sang Chuyến khi khởi tạo."),

        ("Luồng 3: Shipment -> Leg -> Movement", "transport_legs.transport_mode", "transport_movements.transport_mode",
         "Phương thức vận chuyển thực tế của chuyến", "Nhập lại thủ công (Manual Re-entry)",
         "Chọn lại dropdown mode trên form Chuyến.",
         "Sai lệch phương tiện vận chuyển thực tế.",
         "UI: CreateMovementForm.tsx; API: UpsertTransportMovementCommand.cs; DB: transport_movements.transport_mode",
         "Kế thừa tự động transport_mode từ Chặng sang Chuyến.")
    ]
    rows.extend(flow3)

    # -------------------------------------------------------------
    # LUỒNG 4: BILL / CHẶNG -> TÍNH GIÁ (RATING)
    # -------------------------------------------------------------
    flow4 = [
        ("Luồng 4: Bill / Chặng -> Rating", "bills.transport_mode", "ratings.transport_mode",
         "Phương thức vận chuyển để matching bảng giá", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động triệt để tại Backend qua RatingContextResolver.cs. UI hiển thị sẵn mode kế thừa từ Bill (hoặc Leg) và cho phép override.",
         "Không có rủi ro; hoạt động chuẩn xác theo thiết kế H-002/H-003.",
         "Code: RatingReadiness.cs (RatingContextResolver), CreateRatingCommand.cs; UI: RateBillForm.tsx, BillRatingPanel.tsx; Test: RateRatingBaselineTests.cs",
         "Duy trì cơ chế kế thừa hiện tại; tiếp tục bảo toàn quy tắc Leg override Shipment."),

        ("Luồng 4: Bill / Chặng -> Rating", "bills.origin_code", "ratings.origin_code",
         "Cảng đi để matching bảng giá", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động qua RatingContextResolver. Nếu Context là Leg thì lấy từ Leg/Shipment; nếu Context là Bill thì lấy từ Bill.",
         "Hoạt động tốt, giảm thiểu 100% công sức gõ lại cảng đi.",
         "Code: RatingReadiness.cs; UI: RateBillForm.tsx; DB: ratings.origin_code; Test: RateRatingBaselineTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 4: Bill / Chặng -> Rating", "bills.destination_code", "ratings.destination_code",
         "Cảng đến để matching bảng giá", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động qua RatingContextResolver từ Bill hoặc Leg.",
         "Chính xác, không gây lỗi gõ sai mã cảng.",
         "Code: RatingReadiness.cs; UI: RateBillForm.tsx; DB: ratings.destination_code; Test: RateRatingBaselineTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 4: Bill / Chặng -> Rating", "bills.route_code", "ratings.route_code",
         "Tuyến đường matching quy tắc giá (Pricing Rule)", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động từ Bill hoặc Leg vào tham số đánh giá Pricing Rules.",
         "Khớp chính xác quy tắc giá đơn tuyến hoặc đa tuyến.",
         "Code: RatingReadiness.cs; UI: RateBillForm.tsx; DB: ratings.route_code; Test: RateRatingBaselineTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 4: Bill / Chặng -> Rating", "bills.service_type_code", "ratings.service_type_code",
         "Loại dịch vụ vận chuyển để lọc bảng giá", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động từ Bill (hoặc Leg/Shipment).",
         "Lọc đúng bảng giá theo loại dịch vụ tương ứng.",
         "Code: RatingReadiness.cs; UI: RateBillForm.tsx; DB: ratings.service_type_code; Test: RateRatingBaselineTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 4: Bill / Chặng -> Rating", "bills.commodity_type_id", "ratings.commodity_code",
         "Mặt hàng áp dụng đơn giá", "Tự điền / Kế thừa tự động (Auto-filled)",
         "RatingContextResolver tự động query bảng commodity_types lấy code tương ứng để match với điều kiện bảng giá.",
         "Hoạt động ổn định, loại trừ hoàn toàn việc chọn nhầm loại hàng.",
         "Code: RatingReadiness.cs (lines 197-207); DB: commodity_types.code; Test: RateRatingBaselineTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 4: Bill / Chặng -> Rating", "operational_measurements.chargeable_weight_kg", "ratings.chargeable_weight_kg",
         "Khối lượng tính cước quyết định tiền cước chính", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động từ số liệu đo lường vận hành. Nếu người dùng nhập đè trên UI tính giá -> ghi nhận lý do và cập nhật snapshot context_json.",
         "Được kiểm soát chặt chẽ bởi quyền field override và lưu audit trail.",
         "Code: RatingContextResolver.cs, RatingEngine.cs; UI: RateBillForm.tsx; DB: ratings.chargeable_weight_kg; Test: OperationalReferenceEditRatingContextTests.cs",
         "Duy trì cơ chế kế thừa và audit hiện có."),

        ("Luồng 4: Bill / Chặng -> Rating", "bills.rate_effective_date", "ratings.rate_date",
         "Ngày áp dụng bảng giá", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động từ rate_effective_date của Bill (nếu không có thì lấy ngày hiện tại).",
         "Bảo đảm tính đúng giá theo thời điểm hiệu lực của hợp đồng.",
         "Code: RatingReadiness.cs; UI: RateBillForm.tsx; DB: ratings.rate_date; Test: RateRatingBaselineTests.cs",
         "Duy trì cơ chế hiện tại.")
    ]
    rows.extend(flow4)

    # -------------------------------------------------------------
    # LUỒNG 5: BẢNG GIÁ -> RATING -> CHI PHÍ / DOANH THU
    # -------------------------------------------------------------
    flow5 = [
        ("Luồng 5: Rate Card -> Rating -> Cost/Revenue", "pricing_rules.economic_charge_type_id", "costs.charge_code",
         "Khoản mục kinh tế chi phí", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa 100% tự động. Từ Bảng giá -> Rating Details -> Sinh bản ghi Cost qua SeedExpectedCostsFromRatingCommand.",
         "Đảm bảo nguyên tắc Single Economic Charge Type (Chuẩn mực H-003).",
         "Code: SeedExpectedCostsFromRatingCommand.cs; DB: costs.charge_code; Test: ChargeProfitVatTests.cs",
         "Duy trì cơ chế kế thừa tự động; cấm sửa đổi charge_code sau khi đã seed thành công."),

        ("Luồng 5: Rate Card -> Rating -> Cost/Revenue", "pricing_rules.economic_charge_type_id", "revenues.charge_code",
         "Khoản mục kinh tế doanh thu", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa 100% tự động. Từ Bảng giá -> Rating Details -> Sinh bản ghi Revenue qua SeedExpectedRevenuesFromRatingCommand.",
         "Đảm bảo hạch toán doanh thu đồng nhất với biểu giá bán.",
         "Code: SeedExpectedRevenuesFromRatingCommand.cs; DB: revenues.charge_code; Test: ChargeProfitVatTests.cs",
         "Duy trì cơ chế kế thừa tự động."),

        ("Luồng 5: Rate Card -> Rating -> Cost/Revenue", "rating_details.amount", "costs.amount",
         "Số tiền chi phí dự kiến (Expected Cost)", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa chính xác từ kết quả tính giá của RatingEngine sang bảng costs với trạng thái ban đầu là 'expected'.",
         "Idempotent: Nếu đã seed rồi thì không tạo trùng bản ghi thứ hai (C-003).",
         "Code: SeedExpectedCostsFromRatingCommand.cs; DB: costs.amount, costs.source_type, costs.source_id; Test: Sprint4CostTests.cs",
         "Duy trì cơ chế sinh tự động idempotent hiện có."),

        ("Luồng 5: Rate Card -> Rating -> Cost/Revenue", "rating_details.amount", "revenues.amount",
         "Số tiền doanh thu dự kiến (Expected Revenue)", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa chính xác từ kết quả tính giá của RatingEngine sang bảng revenues với trạng thái 'expected'.",
         "Idempotent: Bảo đảm không sinh doanh thu trùng lặp cho cùng một lần tính giá.",
         "Code: SeedExpectedRevenuesFromRatingCommand.cs; DB: revenues.amount, revenues.source_type, revenues.source_id; Test: Sprint5RevenueTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 5: Rate Card -> Rating -> Cost/Revenue", "rate_cards.supplier_party_id", "costs.vendor_party_id",
         "Nhà cung cấp được thanh toán chi phí", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động từ Bảng giá mua (BUY Rate Card) sang Cost khi seed từ rating.",
         "Chính xác, đảm bảo đúng đối tượng công nợ AP hạ nguồn.",
         "Code: SeedExpectedCostsFromRatingCommand.cs, BuyRatingLegTests.cs; DB: costs.vendor_party_id",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 5: Rate Card -> Rating -> Cost/Revenue", "bills.customer_party_id", "revenues.customer_party_id",
         "Khách hàng thu tiền doanh thu", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động từ Bill sang bản ghi Revenue được seed từ Rating SELL.",
         "Đảm bảo chuẩn xác con nợ AR hạ nguồn.",
         "Code: SeedExpectedRevenuesFromRatingCommand.cs; DB: revenues.customer_party_id; Test: Sprint5RevenueTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 5: Rate Card -> Rating -> Cost/Revenue", "rate_versions.vat_rate", "costs.tax_rate",
         "Thuế suất VAT chi phí", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động thuế suất VAT từ phiên bản bảng giá sang bản ghi Cost.",
         "Tính chính xác thuế VAT đầu vào.",
         "Code: SeedExpectedCostsFromRatingCommand.cs; DB: costs.tax_rate; Test: ChargeProfitVatTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 5: Rate Card -> Rating -> Cost/Revenue", "rate_versions.vat_rate", "revenues.tax_rate",
         "Thuế suất VAT doanh thu", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động thuế suất VAT từ phiên bản bảng giá sang bản ghi Revenue.",
         "Tính chính xác thuế VAT đầu ra.",
         "Code: SeedExpectedRevenuesFromRatingCommand.cs; DB: revenues.tax_rate; Test: ChargeProfitVatTests.cs",
         "Duy trì cơ chế hiện tại.")
    ]
    rows.extend(flow5)

    # -------------------------------------------------------------
    # LUỒNG 6: CHI PHÍ / DOANH THU -> CHỨNG TỪ -> AP / AR -> THANH TOÁN (CẢ 2 NHÁNH)
    # -------------------------------------------------------------
    flow6 = [
        # Nhánh Mua (Costs -> Vendor Invoice -> AP -> Payment)
        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "costs.amount", "financial_document_lines.line_total",
         "Dòng chi tiết hóa đơn mua vào", "Kế thừa một phần (Tùy thuộc Mode tiếp nhận)",
         "Nếu chọn Mode 'lcms_generated' (Giao diện nội bộ xuất): Tự động kế thừa dòng từ chi phí đã tick chọn. Nếu chọn Mode 'external_received' (Hóa đơn nhà cung cấp gửi về): Người dùng phải gõ thủ công dòng chứng từ rồi thực hiện khớp sau.",
         "Khi nhập hóa đơn ngoài, kế toán phải gõ lại số tiền, thuế suất, diễn giải từ giấy/file PDF vào hệ thống.",
         "UI: ReceiveDocumentForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_document_lines.line_total; Test: FinancialDocumentSourceMatchingTests.cs",
         "Bổ sung tính năng OCR / Import XML e-Invoice để tự động tạo dòng chứng từ ngoài mà không cần gõ tay."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "costs.amount", "payable_exposures.amount",
         "Nghĩa vụ nợ phải trả phát sinh từ chi phí đã khớp", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động qua lệnh CreateExposuresFromMatchCommand sau khi khớp dòng chứng từ mua với chi phí.",
         "Không bị trùng lặp, đảm bảo nguyên tắc tách biệt Confirmed/Actual với AP.",
         "Code: CreateExposuresFromMatchCommand.cs; DB: payable_exposures.amount; Test: SprintP08AutoExposureFromMatchTests.cs",
         "Duy trì cơ chế kế thừa tự động hiện có."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "costs.vendor_party_id", "payable_exposures.vendor_party_id",
         "Nhà cung cấp chủ nợ AP", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động từ bản ghi Cost sang Payable Exposure.",
         "Đảm bảo chính xác chủ nợ, không thể gán nhầm đối tác.",
         "Code: CreateExposuresFromMatchCommand.cs; DB: payable_exposures.vendor_party_id; Test: SprintP08AutoExposureFromMatchTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "payable_exposures.amount", "accounts_payable.original_amount",
         "Số dư nợ phải trả chính thức trên sổ cái AP", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa 100% khi người dùng thực hiện thao tác 'Recognize' (Ghi nhận công nợ chính thức).",
         "Bảo đảm tính toàn vẹn số liệu sổ cái AP.",
         "Code: RecognizePayableExposureCommand.cs; DB: accounts_payable.original_amount; Test: Sprint7ExposureApArTests.cs",
         "Duy trì cơ chế ghi nhận chuẩn mực hiện có."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "accounts_payable.remaining_amount", "payment_allocations.amount",
         "Số tiền thanh toán cấn trừ nợ AP", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Hệ thống tự động gợi ý số tiền còn lại của hóa đơn (remaining_amount) vào ô phân bổ thanh toán.",
         "Tiết kiệm thao tác tính toán số dư nợ còn lại.",
         "UI: AllocateCashForm.tsx; API: AllocatePaymentCommand.cs; DB: payment_allocations.amount; Test: Sprint8SettlementTests.cs",
         "Duy trì cơ chế gợi ý hiện tại."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "accounts_payable.vendor_party_id", "payments.party_id",
         "Đối tác thụ hưởng thanh toán", "Nhập lại thủ công (Manual Re-entry)",
         "Khi vào màn hình lập lệnh chi tiền (/settlements/payments/new), người dùng phải tự tìm lại Vendor từ dropdown PartyTypeahead, sau đó mới bấm phân bổ vào các khoản nợ của Vendor đó.",
         "Nguy cơ chọn nhầm Vendor ở lệnh chi so với khoản nợ AP định cấn trừ.",
         "UI: CreateCashTxnForm.tsx, AllocateCashForm.tsx; API: CreatePaymentCommand.cs; DB: payments.party_id; Test: Sprint8SettlementTests.cs",
         "Bổ sung nút thao tác 'Tạo lệnh thanh toán ngay' từ danh sách công nợ AP (Pay from AP), tự động điền Vendor, số tiền nợ còn lại và danh sách hóa đơn cần cấn trừ."),

        # Nhánh Bán (Revenues -> Customer Invoice -> AR -> Collection)
        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "revenues.amount", "financial_document_lines.line_total",
         "Dòng chi tiết hóa đơn bán ra (Customer Invoice / Debit Note)", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Khi lập hóa đơn xuất cho khách qua ReceiveDocumentForm (chế độ lcms_generated), hệ thống tự động sinh dòng chứng từ từ các Revenue được tick chọn.",
         "Tiết kiệm 100% thời gian gõ lại số tiền và nội dung dòng doanh thu.",
         "UI: ReceiveDocumentForm.tsx; API: ReceiveDocumentCommand.cs; DB: financial_document_lines.line_total; Test: Sprint6FinancialDocumentTests.cs",
         "Duy trì cơ chế kế thừa tự động hiện có."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "revenues.amount", "receivable_exposures.amount",
         "Quyền thu tiền phát sinh từ doanh thu đã xuất hóa đơn", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động qua CreateExposuresFromMatchCommand khi khớp hóa đơn bán với dòng Revenue.",
         "Đảm bảo ghi nhận đầy đủ quyền thu nợ từ khách hàng.",
         "Code: CreateExposuresFromMatchCommand.cs; DB: receivable_exposures.amount; Test: Sprint7ExposureApArTests.cs",
         "Duy trì cơ chế tự động hiện tại."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "revenues.customer_party_id", "receivable_exposures.customer_party_id",
         "Khách hàng con nợ AR", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa tự động từ bản ghi Revenue sang Receivable Exposure.",
         "Đảm bảo chính xác con nợ, không thể gán nhầm khách hàng.",
         "Code: CreateExposuresFromMatchCommand.cs; DB: receivable_exposures.customer_party_id; Test: Sprint7ExposureApArTests.cs",
         "Duy trì cơ chế hiện tại."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "receivable_exposures.amount", "accounts_receivable.original_amount",
         "Số dư nợ phải thu chính thức trên sổ cái AR", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Kế thừa 100% khi người dùng thực hiện thao tác 'Recognize' khoản phải thu.",
         "Bảo đảm tính toàn vẹn số liệu sổ cái AR và phục vụ theo dõi tuổi nợ (Aging).",
         "Code: RecognizeReceivableExposureCommand.cs; DB: accounts_receivable.original_amount; Test: Sprint7ExposureApArTests.cs",
         "Duy trì cơ chế ghi nhận chuẩn mực hiện có."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "accounts_receivable.remaining_amount", "collection_allocations.amount",
         "Số tiền thu nợ cấn trừ hóa đơn AR", "Tự điền / Kế thừa tự động (Auto-filled)",
         "Hệ thống tự động gợi ý số tiền còn phải thu của hóa đơn vào ô phân bổ thu tiền.",
         "Hỗ trợ kế toán thu tiền cấn trừ chính xác từng hóa đơn xuất khách.",
         "UI: AllocateCashForm.tsx; API: AllocateCollectionCommand.cs; DB: collection_allocations.amount; Test: ArLedgerRegressionTests.cs",
         "Duy trì cơ chế gợi ý hiện tại."),

        ("Luồng 6: Cost/Rev -> Doc -> AP/AR -> Cash", "accounts_receivable.due_date", "collections.collection_date",
         "Hạn thu tiền đối chiếu với ngày thực thu", "Nhập lại thủ công (Manual Re-entry)",
         "Khi lập phiếu thu tiền (/settlements/collections/new), người dùng phải tự tìm lại Customer và gõ ngày nộp tiền thực tế.",
         "Mất thời gian tìm kiếm lại khách hàng nộp tiền trong danh mục hàng trăm đối tác.",
         "UI: CreateCashTxnForm.tsx; API: CreateCollectionCommand.cs; DB: collections.party_id; Test: Sprint8SettlementTests.cs",
         "Bổ sung nút thao tác 'Thu tiền hóa đơn này' trực tiếp từ màn hình chi tiết công nợ AR (Quick Collect Action), tự động điền Customer và số tiền nợ.")
    ]
    rows.extend(flow6)

    # -------------------------------------------------------------
    # LUỒNG 7: BẢNG GIÁ -> PHỤ PHÍ (SURCHARGE)
    # -------------------------------------------------------------
    flow7 = [
        ("Luồng 7: Rate Card -> Surcharge", "rate_cards.party_type", "surcharge_rules.direction",
         "Chiều kinh tế của phụ phí (BUY/SELL)", "Nhập lại thủ công (Manual Re-entry)",
         "Dù form phụ phí có dropdown chọn 'Bảng giá liên kết' (rateCardId), người dùng vẫn phải tự chọn lại Chiều (buy/sell/both) độc lập.",
         "Nguy cơ khai báo bảng giá mua (BUY) nhưng lại gắn phụ phí chiều bán (SELL), gây xung đột khi tính giá.",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.direction; Test: IndependentSurchargeTests.cs",
         "Khi người dùng chọn rateCardId: Tự động khóa và điền direction tương ứng từ Rate Card (BUY -> buy, SELL -> sell)."),

        ("Luồng 7: Rate Card -> Surcharge", "rate_cards.currency_code", "surcharge_rules.currency_code",
         "Đồng tiền yết giá của phụ phí", "Nhập lại thủ công (Manual Re-entry)",
         "Form Surcharge luôn mặc định VND; không tự động lấy đồng tiền của Rate Card được chọn.",
         "Lệch loại tiền giữa cước chính (USD) và phụ phí gắn kèm (VND) khi không chủ ý, gây lỗi tỷ giá quy đổi.",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.currency_code; Test: IndependentSurchargeTests.cs",
         "Tự động điền currencyCode theo Rate Card được chọn, cho phép sửa nếu phụ phí thu nội tệ (Local Charges)."),

        ("Luồng 7: Rate Card -> Surcharge", "rate_cards.transport_mode", "surcharge_rules.transport_mode",
         "Chế độ vận tải áp dụng phụ phí", "Nhập lại thủ công (Manual Re-entry)",
         "Không tự động điền từ Rate Card được liên kết; người dùng phải chọn lại mode.",
         "Khai báo sai chế độ vận tải khiến phụ phí không bao giờ được kích hoạt khi tính giá cước chính.",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_rules.transport_mode",
         "Kế thừa tự động transport_mode từ Rate Card liên kết."),

        ("Luồng 7: Rate Card -> Surcharge", "rate_versions.effective_from", "surcharge_versions.valid_from",
         "Thời điểm bắt đầu hiệu lực của phụ phí", "Nhập lại thủ công (Manual Re-entry)",
         "Người dùng phải nhập lại từ ngày của phụ phí, không tự động kế thừa kỳ hạn của Rate Version.",
         "Phụ phí có hiệu lực lệch so với bảng giá, dẫn đến tính giá không đồng nhất.",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_versions.valid_from; Test: IndependentSurchargeTests.cs",
         "Gợi ý tự động effective_from của Rate Version làm valid_from cho Surcharge."),

        ("Luồng 7: Rate Card -> Surcharge", "rate_versions.effective_to", "surcharge_versions.valid_to",
         "Thời điểm kết thúc hiệu lực của phụ phí", "Nhập lại thủ công (Manual Re-entry)",
         "Người dùng phải nhập lại đến ngày của phụ phí.",
         "Phụ phí hết hạn trước bảng giá, gây tính thiếu phụ phí trong các chuyến vận chuyển cuối kỳ.",
         "UI: SurchargeForm.tsx; API: SurchargeCommands.cs; DB: surcharge_versions.valid_to; Test: IndependentSurchargeTests.cs",
         "Gợi ý tự động effective_to của Rate Version làm valid_to cho Surcharge.")
    ]
    rows.extend(flow7)

    return rows

if __name__ == "__main__":
    r = get_matrix_rows()
    print(f"Total Duplicate & Inheritance Matrix rows: {len(r)}")
