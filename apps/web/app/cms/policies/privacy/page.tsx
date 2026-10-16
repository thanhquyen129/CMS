import type { Metadata } from "next";
import { PrivacyPolicyView } from "@/app/privacy/PrivacyPolicyView";

export const metadata: Metadata = {
  title: "Chính sách về Quyền riêng tư — Hệ thống Quản lý Chi phí Logistics LSS (CMS)",
  description:
    "Chính sách bảo vệ dữ liệu cá nhân, quyền riêng tư định vị GPS và kiểm soát an toàn thông tin tài chính áp dụng cho Hệ sinh thái CMS LSS.",
  alternates: {
    canonical: "https://cms.lsslogistics.vn/privacy",
  },
};

export default function CmsPoliciesPrivacyPage() {
  return <PrivacyPolicyView />;
}
