# LCMS Designer theme (single product skin)

Chuẩn giao diện theo bộ mockup designer UI-01…UI-15. Các preset cũ (Ledger, Harbor Dawn, Invoika, Soft Purple, Classic) đã gỡ.

## Token

| Token | Value | Role |
|-------|-------|------|
| Sidebar | `#001529` | Dark navy chrome |
| Accent | `#1890ff` | Primary CTA / active nav |
| Accent hover | `#096dd9` | Hover |
| BG | `#f0f2f5` | Content canvas |
| Surface | `#ffffff` | Cards / tables |
| Success | `#52c41a` / bg `#f6ffed` | Done / positive |
| Warning | `#faad14` / bg `#fffbe6` | Pending |
| Danger | `#ff4d4f` / bg `#fff2f0` | Overdue / exception |
| Radius | `8px` | Cards, buttons, inputs |

## Layout
- Mặc định **vertical** sidebar (đúng mockup).
- `data-theme` luôn = `lcms` (migrate cookie theme cũ).

## Scope
Skin/shell tokens + status pills + nav active + list/hub chrome (`ListPageHeader`, `FilterBar`, `StatCardGrid`, `hub-module-tabs`, topbar account).
Không ship chart/widget giả chỉ để giống mockup — KPI và filter chỉ từ dữ liệu API.
