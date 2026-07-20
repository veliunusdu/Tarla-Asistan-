"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

import { requestOtp, verifyOtp } from "@/lib/api";
import { saveSession } from "@/lib/auth";

export default function LoginPage() {
  const router = useRouter();
  const [phone, setPhone] = useState("+90");
  const [otp, setOtp] = useState("");
  const [step, setStep] = useState<"phone" | "otp">("phone");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submitPhone(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await requestOtp(phone);
      setStep("otp");
      setMessage(
        result.debug_otp
          ? `Yerel doğrulama kodu: ${result.debug_otp}`
          : "Doğrulama kodu telefonunuza gönderildi.",
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "Kod gönderilemedi.");
    } finally {
      setBusy(false);
    }
  }

  async function submitOtp(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const session = await verifyOtp(phone, otp);
      if (session.user.role !== "AGRONOMIST") {
        throw new Error("Bu panel yalnızca ziraat mühendislerine açıktır.");
      }
      saveSession(session);
      router.replace("/dashboard");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Giriş yapılamadı.");
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
          <h2>{step === "phone" ? "Hoş geldiniz" : "Kodu doğrulayın"}</h2>
          <p className="muted">
            {step === "phone"
              ? "Kayıtlı telefon numaranızla güvenli giriş yapın."
              : `${phone} numarasına gönderilen 6 haneli kodu girin.`}
          </p>

          <form onSubmit={step === "phone" ? submitPhone : submitOtp}>
            {step === "phone" ? (
              <label>
                Telefon numarası
                <input
                  inputMode="tel"
                  autoComplete="tel"
                  value={phone}
                  onChange={(event) => setPhone(event.target.value)}
                  placeholder="+90 555 111 22 33"
                  required
                  aria-describedby={error ? "form-error" : undefined}
                />
              </label>
            ) : (
              <label>
                Doğrulama kodu
                <input
                  className="otp-input"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  value={otp}
                  onChange={(event) =>
                    setOtp(event.target.value.replace(/\D/g, "").slice(0, 6))
                  }
                  placeholder="••••••"
                  pattern="\d{6}"
                  required
                  autoFocus
                  aria-describedby={error ? "form-error" : undefined}
                />
              </label>
            )}

            {message && <p className="notice">{message}</p>}
            {error && <p className="error" id="form-error" role="alert">{error}</p>}

            <button type="submit" disabled={busy}>
              {busy
                ? "Lütfen bekleyin…"
                : step === "phone"
                  ? "Doğrulama kodu gönder"
                  : "Güvenli giriş yap"}
            </button>
            {step === "otp" && (
              <button
                className="text-button"
                type="button"
                onClick={() => {
                  setStep("phone");
                  setOtp("");
                  setMessage("");
                  setError("");
                }}
              >
                Telefon numarasını değiştir
              </button>
            )}
          </form>
          <p className="security-note">Şifresiz ve süreli kod ile korunan giriş</p>
        </div>
      </section>
    </main>
  );
}
