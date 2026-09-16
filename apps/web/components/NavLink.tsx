import Link from "next/link";
import type { ComponentProps } from "react";
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
  const classes = [
    "nav-item",
    icon ? "nav-item-with-icon" : "nav-item-child",
    active ? "active" : "",
    className ?? "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <Link href={href} className={classes} {...rest}>
      {icon ? <NavIcon name={icon} /> : null}
      <span className="nav-item-label">{children}</span>
    </Link>
  );
}
