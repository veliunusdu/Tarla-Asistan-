"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import { loginWithFirebase } from "@/lib/api";
import { firebaseAuth } from "@/lib/firebase";
import { saveSession } from "@/lib/auth";
import { signInWithEmailAndPassword, type AuthError } from "firebase/auth";

export default function AdminLoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const credential = await signInWithEmailAndPassword(firebaseAuth, email.trim(), password);
      const idToken = await credential.user.getIdToken();
      const session = await loginWithFirebase(idToken, "ADMIN");
      if (session.user.role !== "ADMIN" && session.user.active_role !== "ADMIN") {
        throw new Error("Bu hesap için yönetici yetkisi bulunmuyor.");
      }
      saveSession(session);
      router.replace("/admin/users");
    } catch (err) {
      const code = (err as AuthError | undefined)?.code;
      const status = (err as { status?: number } | undefined)?.status;
      if (status === 403) {
        setError("Bu hesap için yönetici yetkisi bulunmuyor.");
      } else if (code === "auth/invalid-credential" || code === "auth/user-not-found") {
        setError("E-posta veya şifre hatalı.");
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Giriş yapılamadı.");
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="login-shell">
      <section className="brand-panel" aria-label="Tarla Asistanı Yönetici Alanı">
        <div className="brand-mark" aria-hidden="true">TA</div>
        <p className="eyebrow">TARLA ASİSTANI</p>
        <h1>Yönetici Portali</h1>
        <p className="brand-copy">
          Kullanıcı rolleri, ziraatçi yetkilendirmeleri ve operasyonel denetim merkezi.
        </p>
        <div className="field-lines" aria-hidden="true">
          <span /><span /><span /><span />
        </div>
      </section>

      <section className="form-panel">
        <div className="login-card">
          <p className="eyebrow dark">YÖNETİCİ GİRİŞİ</p>
          <h2>Hoş geldiniz</h2>
          <p className="muted">Güvenli yönetici oturumu için giriş yapın.</p>

          <form onSubmit={submit}>
            <label>
              Yönetici E-posta Adresi
              <input
                type="email"
                autoComplete="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="admin@example.com"
                required
              />
            </label>
            <label>
              Şifre
              <input
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                placeholder="Şifrenizi girin"
                minLength={6}
                required
              />
            </label>

            {error && <p className="error" id="form-error" role="alert">{error}</p>}

            <button type="submit" disabled={busy}>
              {busy ? "Giriş yapılıyor…" : "Yönetici Girişi Yap"}
            </button>
          </form>

          <p className="auth-switch">
            Uzman paneline dön: <Link href="/login">Uzman Girişi</Link>
          </p>
          <p className="security-note">AdminContext ile korunan oturum</p>
        </div>
      </section>
    </main>
  );
}
