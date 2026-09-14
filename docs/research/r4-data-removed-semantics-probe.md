# R4 — Probe data_removed semantics for stats reads (fs/ms)

**Ticket**: [R4 - Probe data_removed semantics for stats reads (fs/ms)](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/17) · child of [Wayfinder Map: Dashboard 2.0 Spec](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/4)
**Probed**: 2026-09-15 (BRT season 62, round 3, day 2) · shares the 11-call burst with R3 (≥3 s apart, documented burst limit)
**Auth**: owner member credentials + developer credentials read from `BlackoutRugbyDashboard/appsettings.json` at runtime; never published; artifact files scrub-checked (response bodies only — no request URLs)
**Raw responses**: [`docs/research/artifacts/r4-*.xml`](artifacts/) (3 files; shared control/baseline files under `r3-*`)

## Verdict

1. **`data_removed=1` does not gate stats reads today.** `r=fs` returns full 23-slot player data for removed fixtures — 21416925 (s62 League R1, removed) 39,009 B vs 38,988 B control; 21305602 (s61 League R14, removed) 38,961 B — same shape, every field populated (energy before/after, minutes_played=82, kicks, caps flags). `r=ms` on the same removed fixture was already proven full (R2 F3). No error envelopes anywhere.
2. **No flip observed.** `r=f&teamid=45047&last=8` re-probed ~12.5 h after R1's snapshot (2026-09-14 17:53 → 2026-09-15 06:17 BRT) shows the identical 8 fixtures with identical flags. The only gradient ever observed: all season-62 round-1 fixtures are flagged while round-2 are not (R1 F5). When/why the flag flips remains unknown — the field is undocumented in every doc source.
3. **`r=msum` stays invalid** (R2 falsified it); `r=ms` is the read code. Removed fixtures change neither the shape nor the error behavior of `fs` or `ms`.

## Findings

### F1 — fs on removed fixtures vs control (shape + size diff)

| Probe | Fixture | data_removed | Bytes | Shape |
|---|---|---|---|---|
| `r=fs&fixtureid=21416928&teamplayersstats=45047` | s62 League R2 (control) | 0 | 38,988 | 23 × `home_player_N` |
| `r=fs&fixtureid=21416925&teamplayersstats=45047` | s62 League R1 | 1 | 39,009 | 23 × `guest_player_N`, all fields populated |
| `r=fs&fixtureid=21305602&teamplayersstats=45047` | s61 League R14 | 1 | 38,961 | 23 × `home_player_N`, all fields populated |

The removed-fixture reads are byte-comparable to the control and carry live per-match values (player 16396178: 82 minutes, 5 tackles, energy 11→10 in R1's read). Nothing is emptied, error'd, or trimmed.

### F2 — Flag stability re-probe (last-8 window, T+12.5 h)

`r=f&teamid=45047&last=8` — flags identical to R1's 2026-09-14 17:53 BRT snapshot in both probes:

| Fixture | Competition | data_removed (09-14) | data_removed (09-15) |
|---|---|---|---|
| 21416928 | League R2 | 0 | 0 |
| 21460743 | Cup R2 | 0 | 0 |
| 21416925 | League R1 | 1 | 1 |
| 21458860 | ThursdayFriendly R1 | 1 | 1 |
| 21457301 | Cup R1 | 1 | 1 |
| 21361248 | s61 BronzeFinal | 1 | 1 |
| 21356372 | s61 SemiFinal | 1 | 1 |
| 21305602 | s61 League R14 | 1 | 1 |

(The window itself was unchanged — League R3 had not yet been played as of the re-probe.)

### F3 — What `data_removed` is *not*

- **Not a stats-read gate** — `fs` and `ms` both return full data for flagged fixtures (this ticket + R2 F3).
- **Not documented** — absent from `Blackout Rugby Api Reference.md` §5's field list (R1 F5) and from the raw doc dumps; this file is the semantic record.
- **Not an error condition** — flagged fixtures produce normal envelopes on every read probed.
- Remaining unknown: the actual trigger/timing of the flag (round-1 fixtures were already flagged while round-2 were not, one day into round 3) and whether reads ever change behind it. Empirically, season-61 fixtures months old still serve full stats.

## Implications for map tickets

| Ticket | Implication |
|---|---|
| D5 (Home) | No removed-data edge state is needed today — every last-8 fixture serves full stats. Keep a defensive "stats unavailable" row render anyway: the flag is undocumented and its trigger unknown, so the guarantee is observational, not contractual. W/L + score still come from `r=f`; attendance/scorers from `r=ms`. |
| D1 (cache model) | Cache-first (decision 11) is the hedge: every viewed fixture persists locally, so any future removal behind the flag is invisible to the app. Cache flagged fixtures exactly like unflagged ones. |
| D7 (Player trends) | Season-61 remnants in the last-8 window are trendable today — no data holes to spec around. |
| D8 (rules) | The rules engine can rely on per-fixture stats across the whole last-8 window, including flagged fixtures. |
| Docs | `data_removed` remains undocumented upstream; `API_ENDPOINTS_COMPLETE.md` §15 (`msum`) stays falsified per R2 — no doc fixes arise from this ticket beyond recording semantics here. |

## Probe log

| # | Request (auth params elided) | Outcome | Artifact |
|---|---|---|---|
| 1 | `r=f&teamid=45047&last=8` | 8 fixtures, flags unchanged vs R1 | r4-f-last8-refresh.xml |
| 2 | `r=fs&fixtureid=21416925&teamplayersstats=45047` | full 23-slot read (removed) | r4-fs-teamplayers-21416925.xml |
| 3 | `r=fs&fixtureid=21305602&teamplayersstats=45047` | full 23-slot read (removed, s61) | r4-fs-teamplayers-21305602.xml |

(Probes 2–3 shared the burst with R3; the control read and the `ms`-on-removed-fixture proof are logged in R3/R2 respectively. `r=msum` was not re-probed — R2 F1 falsified it live and nothing in this ticket's scope suggested re-testing.)

## Sources

- Live API probes (above) — authoritative for every verdict.
- R1 findings `docs/research/r1-lineup-tactics-probe.md` §F5 (flag discovery + first snapshot), R2 findings `docs/research/r2-finances-matchsummary-probe.md` §F3 (`ms` full on removed fixture; `msum` invalid).
- `docs/research/artifacts/r1-fixtures-f-last8.xml` (baseline snapshot for the flip check).
- `API_ENDPOINTS_COMPLETE.md` §7, §14/§15; `Blackout Rugby Api Reference.md` §5, §12 (per ticket context — all doc-side, none document `data_removed`).
