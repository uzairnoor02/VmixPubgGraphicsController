import { useEffect, useRef, useState } from "react";

const API_BASE = (import.meta as any).env?.VITE_API_BASE ?? "http://localhost:5050";

// sessionStorage (not localStorage) so it clears when the tab/browser closes, mirroring the
// WinForms app asking for the key again each time it starts - not a "remember me forever" login.
//
// This now stores the actual key, not just a "logged in" boolean - every admin-action API call
// requires it as a real `Authorization: Bearer <key>` header (see lib/api.ts's req() helper and
// Pubg Ranking System/DashboardAuth.cs on the server), not just a one-time check this screen did
// and then forgot about.
const SESSION_KEY = "vmix_dashboard_auth_key";

export function isAuthed(): boolean {
  return getAuthKey() !== null;
}

export function getAuthKey(): string | null {
  try {
    return sessionStorage.getItem(SESSION_KEY);
  } catch {
    // Private browsing / storage disabled - just fall back to asking every time.
    return null;
  }
}

function setAuthKey(key: string) {
  try {
    sessionStorage.setItem(SESSION_KEY, key);
  } catch {
    // Ignore - worst case the user gets asked again on the next reload.
  }
}

export function clearAuthed() {
  try {
    sessionStorage.removeItem(SESSION_KEY);
  } catch {
    // Ignore.
  }
}

export default function Login({ onSuccess }: { onSuccess: () => void }) {
  const [key, setKey] = useState("");
  const [error, setError] = useState("");
  const [validating, setValidating] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  async function submit() {
    if (!key.trim()) {
      setError("Please enter a valid key.");
      return;
    }
    setValidating(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ key: key.trim() }),
      });
      const data = await res.json().catch(() => ({ ok: false }));
      if (res.ok && data.ok) {
        setAuthKey(key.trim());
        onSuccess();
      } else {
        setError("Invalid key. Please check your key and try again.");
        setKey("");
        inputRef.current?.focus();
      }
    } catch {
      setError("Couldn't reach the graphics PC. Is the app running?");
    } finally {
      setValidating(false);
    }
  }

  return (
    <div className="login-screen">
      <div className="login-card">
        <h1>Live Match Dashboard</h1>
        <p className="login-sub">Enter the access key to continue.</p>
        <input
          ref={inputRef}
          type="password"
          className="login-input"
          value={key}
          onChange={(e) => setKey(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && submit()}
          placeholder="Access key"
          disabled={validating}
        />
        {error && <div className="login-error">{error}</div>}
        <button className="login-submit" onClick={submit} disabled={validating}>
          {validating ? "Validating…" : "Enter"}
        </button>
      </div>
    </div>
  );
}
