"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";

import {
  createFarmTask,
  fetchCase,
  fetchProtectedMedia,
  requestAdditionalInfo,
  sendExpertResponse,
  updateCaseStatus,
  type CasePriority,
  type MediaAsset,
  type SupportCase,
  type TaskConfidence,
  type TaskPriority,
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
  const [taskTitle, setTaskTitle] = useState("");
  const [taskDescription, setTaskDescription] = useState("");
  const [taskReason, setTaskReason] = useState("");
  const [taskDueDate, setTaskDueDate] = useState("");
  const [taskPriority, setTaskPriority] = useState<TaskPriority>("HIGH");
  const [taskConfidence, setTaskConfidence] = useState<TaskConfidence>("HIGH");
  const [taskBusy, setTaskBusy] = useState(false);
  const [taskSuccess, setTaskSuccess] = useState("");

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

  async function submitTask(event: FormEvent) {
    event.preventDefault();
    if (!item || !taskTitle.trim() || !taskDescription.trim() || !taskReason.trim() || !taskDueDate) return;

    setTaskBusy(true);
    setTaskSuccess("");
    setError("");
    try {
      await createFarmTask(item.farm_id, {
        title: taskTitle.trim(),
        description: taskDescription.trim(),
        reason: taskReason.trim(),
        priority: taskPriority,
        confidence: taskConfidence,
        dueDate: taskDueDate,
      });
      setTaskTitle("");
      setTaskDescription("");
      setTaskReason("");
      setTaskDueDate("");
      setTaskSuccess("Planlı iş çiftçinin İş Planım ekranına eklendi.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Planlı iş oluşturulamadı.");
    } finally {
      setTaskBusy(false);
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
          {item.context && <ContextSnapshot context={item.context} />}
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
          <section className="expert-task-form">
            <h2>Planlı iş öner</h2>
            <p className="muted">Bu iş doğrudan çiftçinin İş Planım ekranına eklenir.</p>
            <form className="response-form" onSubmit={submitTask}>
              <label>
                İş başlığı
                <input value={taskTitle} onChange={(event) => setTaskTitle(event.target.value)} minLength={2} maxLength={160} required />
              </label>
              <label>
                Açıklama
                <textarea value={taskDescription} onChange={(event) => setTaskDescription(event.target.value)} minLength={2} maxLength={4000} required />
              </label>
              <label>
                Öneri nedeni
                <textarea value={taskReason} onChange={(event) => setTaskReason(event.target.value)} minLength={2} maxLength={2000} required />
              </label>
              <label>
                Son tarih
                <input type="date" value={taskDueDate} onChange={(event) => setTaskDueDate(event.target.value)} required />
              </label>
              <label>
                Öncelik
                <select value={taskPriority} onChange={(event) => setTaskPriority(event.target.value as TaskPriority)}>
                  <option value="LOW">Düşük</option>
                  <option value="MEDIUM">Orta</option>
                  <option value="HIGH">Yüksek</option>
                  <option value="CRITICAL">Kritik</option>
                </select>
              </label>
              <label>
                Güven düzeyi
                <select value={taskConfidence} onChange={(event) => setTaskConfidence(event.target.value as TaskConfidence)}>
                  <option value="LOW">Düşük</option>
                  <option value="MEDIUM">Orta</option>
                  <option value="HIGH">Yüksek</option>
                </select>
              </label>
              {taskSuccess && <p className="success" role="status">{taskSuccess}</p>}
              {error && <p className="error" role="alert">{error}</p>}
              <button disabled={taskBusy} type="submit">{taskBusy ? "Ekleniyor…" : "Planlı işi ekle"}</button>
            </form>
          </section>
        </aside>
      </div>
    </main>
  );
}

function ContextSnapshot({ context }: { context: NonNullable<SupportCase["context"]> }) {
  return (
    <div className="context-snapshot">
      <p className="muted">Vaka açıldığı andaki değişmez kayıt</p>
      <dl>
        <div><dt>Konum</dt><dd>{context.latitude != null && context.longitude != null ? `${context.latitude.toFixed(5)}, ${context.longitude.toFixed(5)}` : "Belirtilmemiş"}</dd></div>
        <div><dt>Alan</dt><dd>{context.size_in_hectares != null ? `${context.size_in_hectares} ha` : "Belirtilmemiş"}</dd></div>
        <div><dt>Ürün</dt><dd>{context.crop_name ?? "Belirtilmemiş"}{context.crop_growing_day != null ? ` · ${context.crop_growing_day}. gün` : ""}</dd></div>
        <div><dt>Sulama</dt><dd>{context.irrigation_method ?? "Belirtilmemiş"}</dd></div>
        <div><dt>Toprak</dt><dd>{context.soil_type ?? "Belirtilmemiş"}</dd></div>
        <div><dt>Hava</dt><dd>{context.weather_fetched_at_utc ? `${context.current_temperature_c ?? "?"}°C · ${context.current_humidity_percent ?? "?"}% nem` : "Kayıt yok"}{context.is_based_on_stale_weather ? " · Eski veri" : ""}</dd></div>
      </dl>
      <h3>Son faaliyetler</h3>
      {context.recent_activities.length === 0 ? <p className="muted">Kayıtlı faaliyet yok.</p> : (
        <ul>
          {context.recent_activities.map((activity) => <li key={activity.id}><strong>{activity.activity_name}</strong><span>{new Date(activity.occurred_at_utc).toLocaleDateString("tr-TR")}</span></li>)}
        </ul>
      )}
    </div>
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
