"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { hrefWithPage, PAGE_SIZE_OPTIONS } from "@/lib/list-paging";

type Props = {
  basePath: string;
  params: Record<string, string | undefined | null>;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  pageKey?: string;
  pageSizeKey?: string;
};

export function ListPagination({
  basePath,
  params,
  page,
  pageSize,
  totalCount,
  totalPages: pages,
  pageKey = "page",
  pageSizeKey = "pageSize",
}: Props) {
  const router = useRouter();
  if (totalCount === 0) return null;

  const from = (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);

  const windowStart = Math.max(1, page - 2);
  const windowEnd = Math.min(pages, page + 2);
  const pageNumbers: number[] = [];
  for (let i = windowStart; i <= windowEnd; i++) pageNumbers.push(i);

  const href = (n: number, size = pageSize) =>
    hrefWithPage(basePath, params, n, size, pageKey, pageSizeKey);

  return (
    <nav className="list-pagination" aria-label="Phân trang">
      <p className="list-pagination-meta muted">
        Hiển thị {from}–{to} trong {totalCount} bản ghi
      </p>
      <div className="list-pagination-controls">
        <label className="list-pagination-size">
          <span className="sr-only">Số dòng mỗi trang</span>
          <select
            value={pageSize}
            onChange={(e) => {
              const next = Number(e.target.value);
              router.push(href(1, next), { scroll: false });
            }}
            aria-label="Số dòng mỗi trang"
          >
            {PAGE_SIZE_OPTIONS.map((n) => (
              <option key={n} value={n}>
                {n}/trang
              </option>
            ))}
          </select>
        </label>
        {page > 1 ? (
          <Link className="btn btn-ghost btn-sm" href={href(page - 1)} scroll={false}>
            Trước
          </Link>
        ) : (
          <span className="btn btn-ghost btn-sm" aria-disabled="true">
            Trước
          </span>
        )}
        {pageNumbers.map((n) => (
          <Link
            key={n}
            className={n === page ? "btn btn-sm" : "btn btn-ghost btn-sm"}
            href={href(n)}
            scroll={false}
            aria-current={n === page ? "page" : undefined}
          >
            {n}
          </Link>
        ))}
        {page < pages ? (
          <Link className="btn btn-ghost btn-sm" href={href(page + 1)} scroll={false}>
            Sau
          </Link>
        ) : (
          <span className="btn btn-ghost btn-sm" aria-disabled="true">
            Sau
          </span>
        )}
      </div>
    </nav>
  );
}
