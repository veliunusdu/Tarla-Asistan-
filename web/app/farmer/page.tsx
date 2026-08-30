"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { clearSession, getSession } from "@/lib/auth";

export default function FarmerPage() {
  const router = useRouter();
  useEffect(() => {
    const session = getSession();
    if (!session || session.user.role !== "FARMER") {
      clearSession();
      router.replace("/login");
    }
  }, [router]);

  return (
    <main className="loading-screen">
      <section className="login-card farmer-welcome">
        <p className="eyebrow dark">TARLA ASİSTANI</p>
        <h2>Hesabınız hazır</h2>
        <p className="muted">Çiftçi hesabınız oluşturuldu. Tarla, hava durumu ve AI Asistan özellikleri için mobil uygulamada aynı hesapla giriş yapın.</p>
        <button type="button" onClick={() => { clearSession(); router.replace("/login"); }}>Çıkış yap</button>
      </section>
    </main>
  );
}
