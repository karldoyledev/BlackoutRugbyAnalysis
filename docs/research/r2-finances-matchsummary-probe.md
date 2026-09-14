# R2 — Verify finances endpoint + match-income categories + match-summary batch

**Ticket**: [R2 - Verify finances endpoint + match-income categories + match-summary batch](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/6) · child of [Wayfinder Map: Dashboard 2.0 Spec](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/4)
**Probed**: 2026-09-14/15 (BRT season 62, round 3, day 1–2) · 12 live calls against `http://classic-api.blackoutrugby.com`, ≥3 s apart (documented burst limit)
**Auth**: owner member credentials + developer credentials read from `BlackoutRugbyDashboard/appsettings.json` at runtime; never published; artifact files scrub-checked (response bodies only — no request URLs)
**Raw responses**: [`docs/research/artifacts/r2-*.xml`](artifacts/) (12 files)

## Verdict

1. **Finances: request code `r=fi` is verified live, but the club's finance data is absent.** Every probed variant — S62 rounds 1/2/3, S61 round 1, calendar-year `season=2026` rounds 1/2, with explicit `teamid`, and with no season/round at all (API defaults to current round per the response envelope) — returns `<error>No transactions found</error>`, an application-level result (the code is accepted, not rejected). A credential sanity probe (`r=m`, same credentials) returns the live member record (teamid 45047, active, premium), proving the empty finances are **not** an auth failure. Conclusion: team 45047 has no retrievable transactions via the classic API in any probed period; root cause undetermined (the game may no longer populate finances here, or retention is nil).
2. **Match income categories could not be enumerated live** — there are no transactions to enumerate for this club. Docs-only knowledge stands unverified: `category` values like `Wages`, `type` 1 = income / 2 = expense, `detail` free text. The Home page's "money made from the match" column (decision 7) cannot be sourced from `fi` as of today.
3. **Match summary read is `r=ms`, not `r=msum`.** Live: `r=msum&fixtureids=…` → `<error>"msum" is not a valid request</error>`; `r=ms&fixtureids=21416925,21416928,21460743` → full batch response. `GetMatchSummaryAsync` (`BlackoutRugbyApiClient.cs` line 283, sends `"msum"`) is **broken against the live API and must send `"ms"`** — same class of bug as `GetLineupsAsync` (`"li"` → `"lu"`, R1 F1).
4. **`fixtureids` batching on `r=ms` works**: 3 of 3 requested fixture ids returned as `<match_summary fixtureid="…">` elements in one call.

## Findings

### F1 — Finances request shape (per docs, corroborated as far as live allows)

- Official raw dump "Blackout Rugby Api details" (Finances §, ~line 57): `&r=fi&season=[SEASON]&round=[ROUND]`, no teamid, "provided only if a member key is provided, for the member's club", "If no financial information is available, no information will be returned, not even an empty element."
- Live: code accepted in all variants; zero transactions in all of them. Probes: `r2-fi-s62-r1/r2/r3`, `r2-fi-s61-r1`, `r2-fi-2026-r1/r2`, `r2-fi-s62-r2-teamid`, `r2-fi-noparams` — all 261-byte "No transactions found" envelopes.
- `API_ENDPOINTS_COMPLETE.md` §6 (`r=f`) is wrong — the **fourth** request-code error in that file (finances per decision 15, fixtures per R1 F5, lineups per R1 F1, now re-confirmed here).
- **Build implication**: `GetFinancesAsync` already sends `"fi"` (correct); the endpoint cannot currently back the Home gate-receipts column for this club.

### F2 — Undocumented auth change: developer credentials required on every read

- `r=fi` with member credentials only (no `d`/`dk`) → `<error>Developer ID not provided</error>` ([r2-fi-s62-r2-memberonly.xml](artifacts/r2-fi-s62-r2-memberonly.xml)). The 2010-era docs describe member-key-only access; the live API rejects member-only calls outright.
- Credential sanity: `r=m&memberid=…` with the same credentials returns the live member record (username Doyleski, teamid 45047, `active=1`, `premium=1`) ([r2-m-memberid.xml](artifacts/r2-m-memberid.xml)) — the member key is valid; the empty finances are not an auth problem.
- Stack order confirmed: `d`/`dk` + `memberid`/`memberkey` together on one call (how `SendRequestAsync` already sends them) works.

### F3 — Match summary: `r=ms`, batching, attendance placement

- `r=msum` rejected at code level ([r2-msum-batch3.xml](artifacts/r2-msum-batch3.xml)); `r=ms` accepted ([r2-ms-batch3.xml](artifacts/r2-ms-batch3.xml)). `API_ENDPOINTS_COMPLETE.md` §15 and `endpoint-parameters.json` both say `msum` — wrong; the raw dump (`&r=ms`) is right.
- Batch response: one `<match_summary fixtureid="…">` element per requested fixture, in request order; 3/3 returned for ids `21416925,21416928,21460743`.
- **Attendance** sits at the tail of each summary as `<attendance>` with tier breakdown: `standing`, `uncovered`, `covered`, `members`, `corporate` (sum = total crowd; e.g. 11168 + 17250 + 14831 + 4025 + 504 = 43,755). Docs say youth matches return no attendance.
- Per-summary payload: `points`, scoring lists (`tries`/`conversions`/`dropgoals`/`penalties`, each `<player id number count>`), `injuries` (`daysinjured`, `replacedby`, `minutesplayed`), `subs` (`replacedby`, `minutesplayed`), `intensity` (1/2/3), `weather` (`id`, `night`).
- **Bonus data point for R4**: fixture 21416925 carries `data_removed=1` (per R1 F5) and still returned a **full** summary including attendance — `ms` is not gated by `data_removed`.

### F4 — Season numbering (side-finding)

- The envelope self-reports game numbering (`season="62"`, `round="3"`), while `ApiTestController.cs` line 72 defaults the finances season to `DateTime.Now.Year` (2026). Both `62` and `2026` return empty finances here, so the correct season base for `fi` remains unproven; the app's calendar-year default is suspect and should follow the fixtures envelope's `season` attribute at build time.

## Implications for map tickets

| Ticket | Implication |
|---|---|
| D5 (Home) | Gate receipts via `fi`: not dependable (empty for this club) — the data-call plan's "fi per round" step should be dropped or made best-effort. Attendance + scores: one `r=ms&fixtureids=…` batch per last-8 window (verified). Edge state: finance-less rows are the norm, not the exception. |
| D1 (cache model) | Cache `ms` batch responses per fixture; attendance tier fields (5) + scorer/injury/sub lists are the shape to persist. |
| Build | Fix `GetMatchSummaryAsync` `"msum"` → `"ms"` (client line 283) and `ApiTester.cshtml` code `'msum'` → `'ms'` (line 312). `GetFinancesAsync` unchanged (`fi` correct). |
| R4 (#17) | Probe stats reads with `r=ms` (not `msum`); `data_removed=1` fixture already known to return full `ms`. |
| Docs | `API_ENDPOINTS_COMPLETE.md` §6 (`r=f`) and §15 (`msum`) are errors #4 and #5 in that file (fix during build). |

## Probe log

| # | Request (auth params elided) | Outcome | Artifact |
|---|---|---|---|
| 1 | `r=fi&season=62&round=2` | No transactions found | r2-fi-s62-r2.xml |
| 2 | `r=fi&season=62&round=1` | No transactions found | r2-fi-s62-r1.xml |
| 3 | `r=ms&fixtureids=21416925,21416928,21460743` | 3/3 summaries + attendance | r2-ms-batch3.xml |
| 4 | `r=msum&fixtureids=21416925,21416928,21460743` | `"msum" is not a valid request` | r2-msum-batch3.xml |
| 5 | `r=fi&season=2026&round=2` | No transactions found | r2-fi-2026-r2.xml |
| 6 | `r=fi&season=62&round=3` | No transactions found | r2-fi-s62-r3.xml |
| 7 | `r=fi&season=2026&round=1` | No transactions found | r2-fi-2026-r1.xml |
| 8 | `r=fi&season=62&round=2` (member-only, no d/dk) | `Developer ID not provided` | r2-fi-s62-r2-memberonly.xml |
| 9 | `r=fi&season=62&round=2&teamid=45047` | No transactions found | r2-fi-s62-r2-teamid.xml |
| 10 | `r=fi&season=61&round=1` | No transactions found | r2-fi-s61-r1.xml |
| 11 | `r=m&memberid=<stored>` | Member record (sanity: credentials valid) | r2-m-memberid.xml |
| 12 | `r=fi` (no season/round) | No transactions found | r2-fi-noparams.xml |

## Sources

- Live API probes (above) — authoritative for every verdict.
- "Blackout Rugby Api details" (official API docs HTML dump): Finances §~line 57; Match Summary §~line 544 (`&r=ms`, attendance block, notes).
- `API_ENDPOINTS_COMPLETE.md` §6, §15 — live-falsified codes.
- `endpoint-parameters.json` — `msum` entry live-falsified.
- `BlackoutRugbyApiClient.cs` lines 98 (`"fi"`), 281–283 (`"msum"` — broken), 613–654 (auth wiring); `BlackoutRugbyDashboard/Controllers/ApiTestController.cs` line 72 (calendar-year season default); `BlackoutRugbyDashboard/Pages/ApiTester.cshtml` line 312.
