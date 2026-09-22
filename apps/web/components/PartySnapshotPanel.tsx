import { listPartySnapshots } from "@/lib/reference-masters";
import { partyRoleLabel } from "@/lib/party";
import { formatDateTimeVi } from "@/lib/money";

/** Frozen party facts captured on the Bill. Master edits do not rewrite these rows. */
export async function PartySnapshotPanel({ billId }: { billId: string }) {
  const result = await listPartySnapshots("bill", billId);
  if (!result.ok) {
    return <div className="alert alert-error">{result.message}</div>;
  }
  if (result.data.length === 0) {
    return <p className="note">Chưa ghi snapshot đối tác trên Bill này.</p>;
  }

  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th>Vai trò</th>
            <th>Tên tại thời điểm ghi</th>
            <th>MST</th>
            <th>Điện thoại</th>
            <th>Nguồn</th>
            <th>Ghi lúc</th>
          </tr>
        </thead>
        <tbody>
          {result.data.map((row) => (
            <tr key={row.id}>
              <td>{partyRoleLabel(row.roleCode)}</td>
              <td>{row.displayName}</td>
              <td>{row.taxId || "—"}</td>
              <td>{row.phone || "—"}</td>
              <td>{row.isWalkIn ? "Vãng lai" : "Danh mục"}</td>
              <td>{formatDateTimeVi(row.capturedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
