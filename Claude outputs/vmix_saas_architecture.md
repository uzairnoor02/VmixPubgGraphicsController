# Target architecture: Azure-hosted, agent-fed, React-rendered

This is the design for the deployment model you described: central backend on Azure, an
organizer downloads a small program and runs it at their venue, it feeds the backend, and one
React website renders every graphic live off SignalR. Written for review before more code changes
- most of the wiring below already exists from the last two phases; this document is about what
has to change structurally to make it work centrally instead of on one LAN.

## Target topology

```
 Organizer's venue PC                                    Azure (central backend)
┌─────────────────────────┐                        ┌───────────────────────────────────────┐
│  vMix          pcob      │                        │  API + SignalR host                    │
│   ▲             ▲        │                        │  (Linux container / App Service)       │
│   │ Browser     │ poll   │                        │                                        │
│   │ Source      │        │                        │  - Match Control API   (scoped per     │
│  ┌┴─────────────┴──────┐ │   HTTPS POST            │  - Ingest API            tournament)   │
│  │  VmixIngestAgent     │─┼──/api/ingest/tick──────▶  - LiveStatsBusiness pipeline           │
│  │  (already built)     │ │  (per-tournament        │  - SignalR hub, Groups = tournamentId  │
│  └──────────────────────┘ │   pairing token)         └───────────┬───────────────┬───────────┘
└─────────────────────────┘                                       │               │
                                                          ┌────────▼──────┐ ┌──────▼──────────┐
                                                          │ Azure DB        │ │ Blob storage    │
                                                          │ (MySQL/Postgres)│ │ (custom graphics,│
                                                          │ orgs/tournaments│ │  overlay config) │
                                                          │ /matches/stats  │ └─────────────────┘
                                                          └─────────────────┘
                                                                    │ SignalR (grouped per tournament)
                                                                    ▼
                                                    ┌───────────────────────────────────┐
                                                    │  React frontend, one deployment     │
                                                    │  /dashboard  (organizer login,       │
                                                    │              Match Control, Studio)  │
                                                    │  /overlay/{tournamentId}  (pasted     │
                                                    │              into vMix as a Browser   │
                                                    │              Source)                  │
                                                    └───────────────────────────────────┘
```

Everything on the right already exists in some form from the last two phases (`MatchControlApi`,
`IngestApi`, `LiveStatsBusiness`, the SignalR hub, the Graphics Studio, `/overlay`). What has to
change is making all of it **per-tournament** instead of **global to the process**, and making the
process **deployable to Linux** instead of tied to Windows/WinForms.

## The six structural changes, in priority order

### 1. Multi-tenancy - the load-bearing change

Today, `MatchStateStore`, `IngestCoordinator`, and `OverlayConfigStore` are singletons holding
**one** match/config for the whole process, and the SignalR hub broadcasts to `Clients.All`. That
was correct for "one graphics PC, one match at a time." It breaks the moment two organizers run
tournaments at the same time against the same Azure deployment - their data would collide into
the same "current match" and every dashboard/overlay would see everyone's data mixed together.

Needed:
- `MatchStateStore`/`IngestCoordinator` become keyed by `TournamentId` (`ConcurrentDictionary<Guid,
  ...>` instead of a single field per class).
- SignalR clients join a group named after their tournament id on connect; every broadcast targets
  `Clients.Group(tournamentId)`, never `Clients.All`.
- `/overlay` becomes `/overlay/{tournamentId}` (the URL an organizer pastes into vMix).
- The agent's shared secret becomes a **per-tournament pairing token**, not the one global
  `Agent:IngestKey` in appsettings.json today - so agent A's data can never land in tournament B.
- Custom-graphics uploads and overlay config (currently one global JSON file / one folder) get
  scoped the same way.

### 2. Onboarding flow ("download a file, run it")

- Organizer logs into the dashboard, creates a tournament.
- Dashboard has a "Set Up Agent" step: generates a pairing token for that tournament and gives the
  organizer `VmixIngestAgent`'s already-built executable + a config snippet (or a one-time pairing
  code entered on first run) carrying that token.
- Agent runs locally, authenticates with that token, and every tick it sends is now durably tied to
  that one tournament - no manual "Agent:IngestKey" editing required, unlike today's setup.

### 3. Hosting platform mismatch

The current project targets `net8.0-windows` with `UseWindowsForms=true` (left over from Form1
etc., which no longer run but still compile). That can't run on a Linux container, which is the
standard, cheapest way to host on Azure (App Service Linux plan, or Container Apps). To fix:
- Move the still-compiled-but-unused WinForms files (`Form1`, `AuthenticationForm`,
  `Add_tournament`, `BackupForm`, `ManualDataInputForm` and their `.Designer.cs` files) out of the
  deployed project entirely.
- Retarget the deployed API project to plain `net8.0`, drop `UseWindowsForms`.
- `VmixIngestAgent` is unaffected - it's already a separate, Windows-first console project, and
  stays that way since it has to run next to vMix on the organizer's Windows PC.

### 4. Persistent state that isn't local disk

Local JSON snapshot files (`state/*.json`) and the local `state/custom-graphics` folder aren't
guaranteed to survive a restart/redeploy/scale-out on App Service or Container Apps. Move:
- Overlay config + per-tournament settings → the existing SQL database (already has EF Core).
- Uploaded custom-graphics HTML → Azure Blob Storage.
- SignalR: self-hosted is fine for a single instance; if this ever scales to multiple backend
  instances, you'd want **Azure SignalR Service** (managed fan-out across instances) instead -
  worth deciding now vs. later, since it's a config/package change, not a rewrite.

### 5. Secrets out of the repo

`WebDashboard:AuthKey`, the (now per-tournament) agent tokens, the DB connection string, and the
pre-existing Google Sheets credentials move to **Azure Key Vault** or App Service's Configuration
(environment-variable-backed) instead of `appsettings.json` committed to git.

### 6. Network hardening for "internet-facing" instead of "LAN-only"

- Agent → backend is outbound-only HTTPS from the organizer's PC - no inbound port needed on their
  network, already true today.
- CORS tightens from `AllowAnyOrigin()` to the actual frontend domain(s).
- Rate limiting / abuse protection on `/api/ingest/tick` and `/api/auth/login` becomes worth having
  once this is reachable from the whole internet, not just a LAN.

## Decisions confirmed

- **Full multi-tenant.** Many organizers, concurrent tournaments, self-serve. Section 1's rework
  (per-tournament state, SignalR groups, per-tournament tokens, `/overlay/{tournamentId}`) is in
  scope, not deferred.
- **Payment-gated access, real accounts.** "Every time someone makes a payment they get a key that
  works" - designed below using standard SaaS patterns (Stripe + hashed tokens + JWT sessions), not
  today's single shared dashboard key.
- **Azure App Service (Linux).** Simplest managed option, matches "the app shouldn't just stop" -
  App Service gives always-on hosting, auto-restart, and built-in TLS without managing VMs.

## Identity, billing, and API keys

Three distinct kinds of credential, on purpose - this is the same split Stripe/GitHub/AWS all use,
and mixing them (one key for everything) is a common SaaS security mistake worth avoiding from day
one:

| Credential | Who holds it | Lifetime | Used for |
|---|---|---|---|
| **User session (JWT)** | A human logged into the React dashboard | Short-lived access token (~30-60 min) + refresh token, standard OAuth2-style rotation | Everything a person does in the dashboard - this replaces today's single static `WebDashboard:AuthKey` |
| **Agent pairing token** | `VmixIngestAgent`, one per tournament | Lives as long as the tournament does | The *only* thing that can be true, `/api/ingest/tick` for that one tournament - least privilege, can't touch any other tournament or any dashboard action |
| **Org API key** *(later, optional)* | A customer's own external integration | Until revoked | Programmatic access beyond the agent, if that's ever needed |

**Identity model:** `users` (email + password hash, bcrypt/argon2) + `organizations` (with a
`stripe_customer_id`) + `organization_members` (role: owner/admin/operator/viewer) - this already
matches the `users`/`organizations`/`organization_members` tables drafted in `schema.sql`. Session
auth uses ASP.NET Core's built-in JWT bearer middleware rather than hand-rolling anything - a
well-trodden, audited path instead of custom crypto.

**Billing flow (Stripe, V1 = pay-per-tournament):**
1. Organizer creates an account, creates a tournament in the dashboard.
2. Backend opens a Stripe Checkout Session (one-time payment) for that tournament.
3. On the `checkout.session.completed` webhook (signature-verified, idempotent - Stripe retries
   deliveries), the backend marks the tournament active and generates its agent pairing token.
4. The dashboard's "Download Agent" step is now unlocked, bundling that token with the
   already-built `VmixIngestAgent` executable.

Chose pay-per-tournament over a subscription for V1 because it matches what you described most
directly and needs no recurring-billing/proration logic - a subscription tier ("unlimited
tournaments this month") is a clean addition later once this is proven, without changing the
underlying token model.

**Token format:** `vmix_agent_<32 random bytes, base62>` - prefixed so it's identifiable in logs
(the same pattern Stripe's `sk_live_...` uses), generated with a cryptographically secure RNG,
stored server-side only as a SHA-256 hash (never plaintext at rest), shown to the organizer exactly
once at creation time. A leaked database dump would not leak usable tokens.

**Webhook endpoint:** `POST /api/billing/stripe-webhook` - verifies the Stripe signature header
before doing anything, and is idempotent against Stripe's at-least-once delivery.

## Sequencing

This is a large amount of new surface (real accounts, Stripe integration, webhooks, per-tournament
scoping throughout the existing code) on top of what's already built. Recommended build order,
each step independently testable before moving to the next:

1. Identity: `users`/`organizations`/`organization_members` tables + JWT session auth, replacing
   the shared dashboard key (no billing yet - everyone who signs up can use everything, for now).
2. Multi-tenancy rework: `TournamentId`-scoped state, SignalR groups, `/overlay/{tournamentId}`,
   per-tournament agent tokens (still no billing gate - tokens are just free to generate).
3. Stripe integration: Checkout + webhook + gating tournament creation on payment.
4. Azure deployment: retarget off WinForms/Windows, containerize, move local-disk state to DB/Blob
   Storage, secrets to Key Vault.

Open to reordering this if you'd rather see the Azure deployment working end-to-end (even
single-tenant) before layering in accounts and billing - say so and I'll swap the order.
