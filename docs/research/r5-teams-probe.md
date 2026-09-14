# R5 — Probe the teams read (r=t) for batch, ranking_points, bot

**Ticket**: [R5 - Probe the teams read (r=t) for batch, ranking_points, bot](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/18) · child of [Wayfinder Map: Dashboard 2.0 Spec](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/4)
**Probed**: 2026-09-15 (BRT season 62, round 3, day 2) · 10 live calls against `http://classic-api.blackoutrugby.com`, ≥3–4 s apart (documented burst limit)
**Auth**: owner member credentials + developer credentials read from `BlackoutRugbyDashboard/appsettings.json` at runtime; never published; artifact files scrub-checked (response bodies only — no request URLs)
**Raw responses**: [`docs/research/artifacts/r5-*.xml`](artifacts/) (10 files)

## Verdict

1. **`r=t` is verified live** — single `teamid` read and `teamids` batching (6/6 teams in one call) both work with member + developer credentials, exactly as `API_ENDPOINTS_COMPLETE.md` §26 and the raw dump describe. The read is public-grade: opponent (non-owner) reads return the full record below.
2. **The public field set includes two team-strength figures.** `ranking_points` (documented) and — **not in any doc source** — `average_top15_csr`, a team-level CSR aggregate. Decision 7's "opponent CSR" column has a direct, literal source: one batched `r=t` call returns opponent name + CSR figure for the whole last-8 window.
3. **The documented owner-only extras are absent live.** An owner read of our own team (45047, owner = calling member) returned none of the fields §26 and the raw dump list as owner-only (`drift`, `rush`, `manonman`, `contentment`, `members`, `stadium_*` tiers, `bank_balance`, `ltnt_effectiveness*`, sponsors, `scouting_stars`). Doc divergence #6 in the repo's ledger.
4. **Bot teams verified — and `f.botmatch` is not what it looks like.** Two of the six window opponents return `bot=1` (Kilronan Bobcats 44520, Cahirciveen Rockets 46229) with tell-tale signatures: `ranking_points` pinned at exactly `25`, `average_top15_csr` ≈ 48 k (real teams in the window: 205 k–262 k). Their fixtures nevertheless carry `botmatch=0` — **`f.botmatch` does not flag matches vs bot-managed teams**; its semantics are unknown, and the Home bot-opponent edge state must key off the team read's `bot` field.
5. **`No data requested` is a transient global API state, not a request error.** From 10:07:35 to 10:10:23 BRT every probe — `r=t` in several selector shapes *and* the unrelated `r=rk` — returned the identical 262-byte `<error>No data requested</error>` envelope; by 10:12:04 the known-good `r=m` succeeded, and afterwards even the C# client's exact wire shape (no path slash, selector last) worked. Request construction was never the cause. Methodology rule for future probes: on `No data requested`, pause and retry a known-good code (`r=m`) before diagnosing URL shape.
6. **`leagueid` selector and `r=rk` remain inconclusive** — probed only inside the degraded window, not re-run; D5 needs neither.

## Findings

### F1 — Request shape (what the build must send)

- Working URL forms observed post-recovery: `http://classic-api.blackoutrugby.com/?r=t&d=…&dk=…&memberid=…&memberkey=…&teamid=45047` (slash, selector last) and the C# client's exact shape `http://classic-api.blackoutrugby.com?r=t&d=…&dk=…&memberid=…&memberkey=…&teamid=45047` (no slash, selector last) — byte-identical 1128-byte responses.
- Param stack per `SendRequestAsync` (`BlackoutRugbyApiClient.cs` 613–654): `r`, `d`, `dk`, `memberid`, `memberkey`, then endpoint params. No IV parameter is sent (config `DeveloperIV` is unused by the client).

### F2 — Live public team record vs docs (opponent read, 45037)

Present, per docs (§26 / raw dump Teams §): `id` (attribute + duplicated child element), `name`, `country_iso`, `region`, `stadium`, `bot`, `nickname_1/2/3` (empty elements when unset), `ranking_points`, `regional_rank`, `national_rank`, `world_rank`, `prev_ranking_points`, `prev_regional_rank`, `prev_national_rank`, `prev_world_rank`, `leagueid`, `manager`.

Present, **undocumented**: `owner` (member id), `plural`, `plural_nickname_1/2/3`, `minimum_salary`, `date_taken_over`, **`average_top15_csr`**, `total_salary`.

Documented as owner-only, **absent on an owner read**: `drift`, `rush`, `manonman`, `contentment`, `members`, `stadium_capacity/standing/uncovered/covered/members/corporate`, `bank_balance`, `ltnt_effectiveness`, `ltnt_effectiveness_youth`, `major_sponsor`, `minor_sponsors`, `scouting_stars`. The spec must not rely on any of them.

`average_top15_csr` scale reference from the window: bots ≈ 48 k; our team 45047 = 205,755; division real teams 220,739–262,398. (Field name reads as the average CSR of the top 15 players; treat as an opaque team-strength figure — D4's per-player CSR vocabulary stays consistent.)

### F3 — Batching

`r=t&teamids=45037,44520,44349,85132,46229,45817` → 6/6 `<team id="…">` elements in one 5746-byte response. The docs' max-10 claim is untested at 7+; the last-8 window has never exceeded 6 unique opponents in observation. Silent omission for unknown team ids ("no information will be returned for that team") is doc-claimed but unverified — no missing team occurred in probes.

### F4 — Window snapshot (the data D5's rows would show)

| Fixture | Competition | Opponent | id | bot | average_top15_csr | ranking_points |
|---|---|---|---|---|---|---|
| 21416928 | s62 League R2 | Pok78 | 45037 | 0 | 49,826 | 37.211 |
| 21460743 | s62 Cup R2 | Kilronan Bobcats | 44520 | **1** | 48,396 | 25 |
| 21416925 | s62 League R1 | Pickles RFC | 44349 | 0 | 262,398 | 55.2456 |
| 21458860 | s62 ThursdayFriendly R1 | Crisp Sandwich RFC | 85132 | 0 | 220,739 | 55.797 |
| 21457301 | s62 Cup R1 | Cahirciveen Rockets | 46229 | **1** | 48,294 | 25 |
| 21361248 | s61 BronzeFinal | Pickles RFC | 44349 | 0 | 262,398 | 55.2456 |
| 21356372 | s61 SemiFinal | Galway R.F.C | 45817 | 0 | 257,744 | 55.0741 |
| 21305602 | s61 League R14 | Galway R.F.C | 45817 | 0 | 257,744 | 55.0741 |

All six fixtures involving these opponents carry `f.botmatch=0` (R1 artifact) — including both bot-opponent ties. `data_removed` flags are unchanged from R4's re-probe.

### F5 — Transient global error state (methodology record)

Five probes spanning 10:07:35–10:10:23 BRT (r=t × 4 shapes, r=rk × 1) returned identical 262-byte `No data requested` envelopes; `r=m` succeeded at 10:12:04 and every subsequent probe (incl. the client-exact no-slash form at 10:14:15) succeeded. Trigger unknown (not obviously a day/round boundary — the envelope already said day 2 at 06:17 per R4). Recorded so future sessions don't chase request-shape ghosts.

## Implications for map tickets

| Ticket | Implication |
|---|---|
| D5 (Home) | Opponent name + opponent CSR are sourceable in **one** `r=t&teamids=…` batch per window refresh (cache-first per decision 11 / D1 TeamFact rows). Column source choice between `average_top15_csr` (matches decision 7's "opponent CSR" wording) and `ranking_points` is a user call; both ride in the same cached row. Bot-opponent rows key off `bot=1`; name display falls back to blank/"—" if a team read omits a team. |
| D1 (cache model) | TeamFact row shape is now filled: (TeamId, captured-at) + name, `average_top15_csr`, `ranking_points`, `bot`, regional/national/world ranks, `leagueid` — capture the full public record; "captured when seen" = on Home window refresh per opponent. |
| Build | Client has no teams method — add `GetTeamAsync`/`GetTeamsAsync` (`r=t`, `teamid`/`teamids`); parser note: `id` appears as both attribute and child element (dedupe, same class as R1's duplicated `<lineup>`). |
| Docs | `API_ENDPOINTS_COMPLETE.md` §26 owner-only list is live-falsified (divergence #6); raw dump's owner-response example likewise stale. |

## Probe log

| # | Request (auth params elided) | Outcome | Artifact |
|---|---|---|---|
| 1 | `r=t&teamid=45037` | No data requested (transient global state) | r5-t-single-45037.xml |
| 2 | `r=t&teamids=45037,…,45817` (6 ids) | No data requested (transient global state) | r5-t-batch-window.xml |
| 3 | `r=t&leagueid=349097` | No data requested (transient global state) | r5-t-league-349097.xml |
| 4 | `r=t&teamid=45047` | No data requested (transient global state) | r5-t-own-45047.xml |
| 5 | `r=rk&leagueid=349097` | No data requested (transient global state) | r5-rk-league-349097.xml |
| 6 | `r=m` (sanity) | Member record | r5-m-sanity-slash.xml |
| 7 | `r=t&teamid=45047` (owner read) | Full team record (45047, Doylester) | r5-t-own-clientorder.xml |
| 8 | `r=t&teamid=45037` (public read) | Full team record (45037, Pok78) | r5-t-single-45037-fixed.xml |
| 9 | `r=t&teamids=45037,44520,44349,85132,46229,45817` | 6/6 team records, one call | r5-t-batch-window-fixed.xml |
| 10 | `r=t&teamid=45047` (client-exact no-slash URL) | Full team record — client wire shape verified | r5-t-own-noslash-clientexact.xml |

## Sources

- Live API probes (above) — authoritative for every verdict.
- "Blackout Rugby Api details" (official API docs HTML dump): Teams §(answer461, ~lines 1304–1345) — request shape, public/owner response examples.
- `API_ENDPOINTS_COMPLETE.md` §26 (Teams `r=t`), `endpoint-parameters.json` (`teams`, code `t`).
- R1 findings `docs/research/r1-lineup-tactics-probe.md` §F5 (`f` field set incl. `botmatch`, `data_removed`); R2 F2 (developer credentials on every read); R4 (window stability, flag semantics).

