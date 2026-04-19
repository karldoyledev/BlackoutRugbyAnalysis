# Blackout Rugby API Reference

Source: `html of api details`

## Overview
- Base response root includes Blackout Rugby time and current cycle info.
- All times are in BRT (GMT+12).
- Responses are XML-like and use query parameter `r` to specify the request type.

## Response root example
```xml
<blackoutrugby_api_response
  brt_timestamp="..."
  brt_sql="..."
  brt_iso_8601="..."
  season="..." round="..." day="...">
  ...
</blackoutrugby_api_response>
```

## API modules

### 1. Countries (`r=c`)
- Request:
  - `&iso=[COUNTRY_ISO]`
  - `&isos=[COUNTRY_ISO],[COUNTRY_ISO],...`
  - `&iso=all` or `&isos=all`
- Response fields:
  - `country`, `iso`, `name`, `time_offset`, `timezone`

### 2. Date and Time
- Response attributes:
  - `brt_timestamp`
  - `brt_sql`
  - `brt_iso_8601`
  - `season`
  - `round`
  - `day`

### 3. Divisions (`r=d`)
- Request:
  - `&divisionid=[DIVISION_ID]`
  - `&divisionids=[DIVISION_ID],[DIVISION_ID],...`
  - `&season=[SEASON]&country_iso=[COUNTRY_ISO]&division=[DIVISION]`
  - `&country_iso=[COUNTRY_ISO]`
- Response example: `<division id="...">`

### 4. Finances (`r=fi`)
- Request:
  - `&season=[SEASON]&round=[ROUND]`
- Response fields:
  - `id`, `teamid`, `amount`, `category`, `detail`, `type`, `balance`, `date`
- Notes:
  - `type`: `1` = Income, `2` = Expense
  - `date`: Timestamp BRT
  - `balance`: bank balance after the transaction

### 5. Fixtures (`r=f`)
- Request:
  - `&fixtureid=[FIXTURE_ID]`
  - `&fixtureids=[FIXTURE_ID],[FIXTURE_ID],...`
  - `&teamid=[TEAM_ID]&last=[NUMBER_OF_FIXTURES]`
  - `&future=[TEAM_ID]`
  - `&past=[TEAM_ID]`
  - `&youth=1`, `&nat=1`, `&u20=1`
  - `&latest=[LEAGUE_ID]`
  - `&leagueid=[LEAGUE_ID]&season=[SEASON]&round=[ROUND]`
  - `&roundrobin=1&nat=1` or `&roundrobin=1&u20=1`
  - `&friendlycompid=[FRIENDLY_COMP_ID]`
- Response fields:
  - `id`, `season`, `country_iso`, `leagueid`, `round`, `hometeamid`, `guestteamid`, `competition`, `botmatch`, `matchstart`
- Notes:
  - `matchstart`: Timestamp BRT
  - `botmatch`: `1` if the match was played between two bots
  - Valid competition values include: `Friendly`, `ThursdayFriendly`, `Cup`, `League`, `SemiFinal`, `BronzeFinal`, `Final`, `Qualifier`, `International`, `WorldCup`, `U20International`, `U20WorldCup`

### 6. Fixture Statistics (`r=fs`)
- Request:
  - `&fixtureid=[FIXTURE_ID]`
  - `&fixtureids=[FIXTURE_ID],[FIXTURE_ID],...`
  - `&teamstats=[TEAM_ID]`
  - `&teamplayersstats=[TEAM_ID]`
  - `&playerstats=[PLAYER_ID]`
- Response includes `<fixture_statistics>` with `home_team_stats`, `guest_team_stats`, and player statistics.
- Notes:
  - Use `teamstats`, `teamplayersstats`, or `playerstats` to limit returned data.
  - Fixture stats can be expensive to query.

### 7. Leagues (`r=l`)
- Request:
  - `&leagueid=[LEAGUE_ID]`
  - `&leagueids=[LEAGUE_ID],[LEAGUE_ID],...`
  - `&season=[SEASON]&country_iso=[COUNTRY_ISO]&division=[DIVISION]&league=[LEAGUE]`
  - `&season=[SEASON]&country_iso=[COUNTRY_ISO]&division=[DIVISION]`
  - `&divisionid=[DIVISION_ID]`
- Response fields:
  - `id`, `country_iso`, `season`, `division`, `league`, `lounge`

### 8. Lineups (`r=lu`)
- Request:
  - `&teamid=[TEAM_ID]`
  - `&teamid=[TEAM_ID]&fixtureid=0`
  - `&teamid=[TEAM_ID]&fixtureid=[FIXTURE_ID]`
  - `&teamid=[TEAM_ID]&fixtureids=[FIXTURE_ID],[FIXTURE_ID],...`
  - `&youth=1`, `&nat=1`, `&u20=1`
- Response fields:
  - `id`, `teamid`, `fixtureid`, `p1`...`p15`, `b1`...`b7`, `r1p1`...`r2p15`, `captain`, `kicker`, `deadline`, strategy fields like `pickandgo`, `driving`, `expansive`, `creative`, `defense`, `kicking`, `kickfortouch`, `upandunder`, `dropgoals`, `intensity`, `discipline`
- Notes:
  - If no lineup is found, nothing is returned.
  - `deadline` is in BRT timestamp.

### 9. Mail (`r=ma`)
- Request:
  - `&folderid=[FOLDER_ID],[FOLDER_ID],...`
  - `&subfolderid=[SUBFOLDER_ID],[SUBFOLDER_ID],...`
  - `&messageid=[MESSAGE_ID]`
- Responses include folder summaries, subfolder message lists, and `<MessageDetails>` for single messages.

### 10. Match Commentary (`r=me`)
- Request:
  - `&fixtureid=[FIXTURE_ID]`
  - `&youth=1`, `&nat=1`, `&u20=1`
- Response includes `<event>` items with `commentary`, `possession`, `actualtime`, `losttime`, `gametime`, `homescore`, `guestscore`, `index`, `half`
- Notes:
  - Parse team and player tags from commentary.
  - Empty commentary indicates a paragraph break.
  - `actualtime`: real seconds passed including stoppage.
  - `gametime`: game clock seconds.
  - `losttime`: time lost due to injury or review.

### 11. Match Score (`r=ls`)
- Request:
  - `&fixtureid=[FIXTURE_ID]`
  - `&youth=1`, `&nat=1`, `&u20=1`
- Response fields:
  - `match_has_started`, `match_has_finished`, `homescore`, `guestscore`, `gametime`

### 12. Match Summary (`r=ms`)
- Request:
  - `&fixtureid=[FIXTURE_ID]`
  - `&fixtureids=[FIXTURE_ID],[FIXTURE_ID],...`
  - `&youth=1`, `&nat=1`, `&u20=1`
- Response includes nested home/guest scoring details, attendance, weather.
- Notes:
  - Intensity values: `1` = WNWIM, `2` = Normal, `3` = LTNT.
  - All scoring categories return player entries with `id` and `number`.
  - Youth matches do not return attendance.
  - Night weather uses `night=1`.

### 13. Members (`r=m`)
- Request:
  - `&memberid=[MEMBER_ID]`
  - `&memberids=[MEMBER_ID],[MEMBER_ID],...`
- Response fields:
  - public data for non-owners: `id`, `username`, `teamid`, `active`, `lastclick`, `dateregistered`
  - owner-only data: `realname`, `email`, `memberlevel`, `cash`, etc.
- Notes:
  - Member level values:
    - `1` Unverified
    - `2` Suspended
    - `3` Free member
    - `4` Premium member
    - `5` Forum moderator
    - `6` Game master
    - `7` Developer
  - `cash` is in BR dollars.

### 14. Player History (`r=ph`)
- Request:
  - `&playerid=[PLAYER_ID]`
- Response includes `<entry>` events with tags for `team`, `natteam`, `u20team`, `player`, `youthplayer`, `member`, `rank`.

### 15. Players (`r=p`)
- Request:
  - `&playerid=[PLAYER_ID]`
  - `&playerids=[PLAYER_ID],[PLAYER_ID],...`
  - `&teamid=[TEAM_ID]`
- Response fields depend on ownership and context.
- Public user response includes:
  - `id`, `teamid`, `fname`, `lname`, `birthday`, `age`, `hand`, `foot`, `nationality`, `csr`, `salary`, `form`, `aggression`, `discipline`, `energy`, `leadership`, `experience`, `weight`, `height`, `injured`, `youthid`, `jersey`, `joined`
- Owner response includes:
  - all skills and attributes, plus `contract`, `pops`
- Youth players use `&youth=1`.
- National players use `&nat=1`.
- Under 20 players use `&u20=1`.
- Transfer market fields:
  - `forsale`, `listed`, `deadline`, `asking`, `price`, `bidteamid`

### 16. Player Statistics (`r=ps`)
- Request:
  - `&playerid=[PLAYER_ID]`
  - `&playerids=[PLAYER_ID],[PLAYER_ID],...`
  - `&playerid=[PLAYER_ID]&league=1`
  - `&playerid=[PLAYER_ID]&nat=1`
  - `&playerid=[PLAYER_ID]&nat=1&wc=1`
  - `&playerid=[PLAYER_ID]&u20=1`
  - `&playerid=[PLAYER_ID]&u20=1&wc=1`
- Response: `<player_statistics>` with totals for tackles, metresgained, tries, conversions, penalties, points, cards, linebreaks, interceptions, kicks, knockons, pass stats, caps, kicking metres, and more.

### 17. Ranking History (`r=rkh`)
- Request:
  - `&teamid=[TEAM_ID]`
  - `&nat=1&iso=[COUNTRY_ISO]`
  - `&u20=1&iso=[COUNTRY_ISO]`
- Response: `<ranking>` entries with `regional_position`, `national_position`, `world_position`, `points`, `season`, `round`.
- Note: Premium member required for club history unless National/U20 history is requested.

### 18. Rankings (`r=rk`)
- Request:
  - default world rankings
  - `&iso=[COUNTRY_ISO]`
  - `&regionid=[REGION_ID]`
  - `&leagueid=[LEAGUE_ID]`
  - `&nat=1`
  - `&u20=1`
  - `&start=[START]`
  - `&limit=[LIMIT]`
- Response: `<team id="...">` with `ranking`, `id`, `name`, `country_iso`, `points`.

### 19. Regions (`r=r`)
- Request:
  - `&regionid=[REGION_ID]`
  - `&regionids=[REGION_ID],[REGION_ID],...`
  - `&country=[COUNTRY_ISO]`
- Response: `<region>` with `id`, `name`, `country_iso`.

### 20. Reporter’s Summary (`r=rs`)
- Request:
  - `&fixtureid=[FIXTURE_ID]`
  - `&fixtureids=[FIXTURE_ID],[FIXTURE_ID],...`
  - `&youth=1`, `&nat=1`, `&u20=1`
- Response includes `<reporters_summary>` with `motm`, home/guest team ratings, and player ratings.
- Notes:
  - Bench player ratings appear only if they entered the field.
  - Legacy ratings may include `_old` suffix tags.

### 21. Standings (`r=s`)
- Request:
  - `&leagueid=[LEAGUE_ID]`
  - `&leagueid=[LEAGUE_ID]&youth=1`
  - `&nat=1`
  - `&u20=1`
  - `nat=1&season=[SEASON]`
  - `u20=1&season=[SEASON]`
- Response: `<standing>` with `played`, `w`, `l`, `d`, `for`, `against`, `b1`, `b2`, `points`.

### 22. Team History (`r=th`)
- Request:
  - `&teamid=[TEAM_ID]`
- Response includes `<entry>` events with tags and `date`.

### 23. Teams (`r=t`)
- Request:
  - `&teamid=[TEAM_ID]`
  - `&teamids=[TEAM_ID],[TEAM_ID],...`
  - `&regionid=[REGION_ID]`
  - `&leagueid=[LEAGUE_ID]`
  - `&nat=1`
  - `&u20=1`
- Response fields for non-owners:
  - `id`, `name`, `country_iso`, `region`, `stadium`, `bot`, `nickname_1`, `ranking_points`, `regional_rank`, `national_rank`, `world_rank`, `prev_*`, `leagueid`, `manager`
- Owner response fields:
  - includes stadium capacity, membership, bank balance, sponsors, scouting stars, contentment, LTNT effectiveness.
- National/U20 teams have additional fields and `nat=1`.

### 24. Team Statistics (`r=ts`)
- Request:
  - `&teamid=[TEAM_ID]`
  - `&teamids=[TEAM_ID],[TEAM_ID],...`
  - `&teamid=[TEAM_ID]&nat=1`
  - `&teamid=[TEAM_ID]&nat=1&wc=1`
  - `&teamid=[TEAM_ID]&u20=1`
  - `&teamid=[TEAM_ID]&u20=1&wc=1`
- Response: `<team_statistics>` with team totals for tackles, metres gained, tries, conversions, penalties, points, cards, linebreaks, kicks, possession, territory, lineouts, scrums, injuries, and more.

### 25. Training Instructions (`r=ti`)
- Request:
  - valid member id and key required.
- Response: `<training_instructions>` with `team_training`, `individual_training`, and `youth_team_training`.

### 26. Training Reports (`r=tr`)
- Request:
  - valid member id and key required.
  - optional: `&season=[SEASON]&round=[ROUND]`
- Response: `<training_report>` with team training skills, player drops, pops, CSR old/new values, and energy changes.

### 27. Transfer Market (`r=tm`)
- Request:
  - `&sortby=[name|price|deadline|nationality|age|ranking]`
  - `&sortorder=[asc|desc]`
  - `&offset=[OFFSET]`
  - `&limit=[LIMIT]`
  - `&teamid=[TEAM_ID]`
  - search filters: `priceMin`, `priceMax`, `ageMin`, `ageMax`, `csrMin`, `csrMax`, `nationality`
  - attribute filters: `&skill[X]=[ATTRIBUTE]`, `&skill[X]min=[MIN]`, `&skill[X]max=[MAX]`
- Response: `<auction>` entries with `playerid`, `deadline`.
- Notes:
  - For auction player details, query the player endpoint.

## Notes
- Invalid or missing items generally return no element rather than empty placeholders.
- Some endpoints require member keys for full owner data.
- Use `offset`/`limit` to paginate large transfer market results.
