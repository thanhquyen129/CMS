# 09B — Reviewer chưa ký

Workbook: `docs/po/LCMS_MASTER_FINAL_DEV_HANDOVER_v1.0/MASTER_01-09/LCMS_09B_Final_Requirement_Traceability_DEV_Acceptance_Matrix_v1.2_FINAL.xlsx`, sheet `Final Traceability`.

30/30 dòng: DEV Status = `DONE`, Reviewer Status = `Chờ review`.

DEV không đổi cột Reviewer. `DONE` của DEV không phải nghiệm thu thương mại.

Reviewer giữ «Chờ review» khi còn các cửa này:

| Cửa | Vì sao chưa ký |
|---|---|
| TLS | Hostname và chứng chỉ chưa có. Host đang HTTP. |
| Ảnh UI-01…15 | Chưa có file trong `docs/uat/evidence/`. |
| UX-06 | API đã chặn Bill/Chi phí chéo thuê bao. Chưa có hai phiên đăng nhập trên host. |
| UX-10 | Vòng focus vừa thêm trên nút và ô nhập. Chưa bấm Tab trên host từ Đăng nhập → Bill → Chi phí → Chốt. |
| Idempotency | Xác nhận và thực tế đã lưu key. Gửi lại cùng key không đổi số. |

Khi reviewer ký, họ sửa cột Reviewer trên workbook. Không điền hộ.
