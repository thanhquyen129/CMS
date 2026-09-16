"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { DetailDrawer } from "./DetailDrawer";
import { DrawerTabs } from "./list/DrawerTabs";
import { partyTypeLabel, type RateCard } from "@/lib/rate-cards";
import { formatDateTimeVi } from "@/lib/money";

type Props = {
  cards: RateCard[];
  billLabel: string;
};

export function RateCardListWorkspace({ cards, billLabel }: Props) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [tab, setTab] = useState("overview");
  const selected = useMemo(
    () => cards.find((c) => c.id === selectedId) ?? null,
    [cards, selectedId]
  );
  const close = useCallback(() => {
    setSelectedId(null);
    setTab("overview");
  }, []);

  return (
    <>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Mã bảng giá</th>
              <th scope="col">Tên bảng giá</th>
              <th scope="col">Loại giá</th>
              <th scope="col">Tiền tệ</th>
              <th scope="col">Trạng thái</th>
              <th scope="col">
                <span className="sr-only">Mở</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {cards.map((c) => {
              const active = selectedId === c.id;
              return (
                <tr
                  key={c.id}
                  className={active ? "row-selected" : undefined}
                  tabIndex={0}
                  style={{ cursor: "pointer" }}
                  onClick={() => {
                    setSelectedId(c.id);
                    setTab("overview");
                  }}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      setSelectedId(c.id);
                      setTab("overview");
                    }
                  }}
                >
                  <td>
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
                        setSelectedId(c.id);
                      }}
                    >
                      <code>{c.code}</code>
                    </button>
                  </td>
                  <td>{c.name}</td>
                  <td>
                    <span className="status-pill">{partyTypeLabel(c.partyType)}</span>
                  </td>
                  <td>{c.currencyCode}</td>
                  <td>
                    <span
                      className={`maturity-pill ${c.isActive ? "maturity-confirmed" : ""}`}
                    >
                      {c.isActive ? "Đang hiệu lực" : "Ngưng"}
                    </span>
                  </td>
                  <td>
                    <Link
                      className="btn btn-ghost btn-sm"
                      href={`/rate-cards/${c.id}`}
                      onClick={(e) => e.stopPropagation()}
                    >
                      Chi tiết
                    </Link>
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
        title={selected ? selected.name : null}
        subtitle={
          selected ? (
            <>
              <code>{selected.code}</code>
              {" · "}
              <span
                className={`maturity-pill ${selected.isActive ? "maturity-confirmed" : ""}`}
              >
                {selected.isActive ? "Đang hiệu lực" : "Ngưng"}
              </span>
            </>
          ) : null
        }
        footer={
          selected ? (
            <div className="toolbar-row" style={{ margin: 0 }}>
              <Link className="btn btn-sm" href={`/rate-cards/${selected.id}`}>
                Mở bảng giá / phiên bản
              </Link>
              <Link className="btn btn-sm btn-ghost" href="/bills">
                Tính giá trên {billLabel}
              </Link>
            </div>
          ) : null
        }
      >
        {selected ? (
          <>
            <DrawerTabs
              tabs={[
                { id: "overview", label: "Thông tin chung" },
                { id: "pricing", label: "Chi tiết giá" },
                { id: "history", label: "Lịch sử" },
              ]}
              activeId={tab}
              onChange={setTab}
            />
            {tab === "overview" ? (
              <dl className="metric-grid">
                <div>
                  <dt>Loại giá</dt>
                  <dd>{partyTypeLabel(selected.partyType)}</dd>
                </div>
                <div>
                  <dt>Tiền tệ</dt>
                  <dd>{selected.currencyCode}</dd>
                </div>
                <div>
                  <dt>Mô tả</dt>
                  <dd>{selected.description || "—"}</dd>
                </div>
                <div>
                  <dt>Tạo lúc</dt>
                  <dd>{formatDateTimeVi(selected.createdAt)}</dd>
                </div>
                <div>
                  <dt>Ghi chú nghiệp vụ</dt>
                  <dd className="muted">
                    Rating tạo kỳ vọng tài chính (Dự kiến), không tạo Thực tế.
                  </dd>
                </div>
              </dl>
            ) : null}
            {tab === "pricing" ? (
              <p className="muted">
                Quy tắc / phụ phí / bậc trọng lượng nằm trên hồ sơ phiên bản bảng giá.
                Mở hồ sơ đầy đủ để xem và chỉnh.
              </p>
            ) : null}
            {tab === "history" ? (
              <p className="muted">
                Lịch sử phiên bản (publish) xem tại trang chi tiết bảng giá.
              </p>
            ) : null}
          </>
        ) : null}
      </DetailDrawer>
    </>
  );
}
