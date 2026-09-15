"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

type LoginState = "idle" | "loading" | "error";

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [state, setState] = useState<LoginState>("idle");
  const [message, setMessage] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setMessage(null);

    if (!email.trim() || !password) {
      setState("error");
      setMessage("Vui lòng nhập email và mật khẩu.");
      return;
    }

    setState("loading");
    try {
      const res = await fetch("/bff/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: email.trim(), password }),
      });
      const data = (await res.json().catch(() => ({}))) as {
        message?: string;
      };

      if (!res.ok) {
        setState("error");
        setMessage(data.message || "Đăng nhập thất bại. Kiểm tra lại thông tin.");
        return;
      }

      router.replace("/");
      router.refresh();
    } catch {
      setState("error");
      setMessage("Không kết nối được máy chủ. Thử lại sau.");
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="brand" style={{ marginBottom: "1rem", padding: 0 }}>
          <span className="brand-mark" aria-hidden="true" />
          <div>
            CMS
            <small>Kiểm soát chi phí &amp; lợi nhuận</small>
          </div>
        </div>
        <h1 className="sr-only">Đăng nhập CMS</h1>
        <p className="lede">Đăng nhập để mở shell kiểm soát tài chính.</p>

        {state === "error" && message ? (
          <div className="alert alert-error" role="alert">
            {message}
          </div>
        ) : null}

        {state === "idle" && !message ? (
          <div className="alert alert-info">
            Dùng tài khoản vận hành đã cấp. Phiên đăng nhập lưu cookie httpOnly (không lưu token trên trình duyệt).
          </div>
        ) : null}

        <form onSubmit={onSubmit} noValidate>
          <div className="form-grid cols-2">
            <div className="field">
              <label htmlFor="email">Email</label>
              <input
                id="email"
                name="email"
                type="email"
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                disabled={state === "loading"}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="password">Mật khẩu</label>
              <input
                id="password"
                name="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={state === "loading"}
                required
              />
            </div>
          </div>
          <button className="btn full-width" type="submit" disabled={state === "loading"}>
            {state === "loading" ? "Đang đăng nhập…" : "Đăng nhập"}
          </button>
        </form>
      </div>
    </div>
  );
}
