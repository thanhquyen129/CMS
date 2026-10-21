# -*- coding: utf-8 -*-
"""
Script tạo file Excel chuẩn nghiệp vụ LCMS: LCMS_ASIS_Field_Inventory_And_Inheritance_Matrix.xlsx
Gồm 2 sheets:
Sheet 1: Full Field Inventory
Sheet 2: Duplicate & Inheritance Matrix
"""

import os
import sys
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

from generate_field_inventory_data import get_field_inventory_rows
from generate_matrix_data import get_matrix_rows

sys.stdout.reconfigure(encoding='utf-8')

def build_excel(output_paths):
    wb = openpyxl.Workbook()
    
    # -------------------------------------------------------------
    # STYLE DEFINITIONS
    # -------------------------------------------------------------
    font_title = Font(name="Segoe UI", size=15, bold=True, color="1F497D")
    font_subtitle = Font(name="Segoe UI", size=10, italic=True, color="595959")
    font_header = Font(name="Segoe UI", size=10, bold=True, color="FFFFFF")
    font_data = Font(name="Segoe UI", size=9)
    font_data_bold = Font(name="Segoe UI", size=9, bold=True)
    
    fill_header_navy = PatternFill(start_color="1F497D", end_color="1F497D", fill_type="solid")
    fill_header_teal = PatternFill(start_color="17696A", end_color="17696A", fill_type="solid")
    fill_zebra = PatternFill(start_color="F9FAFB", end_color="F9FAFB", fill_type="solid")
    
    # Status fills for Matrix
    fill_manual = PatternFill(start_color="FDE8E8", end_color="FDE8E8", fill_type="solid") # Red tint
    font_manual = Font(name="Segoe UI", size=9, bold=True, color="9B1C1C")
    
    fill_auto = PatternFill(start_color="DEF7EC", end_color="DEF7EC", fill_type="solid") # Green tint
    font_auto = Font(name="Segoe UI", size=9, bold=True, color="03543F")
    
    fill_partial = PatternFill(start_color="FEF08A", end_color="FEF08A", fill_type="solid") # Yellow tint
    font_partial = Font(name="Segoe UI", size=9, bold=True, color="713F12")

    thin_border = Border(
        left=Side(style="thin", color="D1D5DB"),
        right=Side(style="thin", color="D1D5DB"),
        top=Side(style="thin", color="D1D5DB"),
        bottom=Side(style="thin", color="D1D5DB")
    )
    
    # -------------------------------------------------------------
    # SHEET 1: FULL FIELD INVENTORY
    # -------------------------------------------------------------
    ws1 = wb.active
    ws1.title = "Full Field Inventory"
    ws1.views.sheetView[0].showGridLines = True
    
    # Title Block
    ws1['A1'] = "LCMS — BẢNG KIỂM KÊ TOÀN BỘ TRƯỜNG NHẬP LIỆU HỆ THỐNG (AS-IS AUDIT)"
    ws1['A1'].font = font_title
    ws1['A2'] = "Căn cứ rà soát: Phiên bản code Frontend Next.js, Minimal APIs ASP.NET Core, EF Core DbContext Snapshot & Database PostgreSQL"
    ws1['A2'].font = font_subtitle
    
    headers1 = [
        "STT",
        "Phân hệ / Module",
        "Màn hình / URL Route",
        "Nhóm thông tin (Section)",
        "Tên trường trên UI (Tiếng Việt)",
        "Tên API Field (DTO)",
        "Bảng & Cột Database (table.column)",
        "Kiểu dữ liệu",
        "Ràng buộc (Constraint)",
        "Nguồn dữ liệu hiện tại",
        "Danh mục tham chiếu (Master Data)",
        "Đối tượng sở hữu (Source of Truth)",
        "Tác động nghiệp vụ hạ nguồn",
        "Khả năng chỉnh sửa sau khi lưu",
        "Liên kết ngang (Cross-Module Links)",
        "Căn cứ mã nguồn & Kiểm thử (Code Evidence)"
    ]
    
    row_idx = 4
    for col_idx, h in enumerate(headers1, 1):
        cell = ws1.cell(row=row_idx, column=col_idx, value=h)
        cell.font = font_header
        cell.fill = fill_header_navy
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        cell.border = thin_border
    ws1.row_dimensions[row_idx].height = 30
    
    inventory_data = get_field_inventory_rows()
    for i, item in enumerate(inventory_data, 1):
        row_idx += 1
        ws1.row_dimensions[row_idx].height = 22
        
        # item: 0:Module, 1:Screen, 2:Section, 3:UI_Name, 4:API_Field, 5:DB_Col, 6:DataType, 7:Constraint,
        #       8:SourceType, 9:MasterData, 10:SoT, 11:Downstream, 12:Editability, 13:CrossLink, 14:Evidence
        vals = [i, item[0], item[1], item[2], item[3], item[4], item[5], item[6], item[7],
                item[8], item[9], item[10], item[11], item[12], item[13], item[14]]
        
        for col_idx, val in enumerate(vals, 1):
            cell = ws1.cell(row=row_idx, column=col_idx, value=val)
            cell.font = font_data
            cell.border = thin_border
            if row_idx % 2 == 1:
                cell.fill = fill_zebra
            if col_idx in (1, 8, 9, 10):
                cell.alignment = Alignment(horizontal="center", vertical="center")
            elif col_idx in (15, 16):
                cell.alignment = Alignment(horizontal="left", vertical="center", wrap_text=True)
            else:
                cell.alignment = Alignment(horizontal="left", vertical="center")

    # Auto-adjust column widths for Sheet 1
    for col in ws1.columns:
        col_letter = get_column_letter(col[0].column)
        if col[0].column == 1:
            ws1.column_dimensions[col_letter].width = 6
        elif col[0].column in (2, 3, 4):
            ws1.column_dimensions[col_letter].width = 24
        elif col[0].column in (5, 6, 7):
            ws1.column_dimensions[col_letter].width = 22
        elif col[0].column in (8, 9, 10):
            ws1.column_dimensions[col_letter].width = 18
        elif col[0].column in (11, 12):
            ws1.column_dimensions[col_letter].width = 24
        elif col[0].column in (13, 14, 15):
            ws1.column_dimensions[col_letter].width = 26
        elif col[0].column == 16:
            ws1.column_dimensions[col_letter].width = 45
            
    ws1.freeze_panes = "A5"

    # -------------------------------------------------------------
    # SHEET 2: DUPLICATE & INHERITANCE MATRIX
    # -------------------------------------------------------------
    ws2 = wb.create_sheet(title="Duplicate & Inheritance Matrix")
    ws2.views.sheetView[0].showGridLines = True
    
    # Title Block
    ws2['A1'] = "LCMS — MA TRẬN ĐỐI CHIẾU TRÙNG LẶP & KHẢ NĂNG KẾ THỪA DỮ LIỆU (7 LUỒNG NGHIỆP VỤ)"
    ws2['A1'].font = font_title
    ws2['A2'] = "Đánh giá mức độ tự động hóa nhập liệu hiện trạng (AS-IS) và xác định các điểm nghẽn nhập liệu thủ công"
    ws2['A2'].font = font_subtitle
    
    headers2 = [
        "STT",
        "Chuỗi luồng nghiệp vụ (Flow)",
        "Trường dữ liệu nguồn (Source Field)",
        "Trường dữ liệu đích (Target Field)",
        "Bản chất dữ liệu / Ý nghĩa kinh tế",
        "Trạng thái AS-IS hiện tại",
        "Mức độ trùng lặp & Thao tác người dùng hiện tại",
        "Rủi ro sai lệch / Điểm nghẽn nhập liệu",
        "Căn cứ mã nguồn & Kiểm thử (Evidence)",
        "Đề xuất tối ưu hóa cho BA & DEV"
    ]
    
    row_idx2 = 4
    for col_idx, h in enumerate(headers2, 1):
        cell = ws2.cell(row=row_idx2, column=col_idx, value=h)
        cell.font = font_header
        cell.fill = fill_header_teal
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        cell.border = thin_border
    ws2.row_dimensions[row_idx2].height = 30
    
    matrix_data = get_matrix_rows()
    for i, item in enumerate(matrix_data, 1):
        row_idx2 += 1
        ws2.row_dimensions[row_idx2].height = 36
        
        # item: 0:Flow, 1:Source, 2:Target, 3:Meaning, 4:Status, 5:Duplication, 6:Risk, 7:Evidence, 8:Proposal
        vals = [i, item[0], item[1], item[2], item[3], item[4], item[5], item[6], item[7], item[8]]
        
        for col_idx, val in enumerate(vals, 1):
            cell = ws2.cell(row=row_idx2, column=col_idx, value=val)
            cell.font = font_data
            cell.border = thin_border
            
            # Format status cell
            if col_idx == 6:
                status_str = str(val)
                if "Nhập lại thủ công" in status_str:
                    cell.fill = fill_manual
                    cell.font = font_manual
                elif "Tự điền" in status_str:
                    cell.fill = fill_auto
                    cell.font = font_auto
                else:
                    cell.fill = fill_partial
                    cell.font = font_partial
                cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
            elif col_idx == 1:
                cell.alignment = Alignment(horizontal="center", vertical="center")
            elif col_idx in (2, 3, 4):
                cell.alignment = Alignment(horizontal="left", vertical="center", wrap_text=True)
            else:
                cell.alignment = Alignment(horizontal="left", vertical="center", wrap_text=True)
                if row_idx2 % 2 == 1 and col_idx != 6:
                    cell.fill = fill_zebra

    # Column widths for Sheet 2
    widths2 = [6, 22, 22, 22, 24, 20, 32, 30, 36, 36]
    for idx, w in enumerate(widths2, 1):
        col_letter = get_column_letter(idx)
        ws2.column_dimensions[col_letter].width = w
        
    ws2.freeze_panes = "A5"

    # Save to all requested paths
    for path in output_paths:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        wb.save(path)
        print(f"Saved Excel file to: {path}")

if __name__ == "__main__":
    paths = [
        "docs/po/LCMS_ASIS_Field_Inventory_And_Inheritance_Matrix.xlsx",
        "docs/po/20102026/LCMS_ASIS_Field_Inventory_And_Inheritance_Matrix.xlsx"
    ]
    build_excel(paths)

