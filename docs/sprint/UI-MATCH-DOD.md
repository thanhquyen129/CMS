# UI follow-up — Document Match (deferred from U4)

**Status:** Done  
**Date:** 2026-09-12  
**Plan:** `docs/sprint/PLAN-UI.md` · Follow-up from `UI-4-DOD.md` / UAT G2

## Outcome
Operator **khớp chứng từ** trên UI: mở phiên → thêm chi tiết → đảo chi tiết → hủy phiên. Nhận ≠ Chấp nhận ≠ Khớp; khớp chỉ **liên kết** (không tạo Cost/Revenue).

## Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | Document detail CTA → `/documents/{id}/match` khi đã chấp nhận + còn số mở | Done |
| 2 | Start match BFF → phiên `/documents/{id}/matches/{matchId}` | Done |
| 3 | Add detail (`line_to_cost` / `line_to_revenue` / `line_to_line`) | Done |
| 4 | Reverse detail + cancel session (lý do bắt buộc) | Done |
| 5 | Copy CP6.5; empty/loading/error thật; không invent money API | Done |

## APIs used (existing — Pass 2 S6)
- `POST/GET /api/document-matches`, `POST …/details`, `POST …/details/{id}/reverse`, `POST …/cancel`
- Costs/revenues list by `billId` for target pickers
- BFF: `/bff/document-matches/**`

## UI files
- `apps/web/app/documents/[id]/match/page.tsx`
- `apps/web/app/documents/[id]/matches/[matchId]/page.tsx`
- `apps/web/lib/document-matches.ts`, `document-matches-server.ts`
- Components: `StartDocumentMatchForm`, `AddMatchDetailForm`, `ReverseMatchDetailButton`, `CancelDocumentMatchButton`
- Document detail CTA updated

## Non-goals
Auto-match engine, confirm-match workflow UI (API still draft-only add/reverse), list matches-by-document API, add document line UI (UAT G3).

## Follow-ups
- `GET /api/document-matches?primaryDocumentId=` để liệt kê phiên cũ trên chứng từ.
- Confirm match session (nếu bật FULL sau).
- Add document line từ UI (UAT G3).

## Verify
- `npm run build` trong `apps/web`
- VPS: chứng từ đã chấp nhận → **Khớp chứng từ** → thêm chi tiết → triad MatchingStatus đổi
