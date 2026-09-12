import type { Metadata } from "next";
import { Be_Vietnam_Pro } from "next/font/google";
import "./globals.css";

const beVietnam = Be_Vietnam_Pro({
  subsets: ["latin", "vietnamese"],
  weight: ["400", "500", "600", "700"],
  display: "swap",
  variable: "--font-be-vietnam",
});

export const metadata: Metadata = {
  title: "CMS — Kiểm soát chi phí",
  description: "Cost Management System — lớp kiểm soát tài chính logistics",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="vi">
      <body className={beVietnam.variable} style={{ fontFamily: "var(--font-be-vietnam), var(--font)" }}>
        {children}
      </body>
    </html>
  );
}
