# Generated credentials (this migration)

Two placeholder secrets in the repo were replaced with randomly generated values as part of the
React/Web API migration. Rotate either at any time - nothing else depends on the specific value,
only on both sides agreeing.

## Web dashboard login key

- **Where:** `Pubg Ranking System/appsettings.json` → `WebDashboard:AuthKey`
- **Value:** `nmHztfLcarPE7dqF8x5OfEeD`
- **Used by:** the React dashboard's login screen (`POST /api/auth/login`). Anyone with this key
  can open the admin dashboard (Live/Match Control/Studio/Teams/Overlay Settings/Graphics) - it is
  **not** per-user auth, just a shared door key, same as the WinForms app's old key-entry screen.
- **To rotate:** change the value in `appsettings.json` and tell whoever logs into the dashboard
  the new key. No restart needed beyond the normal app restart to pick up config changes.

## Ingest agent shared secret

- **Where (server):** `Pubg Ranking System/appsettings.json` → `Agent:IngestKey`
- **Where (agent):** `VmixIngestAgent/appsettings.json` → `AgentKey` (or the `AGENTKEY` environment
  variable, which takes priority over the file)
- **Value (both sides, must match):** `pAnMIDO0F8pTE7JpmSnbWEHh`
- **Used by:** `POST /api/ingest/tick` - VmixIngestAgent sends this as the `X-Agent-Key` header on
  every tick; the server rejects any request whose header doesn't match. Only relevant when
  starting a match in "agent" mode from the Match Control tab (i.e. the main application isn't
  running on the same machine/LAN as pcob).
- **To rotate:** change both files to a new matching value and restart both processes. If they
  ever get out of sync, ingest ticks fail with 401 and VmixIngestAgent logs the rejection to its
  console every tick.

## Pre-existing secrets, not touched by this migration

These were already committed before this session and are out of scope for the React/Web API
migration, but worth flagging since they showed up while reading `appsettings.json`:

- `ConnectionStrings:DefaultConnection` has a plaintext MySQL password committed in the repo.
- `HangfireJobSettings:ApiKey` and `GoogleSheets:EncryptedCredentials` are a Google Sheets API key
  and an encrypted credentials blob, also committed in plaintext/ciphertext in the repo.

None of these were changed here - changing a live database password or Google credential without
coordinating with whatever currently depends on them could break things unexpectedly. Worth a
follow-up if this repo (or its history) is ever made public or shared more widely than it is now.
