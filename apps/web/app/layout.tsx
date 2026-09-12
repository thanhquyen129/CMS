import type { Metadata } from "next";
import { Be_Vietnam_Pro } from "next/font/google";
import { DEFAULT_UI_PREFERENCES, uiPreferencesBootScript } from "@/lib/ui-preferences";
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
  const boot = DEFAULT_UI_PREFERENCES;
  return (
    <html
      lang="vi"
      data-theme={boot.theme}
      data-layout={boot.layout}
      data-density={boot.density}
      suppressHydrationWarning
    >
      <head>
        <script dangerouslySetInnerHTML={{ __html: uiPreferencesBootScript() }} />
      </head>
      <body className={beVietnam.variable} style={{ fontFamily: "var(--font-be-vietnam), var(--font)" }}>
        {children}
      </body>
    </html>
  );
}
