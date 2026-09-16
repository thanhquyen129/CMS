# CMS — Checklist go-live tài chính (tuần tự)

Ngày lập: 2026-09-16 · Cập nhật triển khai: 2026-09-16  
Mục tiêu: người làm nghề **xong việc** quanh Bill → chốt.  
Nguồn UAT spine: `docs/sprint/UAT-GO-LIVE-FINANCIAL-RESULT.md` (2026-09-13 PASS).

---

## A. UAT luồng tiền

### A1. Chuẩn bị phiên UAT
- [x] Tài khoản ops Admin bootstrap (`ops@cms.local`) — phiên UAT 2026-09-13
- [x] Tenant `ops` có Party + VND
- [x] Prod `http://194.233.89.26` · health/ready OK

### A2–A11. Spine Bill → Close
- [x] A2 Bill (S1)
- [x] A3 Rate → Expected (S8)
- [x] A4 Cost Direct + Shared allocate (S1/S2/S3)
- [x] A5 Revenue + LN (S1)
- [x] A6 Chứng từ triad + match (S1/S4)
- [x] A7 AP/AR recognize / reverse / write-off (S1/S5/S6)
- [x] A8 Payment / Collection finalize (S1)
- [x] A9 Bank feed + recon (S11) · **bổ sung 2026-09-16:** picker gợi ý GUID + variance accept/clear/write-off
- [x] A10 Financial close + period lock (S1/S7)
- [x] A11 Control queues + reports/dashboard đọc được
- [x] A12 Triage: P0 không còn trên spine; gap minor ghi UAT result

**Gap còn (không P0):**
- S9 chưa chứng minh Cost-only ≠ Revenue-only user (cần 2 user thật)
- Rate-card **components** UI / seed expected revenue (P1 follow-up)
- Global Search vẫn Bill-centric (API `/api/search/operational`)

---

## B. Fix blocker + polish UI

### B1. Fix P0/P1 từ vòng A
- [x] Variance lifecycle API+UI (accept / clear / write-off) — hết dead filter
- [x] Recon detail: datalist gợi ý bank_line / payment / collection
- [ ] Rate-card pricing components UI + seed revenue Expected (còn)
- [ ] UAT tách quyền Cost ≠ Revenue (2 user)

### B2. Pixel UI-09…14
- [x] Hub/nav + KPI đã ship (baseline trước)
- [ ] Screenshot UAT từng màn so mockup PO (còn — không chặn vận hành)

### B3. Search & điều hướng
- [x] UI-15 Workflow: thêm Sao kê + Đối soát (bước 09–10)
- [ ] Global Search đa entity (API hiện chỉ Bill/Order ref)

---

## C. Nợ kỹ thuật list

- [x] Documents: server `page`/`pageSize` + `PagedResult`
- [x] Rate cards: server paging + filter `q` / `partyType` / `isActive`
- [x] Costs: filter `vendorPartyId` + `fromDate`/`toDate`; list pages wire server page
- [x] Bills / costs / revenues: web truyền page lên API (không slice full array khi không cần)
- [ ] AP/AR tách route vật lý — chỉ khi PO yêu cầu (giữ `?tab=`)

---

## D. Ngoài scope — ADR/PO

- [ ] Order / Shipment CRUD trong CMS (H-002)
- [ ] TMS surfaces

---

## E. Definition of Done go-live

- [x] Spine A2→A10 đã PASS trên prod (UAT 2026-09-13)
- [x] Không P0 mở trên spine
- [x] Close immutability verified (S7)
- [ ] Cost≠Revenue quyền tách — chờ UAT 2 user
- [x] Checklist + handoff cập nhật

---

## Việc còn lại (thứ tự)

1. UAT 2 user Cost-only / Revenue-only (S9)
2. Pricing-rule components UI + seed revenue Expected
3. Screenshot pixel UI-09…14
4. Global Search đa entity (mở rộng API)
