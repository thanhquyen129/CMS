import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateBankFeedLineForm } from "@/components/CreateBankFeedLineForm";
import { ImportBankFeedCsvForm } from "@/components/ImportBankFeedCsvForm";
import { IgnoreBankFeedLineButton } from "@/components/IgnoreBankFeedLineButton";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { StatCardGrid } from "@/components/list/StatCardGrid";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  bankDirectionLabel,
  bankFeedStatusLabel,
  listBankFeedLines,
} from "@/lib/bank-feed";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{ status?: string }>;

export default async function BankFeedPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { status } = await searchParams;
  const terms = await fetchTerminology();
  const feedLabel = term(terms, "BANK_FEED", "Sao kê ngân hàng");
  const lineLabel = term(terms, "BANK_FEED_LINE", "Dòng sao kê");
  const reconLabel = term(terms, "RECONCILIATION", "Đối soát");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");

  const result = await listBankFeedLines(status ? { status } : undefined);
  const allRes = status
    ? await listBankFeedLines()
    : result;
  const allLines = allRes.ok ? allRes.data : [];
  const unmatchedCount = allLines.filter(
    (l) => l.status?.toLowerCase() === "unmatched"
  ).length;
  const matchedCount = allLines.filter(
    (l) => l.status?.toLowerCase() === "matched"
  ).length;
  const ignoredCount = allLines.filter(
    (l) => l.status?.toLowerCase() === "ignored"
  ).length;

  return (
    <AppShell terms={terms} active="control">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/control", label: "Kiểm soát tài chính" },
            { label: feedLabel },
          ]}
          title={feedLabel}
          lede={
            <>
              Nhập tay dòng sao kê (ADR-0013). {lineLabel} ≠ {paymentLabel}/
              {collectionLabel} — khớp qua phiên {reconLabel.toLowerCase()}.
            </>
          }
          action={
            <Link className="btn" href="/reconciliations/new">
              + Mở phiên {reconLabel.toLowerCase()}
            </Link>
          }
        />

        {allRes.ok ? (
          <StatCardGrid
            cards={[
              {
                key: "total",
                label: `Tổng ${lineLabel.toLowerCase()}`,
                value: allLines.length,
              },
              {
                key: "unmatched",
                label: term(terms, "BANK_FEED_UNMATCHED", "Chưa đối soát"),
                value: unmatchedCount,
                tone: unmatchedCount > 0 ? "warning" : "default",
                href: "/bank-feed?status=unmatched",
              },
              {
                key: "matched",
                label: term(terms, "BANK_FEED_MATCHED", "Đã đối soát"),
                value: matchedCount,
                tone: "success",
                href: "/bank-feed?status=matched",
              },
              {
                key: "ignored",
                label: "Đã bỏ qua",
                value: ignoredCount,
                href: "/bank-feed?status=ignored",
              },
            ]}
          />
        ) : null}

        <div className="filter-tabs" role="tablist" aria-label="Lọc trạng thái">
          <Link
            className={!status ? "active" : undefined}
            href="/bank-feed"
            role="tab"
            aria-selected={!status}
          >
            Tất cả
          </Link>
          <Link
            className={status === "unmatched" ? "active" : undefined}
            href="/bank-feed?status=unmatched"
            role="tab"
            aria-selected={status === "unmatched"}
          >
            {term(terms, "BANK_FEED_UNMATCHED", "Chưa đối soát")}
          </Link>
          <Link
            className={status === "matched" ? "active" : undefined}
            href="/bank-feed?status=matched"
            role="tab"
            aria-selected={status === "matched"}
          >
            {term(terms, "BANK_FEED_MATCHED", "Đã đối soát")}
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có {lineLabel.toLowerCase()}. Nhập bên dưới.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Ngày</th>
                  <th scope="col">Chiều</th>
                  <th scope="col">Số tiền</th>
                  <th scope="col">Tham chiếu</th>
                  <th scope="col">Đối tác</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((line) => (
                  <tr key={line.id}>
                    <td>{line.valueDate}</td>
                    <td>{bankDirectionLabel(terms, line.direction)}</td>
                    <td>{formatMoney(line.amount, line.currencyCode)}</td>
                    <td>
                      <span className="mono-id">
                        {line.bankReference?.trim() || "—"}
                      </span>
                      <span className="muted small block mono-id">
                        {line.id}
                      </span>
                    </td>
                    <td>{line.counterpartyName?.trim() || "—"}</td>
                    <td>{bankFeedStatusLabel(terms, line.status)}</td>
                    <td>
                      {line.status.toLowerCase() === "unmatched" ? (
                        <IgnoreBankFeedLineButton
                          terms={terms}
                          lineId={line.id}
                        />
                      ) : (
                        "—"
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <h2 className="section-title">Nhập CSV</h2>
        <ImportBankFeedCsvForm terms={terms} />

        <h2 className="section-title">Thêm {lineLabel.toLowerCase()}</h2>
        <CreateBankFeedLineForm terms={terms} />
      </section>
    </AppShell>
  );
}
