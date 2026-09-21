"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { MouseEvent, ReactNode } from "react";
import { replaceSearchShallow } from "@/lib/shallow-query";

type Props = {
  href: string;
  className?: string;
  children: ReactNode;
};

/**
 * Same-path `?selected=` link: update the URL in place.
 * Cross-path hrefs stay a normal client navigation.
 */
export function QuerySelectLink({ href, className, children }: Props) {
  const pathname = usePathname();

  function onClick(event: MouseEvent<HTMLAnchorElement>) {
    const url = new URL(href, window.location.origin);
    if (url.pathname !== pathname) return;
    event.preventDefault();
    replaceSearchShallow(`${url.pathname}${url.search}${url.hash}`);
  }

  return (
    <Link className={className} href={href} scroll={false} onClick={onClick}>
      {children}
    </Link>
  );
}
