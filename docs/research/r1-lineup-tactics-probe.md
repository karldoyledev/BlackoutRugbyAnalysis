# R1 — Probe the lineup endpoint for tactics fields

**Ticket**: [R1 — Probe the lineup endpoint for tactics fields](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/5) · child of [Wayfinder Map: Dashboard 2.0 Spec](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/4)
**Probed**: 2026-09-14 (BRT season 62, round 3, day 1) · 8 live calls against `http://classic-api.blackoutrugby.com`, ≥3 s apart (documented burst limit)
**Auth**: owner member credentials + developer credentials read from `BlackoutRugbyDashboard/appsettings.json` at runtime; never published; artifact files scrubbed + verified clean
**Raw responses**: [`docs/research/artifacts/r1-*.xml`](artifacts/) (8 files, response bodies only — no request URLs)

## Verdict

1. **`r=li` is not a valid request code.** The live API rejects it; the lineups read code is **`r=lu`**. The client's `GetLineupsAsync` (`BlackoutRugbyApiClient.cs` line 206, sends `"li"`) is broken against the live API and must send `"lu"`.
2. **Strategy fields could not be verified on any lineup read — and are absent from every lineup readable today**, including a played fixture read with owner credentials. The only evidence they exist on read at all is the 2010-era official doc example (default lineup, owner key). The spec must not commit to strategy fields on read (decision 9 already requires verified fields only).
3. **Per-area attack tactics (4 field areas × Pick and Go / Driving / Expansive / Creative) do not exist anywhere in the API** — no doc mention, no live response tag. The fog item resolves to **out of scope** per decision 9's condition.

## Findings

### F1 — Request code: `r=lu`, not `r=li`

- Live: `r=li&teamid=45047` → `<error>"li" is not a valid request</error>` ([r1-lineups-li-default.xml](artifacts/r1-lineups-li-default.xml)); `r=lu&teamid=45047` → valid envelope (application-level result, not a code rejection) ([r1-lineups-lu-default.xml](artifacts/r1-lineups-lu-default.xml)).
- Official raw dump: "Blackout Rugby Api details" line 313 — `&r=lu` "(Lower case L, followed by a u)". Corroborated by `Blackout Rugby Api Reference.md` §8.
- `API_ENDPOINTS_COMPLETE.md` §10 (`r=li`) is wrong. Third request-code error in that file (finances `r=f` per decision 15, fixtures `r=fix` per F5, now lineups `r=li`).
- **Build implication**: fix `GetLineupsAsync` to send `"lu"`.

### F2 — Strategy fields: absent from every readable lineup (owner credentials used)

Live probes, all with the team owner's member key:

| Probe | Result | Artifact |
|---|---|---|
| `r=lu&teamid=45047` (default lineup) | `<error>No lineups found</error>` — no default lineup saved for team 45047 | [r1-lineups-lu-default.xml](artifacts/r1-lineups-lu-default.xml) |
| `r=lu&teamid=45047&fixtureid=0` (explicit default) | `<error>No lineups found</error>` | [r1-lineups-lu-fixture-0.xml](artifacts/r1-lineups-lu-fixture-0.xml) |
| `r=lu&teamid=45047&fixtureid=21416928` (played: League R2, 45047 v 45037) | Full lineup, **no strategy fields** | [r1-lineups-lu-fixture-21416928.xml](artifacts/r1-lineups-lu-fixture-21416928.xml) |
| `r=lu&teamid=45047&fixtureid=21462085` (upcoming Cup R3) | `<error>No lineups found</error>` — no lineup set yet | [r1-lineups-lu-fixture-21462085-future.xml](artifacts/r1-lineups-lu-fixture-21462085-future.xml) |

The docs' owner-read example with strategy fields ("Blackout Rugby Api details" lines 344–418: `pickandgo`, `driving`, `expansive`, `creative`, `defense` (text, e.g. `drift`), `kicking`, `kickfortouch`, `upandunder`, `dropgoals`, `intensity`, `discipline`, plus `captain1/2`, `kicker1/2`) is the **default-lineup** (`fixtureid=0`) shape from the **2011-08-13** API reference dump — and the live API already diverges from it in at least one detail (bench size, F4). While no default lineup exists for team 45047, the doc claim is unverifiable. **Nothing readable today exposes tactics.**

### F3 — Per-area attack tactics: no representation anywhere

Grep across all three doc dumps, `endpoint-parameters.json`, and all 8 live artifacts for `own22`/`opp22`/`ownhalf`/`opphalf`/area/quarter/zone/third/territory-style tags: **zero matches**. The in-game per-area tactics UI has no read surface in this API → **out of scope** for Dashboard 2.0 v1 (closes the map's fog item per decision 9).

### F4 — Verified live lineup shape (played fixture, owner read)

`p1`–`p15`, `b1`–`b8`, `captain`, `kicker`, `id`, `teamid`, `fixtureid`. Notes:

- **Bench is 8 spots** (`b1`–`b8`) — both doc sources say `b1`–`b7` (stale). Data model and Match page (D1/D6) must use 8.
- **No `deadline`** element on the played-fixture read (docs list it; live response omits it).
- **The response duplicates the entire `<lineup>` element** (identical twins). Parsers must dedupe / take the first — cache model (D1) and Match page (D6) note.

### F5 — Side-verdicts verified en route (for other tickets)

- **Fixtures read is `r=f`** — live-verified both variants: `r=f&teamid=45047&last=8` ([r1-fixtures-f-last8.xml](artifacts/r1-fixtures-f-last8.xml)) and `r=f&future=45047` ([r1-fixtures-f-future.xml](artifacts/r1-fixtures-f-future.xml)). Official raw dump line 86: `&r=f`. Client line 128 (`"f"`) is correct; `API_ENDPOINTS_COMPLETE.md` §7 (`r=fix`) is wrong (live-probed: `"fix" is not a valid request`, [r1-fixtures-last8.xml](artifacts/r1-fixtures-last8.xml)).
- **Undocumented fixture-read fields**: `weather`, `data_removed`, `stadium`, plus `matchfinish` on played fixtures — beyond `Blackout Rugby Api Reference.md` §5's field list.
- **`data_removed=1`** appears on every season-61 fixture in the last-8 window and one season-62 League fixture; only the two most recent (League R2, Cup R2) are `0`. Unknown what `r=fs` / `r=msum` return for removed-data fixtures — directly affects D5's Home edge states (W/L row without stats) → new research ticket **R4** raised on the map, blocking D5.

## Implications for map tickets

| Ticket | Implication |
|---|---|
| D6 (Match Analysis) | Tactics panel can only show verified data: XV (p1–p15, b1–b8) + captain + kicker. Strategy sliders unverified on read → drop or label unverified; per-area UI out of scope (F3). |
| D1 (cache model) | Dedupe duplicate `<lineup>` elements; bench = 8; no `deadline` on fixture lineup reads. |
| D5 (Home) | `data_removed=1` edge states → R4. |
| Build | Fix `GetLineupsAsync` request code `"li"` → `"lu"`. |

## Probe log

| # | Request (auth params elided) | Outcome | Artifact |
|---|---|---|---|
| 1 | `r=lu&teamid=45047` | No lineups found | r1-lineups-lu-default.xml |
| 2 | `r=li&teamid=45047` | `"li" is not a valid request` | r1-lineups-li-default.xml |
| 3 | `r=fix&teamid=45047&last=8` | `"fix" is not a valid request` | r1-fixtures-last8.xml |
| 4 | `r=f&teamid=45047&last=8` | 8 fixtures | r1-fixtures-f-last8.xml |
| 5 | `r=lu&teamid=45047&fixtureid=21416928` | Lineup, no strategy fields, duplicated element | r1-lineups-lu-fixture-21416928.xml |
| 6 | `r=lu&teamid=45047&fixtureid=0` | No lineups found | r1-lineups-lu-fixture-0.xml |
| 7 | `r=f&future=45047` | 8 future fixtures | r1-fixtures-f-future.xml |
| 8 | `r=lu&teamid=45047&fixtureid=21462085` | No lineups found | r1-lineups-lu-fixture-21462085-future.xml |

## Sources

- Live API probes (above) — the authoritative source for everything marked "live".
- "Blackout Rugby Api details" (official API docs HTML dump): Lineups §lines 310–418 (request 313; public response 315–343; owner response with strategy 344–418; notes 418); Fixtures §lines 83–108 (request 86).
- "Blackout Rugby Api Details" (second dump, self-dated "API Reference (2011-08-13)").
- `API_ENDPOINTS_COMPLETE.md` §7, §10 — request codes live-falsified.
- `Blackout Rugby Api Reference.md` §5, §8 — consistent with raw dump.
- `BlackoutRugbyApiClient.cs` lines 128 (`"f"`), 206 (`"li"`), 613–654 (auth param wiring: `d`, `dk`, `memberid`, `memberkey`).
