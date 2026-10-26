LCMS — UAT FOLLOW-UP / BA DELTA v1.0
Ma goi: LCMS-UAT-FU-20261026-01
Ngay: 26/10/2026

GOI GOM
1. LCMS_BA_UAT_Followup_Change_v1.0_20261026.docx
   Dac ta AS-IS -> TO-BE, guardrails, acceptance, phan loai confirmed vs verify-before-fix.
2. LCMS_DEV_UAT_Followup_Matrix_v1.0_20261026.xlsx
   8 change items, 21 field mappings, 22 business rules, 32 UAT cases,
   8 screenshot evidence records, regression exclusions, 12-item DEV checklist.
3. evidence/E01.png ... E08.png
   Screenshots PO chup tren LCMS, ngay 26/10/2026.

YEU CAU DEV
- Fix cac UI GAP co bang chung trong pham vi bao giao.
- Cac muc VERIFY_THEN_FIX phai doc code/API/DB va giai trinh truoc; chi fix neu co GAP.
- Khong rollback/tu dong thay doi Rate Card v1/v2 da Published, Rating lich su,
  Expected Costs da seed, mapping/chinh sach Source-of-Truth.
- UAT may tu dong PASS khong thay the PO UAT. Excel UAT trang thai NOT RUN.
- No production deployment without PO approval.

DA DAT TU TRUOC / KHONG LAM LAI
- TDNT_SGN_HAN: tao va publish v2 (PO da UAT).
- BUY Bill 120kg: Freight 1,440,000 + Packing surcharge 300,000 = 1,740,000 VND.
- Seed da tao 2 Expected Costs va thong bao khong tao trung.
- 8 issues Operational/Leg/Allocation trong package 23/10 duoc quan ly rieng.

DOC Word va Excel la hai tai lieu phai doc cung nhau.
