"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import type { RateCard, RateVersion } from "@/lib/rate-cards";
import { isPublishedVersion, partyTypeLabel } from "@/lib/rate-cards";
import { term, type TerminologyMap } from "@/lib/terminology";

type CardWithVersions = {
  card: RateCard;
  versions: RateVersion[];
};

type Props = {
  terms: TerminologyMap;
  billId: string;
  cardsWithVersions: CardWithVersions[];
};

export function RateBillForm({ terms, billId, cardsWithVersions }: Props) {
  const router = useRouter();
  const cardsWithPublished = cardsWithVersions.filter((c) =>
    c.versions.some((v) => isPublishedVersion(v.status))
  );

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [cardId, setCardId] = useState(
    cardsWithPublished[0]?.card.id ?? ""
  );

  const expected = term(terms, "EXPECTED", "Dự kiến");
  const costLabel = term(terms, "COST", "Chi phí");

  const publishedVersions = useMemo(() => {
    const entry = cardsWithPublished.find((c) => c.card.id === cardId);
    if (!entry) return [];
    return entry.versions.filter((v) => isPublishedVersion(v.status));
  }, [cardId, cardsWithPublished]);

  if (cardsWithPublished.length === 0) {
    return (
      <div className="empty-state" role="status">
        Chưa có phiên bản bảng giá đã phát hành. Tạo bảng giá → quy tắc → phát
        hành trước khi tính giá.
      </div>
    );
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const qtyRaw = String(fd.get("quantity") ?? "1").trim();
    const quantity = Number(qtyRaw.replace(",", "."));
    if (!Number.isFinite(quantity) || quantity <= 0) {
      setError("Số lượng phải > 0.");
      setSubmitting(false);
      return;
    }

    const baseRaw = String(fd.get("baseAmount") ?? "").trim();
    const baseAmount = baseRaw
      ? Number(baseRaw.replace(",", "."))
      : null;
    if (baseRaw && (baseAmount === null || !Number.isFinite(baseAmount))) {
      setError("Số cơ sở không hợp lệ.");
      setSubmitting(false);
      return;
    }

    const body = {
      billId,
      rateVersionId: String(fd.get("rateVersionId") ?? "").trim(),
      quantity,
      weight: null,
      serviceTypeCode: String(fd.get("serviceTypeCode") ?? "").trim() || null,
      partyTypeCode: String(fd.get("partyTypeCode") ?? "").trim() || null,
      routeCode: String(fd.get("routeCode") ?? "").trim() || null,
      baseAmount,
      supersedesRatingId: null,
      seedExpectedCosts: fd.get("seedExpectedCosts") === "on",
    };

    if (!body.rateVersionId) {
      setError("Chọn phiên bản đã phát hành.");
      setSubmitting(false);
      return;
    }

    try {
      const res = await fetch("/bff/ratings", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify(body),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          payload.message ||
            (res.status === 403
              ? "Bạn không có quyền tính giá."
              : res.status === 409
                ? "Không tính giá được (phiên bản chưa phát hành hoặc không khớp)."
                : "Tính giá thất bại.")
        );
        return;
      }

      setSuccess(
        body.seedExpectedCosts
          ? `Đã tính giá và seed ${costLabel.toLowerCase()} ${expected.toLowerCase()}.`
          : "Đã tính giá. Có thể seed chi phí Dự kiến từ lịch sử bên dưới."
      );
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;
  const selectedCard =
    cardsWithPublished.find((c) => c.card.id === cardId) ??
    cardsWithPublished[0];

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Tính giá theo phiên bản đã phát hành. Tick seed để tạo{" "}
        {costLabel.toLowerCase()} lớp {expected} (idempotent theo dòng rating).
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {success ? (
        <div className="alert alert-info" role="status">
          {success}
        </div>
      ) : null}

      <div className="form-grid">
        <div className="field">
          <label htmlFor="rateCardId">Bảng giá</label>
          <select
            id="rateCardId"
            value={selectedCard?.card.id ?? ""}
            onChange={(ev) => setCardId(ev.target.value)}
          >
            {cardsWithPublished.map(({ card }) => (
              <option key={card.id} value={card.id}>
                {card.code} — {card.name} ({partyTypeLabel(card.partyType)})
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="rateVersionId">Phiên bản đã phát hành</label>
          <select
            id="rateVersionId"
            name="rateVersionId"
            required
            defaultValue={publishedVersions[0]?.id ?? ""}
            key={cardId}
          >
            {publishedVersions.length === 0 ? (
              <option value="">— Không có —</option>
            ) : (
              publishedVersions.map((v) => (
                <option key={v.id} value={v.id}>
                  v{v.versionNo}
                  {v.note ? ` — ${v.note}` : ""}
                </option>
              ))
            )}
          </select>
        </div>
        <div className="field">
          <label htmlFor="quantity">Số lượng</label>
          <input
            id="quantity"
            name="quantity"
            defaultValue="1"
            inputMode="decimal"
            required
          />
        </div>
        <div className="field">
          <label htmlFor="baseAmount">Số cơ sở (cho % — tuỳ chọn)</label>
          <input id="baseAmount" name="baseAmount" inputMode="decimal" />
        </div>
        <div className="field">
          <label htmlFor="serviceTypeCode">Loại dịch vụ (lọc)</label>
          <input id="serviceTypeCode" name="serviceTypeCode" />
        </div>
        <div className="field">
          <label htmlFor="partyTypeCode">Mã loại đối tác (lọc)</label>
          <input id="partyTypeCode" name="partyTypeCode" />
        </div>
        <div className="field">
          <label htmlFor="routeCode">Mã tuyến (lọc)</label>
          <input id="routeCode" name="routeCode" />
        </div>
        <div className="field field-span">
          <label className="checkbox-label">
            <input
              type="checkbox"
              name="seedExpectedCosts"
              defaultChecked
            />{" "}
            Seed {costLabel.toLowerCase()} {expected.toLowerCase()} ngay
          </label>
        </div>
      </div>

      <div className="cta-row">
        <button
          className="btn"
          type="submit"
          disabled={busy || publishedVersions.length === 0}
        >
          {busy ? "Đang tính…" : "Tính giá"}
        </button>
      </div>
    </form>
  );
}
