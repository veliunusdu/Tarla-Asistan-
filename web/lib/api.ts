import {
  clearSession,
  getSession,
  saveSession,
  type AuthSession,
} from "./auth";

const API_ORIGIN = process.env.NEXT_PUBLIC_API_ORIGIN;
const API_URL =
  process.env.NEXT_PUBLIC_API_URL ??
  (API_ORIGIN ? `${API_ORIGIN}/api/v1` : "http://localhost:8000/api/v1");

type ApiErrorBody = { detail?: string };

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      ...init,
      headers: { "Content-Type": "application/json", ...init.headers },
    });
  } catch {
    throw new Error("Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.");
  }
  if (!response.ok) {
    const body = (await response.json().catch(() => ({}))) as ApiErrorBody;
    throw new ApiError(body.detail ?? "İşlem tamamlanamadı.", response.status);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export function requestOtp(phoneNumber: string) {
  return request<{ message: string; expires_in: number; debug_otp?: string }>(
    "/auth/request-otp",
    { method: "POST", body: JSON.stringify({ phone_number: phoneNumber }) },
  );
}

export function verifyOtp(phoneNumber: string, otpCode: string) {
  return request<AuthSession>("/auth/verify-otp", {
    method: "POST",
    body: JSON.stringify({ phone_number: phoneNumber, otp_code: otpCode }),
  });
}

export function loginWithFirebase(idToken: string) {
  return request<AuthSession>("/auth/firebase", {
    method: "POST",
    body: JSON.stringify({ id_token: idToken }),
  });
}

export function fetchMe(token: string) {
  return request<AuthSession["user"]>("/auth/me", {
    method: "GET",
    headers: { Authorization: `Bearer ${token}` },
  });
}

export function refreshSession(refreshToken: string) {
  return request<AuthSession>("/auth/refresh", {
    method: "POST",
    body: JSON.stringify({ refresh_token: refreshToken }),
  });
}

export function logoutSession(refreshToken: string) {
  return request<void>("/auth/logout", {
    method: "POST",
    body: JSON.stringify({ refresh_token: refreshToken }),
  });
}

export type CaseStatus =
  | "OPEN"
  | "IN_REVIEW"
  | "WAITING_FARMER"
  | "ANSWERED"
  | "CLOSED";

export type CasePriority = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";

export type TaskPriority = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";
export type TaskConfidence = "LOW" | "MEDIUM" | "HIGH";

export type FarmTask = {
  id: string;
  farm_id: string;
  title: string;
  description: string;
  reason: string;
  priority: TaskPriority;
  confidence: TaskConfidence;
  due_date: string;
  status: string;
  source: "MANUAL" | "EXPERT";
};

export type CreateFarmTaskInput = {
  title: string;
  description: string;
  reason: string;
  priority: TaskPriority;
  confidence: TaskConfidence;
  dueDate: string;
};

export type MediaAsset = {
  id: string;
  kind: "IMAGE" | "AUDIO";
  original_name: string;
  content_type: string;
  size_bytes: number;
  url: string;
};

export type CaseMessage = {
  id: string;
  case_id: string;
  sender_id: string;
  message_type: "COMMENT" | "ADDITIONAL_INFO_REQUEST" | "EXPERT_RESPONSE";
  body: string;
  media: MediaAsset[];
  created_at_utc: string;
};

export type RecentActivitySnapshot = {
  id: string;
  activity_name: string;
  activity_type: string | null;
  status: string;
  occurred_at_utc: string;
  description: string | null;
};

export type CaseContextSnapshot = {
  farm_name: string;
  latitude: number | null;
  longitude: number | null;
  size_in_hectares: number | null;
  irrigation_method: string | null;
  soil_type: string | null;
  farm_note: string | null;
  crop_name: string | null;
  crop_planted_at: string | null;
  crop_harvested_at: string | null;
  crop_growing_day: number | null;
  weather_provider: string | null;
  weather_fetched_at_utc: string | null;
  is_based_on_stale_weather: boolean;
  current_temperature_c: number | null;
  current_humidity_percent: number | null;
  next24_hours_precipitation_mm: number | null;
  recent_activities: RecentActivitySnapshot[];
};

export type SupportCase = {
  id: string;
  farm_id: string;
  farm_name: string;
  created_by_id: string;
  assigned_expert_id: string | null;
  category: string;
  priority: CasePriority;
  status: CaseStatus;
  title: string;
  description: string;
  media: MediaAsset[];
  messages?: CaseMessage[];
  closed_at_utc: string | null;
  created_at_utc: string;
  updated_at_utc: string;
  context: CaseContextSnapshot | null;
};

export type CaseSummary = {
  id: string;
  farm_id: string;
  farm_name: string;
  created_by_id: string;
  assigned_expert_id: string | null;
  category: string;
  priority: CasePriority;
  status: CaseStatus;
  title: string;
  created_at_utc: string;
  updated_at_utc: string;
  closed_at_utc: string | null;
  message_count: number;
  media_count: number;
};

async function authenticatedRequest<T>(path: string, init: RequestInit): Promise<T> {
  const session = getSession();
  if (!session) throw new ApiError("Oturum açmanız gerekiyor.", 401);
  try {
    return await request<T>(path, {
      ...init,
      headers: { ...init.headers, Authorization: `Bearer ${session.access_token}` },
    });
  } catch (error) {
    if (!(error instanceof ApiError) || error.status !== 401) throw error;
    try {
      const refreshed = await refreshSession(session.refresh_token);
      saveSession(refreshed);
      return await request<T>(path, {
        ...init,
        headers: { ...init.headers, Authorization: `Bearer ${refreshed.access_token}` },
      });
    } catch (refreshError) {
      clearSession();
      throw refreshError;
    }
  }
}

export function fetchCases(status?: CaseStatus) {
  const query = status ? `?status=${status}` : "";
  return authenticatedRequest<{ items: CaseSummary[]; total: number }>(
    `/cases${query}`,
    { method: "GET" },
  );
}

export function fetchCase(caseId: string) {
  return authenticatedRequest<SupportCase>(`/cases/${caseId}`, { method: "GET" });
}

export function updateCaseStatus(
  caseId: string,
  status: CaseStatus,
  priority?: CasePriority,
) {
  return authenticatedRequest<SupportCase>(`/cases/${caseId}/status`, {
    method: "PATCH",
    body: JSON.stringify({ status, priority, assign_to_me: true }),
  });
}

export function requestAdditionalInfo(caseId: string, body: string) {
  return authenticatedRequest<CaseMessage>(`/cases/${caseId}/messages`, {
    method: "POST",
    body: JSON.stringify({
      client_operation_id: crypto.randomUUID(),
      message_type: "ADDITIONAL_INFO_REQUEST",
      body,
    }),
  });
}

export function sendExpertResponse(caseId: string, body: string, closeCase: boolean) {
  return authenticatedRequest<SupportCase>(`/cases/${caseId}/expert-response`, {
    method: "POST",
    body: JSON.stringify({
      client_operation_id: crypto.randomUUID(),
      body,
      close_case: closeCase,
    }),
  });
}

export function createFarmTask(farmId: string, input: CreateFarmTaskInput) {
  return authenticatedRequest<FarmTask>(`/farms/${farmId}/tasks`, {
    method: "POST",
    body: JSON.stringify({
      title: input.title,
      description: input.description,
      reason: input.reason,
      priority: input.priority,
      confidence: input.confidence,
      dueDate: input.dueDate,
    }),
  });
}

export async function fetchProtectedMedia(path: string): Promise<Blob> {
  const mediaUrl = path.startsWith("http")
    ? path
    : `${new URL(API_URL).origin}${path}`;
  const session = getSession();
  if (!session) throw new ApiError("Oturum açmanız gerekiyor.", 401);
  let response = await fetch(mediaUrl, {
    headers: { Authorization: `Bearer ${session.access_token}` },
  });
  if (response.status === 401) {
    const refreshed = await refreshSession(session.refresh_token);
    saveSession(refreshed);
    response = await fetch(mediaUrl, {
      headers: { Authorization: `Bearer ${refreshed.access_token}` },
    });
  }
  if (!response.ok) throw new ApiError("Medya açılamadı.", response.status);
  return response.blob();
}
