"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

import { loginWithFirebase } from "@/lib/api";
import { firebaseAuth } from "@/lib/firebase";
import { saveSession } from "@/lib/auth";
import { signInWithEmailAndPassword, type AuthError } from "firebase/auth";

export default function LoginPage() {
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
      const session = await loginWithFirebase(await credential.user.getIdToken());
      if (session.user.role !== "AGRONOMIST") {
        throw new Error("Bu panel yalnızca ziraat mühendislerine açıktır.");
      }
      saveSession(session);
      router.replace("/dashboard");
    } catch (err) {
      const code = (err as AuthError | undefined)?.code;
      setError(
        code === "auth/invalid-credential" || code === "auth/user-not-found"
          ? "E-posta veya şifre hatalı."
          : err instanceof Error
            ? err.message
            : "Giriş yapılamadı.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="login-shell">
      <section className="brand-panel" aria-label="Tarla Asistanı tanıtımı">
        <div className="brand-mark" aria-hidden="true">TA</div>
        <p className="eyebrow">TARLA ASİSTANI</p>
        <h1>Tarladaki bilgi, doğru karara dönüşsün.</h1>
        <p className="brand-copy">
          Üreticilerinizi, günlük faaliyetleri ve saha bildirimlerini tek bir
          uzman ekranından yönetin.
        </p>
        <div className="field-lines" aria-hidden="true">
          <span /><span /><span /><span />
        </div>
      </section>

      <section className="form-panel">
        <div className="login-card">
          <p className="eyebrow dark">UZMAN PANELİ</p>
          <h2>Hoş geldiniz</h2>
          <p className="muted">Firebase hesabınızla güvenli giriş yapın.</p>

          <form onSubmit={submit}>
            <label>
              E-posta adresi
              <input type="email" autoComplete="email" value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="uzman@example.com" required />
            </label>
            <label>
              Şifre
              <input type="password" autoComplete="current-password" value={password}
                onChange={(event) => setPassword(event.target.value)}
                placeholder="Şifrenizi girin" minLength={6} required />
            </label>

            {error && <p className="error" id="form-error" role="alert">{error}</p>}

            <button type="submit" disabled={busy}>
              {busy ? "Lütfen bekleyin…" : "Güvenli giriş yap"}
            </button>
          </form>
          <p className="security-note">Firebase Authentication ile korunan giriş</p>
        </div>
      </section>
    </main>
  );
}
