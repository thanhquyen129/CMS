# SQL Inventory, Cleanup & Reconciliation (MG-01 → MG-16)

SHA `f5cf209be7088f7acd19bc1b63b1be7bdd21cbfd` · Production cleanup: **không chạy** · MG-13/MG-15 = **NOT RUN** (PO UAT)

## Kết quả chạy thật

| Mã | Trạng thái | Evidence | Số dòng tác động |
|----|------------|----------|------------------|
| MG-01 | COMPLETED | MG01_backup_manifest.json (sha256=140fc9fd0b65dc97...) | 1 DB snapshot (5.394.432 bytes) |
| MG-02 | COMPLETED | run_mg_inventory.py + MG_inventory_result.json | 16 ratings; 17 rating_details |
| MG-03 | COMPLETED | run_mg_inventory.py + MG_inventory_result.json | 130 active costs; 127 active revenues |
| MG-04 | COMPLETED | Bảo toàn toàn vẹn FK, không xóa chứng từ/công nợ | 248 docs; 124 AP; 123 AR (0 deleted) |
| MG-05 | COMPLETED | EconomicIdentity 9-dimension key + Ambiguity guard | 257 dòng Cost/Revenue được gán khóa |
| MG-06 | COMPLETED | MG_inventory_result.json dry-run duplicate check | 1 nhóm trùng Expected-only; 1 nhóm Actual+Expected trên Bill mẫu |
| MG-07 | COMPLETED | Chỉ chạy trên local/staging test tenant (lcms_dev.db) | 1 test tenant (ops) |
| MG-08 | COMPLETED | Migration 20261103120000_EconomicIdentityRatingDedup | 3 cột mới + index trên bảng costs & revenues |
| MG-09 | COMPLETED | Supersede Expected trùng trên test tenant, giữ nguyên snapshot | 1 dòng Expected DONGGOI lặp chuyển sang superseded |
| MG-10 | COMPLETED | seed_lsstest_bill.py tái tạo Bill LSSTEST02112026002 & TESTLSS002 | 6 dòng trên LSSTEST02112026002 (4 Cost + 2 Revenue) |
| MG-11 | COMPLETED | verify_lsstest_effective.py + LSSTEST_effective_verify.json | Effective Cost=1.740.000; Revenue=2.440.000; Profit=700.000 |
| MG-12 | COMPLETED | dotnet test 66/66 PASS + Surcharge P0 regression PASS | 130 active costs; 1 superseded; 0 duplicate active |
| MG-13 | NOT RUN | Chờ PO nghiệm thu Gate G2 (DEV QA P0 đã PASS 44/44 TC) | 0 (Bước PO UAT) |
| MG-14 | COMPLETED | BillRatingPanel.tsx + ApplyRatingFinancialsButton.tsx + BillRatingFinancialWorkspace.tsx | 6 khu vực UI A→F hoàn tất |
| MG-15 | NOT RUN | Chờ PO nghiệm thu Gate G3 (DEV QA P1 đã PASS 11/11 TC) | 0 (Bước PO UAT) |
| MG-16 | READY FOR PO UAT | Hồ sơ bàn giao + Rollback plan sẵn sàng; G4 Production = NOT RUN | 0 Production changes |

## SELECT đối soát LSSTEST02112026002

Effective Cost **1.740.000** · Revenue **2.440.000** · Profit **700.000** (`sql/LSSTEST_effective_verify.json`).

## Rollback

1. Restore MG-01 backup · 2. Revert commit · 3. Không cleanup Production
