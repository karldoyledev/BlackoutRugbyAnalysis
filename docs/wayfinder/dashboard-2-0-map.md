# Wayfinder Map: Dashboard 2.0 Spec

**Repo**: karldoyledev/BlackoutRugbyAnalysis — this file is the local draft of the GitHub issue labelled `wayfinder:map`. When pushed, the map becomes an issue; each ticket below becomes a child issue (`Part of #<map>`, labels `wayfinder:research` / `wayfinder:grilling` / `wayfinder:task`), blocking edges become GitHub native `blocked_by` dependencies.

**Tracker (live)**: Map = [#4](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/4) · R1 #5 · R2 #6 · R3 #7 · R4 #17 · D1 #8 · D2 #9 · D3 #10 · D4 #11 · D5 #12 · D6 #13 · D7 #14 · D8 #15 · D9 #16. All sub-issue links and 18 blocked_by edges verified wired.

## Destination

**A decision-complete specification for "Dashboard 2.0"** — the re-architected BlackoutRugbyAnalysis web app — covering:

1. **Accounts & login**: local sign-up (email + password) → "Link your club" (Member ID + Member Key, live-validated, stored encrypted) → subsequent logins auto-look-up Member, Team, and squad.
2. **Home page**: recent-fixture trend rows (W/L, opponent CSR, attendance, gate receipts) with links outward.
3. **Match Analysis page**: compact tactics panel + per-player stats grouped into tabs + team-vs-opponent compare row + rules-based recommendation button.
4. **Player History page**: per-player trends across cached fixtures, grouped by the same stat categories.
5. **API Playground** retained as-is (re-labeled); old Index folded into Home.
6. **API capability verification**: every unknown probed against the live API before the spec commits to it.

The spec is the destination artifact. **Plan, don't do**: build execution is a separate effort launched after this map is done.

## Notes (how to work this map)

- Resolve one decision ticket per session; research tickets may run together.
- Decision tickets: call **grilling** + **domain-modeling**. Research tickets: call **research**; probes go against `http://classic-api.blackoutrugby.com` — keep them minimal (rate limits: 3s between calls for first 120s of a burst, then 1s for next 120s). Developer credentials come from user-secrets after the scrub (R2-Q12); today they are in `BlackoutRugbyDashboard/appsettings.json` (DeveloperId 1333) — never publish probe commands with live keys.
- Glossary of record: `Context.md` (User, Member, Member Key, Team, Player, Fixture, Fixture Statistics, Player Statistics, Standing, Ranking, Division, League, Region, Transfer Market). New terms crystallising during tickets go there immediately.
- Existing architecture facts: ASP.NET Core 8 Razor Pages (`BlackoutRugbyDashboard`), shared `BlackoutRugbyApiClient.cs` (repo root, all 17 endpoints, already sends `r=fi` for finances), `BlackoutRugbyResponseAdapter` (XML→domain), file-based `SnapshotStore` (`Data/Snapshots/*.json`), parser tests in `BlackoutRugbyDashboard.Tests`.
- Committing to ticket recommendations by default is fine (user pre-authorized), but any change to a settled decision above needs the user explicitly.

## Decisions so far (settled during charting, rounds 1–3)

1. **Login mechanics** *(user's call)*: sign-up with email + password creates a local User; then collect Member ID + Member Key, validate live, store encrypted; later logins use only email + password and auto-look-up team + squad.
2. **Broken credentials** *(user's call)*: hard error on API-backed pages when stored Member credentials stop validating; Settings is the non-API recovery hatch (re-link path).
3. **Destination shape**: spec-first — the map produces the spec; build happens in fresh sessions afterwards.
4. **Account store**: ASP.NET Core Identity + EF Core + SQLite.
5. **Member-key protection at rest**: ASP.NET Core Data Protection.
6. **Sign-up/link flow**: two-step (Sign up → "Link your club", live-validated on save), skippable, relinkable later via Settings (covers in-game password changes invalidating the Member Key).
7. **Home page**: last 8 fixtures, current season, all competitions; W/L + score, opponent CSR, attendance, gate/match income, link to Match Analysis; no season dropdown in v1.
8. **Match page layout**: stat groups as tabs over one sortable player table (Attack / Defence / Kicking / Handling / Discipline / Lineout / Other); compact tactics panel on top; team-vs-opponent compare row.
9. **Tactics panel**: v1 shows only what the API verifiably returns (flat tactic sliders + XV + captain/kicker); per-area attack tactics UI only if research proves the API exposes them.
10. **Recommendations**: deterministic rules engine first, AI/LLM layer later; future-opponent preview deferred (see Out of scope).
11. **Player history**: cache-first — every viewed fixture persists to the local store; Player page trends aggregate the cache; v1 scope = current season + everything cached.
12. **Old pages**: Index folds into Home; ApiTester stays as "API Playground" (nav label); MemberStatsComparison stays linked until the Player page matures; Privacy deleted.
13. **Secrets hygiene**: scrub committed MemberKey + developer credentials out of `appsettings.json` into user-secrets as part of the login work.
14. **Wayfinding infra**: map + tickets live on GitHub Issues via `gh` CLI (installed this session; auth pending user).
15. **API fact**: client already sends `r=fi` for finances, matching the raw game docs; `API_ENDPOINTS_COMPLETE.md` §6 saying `r=f` is the doc error (fix during build).
16. **Lineups read (R1)**: `r=lu` is the live code (`r=li` invalid — client fix needed); no strategy fields verifiable on read (fixture lineup = XV + bench `b1–b8` + captain + kicker; response duplicates the `<lineup>` element); per-area attack tactics don't exist → out of scope; side-facts: fixtures read is `r=f` (`r=fix` invalid), `data_removed=1` edge → new ticket R4. Findings: `docs/research/r1-lineup-tactics-probe.md`.
17. **Finances + match summary (R2)**: match-summary read is `r=ms` (`msum` invalid — client fix needed, same bug class as R1's `li`); `fixtureids` batching verified (3/3), `<attendance>` = tail element per summary (tiers standing/uncovered/covered/members/corporate); `r=fi` verified live but returns zero transactions for team 45047 in every probed season/round with valid member credentials — gate receipts not sourceable from `fi` today (D5 implication: drop "fi per round" or make best-effort); undocumented: developer `d`/`dk` required on all reads; `data_removed=1` fixture returns full `ms` (feeds R4). Findings: `docs/research/r2-finances-matchsummary-probe.md`.
18. **Per-player fixture history (R3)**: per-Player-per-Fixture stats come only from `r=fs` with `fixtureid` + `playerstats=<playerId>` (~2 KB, one element) or `teamplayersstats=<teamId>` (~39 KB, full 23-slot squad); `fixtureids` batching verified (limiter respected per fixture); `playerstats` without `fixtureid` errors ("No valid fixture statistics found") — no player-history mode exists. Live shape is `<home_player_N>/<guest_player_N>` jersey-slot elements (N 1–23 = XV+bench; name = jersey worn that match), NOT the documented `<player_stats>` — the repo parser + regression XML read zero players from live XML and the client has no fs/ps methods (both are build work). Per-fixture fields: 51 snake_case = ps's set minus `avkickingmetres` plus `energy_before`/`energy_after`/`minutes_played`/`played`; fs `*_caps`/`played` unreliable (don't aggregate; `r=ps` is the caps source). Budget: Home 3 calls (1×`f` + 1×`ms` batch + 1×`fs` batch), Match 1–3, Player 0 cached / 1 per uncached fixture + 1×`ps` per player; bare `fs` = team+halftime stats only (58 fields, no players). Findings: `docs/research/r3-per-player-fixture-history-probe.md`.
19. **data_removed semantics (R4)**: the flag does not gate stats reads today — removed fixtures return full `fs` (39,009/38,961 B vs 38,988 B control, season-61 included) and full `ms` (R2 F3); no 0→1 flip in a 12.5 h re-probe of the last-8 window (flags identical to R1's snapshot; only gradient = s62 round-1 flagged, round-2 not). Undocumented field; flip trigger unknown → cache-first (decision 11) is the hedge; keep a defensive "stats unavailable" render on Home rows. Findings: `docs/research/r4-data-removed-semantics-probe.md`.
20. **Match cache data model (D1)**: both layers — raw XML archived per API call under `Data/MatchCache/raw/` (immutable, re-parseable; the data-removal hedge) + parsed SQLite via EF Core in the account DB (decision 4); per completed Fixture cache `f` extract + `ms` + our-squad `fs` + bare-`fs` team/halftime + `lu`; opponent player-level excluded; separate `ps` (Player+season) and TeamFact (point-in-time CSR/name — `f` carries neither) caches; append-only, no eviction, completed Fixtures never re-fetched, unplayed never cached, manual per-table reset in Settings; `SnapshotStore` untouched alongside (time-keyed comparison-page data, not a fixture cache); parsed rows: Fixture (all `f` metadata), PlayerFixture (FixtureId, TeamId, PlayerId + jersey + slot 1–23 + all 51 `fs` fields verbatim), TeamFixtureStat (FixtureId, side, half × 58 fields), MatchSummary (scalars + 5 attendance tiers + scorers joinable on PlayerId + injuries/subs JSON), PlayerSeason, TeamFact. Resolution: [#8](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/8).

21. **Auth & account spec (D3)**: Identity + EF Core + SQLite in one `DashboardDbContext` (migrations in web project, file at `Data/dashboard.db`); relaxed password policy (8+ alphanumeric) with lockout, no email confirmation; 14-day sliding persistent cookie with remember-me, ~12 h otherwise; `DashboardUser` carries MemberId + Data-Protection-encrypted MemberKey + TeamId + LinkedAt; two states Unlinked → Linked — a broken key is a runtime condition, not a state; unique index forbids two Users on one Member; Link-club validated on submit via one `r=m` probe (+ developer creds), per-failure-class error copy; unlinked Home = "Link your club" prompt card, Playground still reachable; Settings = link status + re-link + unlink (Match Cache survives) + D1's per-table cache reset + sign-out; broken-credentials panel deep-links to Settings; secrets scrub → user-secrets (`DeveloperKey`/`DeveloperIV` + demo member key; `DeveloperId` stays in appsettings), `DashboardDefaults` slims to `BaseEndpoint`, git-history keys mitigated by rotation (in-game password change / dev-key reset — manual spec step); `r=f`→`fi` doc fix bundled into build kickoff; no account-deletion flow in v1; glossary adds Club Link + Linked/Unlinked. Resolution: [#10](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/10).

## Not yet specified (fog)

- **Overall visual design/theming** of the new pages (no one has raised it; surface when the user does).
- Whatever else the research probes surface (new tickets get created + wired as answers land).

## Out of scope (consciously ruled out of this effort)

- **Per-area attack tactics UI** — the API has no per-area tactics read surface (verified by R1, [#5](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/5)); the in-game UI feature has no API backing.
- **Build execution** — the spec is the destination; building is a separate effort.
- **AI/LLM recommendation layer** — explicitly "later" (decision 10).
- **Future-opponent tactical preview** — deferred (decision 10).
- **Season dropdown / historical season browsing** on Home (decision 7).
- **Youth / National / U20 team views** — API supports the flags, but no one asked for them in v1.
- **Username + password game login** (scraping the game site) — undocumented, ToS-gray; replaced by decision 1.
- **Multi-Member linking per User / shared accounts / roles** — single Member per User is the v1 model.

## Tickets

Each ticket below becomes a child issue of the map. Blocked-by edges are listed per ticket and wired with GitHub native dependencies at push time. **Frontier at push: R1, R2, R3, D2, D3. R1 closed 2026-09-14 — findings in `docs/research/r1-lineup-tactics-probe.md`; R2 closed 2026-09-14 — findings in `docs/research/r2-finances-matchsummary-probe.md`; frontier was R3, R4, D2, D3. R3 closed 2026-09-15 — findings in `docs/research/r3-per-player-fixture-history-probe.md`; R4 closed 2026-09-15 — findings in `docs/research/r4-data-removed-semantics-probe.md`; D1 closed 2026-09-15 — resolution on [D1 - Lock the match-cache data model](https://github.com/karldoyledev/BlackoutRugbyAnalysis/issues/8); frontier now D2, D3, D4, D5.**

### R1 — Probe the lineup endpoint for tactics fields — `wayfinder:research` — frontier

**Question**: Does `r=li` (lineups READ) actually return the strategy fields (`pickandgo`, `driving`, `expansive`, `creative`, `defense`, `kicking`, `kickfortouch`, `upandunder`, `dropgoals`, `intensity`, `discipline`) — and do per-attack-area tactics (4 field areas × Pick and Go / Driving / Expansive / Creative, e.g. "Own 22") exist anywhere in the API?

**Why**: The Match page tactics panel (D6) can only commit to verified fields. Docs conflict: `API_ENDPOINTS_COMPLETE.md` §10 lists strategy fields only as WRITE params; the user's in-game UI shows per-area attack tactics the API docs never mention.

**How**: one `r=li&teamid=45047` call; one `r=li&teamid=45047&fixtureid=<a played fixture id from r=fix&teamid=45047&last=8>`. Inspect raw XML for strategy keys; grep the response for area-specific keys (own22, own22pickandgo, etc.). Respect rate limits.

**Capture**: raw XML snippets + verdict (flat fields on read: yes/no; per-area tactics exist: yes/no) in the resolution comment.

**Context**: `API_ENDPOINTS_COMPLETE.md` §10; raw dump "BlackoutRugby Api Details" (lineup section, ~line 313); `BlackoutRugbyApiClient.cs` → `GetLineupsAsync`.

**Notes**: resolve with the **research** skill.

### R2 — Verify finances endpoint + match-income categories + match-summary batch — `wayfinder:research` — frontier

**Question**: (a) Does `r=fi&season=X&round=Y` work live? (Client already sends `fi`; `API_ENDPOINTS_COMPLETE.md` §6 wrongly says `r=f` — record the doc fix.) (b) Which transaction `category`/`detail` values represent gate receipts / match income, and what other per-match money moves? (c) Does `r=msum` accept `fixtureids=1,2,3` batching, and where exactly does `attendance` sit in the payload?

**Why**: Home rows (D5) need "money made from the match" + attendance per fixture.

**How**: `r=fix&teamid=45047&last=8` → pick 2–3 played rounds → `r=fi` per season+round → one `r=msum&fixtureids=...` batch call. Enumerate every category value seen.

**Context**: `API_ENDPOINTS_COMPLETE.md` §6, §15; raw dump finances section (~line 57 of "BlackoutRugby Api Details"); `BlackoutRugbyApiClient.cs` → `GetFinancesAsync` (line ~98, already sends `fi`).

**Notes**: resolve with the **research** skill.

### R3 — Pin the per-player fixture-history pattern + call budget — `wayfinder:research` — frontier

**Question**: What is the cheapest reliable way to get per-Player per-Fixture stats? Specifically: the semantics of `r=fs&playerstats=[PLAYERID]` (per-fixture? needs `fixtureid`? historical?), vs the known `r=fs&fixtureid=X&teamplayersstats=TEAMID` (already parsed + tested). How does the per-fixture player field set differ from the `r=ps` cumulative field list? What is the rate-limit-safe call budget for (a) Home (8 fixtures) and (b) the Player page (N fixtures × relevant players)?

**Why**: D1 (cache model), D4 (tab fields), D7 (player trends) and D8 (rules) all consume this answer; the cache-first decision (11) needs realistic costs.

**How**: one `r=fs&fixtureid=<played>&teamplayersstats=45047`; probe `r=fs&playerstats=<knownPlayerId>` with and without `fixtureid`; diff field sets against `r=ps` §19 list; write the call-budget table.

**Context**: `API_ENDPOINTS_COMPLETE.md` §8, §19; `ParseFixturePlayerStatisticsTests.cs` (parser already handles `teamplayersstats`); `PlayerDashboardItem.cs`.

**Notes**: resolve with the **research** skill.

### R4 — Probe data_removed semantics for stats reads — `wayfinder:research` — frontier

**Question**: What do `r=fs` (fixture statistics) and `r=msum` (match summary) return for a fixture with `data_removed=1`? R1's fixtures probe showed every season-61 fixture in the last-8 window and one season-62 League fixture carries `data_removed=1`, while the two most recent are `0`. Empty elements, an error, or full data — and does the flag flip to 1 for current-season fixtures over time (will the Home page's last-8 window decay into missing stats)?

**Why**: D5 (Home page) must spec the W/L row for fixtures whose stats are unavailable; the cache-first decision (11) needs to know whether removed-data fixtures are worth caching.

**How**: probe `r=fs&fixtureid=X&teamplayersstats=45047` and `r=msum&fixtureid=X` on one `data_removed=1` fixture (e.g. 21416925) and one `data_removed=0` control (e.g. 21416928); diff shapes. If `r=msum` is rejected, try `r=ms` (docs conflict: `API_ENDPOINTS_COMPLETE.md` §15 vs `Blackout Rugby Api Reference.md` §12).

**Context**: R1 findings `docs/research/r1-lineup-tactics-probe.md` §F5; `API_ENDPOINTS_COMPLETE.md` §7, §14/§15; `Blackout Rugby Api Reference.md` §5, §12.

**Notes**: resolve with the **research** skill. Tracker: #17.

### D1 — Lock the match-cache data model — `wayfinder:grilling` — blocked by R3

**Question**: What exactly persists per viewed Fixture — parsed DTO, raw XML, or both? Storage layout under `Data/`, retention/eviction rules, and the aggregation key (PlayerId + FixtureId) the Player page joins on. Evolve the existing `SnapshotStore` or replace it outright?

**Why**: Decision 11 (cache-first) chose the strategy; this pins the shape so the spec can state it.

**Notes**: resolve with **grilling** + **domain-modeling** (likely adds glossary terms, e.g. Match Cache). Blocked by: R3.

### D2 — Lock the route map + navigation — `wayfinder:grilling` — frontier

**Question**: Exact routes, nav labels, auth gates, landing behavior. Proposal to grill: `/` Home; `/Fixtures/{id}` Match Analysis; `/Players/{id}` Player History; `/Account/SignUp`, `/Account/Login`, `/Account/LinkClub`, `/Account/Settings`; `/Playground` (ApiTester, moved). Nav order: Home · Squad (MemberStatsComparison) · Player History · Playground · Settings. Landing after login = Home, which hard-errors per decision 2 if stored Member credentials are stale. Which routes are public, which auth-only, which link-required?

**Notes**: resolve with **grilling**.

### D3 — Lock the auth & account spec section — `wayfinder:grilling` — frontier

**Question**: Pin the full account spec: Identity + SQLite config (provider, migrations home, password policy), cookie lifetime + remember-me, the sign-up state machine (unlinked → linked), Link-club live-validation UX + error copy, the Settings re-link flow (the recovery hatch from decision 2), and the secrets-scrub plan (what moves to user-secrets, what happens to `DashboardDefaults`, plus the `r=f`→`fi` doc fix from decision 15).

**Notes**: resolve with **grilling** + **domain-modeling**.

### D4 — Pin per-tab field lists for the Match page — `wayfinder:grilling` — blocked by R3

**Question**: Which verified Fixture Statistics fields go in each tab — Attack (metres gained, line breaks, intercepts, beaten defenders, try assists…), Defence (tackles, missed tackles, turnovers won…), Kicking (kicks, metres kicked, out on full, up-and-unders good/bad, missed pens/drop goals…), Handling (knock-ons, handling errors, forward passes, ball carries…), Discipline (yellow/red, penalties conceded, fights, penalty time…), Lineout (secured/conceded/stolen, throws good/bad…), Other (injuries, ball time…) — plus always-visible columns (name, jersey/position, points, CSR?) and the team-vs-opponent compare row fields. Reconcile the user's wishlist against R3's verified field availability; list every wishlist field the API does not provide per-fixture.

**Notes**: resolve with **grilling** + **domain-modeling** (stat-group names may join the glossary). Blocked by: R3.

### D5 — Lock the Home page spec — `wayfinder:grilling` — blocked by R2, R4

**Question**: Exact row composition per fixture (W/L + score, opponent name + CSR, attendance, match income from R2's category map, link), empty/edge states (bot matches, missing finance rows, unplayed-but-listed fixtures), and the data-call plan (fix last=8 → msum batch → teams batch → fi per round, all cache-backed per decision 11).

**Notes**: resolve with **grilling**. Blocked by: R2, R4.

### D6 — Lock the Match Analysis page spec — `wayfinder:grilling` — blocked by R1, D4

**Question**: Tactics panel contents per R1's verdict (flat sliders + XV + captain/kicker; per-area UI only if proven to exist), team compare row, the tab set from D4, default sort, injuries/notes display, and where the Recommendations output (D8) renders.

**Notes**: resolve with **grilling**. Blocked by: R1, D4.

### D7 — Lock the Player History page spec — `wayfinder:grilling` — blocked by D1, D4

**Question**: Trend granularity (per cached fixture), which stat groups trend (same groups as D4), visual treatment (sparklines vs tables vs both), scope (current season + cache per decision 11), and entry points (from a Match row, from a squad list).

**Notes**: resolve with **grilling**. Blocked by: D1, D4.

### D8 — Pin recommendation rules v1 — `wayfinder:grilling` — blocked by R3, D4

**Question**: The deterministic rule set: per-category your-team-vs-opponent comparisons, weakness→tactic-slider mappings (e.g. lost N lineouts → raise driving / cut expansive), flag thresholds, output rendering, and explicit non-goals (no AI in v1 per decision 10).

**Notes**: resolve with **grilling**. Blocked by: R3, D4.

### D9 — Assemble the Dashboard 2.0 spec — `wayfinder:task` — blocked by D1–D8

**Question**: No new decisions — assemble the spec at `docs/spec/dashboard-2-0-spec.md` from decisions 1–15 + D1–D8 resolutions. This is the destination artifact; when it's written and reviewed, the map is done and the build effort is launched.

**Notes**: resolve with the **to-spec** skill. Blocked by: D1, D2, D3, D4, D5, D6, D7, D8.

## Push plan (when gh is authenticated)

1. `gh label create wayfinder:map` (+ `wayfinder:research`, `wayfinder:grilling`, `wayfinder:task`).
2. Create the map issue from this file's sections (Destination / Notes / Decisions so far / Fog / Out of scope), label `wayfinder:map`.
3. Create R1, R2, R3, D1–D9 as issues with `Part of #<map>` first line + their type label; link each as a sub-issue of the map.
4. Wire `blocked_by` dependencies with `gh api --method POST repos/karldoyledev/BlackoutRugbyAnalysis/issues/<n>/dependencies/blocked_by -F issue_id=<blocker-db-id>` per the edges above.
5. Frontier is then R1, R2, R3, D2, D3 — resolve one decision ticket per session; research tickets may run together.



