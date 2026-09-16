"use client";

/** Collapses vertical sidebar to icon rail (cookie + html class). */
export function SidebarCollapseButton() {
  function toggle() {
    const root = document.documentElement;
    const next = root.getAttribute("data-sidebar") === "collapsed" ? "expanded" : "collapsed";
    root.setAttribute("data-sidebar", next);
    try {
      document.cookie = `lcms_sidebar=${next};path=/;max-age=31536000;samesite=lax`;
    } catch {
      /* ignore */
    }
  }

  return (
    <button
      type="button"
      className="sidebar-collapse"
      onClick={toggle}
      title="Thu gọn / mở rộng menu"
      aria-label="Thu gọn hoặc mở rộng menu"
    >
      <span className="sidebar-collapse-icon" aria-hidden="true" />
    </button>
  );
}
