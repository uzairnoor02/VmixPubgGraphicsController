import { useState } from "react";
import Login, { isAuthed } from "./Login";
import AdminShell from "./AdminShell";
import Overlay from "./Overlay";

export default function App() {
  // /overlay is the one route that has to be a real URL, since it's what gets pasted into vMix
  // as a Web Browser source address - and it's intentionally unauthenticated (a vMix browser
  // source can't type in a login key), lean, and has nothing on it but the graphics themselves.
  // Everything else is the login-gated admin shell. A hand-rolled pathname check instead of a
  // routing library, since one extra real route doesn't justify a new dependency here.
  if (window.location.pathname.startsWith("/overlay")) {
    return <Overlay />;
  }

  // Checked once on load (sessionStorage) so a refresh doesn't force re-entering the key every
  // time, but a fresh tab/browser session does - matching the WinForms app asking at each launch.
  const [authed, setAuthed] = useState(isAuthed());

  if (!authed) {
    return <Login onSuccess={() => setAuthed(true)} />;
  }

  return <AdminShell />;
}
