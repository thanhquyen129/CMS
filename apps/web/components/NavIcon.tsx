/** Compact line icons for LCMS designer sidebar. */
export type NavIconName =
  | "home"
  | "bills"
  | "rates"
  | "costs"
  | "revenues"
  | "documents"
  | "ap"
  | "ar"
  | "settlements"
  | "control"
  | "close"
  | "reports"
  | "admin"
  | "settings";

export function NavIcon({
  name,
  className = "nav-icon",
}: {
  name: NavIconName;
  className?: string;
}) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      width="16"
      height="16"
      aria-hidden="true"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {name === "home" ? (
        <path d="M4 10.5 12 4l8 6.5V20a1 1 0 0 1-1 1h-4.5v-6h-5v6H5a1 1 0 0 1-1-1v-9.5z" />
      ) : null}
      {name === "bills" ? (
        <>
          <path d="M8 3h7l4 4v13a1 1 0 0 1-1 1H8a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z" />
          <path d="M15 3v4h4M10 12h6M10 16h4" />
        </>
      ) : null}
      {name === "rates" ? (
        <>
          <rect x="5" y="4" width="14" height="16" rx="1.5" />
          <path d="M8 9h8M8 13h8M8 17h5" />
        </>
      ) : null}
      {name === "costs" ? (
        <path d="M3 17c2.5-5 5.5-8 9-9 3.5 1 6.5 4 9 9M8 7c1.2-2 2.8-3.2 4-3.2S14.8 5 16 7" />
      ) : null}
      {name === "revenues" ? (
        <path d="M4 19V11M9 19V8M14 19v-6M19 19V6M3 20h18" />
      ) : null}
      {name === "documents" ? (
        <>
          <path d="M8 3h6l4 4v13H8V3z" />
          <path d="M14 3v4h4M10 12h6M10 16h4" />
        </>
      ) : null}
      {name === "ap" ? (
        <>
          <path d="M5 8h14v11H5V8z" />
          <path d="M8 5h8v3H8V5zM10 13h4v4h-4v-4z" />
        </>
      ) : null}
      {name === "ar" ? (
        <>
          <circle cx="12" cy="12" r="7" />
          <path d="M12 8v4.2L14.5 14" />
        </>
      ) : null}
      {name === "settlements" ? (
        <>
          <path d="M6 9h12v10H6V9z" />
          <path d="M9 6h6v3H9V6zM10 13h4v4h-4v-4z" />
        </>
      ) : null}
      {name === "control" ? (
        <>
          <path d="M12 3 5 6.5v5.2c0 4.3 2.9 7.3 7 8.3 4.1-1 7-4 7-8.3V6.5L12 3z" />
          <path d="m9.2 12.2 2.1 2.1 4-4" />
        </>
      ) : null}
      {name === "close" ? (
        <>
          <path d="M5 5h14v4H5V5z" />
          <path d="M5 11h14v8H5v-8zM9 14h6v3H9v-3z" />
        </>
      ) : null}
      {name === "reports" ? (
        <path d="M4 19V10l4 2.5L14 7l6 3.5V19H4z" />
      ) : null}
      {name === "admin" ? (
        <>
          <rect x="4" y="5" width="7" height="6" rx="1" />
          <rect x="13" y="5" width="7" height="6" rx="1" />
          <rect x="4" y="13" width="7" height="6" rx="1" />
          <rect x="13" y="13" width="7" height="6" rx="1" />
        </>
      ) : null}
      {name === "settings" ? (
        <>
          <circle cx="12" cy="12" r="3.2" />
          <path d="M12 3.5v2.2M12 18.3v2.2M4.8 7.2l1.9 1.1M17.3 15.7l1.9 1.1M4.8 16.8l1.9-1.1M17.3 8.3l1.9-1.1" />
        </>
      ) : null}
    </svg>
  );
}
