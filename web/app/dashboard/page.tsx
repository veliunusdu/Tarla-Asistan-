"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

import {
  ApiError,
  fetchCases,
  fetchMe,
  logoutSession,
  refreshSession,
  type CaseStatus,
  type SupportCase,
} from "@/lib/api";
import {
  clearSession,
  getSession,
  saveSession,
  type AuthSession,
  type User,
} from "@/lib/auth";

const STATUS_LABELS: Record<CaseStatus, string> = {
  OPEN: "Yeni",
  IN_REVIEW: "İnceleniyor",
  WAITING_FARMER: "Çiftçiden bilgi bekliyor",
  ANSWERED: "Yanıtlandı",
  CLOSED: "Kapalı",
};

const FILTERS: Array<{ value: CaseStatus | ""; label: string }> = [
  { value: "", label: "Tüm vakalar" },
  { value: "OPEN", label: "Yeni" },
  { value: "IN_REVIEW", label: "İnceleniyor" },
  { value: "WAITING_FARMER", label: "Bilgi bekliyor" },
  { value: "ANSWERED", label: "Yanıtlandı" },
  { value: "CLOSED", label: "Kapalı" },
];

export default function DashboardPage() {
  const router = useRouter();
  const [user, setUser] = useState<User | null>(null);
  const [cases, setCases] = useState<SupportCase[]>([]);
  const [filter, setFilter] = useState<CaseStatus | "">("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const session = getSession();
    if (!session || session.user.role !== "AGRONOMIST") {
      clearSession();
      router.replace("/login");
      return;
    }
    restoreUser(session)
      .then(setUser)
      .catch(() => {
        clearSession();
        router.replace("/login");
      });
  }, [router]);

  useEffect(() => {
    if (!user) return;
    setLoading(true);
    setError("");
    fetchCases(filter || undefined)
      .then((result) => setCases(result.items))
      .catch((err) => setError(err instanceof Error ? err.message : "Vakalar yüklenemedi."))
      .finally(() => setLoading(false));
  }, [filter, user]);

  if (!user) return <main className="loading-screen">Oturum doğrulanıyor…</main>;

  return (
    <main className="dashboard cases-dashboard">
      <header className="dashboard-header">
        <div>
          <p className="eyebrow dark">TARLA ASİSTANI · UZMAN PANELİ</p>
          <h1>Vaka yönetimi</h1>
          <p className="dashboard-subtitle">
            Günaydın{user.full_name ? `, ${user.full_name}` : ""}. Öncelikli saha bildirimleri burada.
          </p>
        </div>
        <button
          className="logout"
          onClick={async () => {
            const session = getSession();
            if (session) await logoutSession(session.refresh_token).catch(() => undefined);
            clearSession();
            router.replace("/login");
          }}
        >
          Çıkış yap
        </button>
      </header>

      <section className="case-toolbar" aria-label="Vaka filtreleri">
        {FILTERS.map((item) => (
          <button
            className={`filter-chip ${filter === item.value ? "active" : ""}`}
            key={item.value || "all"}
            onClick={() => setFilter(item.value)}
            type="button"
          >
            {item.label}
          </button>
        ))}
      </section>

      {error && <p className="error" role="alert">{error}</p>}
      {loading ? (
        <section className="case-empty">Vakalar yükleniyor…</section>
      ) : cases.length === 0 ? (
        <section className="case-empty">
          <div className="brand-mark small" aria-hidden="true">✓</div>
          <h2>Bu görünümde vaka yok</h2>
          <p>Yeni çiftçi bildirimleri geldiğinde burada önceliğine göre sıralanacak.</p>
        </section>
      ) : (
        <section className="case-list" aria-label="Vakalar">
          {cases.map((item) => (
            <Link className="case-card" href={`/dashboard/cases/${item.id}`} key={item.id}>
              <div className="case-card-topline">
                <span className={`priority priority-${item.priority.toLowerCase()}`}>
                  {item.priority === "CRITICAL" ? "Kritik" : item.priority === "HIGH" ? "Yüksek" : item.priority === "MEDIUM" ? "Orta" : "Düşük"}
                </span>
                <span className={`case-status status-${item.status.toLowerCase()}`}>
                  {STATUS_LABELS[item.status]}
                </span>
              </div>
              <h2>{item.title}</h2>
              <p className="case-description">{item.description}</p>
              <div className="case-context">
                <span><strong>{item.farmer_name}</strong></span>
                <span>{item.farm_name}</span>
                <span>{new Date(item.updated_at).toLocaleString("tr-TR")}</span>
                {item.media.length > 0 && <span>{item.media.length} medya</span>}
              </div>
            </Link>
          ))}
        </section>
      )}
    </main>
  );
}

async function restoreUser(current: AuthSession) {
  try {
    const user = await fetchMe(current.access_token);
    if (user.role !== "AGRONOMIST") throw new Error("forbidden");
    return user;
  } catch (error) {
    if (!(error instanceof ApiError) || error.status !== 401) throw error;
    const refreshed = await refreshSession(current.refresh_token);
    if (refreshed.user.role !== "AGRONOMIST") throw new Error("forbidden");
    saveSession(refreshed);
    return refreshed.user;
  }
}
