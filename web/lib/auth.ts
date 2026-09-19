export type Role = "FARMER" | "AGRONOMIST" | "ADMIN";

export type User = {
  id: string;
  phone_number: string;
  firebase_uid?: string | null;
  full_name: string | null;
  province: string | null;
  district: string | null;
  role: Role;
  active_role?: Role;
  roles?: Role[];
  profile_complete: boolean;
};

export type AuthSession = {
  access_token: string;
  refresh_token: string;
  token_type: "bearer";
  expires_in: number;
  user: User;
};

const SESSION_KEY = "tarla-asistani-session";

export function saveSession(session: AuthSession) {
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

export function getSession(): AuthSession | null {
  const value = localStorage.getItem(SESSION_KEY);
  if (!value) return null;
  try {
    return JSON.parse(value) as AuthSession;
  } catch {
    localStorage.removeItem(SESSION_KEY);
    return null;
  }
}

export function clearSession() {
  localStorage.removeItem(SESSION_KEY);
}
