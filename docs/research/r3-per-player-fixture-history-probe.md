# R3 — Pin the per-player fixture-history pattern + call budget

**Ticket**: [R3 - Pin the per-player fixture-history pattern + call budget](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/7) · child of [Wayfinder Map: Dashboard 2.0 Spec](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/4)
**Probed**: 2026-09-15 (BRT season 62, round 3, day 2) · 11 live calls against `http://classic-api.blackoutrugby.com`, ≥3 s apart (documented burst limit; burst shared with R4)
**Auth**: owner member credentials + developer credentials read from `BlackoutRugbyDashboard/appsettings.json` at runtime; never published; artifact files scrub-checked (response bodies only — no request URLs)
**Raw responses**: [`docs/research/artifacts/r3-*.xml`](artifacts/) (8 files; 3 further shared files under `r4-*`)

## Verdict

1. **There is no player-history mode.** Per-Player-per-Fixture stats come only from `r=fs` with a `fixtureid`, narrowed by `playerstats=<playerId>` (~2 KB, that player's single element) or `teamplayersstats=<teamId>` (~39 KB, all 23 squad slots). `r=fs&playerstats=<id>` without `fixtureid` → `<error>No valid fixture statistics found</error>`. A player's history is therefore N per-fixture reads — which `fixtureids` batching collapses into one call per batch (verified N=2; limiter respected per fixture).
2. **The live response shape is neither the documented shape nor the shape the repo's parser expects.** Live `fs` returns one `<home_player_N id="…" teamid="…">` / `<guest_player_N …>` element per jersey slot (N = 1–23: XV then bench 16–23), **not** `<player_stats>` elements. The element name is the jersey worn *in that match* (player 16396178: #10 in League R2, #13 in League R1, #20 in Cup R2). No duplicate elements (unlike `<lineup>`, R1 F4). Slot order matches the lineup read exactly (verified element ids 1–23 = `p1`–`p15` + `b1`–`b8` from R1's lineup artifact).
3. **Values are genuinely per-fixture** — cross-checked against `ms`: the League-R2 read's `<conversions>13</conversions>` matches the match summary's scoring list for fixture 21416928 (player 16396178, count 13).
4. **Field set: fs has 51 snake_case fields vs `r=ps`'s 49 concatenated fields** (live `ps` verified to match `API_ENDPOINTS_COMPLETE.md` §19 exactly). fs = ps's stat core minus `avkickingmetres`, plus `energy_before`, `energy_after`, `minutes_played`, `played`. fs's `*_caps` fields are competition-flagged for the fixture's own match (league read → `league_caps=1`/`cup_caps=0`; cup read → `cup_caps=1`/`league_caps=0`); their exact window (per-fixture vs season-to-date) is ambiguous from available probes — **do not aggregate fs caps; `r=ps` is the caps source**. `played` semantics unclear (both 80+ minute starters read `0`; a 10-minute sub read `1`) — don't rely on it.
5. **Bare `r=fs&fixtureid=X` returns team stats only** — `<home_team_stats>`, `<guest_team_stats>`, `<home_team_halftime_stats>`, `<guest_team_halftime_stats>` (58 fields each; team-only fields: `possession`, `territory`, `phases`, `sevenplus_phases`, `rucks_won`, `mauls_won`, `scrums_won/lost/put_in/against_put_in/secured`, `lineouts_won/lost/thrown/against_throw`, `minutes_in_22`, `injury_breaks`, `category_1..6_injuries`, `penalties_won`, `turnovers_conceded`, `matches_played`) and **no player elements** — contradicting the doc warning that a bare read "returns all team statistics and all player statistics".

## Findings

### F1 — Request shapes (all live-verified)

| Shape | Outcome | Artifact |
|---|---|---|
| `r=fs&fixtureid=X&teamplayersstats=45047` | 23 slot elements for team 45047 only; no team-stats blocks | [r3-fs-teamplayers-21416928.xml](artifacts/r3-fs-teamplayers-21416928.xml) |
| `r=fs&fixtureid=X&playerstats=<playerId>` | 1 slot element for that player (~2 KB) | [r3-fs-playerstats-withfixture.xml](artifacts/r3-fs-playerstats-withfixture.xml) |
| `r=fs&playerstats=<playerId>` (no fixtureid) | `<error>No valid fixture statistics found</error>` | [r3-fs-playerstats-nofixture.xml](artifacts/r3-fs-playerstats-nofixture.xml) |
| `r=fs&fixtureids=X,Y&teamplayersstats=45047` | 2 `<fixture_statistics>` elements, limiter respected per fixture (77,772 B) | [r3-fs-batch2.xml](artifacts/r3-fs-batch2.xml) |
| `r=fs&fixtureid=X` (bare) | team + half-time stats only, no players (8,080 B) | [r3-fs-bare-21416928.xml](artifacts/r3-fs-bare-21416928.xml) |
| `r=ps&playerid=<playerId>` | 1 `<player_statistics>` element, 49 fields, matches §19 exactly (1,817 B) | [r3-ps-playerid.xml](artifacts/r3-ps-playerid.xml) |

Doc source for the parameters ("Blackout Rugby Api details" line 117) matches live on parameter *names* (`fixtureid`, `fixtureids`, `teamstats`, `teamplayersstats`, `playerstats`) but documents `playerstats` only as a fixture-read limiter — the live error on a standalone call confirms exactly that reading. `endpoint-parameters.json` agrees.

### F2 — Live element shape: `<home_player_N>` / `<guest_player_N>`

- One element per squad slot, N = 1–23. Elements 1–15 map to the lineup's `p1`–`p15`, 16–23 to `b1`–`b8` (all 23 ids verified equal to R1's lineup artifact for fixture 21416928).
- Player identity is duplicated: `id`/`teamid` **attributes** and `<id>` child carry the same values (parsers should read one, ignore the other).
- The element name encodes side (home/guest) + **jersey worn in that match**, which varies: player 16396178 was `home_player_10` (League R2), `guest_player_13` (League R1), `home_player_20` (Cup R2). UI must not treat N as a fixed position; jersey comes from the element name per fixture.
- No response duplication (compare `<lineup>` twins, R1 F4).

### F3 — Field set: fs per-fixture vs ps cumulative

- Live `r=ps&playerid=16396178` returned exactly the §19 field list (49 fields incl. caps and `avkickingmetres`) — the cumulative source is stable and parsed already (`ParsePlayerStatisticsTests.cs`).
- fs per-fixture fields (51, union over all 23 slots, verified identical across control, removed fixtures, and batch): `bad_kicks, bad_up_and_unders, ball_time, beaten_defenders, conversions, cup_caps, dropgoals, energy_after, energy_before, fights, forward_passes, friendly_caps, good_kicks, good_up_and_unders, handling_errors, id, injuries, intercepts, kicking_metres, kicks, kicks_out_on_the_full, knockons, league_caps, linebreaks, lineouts_conceded, lineouts_secured, lineouts_stolen, metres_gained, minutes_played, missed_conversions, missed_dropgoals, missed_penalties, missed_tackles, national_caps, other_caps, penalties, penalties_conceded, played, red_cards, successful_lineout_throws, tackles, total_points, tries, try_assists, turnovers, under_twenty_caps, under_twenty_world_cup_caps, unsuccessful_lineout_throws, up_and_unders, world_cup_caps, yellow_cards`.
- Naming convention differs: fs is snake_case (`metres_gained`), ps is concatenated (`metresgained`) — the adapter's alias handling already anticipates this.
- fs-only: `energy_before`, `energy_after`, `minutes_played`, `played`. ps-only: `avkickingmetres` (and career-only caps semantics).
- Caps evidence: league read of League R1 → `league_caps=1`, `cup_caps=0`; cup read of Cup R2 → `cup_caps=1`, `league_caps=0`. Per-fixture and season-to-date-excluding-removed both fit these probes; ambiguous → spec must not aggregate fs caps. `r=ps` returns career caps (leaguecaps=75 for this player) — that is the caps source.
- `played` anomaly: starters (81/82 min) read `0`; the 10-minute sub read `1`. Unverified meaning (possibly live-match or sub flag) — exclude from the spec.

### F4 — Team + half-time stats (bare read)

- `home_team_stats`/`guest_team_stats` carry 58 fields = the player stat core + team-only fields (possession, territory, phases, set-piece detail, injury categories). Sample (home, fixture 21416928): tries=13, conversions=13, penalties=2, possession=3642, territory=3629 — per-match values; possession/territory units unverified (seconds suspected; pin at build time if the compare row displays them).
- `<home_team_halftime_stats>`/`<guest_team_halftime_stats>` are the same 58-field shape at half-time (tries=6, total_points=48 at HT) — a free half-time-score source for the Match page (D6).
- With `teamplayersstats`/`playerstats` limiters the team-stats blocks are absent; a page wanting both player and team stats needs one bare read + one limited read (2 calls), or aggregates team totals from `ms`/players.

### F5 — Call budget (rate-limit-safe)

Documented limits: 3 s between calls for the first 120 s of a burst, then 1 s for the next 120 s. The 11-call burst here ran at 3–4 s spacing, well inside the first window.

| Page | Plan | API calls |
|---|---|---|
| Home (last 8) | `r=f&last=8` → `r=ms&fixtureids=<8>` (R2-verified) → `r=fs&fixtureids=<8>&teamplayersstats=45047` | **3** (batching verified at N=2; if the API caps batch size, worst case 10 — still inside limits) |
| Match Analysis | `r=fs&fixtureid=X&teamplayersstats=<own>` (+ bare `r=fs&fixtureid=X` when the compare row wants possession/territory/set-piece team stats) | **1–3** (opponent players come from the same teamplayersstats call only if you read both teams; else 1 more limiter call) |
| Player History | cache-first (decision 11): **0** extra calls for cached fixtures; for uncached fixtures 1× `fs` per fixture (teamplayersstats returns the whole squad in one call — *not* per player); plus 1× `ps` per player for cumulative stats/caps | **0–N + 1 per player** |
| API Playground | unchanged | — |

Payload sizes: teamplayersstats ≈ 39 KB/fixture; playerstats ≈ 2 KB/fixture-player; ps ≈ 2 KB/player; ms ≈ 2.3 KB/fixture (R2). An 8-fixture batched fs read ≈ 310 KB — one call.

## Implications for map tickets

| Ticket | Implication |
|---|---|
| D1 (cache model) | Cache per-fixture `fs` in the live `<home_player_N>`/`<guest_player_N>` shape (jersey = element name; no dedupe needed; identity = `id` attr or child). Cache bare-fs team/halftime blocks for the compare row. Aggregation key PlayerId (element `<id>`) + FixtureId (envelope/element `id`) is directly supported by the shape. |
| D4 (tab fields) | Tab fields come from the 51-field fs list mapped into the user's stat groups; team compare row fields from bare fs's 58-field team stats. Drop `avkickingmetres` (not in fs); treat fs `*_caps` and `played` as unreliable (use `ps` for caps). |
| D5 (Home) | Nothing new — Home needs `f` + `ms` only (R2); fs enters via the Match link. |
| D6 (Match Analysis) | Bench = slots 16–23; jersey number per element name; half-time score available from halftime stats blocks; tactics panel unchanged (R1). |
| D7 (Player trends) | Trends aggregate per-fixture values of the shared stat fields across cached fixtures; caps/cumulative come from `ps` (1 call/player); entry cost for uncached fixtures is 1 fs call per fixture (whole squad), not per player. |
| D8 (rules) | Rules can consume per-fixture player fields plus team-only fields (possession, territory, phases, lineout/set-piece detail) — the team-only ones require the bare fs read. |
| Build | Client has **no** `fs`/`ps` methods at all (grep-verified) — add `GetFixtureStatisticsAsync`/`GetFixtureStatisticsBatchAsync` + `GetPlayerStatisticsAsync`. **Rewrite `ParseFixturePlayerStatistics` for the live shape**: the current parser + regression XML expect `<player_stats>` elements and will return zero players from every live response. `API_ENDPOINTS_COMPLETE.md` §8's response description is doc-only (its "returns all…player statistics" claim falsified live). |

## Probe log

| # | Request (auth params elided) | Outcome | Artifact |
|---|---|---|---|
| 1 | `r=fs&fixtureid=21416928&teamplayersstats=45047` | 23 slot elements (control) | r3-fs-teamplayers-21416928.xml |
| 2 | `r=fs&fixtureid=21416928&playerstats=16396178` | 1 element (home_player_10) | r3-fs-playerstats-withfixture.xml |
| 3 | `r=fs&playerstats=16396178` | `No valid fixture statistics found` | r3-fs-playerstats-nofixture.xml |
| 4 | `r=fs&fixtureids=21416925,21416928&teamplayersstats=45047` | 2/2 fixture_statistics, limiter respected | r3-fs-batch2.xml |
| 5 | `r=ps&playerid=16396178` | 49-field cumulative element | r3-ps-playerid.xml |
| 6 | `r=fs&fixtureid=21416928` | team + halftime stats only | r3-fs-bare-21416928.xml |
| 7 | `r=fs&fixtureid=21416925&playerstats=16396178` | 1 element (guest_player_13, removed fixture) | r3-fs-playerstats-21416925-r1.xml |
| 8 | `r=fs&fixtureid=21460743&playerstats=16396178` | 1 element (home_player_20, cup) | r3-fs-playerstats-21460743-cup.xml |

(Probes 1 and parts of 4/7 are shared with R4's removed-fixture matrix; R4's probe log cross-references this list. Calls ran 3–4 s apart in one burst with R4's probes.)

## Sources

- Live API probes (above) — authoritative for everything marked "live".
- "Blackout Rugby Api details" (official API docs HTML dump): Fixture Statistics §line 117 (request shapes; `playerstats` as fixture-read limiter; empty-element semantics).
- `API_ENDPOINTS_COMPLETE.md` §8 (fs params — names verified), §19 (ps fields — verified live in full).
- `endpoint-parameters.json` (`teamplayersstats`, `playerstats`, `playerstats`→`ps` entries).
- `BlackoutRugbyApiClient.cs` lines 613–654 (auth wiring `d`/`dk`/`memberid`/`memberkey`); absence of fs/ps methods (grep-verified).
- `ParseFixturePlayerStatisticsTests.cs`, `BlackoutRugbyDashboard/Models/PlayerDashboardItem.cs` (field expectations), `docs/research/artifacts/r1-lineups-lu-fixture-21416928.xml` (slot↔lineup id verification), `docs/research/artifacts/r2-ms-batch3.xml` (conversions cross-check).
- R1/R2 findings docs for shared burst conventions and prior verdicts.
