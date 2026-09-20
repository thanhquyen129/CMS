"use client";

import Link from "next/link";
import { formatCreditLimit, creditStatusLabel, partyCreditModeLabel } from "@/lib/party";
import type { PartyFinancialView } from "@/lib/party";
import { formatMoney } from "@/lib/money";

export function PartyFinancialPanel({ view }: { view: PartyFinancialView }) {
  return (
    <div className="stack-panels">
      <fieldset className="group-box">
        <legend>Hạn mức &amp; công nợ</legend>
        <dl className="metric-grid">
          <div>
            <dt>Chế độ hạn mức</dt>
            <dd>{partyCreditModeLabel(view.credit.mode)}</dd>
          </div>
          <div>
            <dt>Hạn mức AR</dt>
            <dd>
              {formatCreditLimit(
                view.credit.creditLimit,
                view.credit.creditLimitCurrencyCode
              )}
            </dd>
          </div>
          <div>
            <dt>Phải thu cùng tiền tệ hạn mức</dt>
            <dd>
              {formatCreditLimit(
                view.credit.arOutstandingSameCurrency,
                view.credit.creditLimitCurrencyCode
              )}
            </dd>
          </div>
          <div>
            <dt>Mức sử dụng</dt>
            <dd>
              {view.credit.utilizationPercent != null
                ? `${view.credit.utilizationPercent}%`
                : "—"}{" "}
              <span className={`status-pill credit-${view.credit.status}`}>
                {creditStatusLabel(view.credit.status)}
              </span>
            </dd>
          </div>
        </dl>
        {view.credit.message ? (
          <p className="alert alert-warning" role="status">
            {view.credit.message}
          </p>
        ) : null}
      </fieldset>

      <fieldset className="group-box">
        <legend>Phải thu / phải trả đang mở</legend>
        {!view.canViewAr && !view.canViewAp ? (
          <p className="muted">
            Không có quyền xem chi phí hoặc doanh thu — không hiện số công nợ.
          </p>
        ) : (
          <dl className="metric-grid">
            <div>
              <dt>Phải thu đang mở</dt>
              <dd>
                {view.canViewAr
                  ? formatCreditLimit(view.arOutstandingTotal, "VND")
                  : "Không có quyền xem doanh thu"}
              </dd>
            </div>
            <div>
              <dt>Quá hạn phải thu</dt>
              <dd>{view.canViewAr ? view.arOverdueCount ?? 0 : "—"}</dd>
            </div>
            <div>
              <dt>Phải trả đang mở</dt>
              <dd>
                {view.canViewAp
                  ? formatCreditLimit(view.apOutstandingTotal, "VND")
                  : "Không có quyền xem chi phí"}
              </dd>
            </div>
            <div>
              <dt>Quá hạn phải trả</dt>
              <dd>{view.canViewAp ? view.apOverdueCount ?? 0 : "—"}</dd>
            </div>
          </dl>
        )}
        {view.canViewAr && (view.arByCurrency?.length ?? 0) > 0 ? (
          <p className="muted small">
            AR theo tiền tệ:{" "}
            {view.arByCurrency!.map((b) => `${formatMoney(b.outstanding, b.currencyCode)} (${b.openCount})`).join(" · ")}
          </p>
        ) : null}
        {view.canViewAp && (view.apByCurrency?.length ?? 0) > 0 ? (
          <p className="muted small">
            AP theo tiền tệ:{" "}
            {view.apByCurrency!.map((b) => `${formatMoney(b.outstanding, b.currencyCode)} (${b.openCount})`).join(" · ")}
          </p>
        ) : null}
      </fieldset>

      <fieldset className="group-box">
        <legend>Phát sinh gắn đối tác</legend>
        <dl className="metric-grid">
          <div>
            <dt>Bill (khách hàng)</dt>
            <dd>{view.canViewBills ? view.billCount : "—"}</dd>
          </div>
          <div>
            <dt>Chi phí (NCC)</dt>
            <dd>{view.canViewAp ? view.costCount : "—"}</dd>
          </div>
          <div>
            <dt>Doanh thu</dt>
            <dd>{view.canViewAr ? view.revenueCount : "—"}</dd>
          </div>
          <div>
            <dt>Chứng từ</dt>
            <dd>{view.documentCount}</dd>
          </div>
        </dl>
        {view.recentBills.length > 0 ? (
          <ul className="plain-list">
            {view.recentBills.map((b) => (
              <li key={b.id}>
                <Link href={`/bills/${b.id}`}>{b.billNo}</Link>
              </li>
            ))}
          </ul>
        ) : null}
        {view.recentDocuments.length > 0 ? (
          <ul className="plain-list">
            {view.recentDocuments.map((d) => (
              <li key={d.id}>
                <Link href={`/documents/${d.id}`}>
                  {d.documentNo} · {d.direction}
                </Link>
              </li>
            ))}
          </ul>
        ) : null}
      </fieldset>
    </div>
  );
}
