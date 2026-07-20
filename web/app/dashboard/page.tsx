"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

import {
  ApiError,
  fetchMe,
  logoutSession,
  refreshSession,
} from "@/lib/api";
import {
  clearSession,
  getSession,
  saveSession,
  type AuthSession,
  type User,
} from "@/lib/auth";

export default function DashboardPage() {
  const router = useRouter();
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    const session = getSession();
    if (!session || session.user.role !== "AGRONOMIST") {
      clearSession();
      router.replace("/login");
      return;
    }
    async function restoreSession(current: AuthSession) {
      try {
        return await fetchMe(current.access_token);
      } catch (error) {
        if (!(error instanceof ApiError) || error.status !== 401) throw error;
        const refreshed = await refreshSession(current.refresh_token);
        saveSession(refreshed);
        return fetchMe(refreshed.access_token);
      }
    }

    restoreSession(session)
      .then((freshUser) => {
        if (freshUser.role !== "AGRONOMIST") throw new Error("forbidden");
        setUser(freshUser);
      })
      .catch(() => {
        clearSession();
        router.replace("/login");
      });
  }, [router]);

  if (!user) {
    return <main className="loading-screen">Oturum doğrulanıyor…</main>;
  }

  return (
    <main className="dashboard">
      <header>
        <div>
          <p className="eyebrow dark">TARLA ASİSTANI</p>
          <h1>Günaydın{user.full_name ? `, ${user.full_name}` : ""}</h1>
        </div>
        <button
          className="logout"
          onClick={async () => {
            const session = getSession();
            if (session) {
              await logoutSession(session.refresh_token).catch(() => undefined);
            }
            clearSession();
            router.replace("/login");
          }}
        >
          Çıkış yap
        </button>
      </header>
      <section className="empty-state">
        <div className="brand-mark small" aria-hidden="true">TA</div>
        <h2>Uzman paneliniz hazır</h2>
        <p>
          Çiftçi ve vaka yönetimi sonraki sprintlerde bu güvenli çalışma
          alanına eklenecek.
        </p>
      </section>
    </main>
  );
}
