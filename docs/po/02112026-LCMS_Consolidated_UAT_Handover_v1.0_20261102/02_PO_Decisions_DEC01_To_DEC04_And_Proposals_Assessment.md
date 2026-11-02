# DEC-01 … DEC-04 và đánh giá PROP-01 / PROP-02 (Audit remaster)

## DEC-01 — Revenue maturity 2 hoặc 3 lớp theo tenant

Kiến trúc: một dòng `revenues` với `ExpectedAmount` / `ConfirmedAmount` / `ActualAmount`. Không thêm bảng Confirmed giả.

### JSON schema (`tenant_settings.financial_json`)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "properties": {
    "revenueMaturityModel": { "enum": ["two", "three"], "default": "three" },
    "revenueMaturityPolicyVersion": { "type": "string" },
    "revenueMaturityEffectiveFrom": { "type": "string", "format": "date" }
  },
  "additionalProperties": true
}
```

### Payload API ví dụ

**Cài đặt (PATCH tenant financial):**
```json
{
  "revenueMaturityModel": "two",
  "revenueMaturityPolicyVersion": "2026-11-02",
  "revenueMaturityEffectiveFrom": "2026-11-02"
}
```

**Case âm — confirm khi `two`:** `POST /api/revenues/{id}/confirm` → `409 Conflict` (không ghi ConfirmedAmount).

**Case dương — actualize `Expected → Actual` khi `two`:** `POST /api/revenues/{id}/actualize` `{ "actualAmount": 500000 }` → `204`.

Mặc định **không** chuyển mọi tenant sang 2 lớp.

## DEC-02 — Việt hóa Exposure / AP / AR

Từ điển `VietnameseUiTerms` + nhãn cứng.

| Mã | Nhãn |
|---|---|
| Exposure | Công nợ dự kiến |
| Receivable Exposure | Quyền thu dự kiến |
| Payable Exposure | Nghĩa vụ phải trả |
| Due Date | Ngày đến hạn thanh toán |
| Aging | Tuổi nợ |
| AP | Công nợ phải trả đã ghi nhận |
| AR | Công nợ phải thu đã ghi nhận |

### File UI đã Việt hóa (đầy đủ bề mặt DEC-02)

- `apps/web/lib/terminology.ts` / `VietnameseUiTerms`
- `apps/web/components/AppShell.tsx` (menu)
- `apps/web/components/CreateExposureForm.tsx`
- `apps/web/app/settlements/page.tsx` + `SettlementListWorkspace.tsx`
- `apps/web/app/bills/[id]/page.tsx` (khối công nợ)
- `apps/web/app/exposures/**` (nếu có)
- Dashboard / aging widgets dùng `term()`

API domain `ReceivableExposure` / `AccountsReceivable` giữ nguyên tiếng Anh.

## DEC-03 — Tách P&L và Công nợ trên Hồ sơ tài chính Bill

### Cấu trúc 2 bảng

1. **Doanh thu & chi phí (P&L)** — Expected/Confirmed/Actual; `finalizedAllocations` từ `cost_allocation_details` Finalized; lợi nhuận = DT − CP.
2. **Công nợ đã ghi nhận (AP/AR)** — Exposure → Recognize; không cộng vào lợi nhuận.

Tiêu đề khối FX: **Chi tiết quy đổi tiền tệ**. Nguồn `identity` → **Không quy đổi (cùng tiền tệ)**.

Bill UAT: DT 500.000 + phân bổ CP 120.000 → profit **380.000** ở cả hồ sơ tài chính và 3 nhóm. Shared gốc 240.000 không bị sửa.

## DEC-04 — Lịch sử phê duyệt

### Payload `GET /api/queues/approvals?status=all`

```http
GET /api/queues/approvals?status=all HTTP/1.1
X-Tenant-Id: <tenant>
```

```json
{
  "items": [
    {
      "id": "<uuid>",
      "objectType": "cost_allocation",
      "objectId": "<uuid>",
      "status": "approved",
      "requestedBy": "<user-uuid>",
      "decidedAt": "2026-10-28T10:00:00Z",
      "decisionReason": "Đủ chứng từ",
      "link": "/costs/allocations/<uuid>"
    }
  ]
}
```

`status` ∈ `pending` (default) | `approved` | `rejected` | `all`.

UI tabs: Chờ duyệt / Đã duyệt / Từ chối / Tất cả. Maker (`requestedBy` = current user) → nút Phê duyệt disable + tooltip PC-21; backend vẫn chặn tự duyệt.

## PROP-01 — Module công nợ theo tenant (chỉ đánh giá)

| Module / API | Ảnh hưởng nếu `debtModule=off` | Permission | Báo cáo |
|---|---|---|---|
| `/api/receivable-exposures*` | 403 ghi; GET lịch sử vẫn đọc | `ar.write` off | Aging AR ẩn nav |
| `/api/payable-exposures*` | 403 ghi | `ap.write` off | Aging AP ẩn |
| `/api/payments*` `/api/collections*` | 403 | settlement write | Cash report thu hẹp |
| Bill financial profile | Ẩn bảng công nợ; giữ P&L | — | — |
| Close eligibility | Bỏ gate AR/AP mở nếu module off | close.* | — |
| Mobile / BFF | Đồng bộ flag | — | — |

Chưa triển khai. Không xóa dữ liệu lịch sử.

## PROP-02 — Bắt buộc commodity / revenue type (chỉ đánh giá)

| Bề mặt | Hiện trạng | Nếu bắt buộc toàn tenant |
|---|---|---|
| Bill commodity | Cảnh báo khi master trống; không chặn lưu | Cần seed master + migration dữ liệu thiếu |
| CreateRevenue type | Dropdown + "Chưa phân loại" | Chặn empty code → ảnh hưởng báo cáo taxonomy |
| Rating context | Không suy từ tên khoản | — |

Không ép validation mọi tenant trong đợt này.
