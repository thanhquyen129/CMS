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
              <th scope="col">Hãng/NCC</th>
              <th scope="col">Tuyến</th>
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
                    <span className={`status-pill ${c.partyType === "customer" ? "maturity-actual" : "maturity-confirmed"}`}>
                      {partyTypeLabel(c.partyType)}
                    </span>
                  </td>
                  <td>{c.carrierName || "—"}</td>
                  <td>
                    {[c.transportMode, c.routeCode].filter(Boolean).join(" · ") ||
                      "—"}
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
                { id: "surcharge", label: "Phụ phí" },
                { id: "terms", label: "Điều kiện áp dụng" },
                { id: "history", label: "Lịch sử" },
                { id: "related", label: "Liên quan" },
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
                  <dt>Hãng/NCC</dt>
                  <dd>{selected.carrierName || "—"}</dd>
                </div>
                <div>
                  <dt>Phương thức</dt>
                  <dd>{selected.transportMode || "—"}</dd>
                </div>
                <div>
                  <dt>Tuyến</dt>
                  <dd>{selected.routeCode || "—"}</dd>
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
            {tab === "pricing" || tab === "surcharge" || tab === "terms" ? (
              <p className="muted">
                Lưới đơn giá (bậc × loại hàng) và phụ phí nằm trên phiên bản đã phát hành.
                Mở hồ sơ bảng giá để xem; phiên bản đã phát hành không sửa trực tiếp.
              </p>
            ) : null}
            {tab === "history" ? (
              <p className="muted">
                <Link href="/rate-cards/history">Lịch sử giá</Link> giữ snapshot Rating đã tính.
              </p>
            ) : null}
            {tab === "related" ? (
              <p className="muted">
                <Link href={`/rate-cards/compare?partyType=${selected.partyType}`}>So sánh giá</Link>
                {" · "}
                <Link href="/rate-cards/appendices">Phụ lục giá</Link>
              </p>
            ) : null}
          </>
        ) : null}
      </DetailDrawer>
    </>
  );
}
