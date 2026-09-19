"use client";

import { useEffect, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
  adminAssignRole,
  adminFetchRoleHistory,
  adminFetchUser,
  adminFetchUsers,
  adminRevokeRole,
  type AdminRoleHistoryItem,
  type AdminUserDetail,
  type AdminUserListItem,
} from "@/lib/api";
import { clearSession, getSession } from "@/lib/auth";

export default function AdminUsersPage() {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  // Search & List state
  const [search, setSearch] = useState("");
  const [users, setUsers] = useState<AdminUserListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  // Selected User Detail & History
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [userDetail, setUserDetail] = useState<AdminUserDetail | null>(null);
  const [roleHistory, setRoleHistory] = useState<AdminRoleHistoryItem[]>([]);
  const [loadingDetail, setLoadingDetail] = useState(false);

  // Action states (Grant / Revoke modal)
  const [actionType, setActionType] = useState<"grant" | "revoke" | null>(null);
  const [actionReason, setActionReason] = useState("");
  const [submittingAction, setSubmittingAction] = useState(false);

  // 1. Session check on mount
  useEffect(() => {
    const session = getSession();
    if (!session || (session.user.role !== "ADMIN" && session.user.active_role !== "ADMIN")) {
      router.replace("/admin/login");
      return;
    }
    loadUsers();
  }, []);

  async function loadUsers(querySearch?: string) {
    setLoading(true);
    setError(null);
    try {
      const res = await adminFetchUsers(querySearch !== undefined ? querySearch : search);
      setUsers(res.items);
      setTotal(res.total);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Kullanıcılar yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault();
    loadUsers(search);
  }

  async function selectUser(userId: string) {
    setSelectedUserId(userId);
    setLoadingDetail(true);
    setActionType(null);
    setActionReason("");
    try {
      const [detail, history] = await Promise.all([
        adminFetchUser(userId),
        adminFetchRoleHistory(userId),
      ]);
      setUserDetail(detail);
      setRoleHistory(history);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Kullanıcı detayları alınamadı.");
    } finally {
      setLoadingDetail(false);
    }
  }

  async function handleRoleAction() {
    if (!selectedUserId || !actionType) return;
    if (!actionReason.trim()) {
      setError("İşlem için lütfen bir gerekçe belirtin.");
      return;
    }

    setSubmittingAction(true);
    setError(null);
    setFeedback(null);
    try {
      if (actionType === "grant") {
        await adminAssignRole(selectedUserId, "AGRONOMIST", actionReason.trim());
        setFeedback("AGRONOMIST rolü başarıyla atandı.");
      } else {
        await adminRevokeRole(selectedUserId, "AGRONOMIST", actionReason.trim());
        setFeedback("AGRONOMIST rolü başarıyla geri alındı.");
      }
      setActionType(null);
      setActionReason("");

      // Refresh detail and list
      await Promise.all([
        selectUser(selectedUserId),
        loadUsers(search),
      ]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "İşlem sırasında hata oluştu.");
    } finally {
      setSubmittingAction(false);
    }
  }

  function handleLogout() {
    clearSession();
    router.replace("/admin/login");
  }

  const hasAgronomist = userDetail?.roles.includes("AGRONOMIST");

  return (
    <div style={{ minHeight: "100vh", background: "var(--cream)", padding: "24px 32px" }}>
      {/* Header */}
      <header
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "28px",
          borderBottom: "1px solid var(--line)",
          paddingBottom: "16px",
        }}
      >
        <div>
          <span style={{ fontSize: "12px", fontWeight: 800, color: "var(--leaf)", letterSpacing: "0.1em" }}>
            TARLA ASİSTANI
          </span>
          <h1 style={{ margin: "4px 0 0", fontSize: "26px", color: "var(--forest)" }}>
            Yönetici Portali • Rol Yönetimi
          </h1>
        </div>
        <div style={{ display: "flex", gap: "12px", alignItems: "center" }}>
          <Link
            href="/dashboard"
            style={{
              padding: "8px 14px",
              background: "white",
              border: "1px solid var(--line)",
              borderRadius: "8px",
              color: "var(--ink)",
              textDecoration: "none",
              fontSize: "14px",
              fontWeight: 500,
            }}
          >
            Uzman Paneli
          </Link>
          <button
            onClick={handleLogout}
            style={{
              padding: "8px 14px",
              background: "#fee2e2",
              color: "var(--danger)",
              border: "none",
              borderRadius: "8px",
              cursor: "pointer",
              fontWeight: 600,
              fontSize: "14px",
            }}
          >
            Çıkış Yap
          </button>
        </div>
      </header>

      {/* Notifications */}
      {error && (
        <div
          role="alert"
          style={{
            background: "#fef2f2",
            color: "var(--danger)",
            border: "1px solid #fecaca",
            borderRadius: "8px",
            padding: "12px 16px",
            marginBottom: "20px",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <span>{error}</span>
          <button
            onClick={() => setError(null)}
            style={{ background: "none", border: "none", cursor: "pointer", fontWeight: "bold" }}
          >
            ✕
          </button>
        </div>
      )}

      {feedback && (
        <div
          role="status"
          style={{
            background: "#f0fdf4",
            color: "#166534",
            border: "1px solid #bbf7d0",
            borderRadius: "8px",
            padding: "12px 16px",
            marginBottom: "20px",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <span>{feedback}</span>
          <button
            onClick={() => setFeedback(null)}
            style={{ background: "none", border: "none", cursor: "pointer", fontWeight: "bold" }}
          >
            ✕
          </button>
        </div>
      )}

      {/* Search Bar */}
      <section style={{ marginBottom: "24px" }}>
        <form onSubmit={handleSearchSubmit} style={{ display: "flex", gap: "12px", maxWidth: "680px" }}>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Telefon, Firebase UID veya Kullanıcı ID ile ara..."
            style={{
              flex: 1,
              padding: "12px 16px",
              borderRadius: "8px",
              border: "1px solid var(--line)",
              fontSize: "14px",
              background: "white",
            }}
          />
          <button
            type="submit"
            disabled={loading}
            style={{
              padding: "12px 24px",
              background: "var(--forest)",
              color: "white",
              border: "none",
              borderRadius: "8px",
              fontWeight: 600,
              cursor: "pointer",
            }}
          >
            {loading ? "Aranıyor..." : "Ara"}
          </button>
        </form>
      </section>

      {/* Main Grid: User List & User Details */}
      <div style={{ display: "grid", gridTemplateColumns: "1.4fr 1fr", gap: "24px" }}>
        {/* User List Table */}
        <div
          style={{
            background: "white",
            borderRadius: "12px",
            border: "1px solid var(--line)",
            overflow: "hidden",
            boxShadow: "0 2px 4px rgba(0,0,0,0.03)",
          }}
        >
          <div
            style={{
              padding: "16px 20px",
              borderBottom: "1px solid var(--line)",
              background: "#fafaf9",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <h2 style={{ margin: 0, fontSize: "16px", color: "var(--forest)" }}>Kullanıcı Listesi</h2>
            <span style={{ fontSize: "13px", color: "var(--muted)" }}>Toplam {total} kullanıcı</span>
          </div>

          {loading ? (
            <div style={{ padding: "40px", textAlign: "center", color: "var(--muted)" }}>
              Kullanıcılar yükleniyor...
            </div>
          ) : users.length === 0 ? (
            <div style={{ padding: "40px", textAlign: "center", color: "var(--muted)" }}>
              Kullanıcı bulunamadı.
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "14px", textAlign: "left" }}>
                <thead>
                  <tr style={{ background: "#f5f5f4", borderBottom: "1px solid var(--line)" }}>
                    <th style={{ padding: "12px 16px" }}>Telefon</th>
                    <th style={{ padding: "12px 16px" }}>Ad Soyad</th>
                    <th style={{ padding: "12px 16px" }}>Roller</th>
                    <th style={{ padding: "12px 16px" }}>Firebase UID</th>
                    <th style={{ padding: "12px 16px", textAlign: "right" }}>İşlem</th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((u) => {
                    const isSelected = selectedUserId === u.id;
                    return (
                      <tr
                        key={u.id}
                        onClick={() => selectUser(u.id)}
                        style={{
                          borderBottom: "1px solid var(--line)",
                          cursor: "pointer",
                          background: isSelected ? "#f0fdf4" : "transparent",
                          transition: "background 0.15s",
                        }}
                      >
                        <td style={{ padding: "12px 16px", fontWeight: 600 }}>{u.phone_number}</td>
                        <td style={{ padding: "12px 16px" }}>{u.full_name || "—"}</td>
                        <td style={{ padding: "12px 16px" }}>
                          <div style={{ display: "flex", gap: "6px", flexWrap: "wrap" }}>
                            {u.roles.map((r) => (
                              <span
                                key={r}
                                style={{
                                  padding: "2px 8px",
                                  borderRadius: "6px",
                                  fontSize: "11px",
                                  fontWeight: 700,
                                  background:
                                    r === "ADMIN"
                                      ? "#fef3c7"
                                      : r === "AGRONOMIST"
                                      ? "#dbeafe"
                                      : "#dcfce7",
                                  color:
                                    r === "ADMIN"
                                      ? "#92400e"
                                      : r === "AGRONOMIST"
                                      ? "#1e40af"
                                      : "#166534",
                                }}
                              >
                                {r}
                              </span>
                            ))}
                          </div>
                        </td>
                        <td
                          style={{
                            padding: "12px 16px",
                            fontSize: "12px",
                            color: "var(--muted)",
                            maxWidth: "140px",
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            whiteSpace: "nowrap",
                          }}
                        >
                          {u.firebase_uid || "—"}
                        </td>
                        <td style={{ padding: "12px 16px", textAlign: "right" }}>
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              selectUser(u.id);
                            }}
                            style={{
                              padding: "6px 12px",
                              background: isSelected ? "var(--forest)" : "white",
                              color: isSelected ? "white" : "var(--forest)",
                              border: "1px solid var(--forest)",
                              borderRadius: "6px",
                              cursor: "pointer",
                              fontSize: "12px",
                              fontWeight: 600,
                            }}
                          >
                            Detay
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* User Details & Role Assignment Panel */}
        <div
          style={{
            background: "white",
            borderRadius: "12px",
            border: "1px solid var(--line)",
            padding: "24px",
            boxShadow: "0 2px 4px rgba(0,0,0,0.03)",
            alignSelf: "start",
          }}
        >
          {!selectedUserId ? (
            <div style={{ textAlign: "center", padding: "48px 16px", color: "var(--muted)" }}>
              Detayları ve rol geçmişini görüntülemek için sol taraftan bir kullanıcı seçin.
            </div>
          ) : loadingDetail ? (
            <div style={{ textAlign: "center", padding: "48px 16px", color: "var(--muted)" }}>
              Kullanıcı detayları yükleniyor...
            </div>
          ) : userDetail ? (
            <div>
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "flex-start",
                  borderBottom: "1px solid var(--line)",
                  paddingBottom: "16px",
                  marginBottom: "20px",
                }}
              >
                <div>
                  <h3 style={{ margin: "0 0 4px", fontSize: "18px", color: "var(--forest)" }}>
                    {userDetail.full_name || "İsimsiz Kullanıcı"}
                  </h3>
                  <p style={{ margin: 0, fontSize: "14px", color: "var(--muted)" }}>
                    {userDetail.phone_number}
                  </p>
                </div>
                <div style={{ display: "flex", gap: "6px" }}>
                  {userDetail.roles.map((r) => (
                    <span
                      key={r}
                      style={{
                        padding: "4px 10px",
                        borderRadius: "8px",
                        fontSize: "12px",
                        fontWeight: 700,
                        background:
                          r === "ADMIN" ? "#fef3c7" : r === "AGRONOMIST" ? "#dbeafe" : "#dcfce7",
                        color:
                          r === "ADMIN" ? "#92400e" : r === "AGRONOMIST" ? "#1e40af" : "#166534",
                      }}
                    >
                      {r}
                    </span>
                  ))}
                </div>
              </div>

              <div style={{ fontSize: "13px", color: "var(--ink)", marginBottom: "24px" }}>
                <p style={{ margin: "6px 0" }}>
                  <strong>ID:</strong> <code style={{ fontSize: "12px" }}>{userDetail.id}</code>
                </p>
                <p style={{ margin: "6px 0" }}>
                  <strong>Firebase UID:</strong>{" "}
                  <code style={{ fontSize: "12px" }}>{userDetail.firebase_uid || "Bağlanmamış"}</code>
                </p>
                <p style={{ margin: "6px 0" }}>
                  <strong>Konum:</strong> {userDetail.province || "—"} / {userDetail.district || "—"}
                </p>
                <p style={{ margin: "6px 0" }}>
                  <strong>Hesap Durumu:</strong> {userDetail.account_status}
                </p>
              </div>

              {/* Role Action Section */}
              <div
                style={{
                  background: "#fafaf9",
                  padding: "16px",
                  borderRadius: "8px",
                  marginBottom: "24px",
                  border: "1px solid var(--line)",
                }}
              >
                <h4 style={{ margin: "0 0 12px", fontSize: "14px", color: "var(--forest)" }}>
                  Ziraatçi Yetkisi Yönetimi
                </h4>

                {actionType === null ? (
                  <div style={{ display: "flex", gap: "10px" }}>
                    {!hasAgronomist ? (
                      <button
                        onClick={() => setActionType("grant")}
                        style={{
                          padding: "8px 16px",
                          background: "var(--leaf)",
                          color: "white",
                          border: "none",
                          borderRadius: "6px",
                          fontWeight: 600,
                          fontSize: "13px",
                          cursor: "pointer",
                        }}
                      >
                        + Ziraatçi (AGRONOMIST) Yetkisi Ver
                      </button>
                    ) : (
                      <button
                        onClick={() => setActionType("revoke")}
                        style={{
                          padding: "8px 16px",
                          background: "#fee2e2",
                          color: "var(--danger)",
                          border: "1px solid #fca5a5",
                          borderRadius: "6px",
                          fontWeight: 600,
                          fontSize: "13px",
                          cursor: "pointer",
                        }}
                      >
                        Ziraatçi Yetkisini Kaldır
                      </button>
                    )}
                  </div>
                ) : (
                  <div>
                    <p style={{ margin: "0 0 8px", fontSize: "13px", fontWeight: 600 }}>
                      {actionType === "grant"
                        ? "Ziraatçi yetkisi verme gerekçesi:"
                        : "Ziraatçi yetkisini kaldırma gerekçesi:"}
                    </p>
                    <textarea
                      value={actionReason}
                      onChange={(e) => setActionReason(e.target.value)}
                      placeholder="Örn: Ziraat odası kaydı teyit edildi veya görevden ayrıldı..."
                      rows={3}
                      style={{
                        width: "100%",
                        padding: "8px 12px",
                        borderRadius: "6px",
                        border: "1px solid var(--line)",
                        fontSize: "13px",
                        marginBottom: "12px",
                      }}
                    />
                    <div style={{ display: "flex", gap: "8px" }}>
                      <button
                        onClick={handleRoleAction}
                        disabled={submittingAction}
                        style={{
                          padding: "8px 16px",
                          background: actionType === "grant" ? "var(--leaf)" : "var(--danger)",
                          color: "white",
                          border: "none",
                          borderRadius: "6px",
                          fontWeight: 600,
                          fontSize: "13px",
                          cursor: "pointer",
                        }}
                      >
                        {submittingAction
                          ? "İşleniyor..."
                          : actionType === "grant"
                          ? "Yetkiyi Onayla ve Ver"
                          : "Yetkiyi Geri Al"}
                      </button>
                      <button
                        onClick={() => {
                          setActionType(null);
                          setActionReason("");
                        }}
                        disabled={submittingAction}
                        style={{
                          padding: "8px 16px",
                          background: "white",
                          border: "1px solid var(--line)",
                          borderRadius: "6px",
                          fontSize: "13px",
                          cursor: "pointer",
                        }}
                      >
                        İptal
                      </button>
                    </div>
                  </div>
                )}
              </div>

              {/* Role History */}
              <div>
                <h4 style={{ margin: "0 0 12px", fontSize: "14px", color: "var(--forest)" }}>
                  Rol Değişiklik Geçmişi
                </h4>
                {roleHistory.length === 0 ? (
                  <p style={{ fontSize: "13px", color: "var(--muted)", margin: 0 }}>
                    Kayıtlı rol işlemi bulunmuyor.
                  </p>
                ) : (
                  <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                    {roleHistory.map((h) => (
                      <div
                        key={h.id}
                        style={{
                          border: "1px solid var(--line)",
                          borderRadius: "8px",
                          padding: "10px 14px",
                          fontSize: "12px",
                        }}
                      >
                        <div
                          style={{
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                            marginBottom: "4px",
                          }}
                        >
                          <strong>{h.role}</strong>
                          <span
                            style={{
                              color: h.revoked_at_utc ? "var(--danger)" : "var(--leaf)",
                              fontWeight: 600,
                            }}
                          >
                            {h.revoked_at_utc ? "Geri Alındı" : "Aktif"}
                          </span>
                        </div>
                        <div style={{ color: "var(--muted)" }}>
                          Verilme: {new Date(h.granted_at_utc).toLocaleString("tr-TR")}{" "}
                          {h.grant_reason && `(${h.grant_reason})`}
                        </div>
                        {h.revoked_at_utc && (
                          <div style={{ color: "var(--danger)", marginTop: "2px" }}>
                            Geri Alınma: {new Date(h.revoked_at_utc).toLocaleString("tr-TR")}{" "}
                            {h.revoke_reason && `(${h.revoke_reason})`}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
