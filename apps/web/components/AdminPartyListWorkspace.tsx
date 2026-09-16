"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { DetailDrawer } from "./DetailDrawer";
import {
  formatCreditLimit,
  partyLabel,
  partyRoleLabel,
  type BusinessParty,
} from "@/lib/party";

type Props = {
  parties: BusinessParty[];
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
              <th scope="col">Vai trò</th>
              <th scope="col">Hạn mức</th>
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
                    <button
                      type="button"
                      className="row-link"
                      style={{
                        background: "none",
                        border: "none",
                        padding: 0,
                        font: "inherit",
                        cursor: "pointer",
                      }}
                      onClick={(e) => {
                        e.stopPropagation();
                        setSelectedId(p.id);
                      }}
                    >
                      {p.code}
                    </button>
                  </td>
                  <td>{p.name}</td>
                  <td className="mono-id">{p.taxId || "—"}</td>
                  <td>
                    {(p.roleCodes ?? []).length === 0
                      ? "—"
                      : (p.roleCodes ?? []).map(partyRoleLabel).join(", ")}
                  </td>
                  <td>{formatCreditLimit(p.creditLimit, p.creditLimitCurrencyCode)}</td>
                  <td>{p.isActive ? "Đang dùng" : "Ngừng"}</td>
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
        subtitle={selected ? (selected.isActive ? "Đang dùng" : "Ngừng") : null}
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
                {formatCreditLimit(selected.creditLimit, selected.creditLimitCurrencyCode)}
              </dd>
            </div>
            <div>
              <dt>Tiền tệ mặc định</dt>
              <dd>{selected.defaultCurrencyCode || "—"}</dd>
            </div>
          </dl>
        ) : null}
      </DetailDrawer>
    </>
  );
}
