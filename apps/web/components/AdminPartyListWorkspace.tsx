"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { DetailDrawer } from "./DetailDrawer";
import {
  creditStatusLabel,
  formatCreditLimit,
  partyLabel,
  partyRoleLabel,
  partyStatusLabel,
  type PartyDirectoryItem,
} from "@/lib/party";

type Props = {
  parties: PartyDirectoryItem[];
};

export function AdminPartyListWorkspace({ parties }: Props) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = useMemo(
    () => parties.find((p) => p.id === selectedId) ?? null,
    [parties, selectedId]
  );
  const close = useCallback(() => setSelectedId(null), []);

  return (
    <>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Mã</th>
              <th scope="col">Tên</th>
              <th scope="col">MST</th>
              <th scope="col">SĐT</th>
              <th scope="col">Vai trò</th>
              <th scope="col">Hạn mức</th>
              <th scope="col">Công nợ</th>
              <th scope="col">Trạng thái</th>
            </tr>
          </thead>
          <tbody>
            {parties.map((p) => {
              const active = selectedId === p.id;
              return (
                <tr
                  key={p.id}
                  className={active ? "row-selected" : undefined}
                  tabIndex={0}
                  style={{ cursor: "pointer" }}
                  onClick={() => setSelectedId(p.id)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      setSelectedId(p.id);
                    }
                  }}
                >
                  <td className="mono-id">
                    <Link href={`/admin/parties/${p.id}`}>{p.code}</Link>
                  </td>
                  <td>
                    {p.name}
                    {p.shortName ? (
                      <div className="muted small">{p.shortName}</div>
                    ) : null}
                  </td>
                  <td className="mono-id">{p.taxId || "—"}</td>
                  <td>{p.phone || "—"}</td>
                  <td>
                    {(p.roleCodes ?? []).length === 0
                      ? "—"
                      : (p.roleCodes ?? []).map(partyRoleLabel).join(", ")}
                  </td>
                  <td>
                    {formatCreditLimit(p.creditLimit, p.creditLimitCurrencyCode)}
                  </td>
                  <td>
                    {p.arOutstanding != null
                      ? formatCreditLimit(
                          p.arOutstanding,
                          p.creditLimitCurrencyCode || p.defaultCurrencyCode
                        )
                      : p.apOutstanding != null
                        ? formatCreditLimit(
                            p.apOutstanding,
                            p.defaultCurrencyCode
                          )
                        : "—"}
                  </td>
                  <td>
                    <span className={`status-pill status-${p.statusCode}`}>
                      {partyStatusLabel(p.statusCode, p.isActive)}
                    </span>
                    {p.creditStatus &&
                    p.creditStatus !== "none" &&
                    p.creditStatus !== "ok" ? (
                      <>
                        {" "}
                        <span className={`status-pill credit-${p.creditStatus}`}>
                          {creditStatusLabel(p.creditStatus)}
                        </span>
                      </>
                    ) : null}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <DetailDrawer
        open={Boolean(selected)}
        onClose={close}
        title={selected ? partyLabel(selected) : null}
        subtitle={
          selected
            ? partyStatusLabel(selected.statusCode, selected.isActive)
            : null
        }
        footer={
          selected ? (
            <Link className="btn btn-sm" href={`/admin/parties/${selected.id}`}>
              Mở hồ sơ đầy đủ
            </Link>
          ) : null
        }
      >
        {selected ? (
          <dl className="metric-grid">
            <div>
              <dt>Vai trò</dt>
              <dd>
                {(selected.roleCodes ?? []).length === 0
                  ? "—"
                  : (selected.roleCodes ?? []).map(partyRoleLabel).join(", ")}
              </dd>
            </div>
            <div>
              <dt>Điện thoại</dt>
              <dd>{selected.phone || "—"}</dd>
            </div>
            <div>
              <dt>Email</dt>
              <dd>{selected.email || "—"}</dd>
            </div>
            <div>
              <dt>Điều khoản thanh toán</dt>
              <dd>
                {selected.paymentTermDays != null
                  ? `${selected.paymentTermDays} ngày`
                  : "—"}
              </dd>
            </div>
            <div>
              <dt>Hạn mức công nợ</dt>
              <dd>
                {formatCreditLimit(
                  selected.creditLimit,
                  selected.creditLimitCurrencyCode
                )}
              </dd>
            </div>
            <div>
              <dt>Phải thu đang mở</dt>
              <dd>
                {selected.arOutstanding != null
                  ? formatCreditLimit(
                      selected.arOutstanding,
                      selected.creditLimitCurrencyCode ||
                        selected.defaultCurrencyCode
                    )
                  : "Không có quyền xem doanh thu"}
              </dd>
            </div>
            <div>
              <dt>Phải trả đang mở</dt>
              <dd>
                {selected.apOutstanding != null
                  ? formatCreditLimit(
                      selected.apOutstanding,
                      selected.defaultCurrencyCode
                    )
                  : "Không có quyền xem chi phí"}
              </dd>
            </div>
          </dl>
        ) : null}
      </DetailDrawer>
    </>
  );
}
