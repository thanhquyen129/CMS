import { RatingDedupWorkspacePreview } from "@/components/RatingDedupWorkspacePreview";

export const dynamic = "force-static";

export default function RatingWorkspacePreviewPage() {
  return (
    <main className="page-main">
      <header
        className="page-header"
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          padding: "12px 20px",
          borderBottom: "1px solid var(--line, #dfe7f1)",
          background: "#fff",
        }}
      >
        <div>
          <div className="muted small">CMS · Kiểm soát chi phí</div>
          <strong>Bill LSSTEST02112026002 · Tính giá &amp; Tài chính</strong>
        </div>
        <nav className="muted small" aria-label="Breadcrumb">
          Bill / Chi tiết / Tính giá &amp; Tài chính
        </nav>
      </header>
      <aside
        aria-hidden
        style={{
          position: "fixed",
          inset: "0 auto 0 0",
          width: 248,
          background: "linear-gradient(180deg, #142f4d, #0d2946)",
          color: "#f4f8ff",
          padding: "20px 16px",
          fontSize: 13,
        }}
      >
        <div style={{ fontWeight: 700, marginBottom: 16 }}>LCMS</div>
        <div style={{ opacity: 0.85 }}>Bảng điều khiển</div>
        <div style={{ marginTop: 10, background: "rgba(255,255,255,.12)", borderRadius: 6, padding: "8px 10px" }}>
          Bill
        </div>
        <div style={{ marginTop: 8, opacity: 0.75 }}>Bảng giá</div>
        <div style={{ marginTop: 8, opacity: 0.75 }}>Chi phí</div>
        <div style={{ marginTop: 8, opacity: 0.75 }}>Doanh thu</div>
      </aside>
      <div style={{ marginLeft: 248, minHeight: "100vh", background: "var(--bg, #f5f8fc)" }}>
        <RatingDedupWorkspacePreview />
      </div>
    </main>
  );
}
