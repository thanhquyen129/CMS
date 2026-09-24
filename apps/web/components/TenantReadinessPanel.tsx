import Link from "next/link";
import type { TenantReadiness } from "@/lib/tenant-admin";

export function TenantReadinessPanel({ data }: { data: TenantReadiness }) {
  const next = data.items.find((item) => !item.done);

  return (
    <section className="panel panel-wide" aria-label="Sẵn sàng lập Bill">
      <h2>{data.readyForBill ? "Sẵn sàng lập Bill" : "Chưa sẵn sàng lập Bill"}</h2>
      <p>{data.note}</p>
      <ul className="close-gate-list">
        {data.items.map((item) => (
          <li key={item.code} className={`close-gate-item ${item.done ? "pass" : "fail"}`}>
            <div className="close-gate-body">
              <strong>{item.done ? "Đạt" : "Chưa"} · {item.label}</strong>
              {!item.done && item.code === next?.code ? (
                <Link className="btn" href={item.href}>
                  Làm bước này
                </Link>
              ) : null}
            </div>
          </li>
        ))}
      </ul>
      {data.readyForBill ? (
        <Link className="btn" href="/bills/new">
          Lập Bill
        </Link>
      ) : null}
    </section>
  );
}
