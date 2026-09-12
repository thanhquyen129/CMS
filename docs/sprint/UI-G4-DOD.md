# UAT G4 — Lọc chứng từ theo Bill (`billId`)

**Status:** Done (FULL)  
**Date:** 2026-09-12  
**Source:** `docs/sprint/UAT-VPS-ONE-ROUND.md` gap G4

## Outcome
Operator xem chứng từ **theo Bill** từ Bill detail và `/documents?billId=` — API lọc header hoặc dòng gắn Bill.

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `GET /api/financial-documents?billId=` | Done |
| 2 | Khớp header `BillId` **hoặc** dòng có `BillId` | Done |
| 3 | List DTO trả `billId` | Done |
| 4 | `/documents?billId=` + chip Bill + giữ filter khi quick filter | Done |
| 5 | Bill panel: bảng chứng từ Bill + CTA «Danh sách theo Bill» | Done |
| 6 | Test `DocumentBillIdFilterTests` | Done |

## Non-goals
G5 AP/AR settled tab · G6 settlement `billNo` · sửa header Bill từ list

## Verify
- `dotnet test --filter DocumentBillIdFilter`
- `npm run build` apps/web
- VPS: Bill detail → chứng từ Bill; `/documents?billId=` chỉ doc liên quan
