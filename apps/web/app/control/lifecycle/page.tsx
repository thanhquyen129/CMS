import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

const rows: { object: string; state: string; edit: string; remove: string; voidOrReverse: string; permission: string }[] = [
  {
    object: "Bill",
    state: "Đang dùng",
    edit: "Sửa trường vận hành theo quyền",
    remove: "Không xóa Bill",
    voidOrReverse: "Gỡ liên kết vận hành, không xóa số tài chính",
    permission: "Bill",
  },
  {
    object: "Đơn hàng / Lô hàng",
    state: "Đang liên kết",
    edit: "Sửa danh mục vận hành",
    remove: "Không xóa khi còn neo Bill",
    voidOrReverse: "Gỡ liên kết",
    permission: "Bill / vận hành",
  },
  {
    object: "Bảng giá",
    state: "Chưa phát hành",
    edit: "Sửa header, thêm phiên bản nháp",
    remove: "Ngừng bảng giá (mềm) + audit",
    voidOrReverse: "—",
    permission: "Bảng giá mua / bán",
  },
  {
    object: "Bảng giá",
    state: "Đã có phiên bản phát hành",
    edit: "Không sửa phiên bản đã phát hành",
    remove: "Không ngừng",
    voidOrReverse: "Lập phiên bản mới",
    permission: "Phát hành bảng giá",
  },
  {
    object: "Chi phí / Doanh thu",
    state: "Dự kiến",
    edit: "Chuyển maturity",
    remove: "Không xóa cứng",
    voidOrReverse: "Điều chỉnh có lý do",
    permission: "Chi phí hoặc doanh thu — tách quyền",
  },
  {
    object: "Chi phí / Doanh thu",
    state: "Đã xác nhận / Thực tế",
    edit: "Không ghi đè im lặng",
    remove: "Không xóa cứng",
    voidOrReverse: "Điều chỉnh giữ lịch sử",
    permission: "Chi phí hoặc doanh thu",
  },
  {
    object: "Chia doanh thu",
    state: "Nháp",
    edit: "Chưa chốt thì hủy phiên",
    remove: "Không xóa doanh thu",
    voidOrReverse: "Hủy phiên + audit; chia lại sau khi hủy",
    permission: "Doanh thu",
  },
  {
    object: "Chia doanh thu",
    state: "Đã chốt",
    edit: "Không sửa dòng đã chốt",
    remove: "Không xóa",
    voidOrReverse: "Chia lại thì phiên cũ bị thay",
    permission: "Doanh thu",
  },
  {
    object: "Phân bổ chi phí chung",
    state: "Nháp",
    edit: "Sửa phiên",
    remove: "Hủy phiên nháp",
    voidOrReverse: "—",
    permission: "Phân bổ",
  },
  {
    object: "Phân bổ chi phí chung",
    state: "Đã chốt",
    edit: "Không sửa",
    remove: "Không xóa",
    voidOrReverse: "Hủy phân bổ",
    permission: "Người khác người tạo mới chốt",
  },
  {
    object: "Chứng từ",
    state: "Đã nhận, chưa chấp nhận, chưa khớp",
    edit: "Sửa header (tiền tệ, Bill) + lý do",
    remove: "Không xóa chứng từ",
    voidOrReverse: "Void + lý do + audit",
    permission: "Chứng từ",
  },
  {
    object: "Chứng từ",
    state: "Đã chấp nhận / Đã khớp",
    edit: "Không sửa header",
    remove: "Không xóa",
    voidOrReverse: "Hủy khớp hoặc hủy dòng khớp",
    permission: "Khớp chứng từ",
  },
  {
    object: "Phải trả / Phải thu",
    state: "Đang hiệu lực",
    edit: "Điều chỉnh +/- có lý do, không ghi đè số ghi nhận",
    remove: "Không xóa",
    voidOrReverse: "Xóa sổ phần còn lại, hoặc hủy ghi nhận khi chưa tất toán",
    permission: "AP / AR",
  },
  {
    object: "Thanh toán / Phiếu thu",
    state: "Đang mở, chưa có phân bổ đã chốt",
    edit: "Không sửa số tiền",
    remove: "Không xóa",
    voidOrReverse: "Hủy phiếu + lý do; phân bổ nháp bị hủy",
    permission: "Thanh toán / Thu tiền",
  },
  {
    object: "Thanh toán / Phiếu thu",
    state: "Có phân bổ đã chốt",
    edit: "Không sửa",
    remove: "Không hủy phiếu",
    voidOrReverse: "Hủy phân bổ đã chốt rồi mới hủy phiếu",
    permission: "Thanh toán / Thu tiền",
  },
];

export default async function LifecycleMatrixPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();

  return (
    <AppShell terms={terms} active="control">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/control", label: "Kiểm soát tài chính" },
            { label: "Ma trận thao tác" },
          ]}
          title="Ma trận trạng thái × thao tác"
          lede="Nút trên màn hình theo đúng hàng này. Số đã ghi nhận không bị xóa cứng và không bị ghi đè im lặng."
        />
        <p className="note">
          <Link href="/control">Kiểm soát tài chính</Link>
          {" · "}
          Quyền xem chi phí không mở doanh thu hay biên lợi nhuận.
        </p>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Đối tượng</th>
                <th>Trạng thái</th>
                <th>Sửa</th>
                <th>Xóa</th>
                <th>Hủy / hoàn tác</th>
                <th>Quyền</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={`${row.object}-${row.state}`}>
                  <td>{row.object}</td>
                  <td>{row.state}</td>
                  <td>{row.edit}</td>
                  <td>{row.remove}</td>
                  <td>{row.voidOrReverse}</td>
                  <td>{row.permission}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </AppShell>
  );
}
