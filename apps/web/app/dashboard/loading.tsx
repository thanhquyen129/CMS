export default function DashboardLoading() {
  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          CMS
          <small>Kiểm soát chi phí &amp; lợi nhuận</small>
        </div>
      </aside>
      <div className="main">
        <section className="panel panel-wide">
          <div className="skeleton-line w-40" />
          <div className="skeleton-line w-70" />
          <div className="skeleton-block" />
          <p className="muted">Đang tải bảng điều khiển…</p>
        </section>
      </div>
    </div>
  );
}
