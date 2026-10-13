# PARTNER-ROLE-01 — Rà soát vai trò đối tác (không triển khai gộp)

Ngày: 2026-10-13. PO chỉ duyệt rà soát. Không xóa, không gộp role, không migration.

## AS-IS

Chín vai trò master trên `party_roles` (`PartyRoleCodes`):

| Mã | Nhãn | Nhóm danh mục |
| --- | --- | --- |
| customer | Khách hàng | Tab Khách hàng |
| payer | Bên trả tiền | Tab Khách hàng |
| bill_to | Bên nhận hóa đơn | Tab Khách hàng |
| shipper | Người gửi hàng | Tab Khách hàng |
| consignee | Người nhận hàng | Tab Khách hàng |
| vendor | Nhà cung cấp | Tab Nhà cung cấp |
| payee | Bên nhận tiền | Tab Nhà cung cấp |
| carrier | Hãng vận chuyển | Tab Nhà cung cấp |
| agent | Đại lý | Tab Nhà cung cấp |

Tab danh mục dùng `ForDirectory`: chọn Khách hàng hiện cả năm role phía khách; chọn Nhà cung cấp hiện cả bốn role phía cung. Lookup đúng một role (`LookupBusinessPartiesQuery`) không nới theo tab.

Vai trò giao dịch không lấy từ tên role master:

| Chỗ dùng | Role giao dịch |
| --- | --- |
| Bill | customer, payer, shipper, consignee, bill_to |
| Bảng giá | party type `vendor` (mua) / `customer` (bán); `SupplierPartyId` / `CustomerPartyId`; `CarrierName` là chữ, không phải party |
| Chi phí / AP | vendor / đối tác phải trả |
| Doanh thu / AR | customer / đối tác phải thu |
| Chứng từ | counterparty theo chiều chứng từ |
| Rating | chiều mua/bán của phiên bản; mode lấy từ chặng khi tính theo chặng |

## TO-BE đề xuất (chờ PO)

Giữ đủ 9 role master và các role giao dịch ở trên. Không gộp carrier vào vendor, không gộp payer vào customer.

RATE-PARTNER-01 (đã triển khai riêng): giá mua chỉ nhận party có role `vendor`; giá bán chỉ nhận party có role `customer`. Party có cả hai được chọn cả hai chiều. Carrier hoặc đại lý đơn thuần không được chọn làm nhà cung cấp. Bảng giá cũ giữ nguyên đối tác đã lưu.

## Phụ thuộc nếu PO duyệt gộp sau này

Rate card, Cost, Revenue, AP/AR, chứng từ, snapshot party trên Bill, import đối tác, phân quyền `master.party.manage`, báo cáo đối tác. Đổi tab danh mục sẽ đổi kết quả lọc `ForDirectory` — không nằm trong RATE-PARTNER-01.

## PO cần chốt

1. Có gộp role nào trong chín role hay không.
2. Tab Nhà cung cấp có tiếp tục gồm carrier và agent hay không.
3. `CarrierName` trên bảng giá có chuyển thành party role `carrier` hay giữ là chữ.

## Regression đã giữ

Không migration. Directory và Bill party roles không đổi. Test `ChangeBatch20261013Tests.RateCardPartner_RequiresRole_AndLeavesHistoricalPartner`.
