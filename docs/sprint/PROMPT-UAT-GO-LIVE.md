# PROMPT — UAT go-live tài chính đủ (người nghiệp vụ / VPS)

Dán vào **New Chat** khi điều phối hoặc hỗ trợ phiên UAT. **Không** mở lại checklist P01–P25.

## Mục tiêu

Chạy / hỗ trợ **UAT go-live tài chính đủ** trên VPS với người nghiệp vụ theo `docs/sprint/UAT-GO-LIVE-FINANCIAL.md`. Outcome: verdict PASS/FAIL + RESULT điền đủ; gap nghiệp vụ (không mã Pxx).

## Host

- UI: `http://194.233.89.26`
- Preflight: `/health` `/ready` = 200
- Login: tài khoản bootstrap trên host — **không** commit password

## Việc agent được làm

1. Preflight health/ready + smoke trang login.
2. Hỗ trợ facilitator: làm rõ bước S1–S14, map màn UI, ghi RESULT.
3. Nếu PO nhờ **chạy hộ** vòng S1 (UI-only browser): làm như `UAT-VPS-ONE-ROUND` — ghi billNo + số P&L vào RESULT; không Postman.
4. Gap blocker/major → sửa mỏng nhất + ship (handoff/commit/push) **hoặc** ghi Architecture Deviation nếu đụng H-001…H-012.
5. Cập nhật `docs/handoff.md` sau phiên.

## Không làm

- Mở lại / thêm item P-series.
- OIDC IdP / OTLP / soak CI / broker ngoài process trừ khi có ADR mới.
- Deploy `alogex`. Force-push. Ghi secret vào doc.

## DoD phiên

- [ ] `UAT-GO-LIVE-FINANCIAL-RESULT.md` có verdict + S1 đủ bước
- [ ] Gap list (nếu có) severity rõ
- [ ] Handoff ngày phiên; commit nếu có sửa code/doc kết quả
