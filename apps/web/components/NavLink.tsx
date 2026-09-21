"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import type { ComponentProps } from "react";
import { isNavHrefActive } from "@/lib/nav-match";
import { NavIcon, type NavIconName } from "./NavIcon";

type NavLinkProps = {
  href: string;
  icon?: NavIconName;
  active?: boolean;
  children: React.ReactNode;
  className?: string;
} & Omit<ComponentProps<typeof Link>, "href" | "className" | "children">;

/** Top-level or child sidebar link with optional designer icon. */
export function NavLink({
  href,
  icon,
  active,
  children,
  className,
  ...rest
}: NavLinkProps) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const current =
    active ?? isNavHrefActive(href, pathname, searchParams.toString());
  const classes = [
    "nav-item",
    icon ? "nav-item-with-icon" : "nav-item-child",
    current ? "active" : "",
    className ?? "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <Link href={href} className={classes} prefetch {...rest}>
      {icon ? <NavIcon name={icon} /> : null}
      <span className="nav-item-label">{children}</span>
    </Link>
  );
}
