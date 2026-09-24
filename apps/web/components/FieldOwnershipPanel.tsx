import { getFieldOwnerships } from "@/lib/field-ownership";

const FIELD_LABEL: Record<string, string> = {
  origin_code: "Điểm đi",
  destination_code: "Điểm đến",
  actual_revenue: "Doanh thu thực tế",
  chargeable_weight_kg: "Trọng lượng tính cước",
};

function ownerLabel(owner: string) {
  return owner === "lcms_manual" ? "CMS (nhập tay)" : owner;
}

export async function FieldOwnershipPanel({
  objectType,
  objectId,
}: {
  objectType: string;
  objectId: string;
}) {
  const rows = await getFieldOwnerships(objectType, objectId);
  if (rows.length === 0) return null;

  return (
    <div className="alert alert-info" role="note">
      <ul className="stack-list">
        {rows.map((row) => (
          <li key={row.fieldName}>
            <strong>{FIELD_LABEL[row.fieldName] ?? row.fieldName}</strong>
            {" · nguồn "}
            {ownerLabel(row.ownerSystem)}
            {row.overrideReason ? ` · ghi đè: ${row.overrideReason}` : ""}
          </li>
        ))}
      </ul>
    </div>
  );
}
