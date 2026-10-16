import type { Metadata } from "next";
import { PrivacyPolicyView } from "./PrivacyPolicyView";

export const metadata: Metadata = {
  title: "Chính sách về Quyền riêng tư — Hệ thống Quản lý Chi phí Logistics LSS (CMS)",
  description:
    "Chính sách bảo vệ dữ liệu cá nhân, quyền riêng tư định vị GPS và kiểm soát an toàn thông tin tài chính áp dụng cho Hệ sinh thái CMS LSS (Web & Ứng dụng Di động iOS / Android).",
  alternates: {
    canonical: "https://cms.lsslogistics.vn/privacy",
  },
  openGraph: {
    title: "Chính sách về Quyền riêng tư — Hệ thống Quản lý Chi phí Logistics LSS (CMS)",
    description:
      "Chính sách bảo vệ dữ liệu cá nhân, quyền riêng tư định vị GPS và kiểm soát an toàn thông tin tài chính áp dụng cho Hệ sinh thái CMS LSS.",
    url: "https://cms.lsslogistics.vn/privacy",
    siteName: "LSS Logistics CMS",
    locale: "vi_VN",
    type: "website",
  },
};

export default function PrivacyPage() {
  return <PrivacyPolicyView />;
}
