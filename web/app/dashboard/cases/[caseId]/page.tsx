"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";

import {
  fetchCase,
  fetchProtectedMedia,
  requestAdditionalInfo,
  sendExpertResponse,
  updateCaseStatus,
  type CasePriority,
  type MediaAsset,
  type SupportCase,
} from "@/lib/api";
import { clearSession, getSession } from "@/lib/auth";

const STATUS_LABELS: Record<string, string> = {
  OPEN: "Yeni",
  IN_REVIEW: "İnceleniyor",
  WAITING_FARMER: "Çiftçiden bilgi bekliyor",
  ANSWERED: "Yanıtlandı",
  CLOSED: "Kapalı",
};

export default function CaseDetailPage() {
  const params = useParams<{ caseId: string }>();
  const router = useRouter();
  const [item, setItem] = useState<SupportCase | null>(null);
  const [message, setMessage] = useState("");
  const [mode, setMode] = useState<"response" | "request">("response");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const session = getSession();
    if (!session || session.user.role !== "AGRONOMIST") {
      clearSession();
      router.replace("/login");
      return;
    }
    fetchCase(params.caseId)
      .then(setItem)
      .catch((err) => setError(err instanceof Error ? err.message : "Vaka yüklenemedi."));
  }, [params.caseId, router]);

  async function submitMessage(event: FormEvent, closeCase = false) {
    event.preventDefault();
    if (!message.trim() || !item) return;
    setBusy(true);
    setError("");
    try {
      if (mode === "request") {
        await requestAdditionalInfo(item.id, message);
      } else {
        await sendExpertResponse(item.id, message, closeCase);
      }
      setMessage("");
      setItem(await fetchCase(item.id));
    } catch (err) {
      setError(err instanceof Error ? err.message : "İşlem tamamlanamadı.");
    } finally {
      setBusy(false);
    }
  }

  async function changePriority(priority: CasePriority) {
    if (!item) return;
    setBusy(true);
    try {
      const nextStatus = item.status === "OPEN" ? "IN_REVIEW" : item.status;
      setItem(await updateCaseStatus(item.id, nextStatus, priority));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Öncelik güncellenemedi.");
    } finally {
      setBusy(false);
    }
  }

  if (!item && !error) return <main className="loading-screen">Vaka yükleniyor…</main>;
  if (!item) return <main className="loading-screen"><p className="error">{error}</p></main>;

  return (
    <main className="dashboard case-detail-page">
      <Link className="back-link" href="/dashboard">← Vaka listesine dön</Link>
      <div className="case-detail-grid">
        <section className="case-main-panel">
          <div className="case-card-topline">
            <span className={`priority priority-${item.priority.toLowerCase()}`}>{item.priority}</span>
            <span className={`case-status status-${item.status.toLowerCase()}`}>{STATUS_LABELS[item.status]}</span>
          </div>
          <h1>{item.title}</h1>
          <p className="case-description large">{item.description}</p>

          {item.media.length > 0 && (
            <div className="media-grid">
              {item.media.map((media) => <ProtectedMedia key={media.id} media={media} />)}
            </div>
          )}

          <div className="conversation">
            <h2>Vaka konuşması</h2>
            {(item.messages ?? []).length === 0 ? (
              <p className="muted">Henüz mesaj yok.</p>
            ) : (
              item.messages?.map((entry) => (
                <article className={`message-bubble ${entry.message_type === "EXPERT_RESPONSE" || entry.message_type === "ADDITIONAL_INFO_REQUEST" ? "expert" : "farmer"}`} key={entry.id}>
                  <div className="message-meta">
                    <strong>{entry.message_type === "EXPERT_RESPONSE" || entry.message_type === "ADDITIONAL_INFO_REQUEST" ? "Uzman" : "Çiftçi"}</strong>
                    <span>{entry.message_type === "ADDITIONAL_INFO_REQUEST" ? "Ek bilgi isteği" : entry.message_type === "EXPERT_RESPONSE" ? "Uzman yanıtı" : "Mesaj"}</span>
                    <time>{new Date(entry.created_at_utc).toLocaleString("tr-TR")}</time>
                  </div>
                  <p>{entry.body}</p>
                  {entry.media.map((media) => <ProtectedMedia key={media.id} media={media} compact />)}
                </article>
              ))
            )}
          </div>

          {item.status !== "CLOSED" && (
            <form className="response-form" onSubmit={(event) => submitMessage(event)}>
              <div className="mode-tabs">
                <button className={mode === "response" ? "active" : ""} type="button" onClick={() => setMode("response")}>Uzman yanıtı</button>
                <button className={mode === "request" ? "active" : ""} type="button" onClick={() => setMode("request")}>Ek bilgi iste</button>
              </div>
              <label>
                {mode === "response" ? "Çiftçiye öneriniz" : "İhtiyaç duyduğunuz ek bilgi"}
                <textarea value={message} onChange={(event) => setMessage(event.target.value)} minLength={2} required />
              </label>
              {error && <p className="error" role="alert">{error}</p>}
              <div className="form-actions">
                <button disabled={busy} type="submit">{busy ? "Kaydediliyor…" : mode === "response" ? "Yanıtı gönder" : "Bilgi isteğini gönder"}</button>
                {mode === "response" && (
                  <button className="close-case-button" disabled={busy} type="button" onClick={(event) => submitMessage(event as unknown as FormEvent, true)}>Yanıtla ve kapat</button>
                )}
              </div>
            </form>
          )}
        </section>

        <aside className="case-side-panel">
          <h2>Tarla bağlamı</h2>
          <dl>
            <div><dt>Tarla</dt><dd>{item.farm_name}</dd></div>
            <div><dt>Kategori</dt><dd>{item.category}</dd></div>
            <div><dt>Oluşturulma</dt><dd>{new Date(item.created_at_utc).toLocaleString("tr-TR")}</dd></div>
          </dl>
          <label>
            Vaka önceliği
            <select value={item.priority} disabled={busy} onChange={(event) => changePriority(event.target.value as CasePriority)}>
              <option value="LOW">Düşük</option>
              <option value="MEDIUM">Orta</option>
              <option value="HIGH">Yüksek</option>
              <option value="CRITICAL">Kritik</option>
            </select>
          </label>
          {item.status === "OPEN" && (
            <button disabled={busy} onClick={() => changePriority(item.priority)} type="button">İncelemeye al</button>
          )}
        </aside>
      </div>
    </main>
  );
}

function ProtectedMedia({ media, compact = false }: { media: MediaAsset; compact?: boolean }) {
  const [url, setUrl] = useState("");
  useEffect(() => {
    let objectUrl = "";
    fetchProtectedMedia(media.url)
      .then((blob) => {
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch(() => undefined);
    return () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [media.url]);
  if (!url) return <div className="media-placeholder">{media.original_name}</div>;
  if (media.kind === "AUDIO") return <audio className={compact ? "compact-media" : ""} controls src={url} />;
  return <img className={compact ? "compact-media" : ""} src={url} alt={media.original_name} />;
}
