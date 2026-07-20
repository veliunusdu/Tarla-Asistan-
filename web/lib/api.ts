import type { AuthSession } from "./auth";

const API_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8000/api/v1";

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
