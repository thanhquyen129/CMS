export default function VarianceQueueLoading() {
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
          <div className="skeleton-line w-55" />
          <div className="skeleton-block tall" />
          <p className="muted">Đang tải hàng đợi chênh lệch…</p>
        </section>
      </div>
    </div>
  );
}
