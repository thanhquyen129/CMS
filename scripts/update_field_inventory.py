import re

with open('scripts/generate_field_inventory_data.py', 'r', encoding='utf-8') as f:
    text = f.read()

# Let's add the missing fields:
# 1. bills.customer_ref in bill_fields
# 2. ratings.origin_code, ratings.destination_code, ratings.transport_mode, ratings.service_type_code, ratings.commodity_code in rt_fields
# 3. rating_details.amount in pr_fields or rt_fields
# 4. accounts_payable.vendor_party_id in ap_fields

bill_addition = '''        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",
         "Reference khách hàng (Customer Ref)", "customerRef", "bills.customer_ref", "String(64)", "Optional",
         "Nhập tự do", "Không", "Khách hàng",
         "In trên chứng từ đối soát cho khách", "Có (khi Draft)", "orders.customer_ref",
         "UI: CreateBillWorkspaceForm.tsx; API: UpdateBillContextCommand.cs; DB: bills.customer_ref; Test: Ui02OperationalCreateContextTests.cs"),
'''

rating_addition = '''        ("Bảng giá & Tính giá", rt_screen, "Thông số tính toán (Cho phép Override)",
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
'''

ap_addition = '''        ("Công nợ & Dòng tiền", ap_screen, "Sổ cái AP",
         "Nhà cung cấp chủ nợ AP *", "vendorPartyId", "accounts_payable.vendor_party_id", "Guid", "Required",
         "Dropdown / Kế thừa từ Exposure", "business_parties.id", "Sổ cái AP",
         "Định danh chủ thể được trả tiền", "Khóa sau khi tạo", "accounts_payable.vendor_party_id",
         "UI: ApArLedgerPanel.tsx; API: RecognizePayableExposureCommand.cs; DB: accounts_payable.vendor_party_id; Test: Sprint7ExposureApArTests.cs"),
'''

# Insert bill_addition before bill_fields closing
text = text.replace('        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",\n         "Ngày áp dụng giá (Rate Date)"', bill_addition + '        ("Vận hành (Operations)", bills_screen, "Thương mại & Đối tác",\n         "Ngày áp dụng giá (Rate Date)"')

# Insert rating_addition before rt_fields closing
text = text.replace('        ("Bảng giá & Tính giá", rt_screen, "Tự động sinh Chi phí / Doanh thu",\n         "Tự động sinh Chi phí dự kiến"', rating_addition + '        ("Bảng giá & Tính giá", rt_screen, "Tự động sinh Chi phí / Doanh thu",\n         "Tự động sinh Chi phí dự kiến"')

# Insert ap_addition in ap_fields
text = text.replace('        ("Công nợ & Dòng tiền", ap_screen, "Sổ cái AP",\n         "Số tiền nợ gốc ban đầu"', ap_addition + '        ("Công nợ & Dòng tiền", ap_screen, "Sổ cái AP",\n         "Số tiền nợ gốc ban đầu"')

with open('scripts/generate_field_inventory_data.py', 'w', encoding='utf-8') as f:
    f.write(text)

print("Updated generate_field_inventory_data.py with missing cross-reference fields.")

