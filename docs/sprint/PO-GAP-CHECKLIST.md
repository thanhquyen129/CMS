# Checklist còn thiếu vs PO (sau Pass 1/2 + UI + UAT PASS)

**Cập nhật:** 2026-09-12 (P01–P07 Done)  
**Đã xong:** Pass 1/2 · UI U0–U5 · Match · G1–G6 · seed · write-off/reverse · queue decide · recon/bank-feed · UAT UI-only · **P01–P07**.

**Cách dùng:** làm theo thứ tự; một item = một chat/agent; tick Done + DoD ngắn khi ship.

---

## Wave tiếp theo (làm ngay) — P08→P12

| # | ID | Việc | Lát cắt MVP |
|---|-----|------|-------------|
| 1 | **P08** | Auto Exposure từ chứng từ đã khớp | Sau match → đề xuất/tạo exposure (không invent Cost/Revenue) |
| 2 | **P09** | Confirm match session + auto-suggest | Confirm phiên; tolerance suggest (vẫn review tay) |
| 3 | **P10** | Reverse recognize AP/AR + sổ điều chỉnh | Reverse recognition + history (không silent overwrite) |
| 4 | **P11** | Inbox Variance (+ escalate thủ công) | List/filter variance; CTA mở Exception (giữ tách lớp) |
| 5 | **P12** | Ma trận approver / wizard multi-step | Config theo object/amount + wizard |

**Next kickoff:** **P08**.

---

## Go-live tài chính (đầy đủ)

| ID | Tên | PO / ADR | Hiện trạng | Lát cắt MVP | Phụ thuộc |
|----|-----|----------|------------|-------------|-----------|
| **P01** | Phân bổ Shared cost → Bill (UI) | TD6 E05/E06; C-005/C-006 | **Done** (`P01-DOD.md`) | Basis equal/qty/manual → draft → finalize | — |
| **P02** | Adjust Cost/Revenue + nhập số Confirm/Actual | TD6 E05/E07; C-009 | **Done** (`P02-DOD.md`) | Dialog số + lý do; list adjustments | — |
| **P03** | Approval gate write-off > trần | ADR-0008; E10/E11; H-009 | **Done** (`P03-DOD.md`) | Trên trần → Approval → apply khi approved | Queue decide Done |
| **P04** | UAT Strict close + period lock stress | E12; ADR-0010; AC-008 | **Done** (`P04-DOD.md` · VPS PASS) | Close `strict` + Locked chặn confirm/allocate | Close UI Done |
| **P05** | Rate card / Rating / seed Expected (UI) | TD6 E04; C-011 | **Done** (`P05-DOD.md`) | CRUD card → publish → rate Bill → seed Expected | — |
| **P06** | Bảng `fx_rates` theo ngày | TD1 D02; ADR-0004/0008/0011 | **Done** (`P06-DOD.md`) | Entity dated rates + gắn Cost/Revenue/Settlement | — |
| **P07** | Aging summary + dashboard tách quyền tài chính | E09; ADR-0006; H View Cost≠Revenue | **Done** (`P07-DOD.md`) | Màn bucket + export; dashboard theo quyền | — |
| **P08** | Auto Exposure từ chứng từ đã khớp | E09; Received≠…≠Recognized | Link thủ công · chưa auto | Sau match → đề xuất/tạo exposure (không invent C/R) | Match Done |
| **P09** | Confirm match session + auto-suggest | E08; ADR-0005 | Manual match Done · confirm/auto deferred | Confirm phiên; tolerance suggest (review tay) | P08 optional |
| **P10** | Reverse recognize AP/AR + sổ điều chỉnh | E09; C-015 | Write-off Done · reverse recognize thiếu | Reverse recognition + history (không silent overwrite) | P03 nếu lớn |

## Kiểm soát / vận hành (sau P07–P11)

| ID | Tên | PO / ADR | Hiện trạng | Lát cắt MVP | Phụ thuộc |
|----|-----|----------|------------|-------------|-----------|
| **P11** | Inbox Variance (+ escalate thủ công) | E11; ADR-0009 | API Done · **thiếu UI** | List/filter; CTA mở Exception (giữ tách lớp) | Recon Done |
| **P12** | Ma trận approver / wizard multi-step | E11; H-009 | Level 1\|2 stub | Config theo object/amount + wizard | P03 |
| **P13** | UI Audit trail đối tượng tiền | E14/E16 | API Done · **thiếu UI** | Panel lịch sử Bill/Cost/Payment/Close | — |
| **P14** | UI recovery tích hợp (retry / dead-letter) | E15; H-010 | API stub Done · thiếu UI | List errors → mark-retried / dead-letter | — |
| **P15** | Bank feed CSV / sync | ADR-0013 | Manual feed Done · CSV chưa | Import CSV → `bank_feed_lines` | Bank-feed thin Done |
| **P16** | Recognition policy engine | E07 | Version string stub | Rule tenant khi nào recognize (VI gate) | P08 hữu ích |
| **P17** | Dated adjustments cho asOf profile | E13 | asOf live AdjustmentAmount | Events có ngày → profile trung thực | P02 / P10 |

## Nền tảng / NFR (sau khi P07–P11 ổn)

| ID | Tên | PO / ADR | Hiện trạng | Lát cắt MVP | Phụ thuộc |
|----|-----|----------|------------|-------------|-----------|
| **P18** | Admin master data UI (party/role/org/currency) | E01/E02 | API Done · thiếu manage UI | CRUD tối thiểu tenant ops | — |
| **P19** | Tenant settings API + `settings.manage` | ADR-0014; CP5 | Cookie prefs · deferred DB | `tenant_settings` + quyền | — |
| **P20** | Ngưỡng/SLA/FX theo tenant | ADR-0004/0009/0010 | appsettings node-wide | Override confirm/write-off/SLA/close | P06, P12, P19 |
| **P21** | Data Scope Revenue / Documents / AP·AR | ADR-0003; H-009 | Scope Bill/Cost partial | Enforce `all`/`own`(+org) list còn lại | — |
| **P22** | OIDC / refresh / revocation | ADR-0002; E14 | JWT+BFF Done · IdP deferred | External IdP; giữ `tenant_id`+`sub` | — |
| **P23** | Outbox broker + Redis rate-limit + OTel | E14–E16 | Stub / in-process | Broker hoặc worker; Redis; export | P14 trước scale |
| **P24** | Permission fine-grained (`cost.confirm`…) | H-009 | 403 runtime · catalog mỏng | Seed + enforce confirm/actualize/write-off | P03/P12 |
| **P25** | Load/soak + audit range PG pushdown | E16 | Timed smoke only | Soak money-path; filter audit native PG | P23 |

---

## Không làm (trừ ADR mới)

TMS tropes (GPS, e-POD, xe trống); rewrite Pass 3 toàn API; microservice/CQRS sớm.
