import { useEffect, useRef, useState } from "react";
import { API_BASE, api } from "../lib/api";
import type { GraphicsFile } from "../lib/api";

export default function GraphicsTab() {
  const [files, setFiles] = useState<GraphicsFile[]>([]);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  async function refresh() {
    setLoading(true);
    try {
      setFiles(await api.getGraphics());
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  async function handleUpload(file: File) {
    setUploading(true);
    setMessage(null);
    try {
      await api.uploadGraphic(file);
      setMessage({ kind: "ok", text: `${file.name} uploaded.` });
      await refresh();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Upload failed." });
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  }

  async function handleDelete(name: string) {
    await api.deleteGraphic(name);
    await refresh();
  }

  return (
    <div>
      <div className="header">
        <h1>Custom Graphics</h1>
      </div>

      <div className="panel">
        <h2>Upload a custom graphic</h2>
        <p className="panel-hint">
          Upload a self-contained .html file (inline its own CSS/JS/images) — a sponsor bumper, a bracket graphic, anything not
          already covered by the main overlay. It gets a stable URL below that you add as its own Web Browser source in vMix,
          separate from the main overlay.
        </p>
        <div className="actions">
          <button onClick={() => fileInputRef.current?.click()} disabled={uploading}>
            {uploading ? "Uploading…" : "Choose .html file…"}
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".html,text/html"
            style={{ display: "none" }}
            onChange={(e) => e.target.files?.[0] && handleUpload(e.target.files[0])}
          />
        </div>
        {message && <div className={message.kind === "ok" ? "message-ok" : "message-error"}>{message.text}</div>}
      </div>

      <div className="panel">
        <div className="panel-header-row">
          <h2>Uploaded graphics</h2>
          <button className="secondary" onClick={refresh} disabled={loading}>
            {loading ? "Refreshing…" : "Refresh"}
          </button>
        </div>

        {files.length === 0 ? (
          <div className="empty-state">Nothing uploaded yet.</div>
        ) : (
          <div className="graphics-list">
            {files.map((file) => {
              const url = `${API_BASE}${file.url}`;
              return (
                <div key={file.name} className="graphics-row">
                  <div>
                    <div className="graphics-name">{file.name}</div>
                    <div className="graphics-meta">{(file.sizeBytes / 1024).toFixed(1)} KB · {new Date(file.uploadedAtUtc).toLocaleString()}</div>
                  </div>
                  <div className="copy-row">
                    <code>{url}</code>
                    <button className="secondary" onClick={() => navigator.clipboard?.writeText(url)}>Copy</button>
                    <button className="danger" onClick={() => handleDelete(file.name)}>Delete</button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
