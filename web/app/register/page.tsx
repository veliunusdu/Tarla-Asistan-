"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { createUserWithEmailAndPassword, updateProfile } from "firebase/auth";

import { loginWithFirebase } from "@/lib/api";
import { firebaseAuth } from "@/lib/firebase";
import { saveSession } from "@/lib/auth";

export default function RegisterPage() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const credential = await createUserWithEmailAndPassword(firebaseAuth, email.trim(), password);
      if (name.trim()) await updateProfile(credential.user, { displayName: name.trim() });
      const session = await loginWithFirebase(await credential.user.getIdToken());
      if (session.user.role !== "FARMER") throw new Error("Bu kayıt akışı yalnızca çiftçi hesapları içindir.");
      saveSession(session);
      router.replace("/farmer");
    } catch (err) {
      const code = (err as { code?: string } | undefined)?.code;
      setError(
        code === "auth/email-already-in-use" ? "Bu e-posta adresi zaten kayıtlı. Giriş yapmayı deneyin."
          : code === "auth/invalid-email" ? "Geçerli bir e-posta adresi girin."
            : code === "auth/weak-password" ? "Şifre en az 6 karakter olmalıdır."
              : err instanceof Error ? err.message : "Hesap oluşturulamadı.",
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
        <h1>Tarlanızı tek yerden yönetin.</h1>
        <p className="brand-copy">Çiftçi hesabınızı oluşturun, ardından web veya mobil uygulamada aynı hesapla devam edin.</p>
        <div className="field-lines" aria-hidden="true"><span /><span /><span /><span /></div>
      </section>
      <section className="form-panel">
        <div className="login-card">
          <p className="eyebrow dark">ÇİFTÇİ HESABI</p>
          <h2>Hesap oluşturun</h2>
          <p className="muted">Bilgilerinizi girerek ücretsiz başlayın.</p>
          <form onSubmit={submit}>
            <label>Ad soyad<input value={name} onChange={(event) => setName(event.target.value)} placeholder="Adınız Soyadınız" required /></label>
            <label>E-posta adresi<input type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="siz@example.com" required /></label>
            <label>Şifre<input type="password" autoComplete="new-password" value={password} onChange={(event) => setPassword(event.target.value)} placeholder="En az 6 karakter" minLength={6} required /></label>
            {error && <p className="error" role="alert">{error}</p>}
            <button type="submit" disabled={busy}>{busy ? "Hesap oluşturuluyor…" : "Hesap oluştur"}</button>
          </form>
          <p className="auth-switch">Zaten hesabınız var mı? <Link href="/login">Giriş yapın</Link></p>
          <p className="security-note">Firebase Authentication ile korunan kayıt</p>
        </div>
      </section>
    </main>
  );
}
