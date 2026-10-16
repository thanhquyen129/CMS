"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";

interface TocItem {
  id: string;
  title: string;
  badge?: string;
}

const TOC_ITEMS: TocItem[] = [
  { id: "tong-quan-store", title: "Tóm tắt tuân thủ App Store & Google Play", badge: "Ưu tiên" },
  { id: "gioi-thieu", title: "1. Giới thiệu và Phạm vi áp dụng" },
  { id: "thong-tin-thu-thap", title: "2. Loại thông tin chúng tôi thu thập" },
  { id: "dinh-vi-gps", title: "— Dữ liệu định vị GPS (Ứng dụng Di động)" },
  { id: "quyen-thiet-bi", title: "— Quyền truy cập thiết bị di động" },
  { id: "muc-dich", title: "3. Mục đích sử dụng thông tin" },
  { id: "phan-quyen-chia-se", title: "4. Phân quyền và Chia sẻ dữ liệu" },
  { id: "bao-mat-luu-tru", title: "5. Bảo mật và Lưu trữ dữ liệu" },
  { id: "quyen-nguoi-dung-xoa-tai-khoan", title: "6. Quyền của người dùng & Xóa tài khoản", badge: "Quan trọng" },
  { id: "thay-doi-chinh-sach", title: "7. Cập nhật và Thay đổi chính sách" },
  { id: "lien-he", title: "8. Thông tin liên hệ & Tiếp nhận khiếu nại" },
];

export function PrivacyPolicyView() {
  const [activeId, setActiveId] = useState<string>("tong-quan-store");
  const [copied, setCopied] = useState<boolean>(false);

  useEffect(() => {
    const handleScroll = () => {
      const scrollPosition = window.scrollY + 180;
      for (let i = TOC_ITEMS.length - 1; i >= 0; i--) {
        const item = TOC_ITEMS[i];
        const el = document.getElementById(item.id);
        if (el && el.offsetTop <= scrollPosition) {
          setActiveId(item.id);
          break;
        }
      }
    };

    window.addEventListener("scroll", handleScroll, { passive: true });
    return () => window.removeEventListener("scroll", handleScroll);
  }, []);

  const handleCopyLink = async () => {
    try {
      await navigator.clipboard.writeText("https://cms.lsslogistics.vn/privacy");
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    } catch {
      // Fallback nếu clipboard API bị chặn
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    }
  };

  const handlePrint = () => {
    if (typeof window !== "undefined") {
      window.print();
    }
  };

  return (
    <div className="privacy-page-container">
      {/* Top Banner & Header */}
      <header className="privacy-header">
        <div className="privacy-header-inner">
          <div className="privacy-brand-row">
            <div className="privacy-brand">
              <span className="privacy-logo-badge" aria-hidden="true">LSS</span>
              <div>
                <span className="privacy-system-name">HỆ THỐNG QUẢN LÝ CHI PHÍ LOGISTICS LSS (CMS)</span>
                <span className="privacy-doc-tag">CHÍNH SÁCH BẢO MẬT &amp; QUYỀN RIÊNG TƯ DỮ LIỆU</span>
              </div>
            </div>

            <div className="privacy-header-actions no-print">
              <Link href="/login" className="privacy-btn privacy-btn-subtle" title="Trở về trang Đăng nhập CMS">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <line x1="19" y1="12" x2="5" y2="12"></line>
                  <polyline points="12 19 5 12 12 5"></polyline>
                </svg>
                <span>Về Đăng nhập</span>
              </Link>
              <button onClick={handleCopyLink} className="privacy-btn privacy-btn-secondary" type="button" title="Sao chép liên kết chính sách">
                {copied ? (
                  <>
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#0aae7b" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <polyline points="20 6 9 17 4 12"></polyline>
                    </svg>
                    <span style={{ color: "#008b62", fontWeight: 700 }}>Đã chép link</span>
                  </>
                ) : (
                  <>
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"></path>
                      <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"></path>
                    </svg>
                    <span>Sao chép liên kết</span>
                  </>
                )}
              </button>
              <button onClick={handlePrint} className="privacy-btn privacy-btn-primary" type="button" title="In hoặc lưu dạng PDF">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <polyline points="6 9 6 2 18 2 18 9"></polyline>
                  <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path>
                  <rect x="6" y="14" width="12" height="8"></rect>
                </svg>
                <span>In / Xuất PDF</span>
              </button>
            </div>
          </div>

          <h1 className="privacy-title">Chính sách về Quyền riêng tư</h1>
          <p className="privacy-subtitle">
            Hệ thống Quản lý Chi phí Logistics LSS (CMS) — Nền tảng Web &amp; Ứng dụng Di động
          </p>

          <div className="privacy-meta-strip">
            <span className="privacy-meta-item">
              <strong>Phiên bản:</strong> v1.0
            </span>
            <span className="privacy-meta-sep">•</span>
            <span className="privacy-meta-item">
              <strong>Ngày có hiệu lực:</strong> 16/10/2026
            </span>
            <span className="privacy-meta-sep">•</span>
            <span className="privacy-meta-item">
              <strong>Phạm vi:</strong> Web CMS (<code className="privacy-code-inline">cms.lsslogistics.vn</code>) &amp; Ứng dụng Di động LSS LCMS Mobile trên Google Play Store &amp; Apple App Store
            </span>
          </div>
        </div>
      </header>

      {/* Main Content Body */}
      <div className="privacy-body">
        {/* Left Sticky Table of Contents (Desktop) */}
        <aside className="privacy-sidebar no-print">
          <nav className="privacy-toc" aria-label="Mục lục chính sách quyền riêng tư">
            <h2 className="privacy-toc-title">Mục lục điều khoản</h2>
            <ul className="privacy-toc-list">
              {TOC_ITEMS.map((item) => (
                <li key={item.id}>
                  <a
                    href={`#${item.id}`}
                    className={`privacy-toc-link ${activeId === item.id ? "is-active" : ""}`}
                    onClick={() => setActiveId(item.id)}
                  >
                    <span>{item.title}</span>
                    {item.badge ? (
                      <span className={`privacy-toc-badge ${item.badge === "Ưu tiên" ? "badge-info" : "badge-warn"}`}>
                        {item.badge}
                      </span>
                    ) : null}
                  </a>
                </li>
              ))}
            </ul>

            <div className="privacy-sidebar-card">
              <h3 className="privacy-sidebar-card-title">Hỗ trợ khẩn cấp</h3>
              <p className="privacy-sidebar-card-desc">
                Cần hỗ trợ tra soát quyền riêng tư hoặc hủy kích hoạt tài khoản?
              </p>
              <a href="mailto:privacy@lsslogistics.vn" className="privacy-sidebar-email-link">
                privacy@lsslogistics.vn
              </a>
              <div className="privacy-sidebar-hotline">
                Hotline: <strong>1900 636 789</strong>
              </div>
            </div>
          </nav>
        </aside>

        {/* Right Policy Document */}
        <main className="privacy-main">
          {/* Quick Notice for Apple & Google Store Reviewers */}
          <section id="tong-quan-store" className="privacy-section privacy-card-highlight">
            <div className="privacy-card-highlight-header">
              <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#1677e8" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
              </svg>
              <h2>Tóm tắt Tuân thủ dành cho Đơn vị Kiểm duyệt (Google Play &amp; Apple App Store)</h2>
            </div>
            <p>
              Tài liệu này là cam kết bảo vệ dữ liệu chính thức của Công ty Cổ phần Dịch vụ Logistics LSS dành cho người dùng Hệ sinh thái CMS (Logistics Cost Management System), bao gồm bản Web và Ứng dụng Di động LSS LCMS Mobile. Ứng dụng tuân thủ nghiêm ngặt các nguyên tắc sau:
            </p>
            <div className="privacy-grid-summary">
              <div className="privacy-summary-box">
                <strong>1. Dữ liệu Định vị (GPS)</strong>
                <p>Chỉ thu thập khi tài xế bắt đầu chặng vận chuyển (Leg/Movement) nhằm đối soát chi phí lộ trình và bảo đảm tiến độ. Tuyệt đối không theo dõi ngầm ngoài giờ làm việc.</p>
              </div>
              <div className="privacy-summary-box">
                <strong>2. Quyền Camera</strong>
                <p>Chỉ dùng để quét mã vận đơn (HAWB/MAWB) và chụp ảnh chứng từ giao nhận (POD)/hóa đơn hiện trường. Không truy cập thư viện ảnh cá nhân trái phép.</p>
              </div>
              <div className="privacy-summary-box">
                <strong>3. Quyền Sinh trắc học</strong>
                <p>Chỉ xác thực an toàn cục bộ trên thiết bị qua Secure Enclave/Android Keystore. Không bao giờ gửi hay lưu dữ liệu khuôn mặt/vân tay trên máy chủ.</p>
              </div>
              <div className="privacy-summary-box">
                <strong>4. Xóa Tài khoản &amp; Dữ liệu</strong>
                <p>Hỗ trợ đầy đủ quyền gửi yêu cầu xóa tài khoản trực tiếp trong ứng dụng di động hoặc qua email bảo mật. Xử lý vô hiệu hóa trong 07 ngày làm việc.</p>
              </div>
            </div>
          </section>

          {/* Section 1 */}
          <section id="gioi-thieu" className="privacy-section">
            <h2 className="privacy-section-heading">
              <span className="privacy-heading-num">1</span>
              <span>Giới thiệu và Phạm vi áp dụng</span>
            </h2>
            <div className="privacy-prose">
              <p>
                <strong>Công ty Cổ phần Dịch vụ Logistics LSS</strong> (&quot;LSS&quot;, &quot;chúng tôi&quot;) là đơn vị phát triển và vận hành <strong>Hệ thống Quản lý Chi phí Logistics (Cost Management System — CMS / LCMS)</strong>. Hệ thống được thiết kế nhằm phục vụ việc kiểm soát tài chính, dự toán và phân bổ chi phí, quản lý dòng tiền vận tải, đối soát chứng từ, quản lý công nợ nhà vận chuyển (AP) và công nợ khách hàng (AR).
              </p>
              <p>
                Chính sách về Quyền riêng tư này áp dụng đối với tất cả cá nhân và tổ chức truy cập hoặc sử dụng các nền tảng thuộc hệ sinh thái CMS của LSS, bao gồm:
              </p>
              <ul>
                <li><strong>Nền tảng Quản trị Web CMS:</strong> Truy cập tại địa chỉ <code className="privacy-code-inline">https://cms.lsslogistics.vn</code> dành cho Quản trị viên, Cán bộ Kiểm soát Tài chính, Kế toán Chi phí, Kế toán Doanh thu và Nhân viên Điều phối.</li>
                <li><strong>Ứng dụng Di động LSS LCMS Mobile:</strong> Được phân phối chính thức trên Google Play Store (dành cho thiết bị Android) và Apple App Store (dành cho thiết bị iOS), phục vụ Đội ngũ Tài xế, Điều phối viên hiện trường và Đại diện Đối tác vận chuyển.</li>
              </ul>
              <p>
                Chúng tôi cam kết tôn trọng, bảo vệ toàn vẹn thông tin định danh cá nhân (PII) cũng như dữ liệu tài chính, nghiệp vụ vận tải của Quý khách hàng và Đối tác theo đúng quy định của pháp luật Việt Nam (bao gồm Nghị định 13/2023/NĐ-CP về bảo vệ dữ liệu cá nhân) và các tiêu chuẩn bảo mật quốc tế.
              </p>
            </div>
          </section>

          {/* Section 2 */}
          <section id="thong-tin-thu-thap" className="privacy-section">
            <h2 className="privacy-section-heading">
              <span className="privacy-heading-num">2</span>
              <span>Loại thông tin chúng tôi thu thập</span>
            </h2>
            <div className="privacy-prose">
              <p>
                Để cung cấp dịch vụ kiểm soát tài chính và quản lý chuỗi vận hành chính xác, chúng tôi chỉ thu thập các nhóm thông tin cần thiết tối thiểu cho nghiệp vụ, được phân nhóm cụ thể như sau:
              </p>

              <div className="privacy-table-wrapper">
                <table className="privacy-data-table">
                  <thead>
                    <tr>
                      <th style={{ width: "22%" }}>Nhóm thông tin</th>
                      <th style={{ width: "40%" }}>Dữ liệu chi tiết thu thập</th>
                      <th style={{ width: "38%" }}>Mục đích nghiệp vụ</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td><strong>Thông tin tài khoản &amp; Định danh (PII)</strong></td>
                      <td>Họ và tên, địa chỉ email công việc, số điện thoại, vai trò nghiệp vụ (Role), phòng ban / chi nhánh, ảnh đại diện tài khoản (nếu cung cấp).</td>
                      <td>Xác thực đăng nhập, phân quyền dữ liệu (Data Scope), định danh người tạo và người phê duyệt chi phí (Maker-Checker).</td>
                    </tr>
                    <tr>
                      <td><strong>Dữ liệu vận hành &amp; Chứng từ tài chính</strong></td>
                      <td>Mã chuyến hàng, số vận đơn (HAWB/MAWB), ảnh chụp biên bản bàn giao (POD), hóa đơn giá trị gia tăng, phiếu chi phụ phí, cước đường bộ, phiếu xăng dầu.</td>
                      <td>Thực hiện đối soát ba bên (Matching), ghi nhận chi phí thực tế (Actual Cost), kiểm soát công nợ phải trả/phải thu (AP/AR).</td>
                    </tr>
                    <tr>
                      <td><strong>Nhật ký hoạt động (Audit Trail)</strong></td>
                      <td>Địa chỉ IP, dấu thời gian thực hiện thao tác, loại hành động (duyệt, từ chối, điều chỉnh giá cước, khóa sổ kỳ tài chính), thông tin thiết bị truy cập.</td>
                      <td>Kiểm toán độc lập, đảm bảo trách nhiệm giải trình, ngăn ngừa gian lận tài chính nội bộ theo nguyên tắc Phân tách nhiệm vụ (SoD).</td>
                    </tr>
                    <tr>
                      <td><strong>Dữ liệu thiết bị di động</strong></td>
                      <td>Mã định danh thiết bị ẩn danh (UUID), hệ điều hành (iOS / Android), phiên bản ứng dụng, trạng thái kết nối mạng.</td>
                      <td>Gửi thông báo đẩy (Push Notification) về trạng thái phê duyệt chi phí, cảnh báo sai lệch chứng từ và duy trì phiên làm việc an toàn.</td>
                    </tr>
                  </tbody>
                </table>
              </div>

              {/* Sub-section: GPS */}
              <div id="dinh-vi-gps" className="privacy-subcard">
                <div className="privacy-subcard-title">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#ff7a00" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"></path>
                    <circle cx="12" cy="10" r="3"></circle>
                  </svg>
                  <h3>Dữ liệu định vị vị trí địa lý (GPS Tracking) — Dành riêng cho Ứng dụng Di động</h3>
                </div>
                <p>
                  Đối với tài xế và nhân sự vận hành sử dụng ứng dụng di động LSS LCMS Mobile, hệ thống có thể yêu cầu quyền truy cập vị trí địa lý (GPS). Chúng tôi áp dụng chính sách thu thập vị trí với các nguyên tắc minh bạch tuyệt đối:
                </p>
                <ul>
                  <li>
                    <strong>Chỉ thu thập khi đang trong nhiệm vụ:</strong> Hệ thống chỉ ghi nhận vị trí GPS khi tài xế chủ động bấm nút <em>&quot;Bắt đầu chặng vận chuyển&quot; (Start Movement/Leg)</em> và duy trì cho đến khi xác nhận <em>&quot;Hoàn tất giao hàng&quot; (Complete Leg)</em>.
                  </li>
                  <li>
                    <strong>Tuyệt đối không theo dõi ngoài giờ:</strong> Khi tài xế kết thúc chặng hoặc đăng xuất khỏi ứng dụng, toàn bộ tiến trình ghi nhận vị trí sẽ lập tức ngắt hoàn toàn. Ứng dụng không giám sát đời tư cá nhân của người lao động.
                  </li>
                  <li>
                    <strong>Mục đích tài chính - vận hành:</strong> Vị trí được dùng để đối soát quãng đường thực tế di chuyển, xác thực việc đi qua các trạm thu phí BOT tương ứng với vé cước được yêu cầu thanh toán, phát hiện lộ trình bất thường và tính toán mức tiêu hao nhiên liệu định mức.
                  </li>
                </ul>
              </div>

              {/* Sub-section: Device Permissions */}
              <div id="quyen-thiet-bi" className="privacy-subcard">
                <div className="privacy-subcard-title">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#1677e8" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <rect x="5" y="2" width="14" height="20" rx="2" ry="2"></rect>
                    <line x1="12" y1="18" x2="12.01" y2="18"></line>
                  </svg>
                  <h3>Quyền truy cập phần cứng trên thiết bị di động (Camera &amp; Sinh trắc học)</h3>
                </div>
                <ul>
                  <li>
                    <strong>Quyền Camera:</strong> Cho phép người dùng quét nhanh mã vạch (Barcode / QR Code) trên phiếu gửi hàng, tem nhãn pallet hoặc chụp chứng từ giao nhận (biên bản bàn giao, hóa đơn trạm dừng nghỉ). Chúng tôi không bao giờ tự ý kích hoạt Camera khi không có sự tương tác trực tiếp của người dùng.
                  </li>
                  <li>
                    <strong>Quyền Xác thực sinh trắc học (Face ID / Vân tay):</strong> Cho phép đăng nhập nhanh và ký xác nhận điện tử trước khi gửi duyệt các khoản chi lớn. Cơ chế này được xử lý hoàn toàn nội bộ trên phần cứng an toàn của điện thoại (Apple Secure Enclave hoặc Android Biometric Keystore). Máy chủ LSS không bao giờ tiếp cận, lưu trữ hoặc truyền tải dữ liệu khuôn mặt hay vân tay thô của người dùng.
                  </li>
                </ul>
              </div>
            </div>
          </section>

          {/* Section 3 */}
          <section id="muc-dich" className="privacy-section">
            <h2 className="privacy-section-heading">
              <span className="privacy-heading-num">3</span>
              <span>Mục đích sử dụng thông tin</span>
            </h2>
            <div className="privacy-prose">
              <p>Mọi thông tin được thu thập trong hệ sinh thái CMS LSS đều phục vụ cho các mục đích cụ thể, hợp pháp và được giới hạn chặt chẽ:</p>
              <div className="privacy-purposes-grid">
                <div className="privacy-purpose-item">
                  <div className="privacy-purpose-icon">🔒</div>
                  <div>
                    <h4>1. Xác thực và Phân quyền Data Scope</h4>
                    <p>Đảm bảo mỗi người dùng chỉ nhìn thấy dữ liệu tài chính, giá cước và chứng từ nằm trong phạm vi được chỉ định theo đúng vai trò nghiệp vụ.</p>
                  </div>
                </div>
                <div className="privacy-purpose-item">
                  <div className="privacy-purpose-icon">📊</div>
                  <div>
                    <h4>2. Kiểm soát chi phí chu trình khép kín</h4>
                    <p>Theo dõi 3 trạng thái của dòng tiền vận tải: Dự toán ban đầu (Expected) → Xác nhận trung gian (Confirmed) → Thực tế phát sinh (Actual).</p>
                  </div>
                </div>
                <div className="privacy-purpose-item">
                  <div className="privacy-purpose-icon">⚡</div>
                  <div>
                    <h4>3. Đối soát tự động (Document Matching)</h4>
                    <p>Khớp nối tự động giữa chứng từ gốc hiện trường, hóa đơn nhà cung cấp và số liệu hợp đồng cước nhằm loại bỏ sai lệch và ngăn chặn gian lận.</p>
                  </div>
                </div>
                <div className="privacy-purpose-item">
                  <div className="privacy-purpose-icon">⚖️</div>
                  <div>
                    <h4>4. Tuân thủ chế độ Kế toán và Pháp lý</h4>
                    <p>Lưu trữ và phục vụ việc lập báo cáo tài chính, quyết toán thuế doanh nghiệp, kiểm toán độc lập theo đúng quy chuẩn kế toán Việt Nam hiện hành.</p>
                  </div>
                </div>
              </div>
            </div>
          </section>

          {/* Section 4 */}
          <section id="phan-quyen-chia-se" className="privacy-section">
            <h2 className="privacy-section-heading">
              <span className="privacy-heading-num">4</span>
              <span>Phân quyền và Chia sẻ dữ liệu</span>
            </h2>
            <div className="privacy-prose">
              <div className="privacy-alert-box">
                <strong>Cam kết bảo vệ dữ liệu:</strong> LSS cam kết tuyệt đối <strong>KHÔNG BÁN, KHÔNG CHO THUÊ VÀ KHÔNG THƯƠNG MẠI HÓA</strong> bất kỳ dữ liệu cá nhân hay dữ liệu tài chính của người dùng cho bất kỳ bên thứ ba nào vì mục đích quảng cáo hoặc tiếp thị.
              </div>

              <h3>Kiểm soát nội bộ theo nguyên tắc Maker-Checker &amp; Phân tách nhiệm vụ (SoD)</h3>
              <p>
                Dữ liệu chỉ được truy cập nội bộ bởi nhân sự có thẩm quyền theo chính sách bảo mật đa tầng:
              </p>
              <ul>
                <li><strong>Quản trị viên (Admin):</strong> Cấu hình hệ thống, quản lý danh mục và danh sách tài khoản; không can thiệp trái phép vào chứng từ kế toán đã khóa sổ.</li>
                <li><strong>Kiểm soát tài chính &amp; Kế toán (Controller / Accountant):</strong> Chỉ truy cập dữ liệu chứng từ, công nợ thuộc chi nhánh hoặc hợp đồng được giao phụ trách.</li>
                <li><strong>Điều phối viên &amp; Tài xế (Dispatcher / Driver):</strong> Chỉ tiếp cận thông tin vận đơn, điểm nhận/trả hàng và chi phí phụ cấp chặng được phân công, hoàn toàn không được quyền xem biên độ lợi nhuận hay doanh thu tổng của doanh nghiệp.</li>
              </ul>

              <h3>Chia sẻ với các đối tác kỹ thuật và cơ quan chức năng</h3>
              <p>Chúng tôi chỉ chia sẻ dữ liệu trong các trường hợp giới hạn sau:</p>
              <ul>
                <li><strong>Nhà cung cấp hạ tầng đám mây:</strong> Dữ liệu được lưu trữ trên hệ thống máy chủ chuyên dụng được bảo vệ nghiêm ngặt, đáp ứng các tiêu chuẩn an toàn an ninh mạng.</li>
                <li><strong>Ngân hàng &amp; Cổng thanh toán:</strong> Khi thực hiện các lệnh chi hộ / thanh toán tự động theo ủy thác hợp lệ từ phía doanh nghiệp người dùng.</li>
                <li><strong>Yêu cầu pháp lý:</strong> Khi có văn bản yêu cầu chính thức, hợp pháp từ Cơ quan Thuế, Hải quan, Viện Kiểm sát hoặc Tòa án theo đúng trình tự pháp luật Việt Nam.</li>
              </ul>
            </div>
          </section>

          {/* Section 5 */}
          <section id="bao-mat-luu-tru" className="privacy-section">
            <h2 className="privacy-section-heading">
              <span className="privacy-heading-num">5</span>
              <span>Bảo mật và Lưu trữ dữ liệu (Data Security)</span>
            </h2>
            <div className="privacy-prose">
              <p>Hệ sinh thái CMS LSS triển khai đồng bộ các biện pháp an ninh kỹ thuật chuyên sâu:</p>

              <div className="privacy-security-grid">
                <div className="privacy-security-card">
                  <h4>Mã hóa khi truyền tải (In-Transit)</h4>
                  <p>100% kết nối giữa Web/App và hệ thống máy chủ được mã hóa qua HTTPS bằng giao thức TLS 1.2 / TLS 1.3. Ứng dụng di động được trang bị cơ chế khóa chứng chỉ SSL (SSL Pinning) nhằm ngăn chặn triệt để các hình thức tấn công trung gian (Man-in-the-Middle).</p>
                </div>

                <div className="privacy-security-card">
                  <h4>Mã hóa khi lưu trữ (At-Rest)</h4>
                  <p>Mật khẩu người dùng được băm (hash) bằng thuật toán an toàn một chiều chuẩn công nghiệp. Các thông tin tài chính nhạy cảm được mã hóa an toàn trong cơ sở dữ liệu chuyên biệt.</p>
                </div>

                <div className="privacy-security-card">
                  <h4>Cách ly dữ liệu đa người thuê (Tenant Isolation)</h4>
                  <p>Mọi truy vấn dữ liệu, tác vụ nền và xử lý tích hợp đều được gắn thẻ và cách ly theo mã định danh doanh nghiệp. Tuyệt đối không xảy ra tình trạng rò rỉ dữ liệu chéo giữa các khách hàng tổ chức khác nhau.</p>
                </div>

                <div className="privacy-security-card">
                  <h4>Bảo mật phiên làm việc</h4>
                  <p>Trình duyệt Web sử dụng cookie bảo mật với thuộc tính HttpOnly, Secure và SameSite để chống tấn công XSS. Ứng dụng di động lưu trữ token phiên trong phân vùng an toàn của phần cứng (iOS Keychain / Android Encrypted Storage).</p>
                </div>
              </div>

              <h3>Thời hạn lưu trữ dữ liệu</h3>
              <ul>
                <li><strong>Dữ liệu tài khoản cá nhân:</strong> Duy trì trong suốt thời gian tài khoản hoạt động và được xóa bỏ hoặc ẩn danh theo quy định khi có yêu cầu hủy tài khoản.</li>
                <li><strong>Dữ liệu chứng từ tài chính &amp; Hóa đơn:</strong> Lưu trữ tối thiểu theo quy định bắt buộc của <em>Luật Kế toán Việt Nam</em> (tối thiểu từ 05 đến 10 năm tùy nhóm chứng từ) nhằm phục vụ quyết toán thuế và thanh tra kiểm toán.</li>
                <li><strong>Dữ liệu tọa độ GPS chi tiết:</strong> Được lưu trữ trong suốt thời gian chuyến hàng diễn ra. Sau khi hoàn tất đối soát và quyết toán tài chính chuyến xe, dữ liệu định vị chi tiết sẽ được nén và tổng hợp thành báo cáo hành trình tổng quan nhằm bảo vệ tối đa quyền riêng tư.</li>
              </ul>
            </div>
          </section>

          {/* Section 6 - Account Deletion */}
          <section id="quyen-nguoi-dung-xoa-tai-khoan" className="privacy-section privacy-card-deletion">
            <h2 className="privacy-section-heading">
              <span className="privacy-heading-num">6</span>
              <span>Quyền của người dùng &amp; Cơ chế Yêu cầu Xóa tài khoản</span>
            </h2>
            <div className="privacy-prose">
              <p>
                Nhằm tuân thủ các quy định khắt khe của <strong>Apple App Store Review Guidelines (Điều khoản 5.1.1)</strong> và <strong>Google Play Developer Policy (Data Safety &amp; Account Deletion)</strong>, chúng tôi trao cho Quý người dùng toàn quyền kiểm soát thông tin cá nhân của mình:
              </p>

              <h3>Các quyền lợi cơ bản của người dùng</h3>
              <ul>
                <li><strong>Quyền tra cứu và sao chép:</strong> Bạn có quyền xem lại thông tin hồ sơ tài khoản và lịch sử thao tác của mình trên hệ thống.</li>
                <li><strong>Quyền chỉnh sửa:</strong> Bạn có quyền yêu cầu cập nhật, đính chính các thông tin định danh chưa chính xác.</li>
                <li><strong>Quyền thu hồi quyền ứng dụng:</strong> Bạn có thể bật/tắt quyền truy cập Vị trí GPS, Camera, Thông báo đẩy bất kỳ lúc nào trong phần Cài đặt quyền riêng tư trên điện thoại.</li>
              </ul>

              <div className="privacy-deletion-guide">
                <div className="privacy-deletion-guide-header">
                  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#f32945" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <polyline points="3 6 5 6 21 6"></polyline>
                    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                    <line x1="10" y1="11" x2="10" y2="17"></line>
                    <line x1="14" y1="11" x2="14" y2="17"></line>
                  </svg>
                  <h3>Quy trình Yêu cầu Xóa tài khoản và Dữ liệu cá nhân (Account Deletion Request)</h3>
                </div>
                <p>
                  Khi người dùng không còn nhu cầu sử dụng hoặc chấm dứt hợp đồng lao động/hợp tác với doanh nghiệp, người dùng có quyền yêu cầu xóa bỏ tài khoản thông qua một trong hai hình thức thuận tiện sau:
                </p>

                <div className="privacy-deletion-methods">
                  <div className="privacy-deletion-method-card">
                    <span className="privacy-method-badge">Cách 1: Trực tiếp trên App Mobile</span>
                    <h4>Thực hiện trong Ứng dụng Di động LSS</h4>
                    <ol>
                      <li>Mở ứng dụng <strong>LSS LCMS Mobile</strong> trên điện thoại.</li>
                      <li>Vào tab <strong>Mô-đun (Modules)</strong> → Chọn <strong>Cài đặt &amp; Hồ sơ cá nhân</strong>.</li>
                      <li>Kéo xuống mục <strong>Bảo mật &amp; Quyền riêng tư</strong>.</li>
                      <li>Nhấn vào nút <strong>&quot;Yêu cầu xóa tài khoản&quot;</strong>, nhập mật khẩu hiện tại để xác nhận.</li>
                    </ol>
                    <p className="privacy-method-note">Hệ thống sẽ ghi nhận và thực hiện vô hiệu hóa phiên làm việc ngay lập tức.</p>
                  </div>

                  <div className="privacy-deletion-method-card">
                    <span className="privacy-method-badge">Cách 2: Gửi yêu cầu qua Email</span>
                    <h4>Gửi Thư điện tử đến Bộ phận Bảo mật</h4>
                    <p>Gửi email từ chính địa chỉ email đã đăng ký tài khoản trong hệ thống đến:</p>
                    <div className="privacy-email-highlight">
                      <code>privacy@lsslogistics.vn</code> hoặc <code>hotro@lsslogistics.vn</code>
                    </div>
                    <p><strong>Tiêu đề:</strong> <em>[Yêu cầu xóa tài khoản CMS LSS] - [Họ tên] - [Số điện thoại]</em></p>
                    <p><strong>Nội dung:</strong> Nêu rõ email đăng ký, tên đơn vị trực thuộc và yêu cầu hủy kích hoạt tài khoản.</p>
                  </div>
                </div>

                <div className="privacy-deletion-notice">
                  <h4>Thời gian xử lý và Quy định về Dữ liệu Kế toán Bắt buộc</h4>
                  <ul>
                    <li>
                      <strong>Thời hạn hoàn tất:</strong> Đội ngũ quản trị an ninh sẽ xác thực thông tin và tiến hành vô hiệu hóa, hủy bỏ liên kết dữ liệu trong vòng <strong>tối đa 07 ngày làm việc</strong> kể từ ngày tiếp nhận yêu cầu hợp lệ.
                    </li>
                    <li>
                      <strong>Lưu ý bắt buộc về quy chuẩn kế toán:</strong> Theo quy định của <em>Luật Kế toán Việt Nam</em> và <em>Luật Quản lý Thuế</em>, các hóa đơn, chứng từ tài chính, biên bản giao nhận đã ký duyệt và các giao dịch thanh toán mà người dùng đã từng thực hiện trong quá khứ <strong>không thể bị xóa bỏ hoàn toàn</strong> nhằm bảo vệ tính toàn vẹn của sổ sách kế toán doanh nghiệp. Trong trường hợp này, hệ thống sẽ thực hiện <strong>ẩn danh hóa (anonymization)</strong> toàn bộ thông tin cá nhân và ngắt kết nối vĩnh viễn tài khoản người dùng khỏi hệ thống.
                    </li>
                  </ul>
                </div>
              </div>
            </div>
          </section>

          {/* Section 7 */}
          <section id="thay-doi-chinh-sach" className="privacy-section">
            <h2 className="privacy-section-heading">
              <span className="privacy-heading-num">7</span>
              <span>Cập nhật và Thay đổi chính sách</span>
            </h2>
            <div className="privacy-prose">
              <p>
                LSS có thể định kỳ cập nhật, điều chỉnh Chính sách về Quyền riêng tư này để phản ánh các cải tiến về tính năng kỹ thuật của hệ thống, bổ sung nghiệp vụ quản lý chi phí mới hoặc đáp ứng các thay đổi trong quy định pháp luật của cơ quan nhà nước.
              </p>
              <p>
                Mọi thay đổi quan trọng sẽ được thông báo công khai trên trang chủ Web CMS (<code className="privacy-code-inline">https://cms.lsslogistics.vn/privacy</code>) và gửi thông báo trực tiếp qua ứng dụng di động trước khi chính sách mới chính thức có hiệu lực thi hành. Việc Quý khách tiếp tục sử dụng hệ thống sau ngày có hiệu lực đồng nghĩa với việc Quý khách đã hiểu rõ và đồng ý với các nội dung cập nhật đó.
              </p>
            </div>
          </section>

          {/* Section 8 - Contact */}
          <section id="lien-he" className="privacy-section privacy-card-contact">
            <h2 className="privacy-section-heading">
              <span className="privacy-heading-num">8</span>
              <span>Thông tin liên hệ &amp; Tiếp nhận khiếu nại</span>
            </h2>
            <div className="privacy-prose">
              <p>
                Nếu Quý người dùng có bất kỳ thắc mắc, đề xuất, hoặc yêu cầu giải quyết khiếu nại liên quan đến việc thu thập và bảo mật thông tin cá nhân, xin vui lòng liên hệ với Ban Bảo mật Dữ liệu LSS qua các kênh chính thức sau:
              </p>

              <div className="privacy-contact-grid">
                <div className="privacy-contact-box">
                  <span className="privacy-contact-label">Đơn vị chủ quản:</span>
                  <strong>CÔNG TY CỔ PHẦN DỊCH VỤ LOGISTICS LSS</strong>
                  <span className="privacy-contact-desc">Hệ thống Quản lý Chi phí Logistics (LSS CMS)</span>
                </div>

                <div className="privacy-contact-box">
                  <span className="privacy-contact-label">Bộ phận chuyên trách:</span>
                  <strong>Ban Bảo mật &amp; Tuân thủ Dữ liệu (Privacy &amp; Compliance)</strong>
                  <span className="privacy-contact-desc">Tiếp nhận tra soát và giải quyết khiếu nại quyền riêng tư</span>
                </div>

                <div className="privacy-contact-box">
                  <span className="privacy-contact-label">Hòm thư chuyên trách Quyền riêng tư:</span>
                  <a href="mailto:privacy@lsslogistics.vn" className="privacy-contact-link">privacy@lsslogistics.vn</a>
                  <span className="privacy-contact-desc">Thời gian phản hồi thông thường: Trong vòng 24 - 48 giờ</span>
                </div>

                <div className="privacy-contact-box">
                  <span className="privacy-contact-label">Hỗ trợ kỹ thuật &amp; Vận hành:</span>
                  <a href="mailto:hotro@lsslogistics.vn" className="privacy-contact-link">hotro@lsslogistics.vn</a>
                  <span className="privacy-contact-desc">Đường dây nóng hỗ trợ: <strong>1900 636 789</strong></span>
                </div>
              </div>

              <div className="privacy-footer-note">
                <p>
                  © 2026 LSS Logistics. Tất cả các quyền được bảo lưu. Chính sách có hiệu lực áp dụng thống nhất cho toàn bộ hệ thống Web CMS và Ứng dụng Di động LSS LCMS Mobile trên toàn lãnh thổ Việt Nam.
                </p>
              </div>
            </div>
          </section>
        </main>
      </div>

      {/* Scoped CSS styling matching LSS Design Tokens */}
      <style jsx>{`
        .privacy-page-container {
          min-height: 100vh;
          background: #f8fafc;
          color: #0f172a;
          font-family: var(--font-be-vietnam, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif);
          line-height: 1.65;
          padding-bottom: 4rem;
        }

        .privacy-header {
          background: #ffffff;
          border-bottom: 1px solid #e2e8f0;
          box-shadow: 0 1px 3px rgba(15, 23, 42, 0.04);
          padding: 2.25rem 1.5rem 2rem;
        }

        .privacy-header-inner {
          max-width: 1200px;
          margin: 0 auto;
        }

        .privacy-brand-row {
          display: flex;
          align-items: center;
          justify-content: space-between;
          flex-wrap: wrap;
          gap: 1rem;
          margin-bottom: 1.25rem;
        }

        .privacy-brand {
          display: flex;
          align-items: center;
          gap: 0.85rem;
        }

        .privacy-logo-badge {
          background: #0f172a;
          color: #ffffff;
          font-weight: 800;
          font-size: 1rem;
          padding: 0.35rem 0.75rem;
          border-radius: 6px;
          letter-spacing: 0.05em;
        }

        .privacy-system-name {
          display: block;
          font-size: 0.75rem;
          font-weight: 800;
          color: #1e293b;
          letter-spacing: 0.04em;
        }

        .privacy-doc-tag {
          display: block;
          font-size: 0.7rem;
          font-weight: 600;
          color: #64748b;
          text-transform: uppercase;
          letter-spacing: 0.03em;
        }

        .privacy-header-actions {
          display: flex;
          align-items: center;
          gap: 0.65rem;
        }

        .privacy-btn {
          display: inline-flex;
          align-items: center;
          gap: 0.45rem;
          padding: 0.5rem 0.9rem;
          border-radius: 7px;
          font-size: 0.82rem;
          font-weight: 600;
          cursor: pointer;
          transition: all 0.15s ease;
          border: 1px solid transparent;
          text-decoration: none;
        }

        .privacy-btn-subtle {
          background: #f1f5f9;
          color: #334155;
          border-color: #e2e8f0;
        }
        .privacy-btn-subtle:hover {
          background: #e2e8f0;
          color: #0f172a;
        }

        .privacy-btn-secondary {
          background: #ffffff;
          color: #1e293b;
          border-color: #cbd5e1;
        }
        .privacy-btn-secondary:hover {
          background: #f8fafc;
          border-color: #94a3b8;
        }

        .privacy-btn-primary {
          background: #1677e8;
          color: #ffffff;
        }
        .privacy-btn-primary:hover {
          background: #0f63c7;
        }

        .privacy-title {
          font-size: 2rem;
          font-weight: 800;
          color: #0f172a;
          margin: 0 0 0.5rem;
          line-height: 1.25;
          letter-spacing: -0.02em;
        }

        .privacy-subtitle {
          font-size: 1.05rem;
          color: #475569;
          margin: 0 0 1.25rem;
        }

        .privacy-meta-strip {
          display: flex;
          align-items: center;
          flex-wrap: wrap;
          gap: 0.5rem;
          font-size: 0.82rem;
          color: #64748b;
          background: #f8fafc;
          padding: 0.65rem 1rem;
          border-radius: 8px;
          border: 1px solid #e2e8f0;
        }

        .privacy-meta-sep {
          color: #cbd5e1;
        }

        .privacy-code-inline {
          font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
          background: #e2e8f0;
          color: #0f172a;
          padding: 0.15rem 0.4rem;
          border-radius: 4px;
          font-size: 0.78rem;
        }

        /* Body Layout */
        .privacy-body {
          max-width: 1200px;
          margin: 2rem auto 0;
          padding: 0 1.5rem;
          display: grid;
          grid-template-columns: 290px minmax(0, 1fr);
          gap: 2.25rem;
          align-items: start;
        }

        /* Sidebar TOC */
        .privacy-sidebar {
          position: sticky;
          top: 1.5rem;
        }

        .privacy-toc {
          background: #ffffff;
          border: 1px solid #e2e8f0;
          border-radius: 10px;
          padding: 1.25rem;
          box-shadow: 0 1px 3px rgba(15, 23, 42, 0.04);
        }

        .privacy-toc-title {
          font-size: 0.85rem;
          font-weight: 800;
          color: #64748b;
          text-transform: uppercase;
          letter-spacing: 0.05em;
          margin: 0 0 0.85rem;
        }

        .privacy-toc-list {
          list-style: none;
          margin: 0;
          padding: 0;
          display: flex;
          flex-direction: column;
          gap: 0.35rem;
        }

        .privacy-toc-link {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 0.45rem 0.65rem;
          border-radius: 6px;
          font-size: 0.8rem;
          color: #334155;
          text-decoration: none;
          transition: all 0.15s ease;
          line-height: 1.35;
        }

        .privacy-toc-link:hover {
          background: #f1f5f9;
          color: #1677e8;
        }

        .privacy-toc-link.is-active {
          background: #e9f4ff;
          color: #1677e8;
          font-weight: 700;
        }

        .privacy-toc-badge {
          font-size: 0.65rem;
          font-weight: 700;
          padding: 0.15rem 0.45rem;
          border-radius: 10px;
          text-transform: uppercase;
        }

        .badge-info {
          background: #e0f2fe;
          color: #0369a1;
        }

        .badge-warn {
          background: #fef3c7;
          color: #b45309;
        }

        .privacy-sidebar-card {
          margin-top: 1.25rem;
          padding: 1rem;
          background: #f8fafc;
          border-radius: 8px;
          border: 1px solid #e2e8f0;
        }

        .privacy-sidebar-card-title {
          font-size: 0.8rem;
          font-weight: 700;
          color: #0f172a;
          margin: 0 0 0.35rem;
        }

        .privacy-sidebar-card-desc {
          font-size: 0.75rem;
          color: #64748b;
          margin: 0 0 0.65rem;
        }

        .privacy-sidebar-email-link {
          display: block;
          font-size: 0.8rem;
          font-weight: 700;
          color: #1677e8;
          text-decoration: none;
          margin-bottom: 0.35rem;
        }

        .privacy-sidebar-email-link:hover {
          text-decoration: underline;
        }

        .privacy-sidebar-hotline {
          font-size: 0.75rem;
          color: #334155;
        }

        /* Main Content */
        .privacy-main {
          display: flex;
          flex-direction: column;
          gap: 2rem;
        }

        .privacy-section {
          background: #ffffff;
          border: 1px solid #e2e8f0;
          border-radius: 12px;
          padding: 2rem;
          box-shadow: 0 1px 3px rgba(15, 23, 42, 0.04);
        }

        .privacy-section-heading {
          display: flex;
          align-items: center;
          gap: 0.75rem;
          font-size: 1.35rem;
          font-weight: 800;
          color: #0f172a;
          margin: 0 0 1.25rem;
          padding-bottom: 0.75rem;
          border-bottom: 2px solid #f1f5f9;
          letter-spacing: -0.01em;
        }

        .privacy-heading-num {
          display: inline-flex;
          align-items: center;
          justify-content: center;
          width: 32px;
          height: 32px;
          border-radius: 8px;
          background: #1677e8;
          color: #ffffff;
          font-size: 0.95rem;
          font-weight: 800;
          flex-shrink: 0;
        }

        .privacy-prose {
          font-size: 0.92rem;
          color: #334155;
          line-height: 1.7;
        }

        .privacy-prose p {
          margin: 0 0 1rem;
        }

        .privacy-prose ul, .privacy-prose ol {
          margin: 0 0 1.25rem;
          padding-left: 1.35rem;
        }

        .privacy-prose li {
          margin-bottom: 0.5rem;
        }

        .privacy-prose h3 {
          font-size: 1.08rem;
          font-weight: 700;
          color: #0f172a;
          margin: 1.5rem 0 0.75rem;
        }

        /* Highlight Card for Store Reviewers */
        .privacy-card-highlight {
          border-left: 4px solid #1677e8;
          background: linear-gradient(180deg, #ffffff, #fbfcfe);
        }

        .privacy-card-highlight-header {
          display: flex;
          align-items: center;
          gap: 0.65rem;
          margin-bottom: 0.85rem;
        }

        .privacy-card-highlight-header h2 {
          font-size: 1.15rem;
          font-weight: 800;
          color: #0f172a;
          margin: 0;
        }

        .privacy-grid-summary {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
          gap: 1rem;
          margin-top: 1.25rem;
        }

        .privacy-summary-box {
          background: #f8fafc;
          border: 1px solid #e2e8f0;
          border-radius: 8px;
          padding: 1rem;
        }

        .privacy-summary-box strong {
          display: block;
          font-size: 0.85rem;
          color: #0f172a;
          margin-bottom: 0.35rem;
        }

        .privacy-summary-box p {
          font-size: 0.8rem;
          color: #475569;
          margin: 0;
          line-height: 1.5;
        }

        /* Table */
        .privacy-table-wrapper {
          overflow-x: auto;
          margin: 1.25rem 0 1.75rem;
          border: 1px solid #e2e8f0;
          border-radius: 8px;
        }

        .privacy-data-table {
          width: 100%;
          border-collapse: collapse;
          text-align: left;
          font-size: 0.85rem;
        }

        .privacy-data-table th {
          background: #f1f5f9;
          color: #1e293b;
          font-weight: 700;
          padding: 0.75rem 1rem;
          border-bottom: 1px solid #cbd5e1;
        }

        .privacy-data-table td {
          padding: 0.75rem 1rem;
          border-bottom: 1px solid #e2e8f0;
          vertical-align: top;
          color: #334155;
        }

        .privacy-data-table tr:last-child td {
          border-bottom: none;
        }

        /* Subcards */
        .privacy-subcard {
          background: #f8fafc;
          border: 1px solid #e2e8f0;
          border-radius: 10px;
          padding: 1.25rem;
          margin: 1.5rem 0;
        }

        .privacy-subcard-title {
          display: flex;
          align-items: center;
          gap: 0.5rem;
          margin-bottom: 0.75rem;
        }

        .privacy-subcard-title h3 {
          font-size: 1rem;
          font-weight: 700;
          color: #0f172a;
          margin: 0;
        }

        /* Purposes Grid */
        .privacy-purposes-grid {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
          gap: 1.25rem;
          margin-top: 1rem;
        }

        .privacy-purpose-item {
          display: flex;
          gap: 0.85rem;
          background: #f8fafc;
          border: 1px solid #e2e8f0;
          border-radius: 8px;
          padding: 1.15rem;
        }

        .privacy-purpose-icon {
          font-size: 1.5rem;
          line-height: 1;
        }

        .privacy-purpose-item h4 {
          font-size: 0.92rem;
          font-weight: 700;
          color: #0f172a;
          margin: 0 0 0.35rem;
        }

        .privacy-purpose-item p {
          font-size: 0.8rem;
          color: #475569;
          margin: 0;
          line-height: 1.5;
        }

        /* Alert Box */
        .privacy-alert-box {
          background: #eff6ff;
          border: 1px solid #bfdbfe;
          border-radius: 8px;
          padding: 1rem 1.25rem;
          font-size: 0.88rem;
          color: #1e40af;
          margin-bottom: 1.25rem;
        }

        /* Security Grid */
        .privacy-security-grid {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
          gap: 1rem;
          margin: 1.25rem 0 1.5rem;
        }

        .privacy-security-card {
          background: #f8fafc;
          border: 1px solid #e2e8f0;
          border-radius: 8px;
          padding: 1.15rem;
        }

        .privacy-security-card h4 {
          font-size: 0.88rem;
          font-weight: 700;
          color: #0f172a;
          margin: 0 0 0.45rem;
        }

        .privacy-security-card p {
          font-size: 0.8rem;
          color: #475569;
          margin: 0;
          line-height: 1.55;
        }

        /* Deletion Card */
        .privacy-card-deletion {
          border-left: 4px solid #f32945;
        }

        .privacy-deletion-guide {
          background: #fff8f8;
          border: 1px solid #fed7d7;
          border-radius: 10px;
          padding: 1.5rem;
          margin-top: 1.25rem;
        }

        .privacy-deletion-guide-header {
          display: flex;
          align-items: center;
          gap: 0.65rem;
          margin-bottom: 0.85rem;
        }

        .privacy-deletion-guide-header h3 {
          font-size: 1.05rem;
          font-weight: 800;
          color: #c53030;
          margin: 0;
        }

        .privacy-deletion-methods {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
          gap: 1rem;
          margin: 1.25rem 0;
        }

        .privacy-deletion-method-card {
          background: #ffffff;
          border: 1px solid #e2e8f0;
          border-radius: 8px;
          padding: 1.15rem;
        }

        .privacy-method-badge {
          display: inline-block;
          font-size: 0.68rem;
          font-weight: 800;
          text-transform: uppercase;
          background: #f1f5f9;
          color: #475569;
          padding: 0.2rem 0.5rem;
          border-radius: 4px;
          margin-bottom: 0.5rem;
        }

        .privacy-deletion-method-card h4 {
          font-size: 0.92rem;
          font-weight: 700;
          color: #0f172a;
          margin: 0 0 0.5rem;
        }

        .privacy-method-note {
          font-size: 0.78rem;
          color: #64748b;
          font-style: italic;
          margin-top: 0.5rem;
        }

        .privacy-email-highlight {
          background: #f8fafc;
          border: 1px solid #cbd5e1;
          border-radius: 6px;
          padding: 0.5rem;
          margin: 0.5rem 0;
          font-size: 0.85rem;
        }

        .privacy-email-highlight code {
          color: #1677e8;
          font-weight: 700;
        }

        .privacy-deletion-notice {
          background: #ffffff;
          border: 1px dashed #cbd5e1;
          border-radius: 8px;
          padding: 1rem 1.25rem;
          margin-top: 1rem;
        }

        .privacy-deletion-notice h4 {
          font-size: 0.88rem;
          font-weight: 700;
          color: #0f172a;
          margin: 0 0 0.5rem;
        }

        /* Contact Section */
        .privacy-contact-grid {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
          gap: 1rem;
          margin: 1.25rem 0 1.5rem;
        }

        .privacy-contact-box {
          background: #f8fafc;
          border: 1px solid #e2e8f0;
          border-radius: 8px;
          padding: 1.15rem;
        }

        .privacy-contact-label {
          display: block;
          font-size: 0.72rem;
          font-weight: 700;
          color: #64748b;
          text-transform: uppercase;
          margin-bottom: 0.25rem;
        }

        .privacy-contact-box strong {
          display: block;
          font-size: 0.92rem;
          color: #0f172a;
          margin-bottom: 0.25rem;
        }

        .privacy-contact-link {
          display: inline-block;
          font-size: 0.92rem;
          font-weight: 700;
          color: #1677e8;
          text-decoration: none;
          margin-bottom: 0.25rem;
        }

        .privacy-contact-link:hover {
          text-decoration: underline;
        }

        .privacy-contact-desc {
          display: block;
          font-size: 0.78rem;
          color: #64748b;
        }

        .privacy-footer-note {
          padding-top: 1.25rem;
          border-top: 1px solid #e2e8f0;
          font-size: 0.78rem;
          color: #64748b;
          text-align: center;
        }

        /* Responsive Styles */
        @media (max-width: 900px) {
          .privacy-body {
            grid-template-columns: 1fr;
          }

          .privacy-sidebar {
            display: none;
          }

          .privacy-title {
            font-size: 1.6rem;
          }

          .privacy-section {
            padding: 1.35rem;
          }
        }

        /* Print Mode */
        @media print {
          .no-print, .privacy-sidebar, .privacy-header-actions {
            display: none !important;
          }

          .privacy-page-container {
            background: #ffffff !important;
            padding: 0 !important;
          }

          .privacy-body {
            display: block !important;
            max-width: 100% !important;
            margin: 0 !important;
            padding: 0 !important;
          }

          .privacy-section {
            box-shadow: none !important;
            border: 1px solid #ccc !important;
            page-break-inside: avoid;
            margin-bottom: 1.5rem !important;
            padding: 1rem !important;
          }

          .privacy-header {
            border-bottom: 2px solid #000 !important;
            padding: 1rem 0 !important;
          }

          .privacy-title {
            font-size: 1.5rem !important;
            color: #000 !important;
          }
        }
      `}</style>
    </div>
  );
}
