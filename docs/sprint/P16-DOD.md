# P16 — Recognition policy gate DoD

**Ngày:** 2026-09-13 · E07

## Done
1. Tenant `recognitionPolicyMode`: `manual` | `require_document_link`.
2. Recognize AP/AR: khi `require_document_link` → bắt buộc `FinancialDocumentId` trên exposure (409 VI).
3. Cấu hình qua `/settings` → `TenantFinancialSettingsForm`.

## Verify
- `TenantSettings_RecognitionPolicy_BlocksRecognizeWithoutDoc` passed.

## Non-goals
- Rule engine phức tạp / auto-recognize batch.
