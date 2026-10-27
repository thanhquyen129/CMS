import openpyxl

excel_paths = [
    "docs/po/27102026-LCMS_UAT_Correction_DEV_Package_v1.0_20261027/LCMS_DEV_UAT_Correction_Matrix_v1.0_20261027.xlsx",
    "Docs/PO/27102026-LCMS_UAT_Correction_DEV_Package_v1.0_20261027/LCMS_DEV_UAT_Correction_Matrix_v1.0_20261027.xlsx"
]

for p in set(excel_paths):
    wb = openpyxl.load_workbook(p)

    # 1. Update 03_TruongDuLieu
    ws_fields = wb["03_TruongDuLieu"]
    # Row 5 is transport_mode
    ws_fields.cell(row=5, column=8).value = (
        "Master mode active hiện chỉ có 'air' (Đường hàng không). "
        "Không silent seed các mode chưa có trong danh mục (sea, road, rail). "
        "Dropdown đồng bộ tuyệt đối từ danh mục quản trị master catalog qua GetOperationalReferenceEditQuery."
    )
    # Row 10 is cost -> rating_detail FK
    ws_fields.cell(row=10, column=8).value = (
        "Bảng costs.source_type ('rating_detail') & costs.source_id liên kết trực tiếp rating_details.id. "
        "Quan hệ định danh 1-1 chính xác theo BR27-13 và FU27-TC-023, tuyệt đối không suy diễn qua charge_code hay RatingId chung."
    )

    # 2. Update 05_XacMinh
    ws_verify = wb["05_XacMinh"]
    # Row 5 is TV-01
    ws_verify.cell(row=5, column=6).value = (
        "PASS - Đã truy vấn DB & API: Danh mục master_catalog_items hiện chỉ active duy nhất mã 'air' (Đường hàng không). "
        "Tuyệt đối không tự động seed thêm mode ngoài quy trình quản trị. Form và dropdown đồng bộ tuyệt đối với danh mục active này."
    )
    # Row 6 is TV-02
    ws_verify.cell(row=6, column=6).value = (
        "PASS - Đã đối chiếu toàn diện: Không còn các option hardcode (sea, road, rail) trên form Sửa Bill. "
        "Dropdown Phương thức vận chuyển chỉ hiển thị đúng các mode đang active từ danh mục ('air')."
    )
    # Row 10 is TV-06
    ws_verify.cell(row=10, column=6).value = (
        "PASS - Đã xác minh trực tiếp trong CSDL và mã nguồn CostQueries.cs: Khoản mục chi phí (Cost) liên kết trực tiếp 1-1 với "
        "dòng chi tiết tính giá (Rating Detail) thông qua quan hệ định danh (cost.source_type == 'rating_detail' và "
        "cost.source_id == rating_detail.id), tuân thủ nghiêm ngặt BR27-13, không suy diễn qua charge_code hay RatingId chung. "
        "Đã xóa điều kiện sai '|| detail.SourceVersionId is not null' vốn khiến dòng Base Rate bị biến thành Phụ phí."
    )

    # Verify 04_UAT has PO UAT = NOT RUN
    ws_uat = wb["04_UAT"]
    for i in range(5, 42): # rows 5 to 41 are 37 test cases
        assert ws_uat.cell(row=i, column=10).value == "NOT RUN", f"Row {i} PO UAT is not NOT RUN"
        assert ws_uat.cell(row=i, column=9).value == "PASS", f"Row {i} DEV Test is not PASS"

    wb.save(p)
    print(f"Updated Excel matrix successfully: {p}")
