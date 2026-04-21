# Blackout Rugby API Endpoints - Complete Reference

**Last Updated:** April 21, 2026  
**Base URL:** `http://classic-api.blackoutrugby.com` or `https://classic-api.blackoutrugby.com` (SSL)  
**Default Format:** XML | **JSON Support:** Add `&json=1`

---

## Required Authentication Parameters
All requests must include:
- `d=[DEVELOPER_ID]` (required)
- `dk=[DEVELOPER_KEY]` (required for GET/read requests)
- `m=[MEMBER_ID]` (optional, for member-specific data)
- `mk=[MEMBER_KEY]` (optional, for member-specific data)

---

## Endpoint Reference

### 1. Account (`r=acc`)
**Type:** POST (Write)  
**Supports JSON:** Yes  
**Authentication:** Member key required

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `acc` | Request type |
| `username` | string | NO | - | New username to set |
| `password` | string | NO | - | New password (raw, un-hashed) |
| `email` | string | NO | - | New email address |
| `suspend` | boolean | NO | `0`, `1` | Suspend account for 2 weeks |
| `asap` | boolean | NO | `0`, `1` | Lock account immediately (testing only) |
| `unsuspend` | boolean | NO | `0`, `1` | Unsuspend user account |

**Notes:**
- Only one item can be updated per request
- Changing password requires new API key for future calls
- `asap=1` only works on accounts <1 hour old
- Account updates not available to all developers

---

### 2. Bidding (`r=tm`)
**Type:** POST (Write)  
**Supports JSON:** Yes  
**Authentication:** Member key required

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `tm` | Request type |
| `playerid` | integer | YES | - | Player ID to bid on |
| `biddingteamid` | integer | YES | - | Team ID placing the bid |
| `bid` | integer | YES | - | Bid amount in currency |
| `autobid` | boolean | NO | `0`, `1` | Make auto-bid (premium only) |

**Notes:**
- Minimum next bid available in player API response
- Premium members only for auto-bids

---

### 3. Bid History (`r=bh`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `bh` | Request type |
| `playerid` | integer | Optional (one required) | - | Player ID (must be for sale) |
| `auctionid` | integer | Optional (one required) | - | Auction ID from Transfer Market |

**Response Fields:**
- `id`, `teamid`, `memberid`, `bid`, `date`, `revert`, `autobid_race`, `auctionid`

**Notes:**
- `revert=1` indicates Game Master reversal
- `autobid_race=1` indicates auto-bid race between two members

---

### 4. Countries (`r=c`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `c` | Request type |
| `country_iso` | string | NO | [see notes] | Individual country ISO code |
| `country_isos` | string | NO | Comma-separated ISO codes | Multiple countries |
| `real` | boolean | NO | `0`, `1` | Return real countries (1) vs game countries (0, default) |

**Valid Country ISO Codes:**  
`AR`, `AU`, `BE`, `BR`, `CA`, `CL`, `HR`, `CZ`, `EN`, `FR`, `GE`, `DE`, `HK`, `IE`, `IT`, `CI`, `JP`, `KE`, `NA`, `NL`, `NZ`, `PI`, `PL`, `PT`, `RO`, `RU`, `SL`, `ZA`, `KR`, `ES`, `US`, `UY`, `WA`, `ZW`

**Response Fields (Game Countries):**
- `country_iso`, `name`, `time_offset` (hours from BRT), `timezone` (PHP identifier)

**Response Fields (Real Countries):**
- `country_iso`, `name`, `printable_name`

---

### 5. Divisions (`r=d`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `d` | Request type |
| `divisionid` | integer | Optional | - | Individual division ID |
| `divisionids` | string | NO | Comma-separated IDs | Multiple divisions |
| `season` | integer | NO | - | Season number |
| `country_iso` | string | NO | ISO code | Country for divisions |
| `division` | integer | NO | - | Division number within country |

**Notes:**
- All divisions returned if no parameters specified

---

### 6. Finances (`r=f`)
**Type:** GET (Read)  
**Supports JSON:** Yes  
**Authentication:** Member key required

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `f` | Request type |
| `season` | integer | YES | - | Season number |
| `round` | integer | YES | - | Round number |

**Response Fields:**
- `id`, `teamid`, `amount`, `category`, `detail`, `type`, `balance`, `date`

**Field Details:**
- `type`: `1` = Income, `2` = Expense
- `date`: Timestamp in BRT
- `balance`: Bank balance after transaction

---

### 7. Fixtures (`r=fix`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `fix` | Request type |
| `fixtureid` | integer | Optional | - | Individual fixture ID |
| `fixtureids` | string | NO | Comma-separated IDs | Multiple fixture IDs |
| `teamid` | integer | NO | - | Get fixtures for team |
| `last` | integer | NO | - | Number of last fixtures for team |
| `future` | integer | NO | Team ID | Future fixtures for team |
| `past` | integer | NO | Team ID | Past fixtures for team |
| `latest` | integer | NO | League ID | Latest match in league |
| `leagueid` | integer | NO | - | Fixtures for league |
| `season` | integer | NO | - | Season number |
| `round` | integer | NO | - | Round number |
| `roundrobin` | boolean | NO | `0`, `1` | Round-robin fixtures |
| `friendlycompid` | integer | NO | - | Friendly competition ID |
| `youth` | boolean | NO | `0`, `1` | Youth fixtures |
| `nat` | boolean | NO | `0`, `1` | National fixtures |
| `u20` | boolean | NO | `0`, `1` | Under 20 fixtures |

**Response Fields:**
- `id`, `season`, `country_iso`, `leagueid`, `round`, `hometeamid`, `guestteamid`, `competition`, `botmatch`, `matchstart`

**Valid Competition Values:**  
`Friendly`, `ThursdayFriendly`, `Cup`, `League`, `SemiFinal`, `BronzeFinal`, `Final`, `Qualifier`, `International`, `WorldCup`, `U20International`, `U20WorldCup`

**Field Details:**
- `matchstart`: Timestamp in BRT
- `botmatch`: `1` = match between two bots

---

### 8. Fixture Statistics (`r=fs`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `fs` | Request type |
| `fixtureid` | integer | Optional | - | Individual fixture |
| `fixtureids` | string | NO | Comma-separated IDs | Multiple fixtures |
| `teamstats` | integer | NO | Team ID | Team statistics only |
| `teamplayersstats` | integer | NO | Team ID | Team + player statistics |
| `playerstats` | integer | NO | Player ID | Player statistics only |

**Notes:**
- Returns home and guest team stats
- Can be expensive to query
- Use limiting parameters when possible

---

### 9. Leagues (`r=l`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `l` | Request type |
| `leagueid` | integer | Optional | - | Individual league ID |
| `leagueids` | string | NO | Comma-separated IDs | Multiple leagues |
| `season` | integer | NO | - | Season number |
| `country_iso` | string | NO | ISO code | Country |
| `division` | integer | NO | - | Division number |
| `league` | integer | NO | - | League number |
| `divisionid` | integer | NO | - | Division ID |

**Response Fields:**
- `id`, `country_iso`, `season`, `division`, `league`, `lounge`

---

### 10. Lineups (`r=li`)
**Type:** GET/POST (Read/Write)  
**Supports JSON:** Yes

#### READ Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `li` | Request type |
| `teamid` | integer | YES | - | Team ID |
| `fixtureid` | integer | NO | `0` or fixture ID | Specific fixture |
| `fixtureids` | string | NO | Comma-separated IDs | Multiple fixtures |
| `youth` | boolean | NO | `0`, `1` | Youth lineup |
| `nat` | boolean | NO | `0`, `1` | National lineup |
| `u20` | boolean | NO | `0`, `1` | Under 20 lineup |

#### WRITE Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `p1` - `p15` | integer | - | Player IDs for starting positions |
| `b1` - `b7` | integer | - | Player IDs for bench positions |
| `r1p1` - `r2p15` | integer | - | Reserve player IDs |
| `captain` | integer | - | Captain player ID |
| `kicker` | integer | - | Goal kicker player ID |
| `strategy` fields | - | - | See strategy options below |

#### Strategy Parameters (Write)
| Parameter | Type | Valid Values | Description |
|-----------|------|--------------|-------------|
| `pickandgo` | integer | 1-11 | Pick and go intensity |
| `driving` | integer | 1-11 | Driving intensity |
| `expansive` | integer | 1-11 | Expansive play intensity |
| `creative` | integer | 1-11 | Creative play intensity |
| `defense` | integer | 1-11 | Defense intensity |
| `kicking` | integer | 1-11 | Kicking intensity |
| `kickfortouch` | integer | 1-11 | Kick for touch intensity |
| `upandunder` | integer | 1-11 | Up and under intensity |
| `dropgoals` | integer | 1-11 | Drop goal attempts |
| `intensity` | integer | `1`, `2`, `3` | `1`=WNWIM, `2`=Normal, `3`=LTNT |
| `discipline` | integer | 1-11 | Discipline level |

**Response Fields:**
- `id`, `teamid`, `fixtureid`, `p1`-`p15`, `b1`-`b7`, `r1p1`-`r2p15`, `captain`, `kicker`, `deadline`

**Field Details:**
- `deadline`: Timestamp in BRT
- Returns nothing if no lineup found

---

### 11. Mail - Reads (`r=m`)
**Type:** GET (Read)  
**Supports JSON:** Yes  
**Authentication:** Member key required

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `m` | Request type |
| `folderid` | string | Optional | Comma-separated IDs | Folder IDs to retrieve |
| `subfolderid` | string | Optional | Comma-separated IDs | Subfolder IDs |
| `messageid` | integer | Optional | - | Specific message ID |

**Response Types:**
- Folder summaries
- Subfolder message lists
- Message details

---

### 12. Mail - Writes (`r=m`)
**Type:** POST (Write)  
**Supports JSON:** Yes  
**Authentication:** Member key required

#### Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `r` | string | YES | `m` |
| `send` | string | YES | Recipient member ID |
| `subject` | string | YES | Message subject |
| `body` | string | YES | Message body |
| `type` | string | NO | Message type |

---

### 13. Match Commentary (`r=mc`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `mc` | Request type |
| `fixtureid` | integer | YES | - | Fixture ID |
| `youth` | boolean | NO | `0`, `1` | Youth match |
| `nat` | boolean | NO | `0`, `1` | National match |
| `u20` | boolean | NO | `0`, `1` | Under 20 match |

**Response Fields:**
- `event` entries with: `commentary`, `possession`, `actualtime`, `losttime`, `gametime`, `homescore`, `guestscore`, `index`, `half`

**Field Details:**
- `actualtime`: Real seconds including stoppage
- `gametime`: Game clock seconds
- `losttime`: Time lost to injury/review
- Empty commentary = paragraph break

---

### 14. Match Score (`r=ms`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `ms` | Request type |
| `fixtureid` | integer | YES | - | Fixture ID |
| `youth` | boolean | NO | `0`, `1` | Youth match |
| `nat` | boolean | NO | `0`, `1` | National match |
| `u20` | boolean | NO | `0`, `1` | Under 20 match |

**Response Fields:**
- `match_has_started`, `match_has_finished`, `homescore`, `guestscore`, `gametime`

---

### 15. Match Summary (`r=msum`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `msum` | Request type |
| `fixtureid` | integer | Optional | - | Individual fixture |
| `fixtureids` | string | NO | Comma-separated IDs | Multiple fixtures |
| `youth` | boolean | NO | `0`, `1` | Youth match |
| `nat` | boolean | NO | `0`, `1` | National match |
| `u20` | boolean | NO | `0`, `1` | Under 20 match |

**Response Fields:**
- Nested home/guest scoring details: tries, conversions, drop goals, penalties
- Attendance, weather, date

**Notes:**
- Youth matches do not return attendance
- Night weather indicated with `night=1`

---

### 16. Members (`r=mem`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `mem` | Request type |
| `memberid` | integer | Optional | - | Individual member ID |
| `memberids` | string | NO | Comma-separated IDs | Multiple member IDs |

**Response Fields (Public):**
- `id`, `username`, `teamid`, `active`, `lastclick`, `dateregistered`

**Response Fields (Owner Only):**
- `realname`, `email`, `memberlevel`, `cash`, and private data

**Member Level Values:**
- `1` = Unverified
- `2` = Suspended
- `3` = Free member
- `4` = Premium member
- `5` = Forum moderator
- `6` = Game master
- `7` = Developer

---

### 17. Player History (`r=ph`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `ph` | Request type |
| `playerid` | integer | YES | - | Player ID |

**Response Fields:**
- `entry` items with: `id`, `playerid`, `event`, `date`
- Event tags: `team`, `natteam`, `u20team`, `player`, `youthplayer`, `member`, `rank`

**Notes:**
- `date`: Timestamp in BRT
- Pops hidden for 7 days for free members
- Private information hidden without member key

---

### 18. Players (`r=p`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `p` | Request type |
| `playerid` | integer | Optional | - | Individual player |
| `playerids` | string | NO | Comma-separated IDs | Multiple players |
| `teamid` | integer | NO | - | All players from team |
| `teamids` | string | NO | Comma-separated IDs | Players from multiple teams |
| `youth` | boolean | NO | `0`, `1` | Youth player |
| `nat` | boolean | NO | `0`, `1` | National player |
| `u20` | boolean | NO | `0`, `1` | Under 20 player |

**Response Fields (Public):**
- `id`, `teamid`, `fname`, `lname`, `birthday`, `age`, `hand`, `foot`, `nationality`, `csr`, `salary`, `form`, `aggression`, `discipline`, `energy`, `leadership`, `experience`, `weight`, `height`, `injured`, `youthid`, `jersey`, `joined`

**Response Fields (Owner Only - Additional):**
- `stamina`, `handling`, `attack`, `defense`, `technique`, `strength`, `jumping`, `speed`, `agility`, `kicking`, `contract`, `pops`

**Youth Player Notes:**
- `&youth=1` parameter required
- Skills shown as "Unknown", "Low", "Medium", or integer
- Includes `scouted` date and `reveals` array

**Transfer Market Fields:**
- `forsale`, `listed`, `deadline`, `asking`, `price`, `bidteamid`, `nextbid`, `canbid`

**Field Details:**
- `injured`: Timestamp; if > current BRT, player is injured
- `youthid`: Non-zero = youth player ID
- `contract`: Timestamp; if > current BRT, player under contract
- `jersey`: Non-255 value = assigned jersey number
- `birthday`: Format `WW:D` (week:day)
- `energy`: 1-11 (free) or 0-110% (premium)

---

### 19. Player Statistics (`r=ps`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `ps` | Request type |
| `playerid` | integer | Optional | - | Individual player |
| `playerids` | string | NO | Comma-separated IDs | Multiple players |
| `league` | boolean | NO | `0`, `1` | League statistics |
| `season` | integer | NO | - | Season number |
| `nat` | boolean | NO | `0`, `1` | National statistics |
| `wc` | boolean | NO | `0`, `1` | World Cup statistics |
| `u20` | boolean | NO | `0`, `1` | Under 20 statistics |

**Response Fields:**
- `playerid`, `tackles`, `metresgained`, `tries`, `conversions`, `dropgoals`, `penalties`, `totalpoints`, `yellowcards`, `redcards`, `linebreaks`, `intercepts`, `kicks`, `knockons`, `forwardpasses`, `tryassists`, `beatendefenders`, `injuries`, `handlingerrors`, `missedtackles`, `fights`, `kickingmetres`, `leaguecaps`, `friendlycaps`, `cupcaps`, `undertwentycaps`, `nationalcaps`, `othercaps`, `avkickingmetres`, `penaltiesconceded`, `kicksoutonthefull`, `balltime`, `penaltytime`, `missedconversions`, `misseddropgoals`, `missedpenalties`, `goodupandunders`, `badupandunders`, `upandunders`, `goodkicks`, `badkicks`, `turnoverswon`, `lineoutssecured`, `lineoutsconceded`, `lineoutsstolen`, `successfullineoutthrows`, `unsuccessfullineoutthrows`, `worldcupcaps`, `undertwentyworldcupcaps`

---

### 20. Ranking History (`r=rkh`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `rkh` | Request type |
| `teamid` | integer | Optional | - | Club team ID |
| `nat` | boolean | NO | `0`, `1` | National team |
| `u20` | boolean | NO | `0`, `1` | Under 20 team |
| `iso` | string | NO | Country ISO | Country (with nat/u20) |

**Response Fields:**
- `regional_position`, `national_position`, `world_position`, `points`, `season`, `round`

**Notes:**
- Premium member required for club history (unless National/U20)

---

### 21. Rankings (`r=rk`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `rk` | Request type |
| `iso` | string | NO | Country ISO | Regional rankings |
| `regionid` | integer | NO | - | Rankings for region |
| `leagueid` | integer | NO | - | Rankings for league |
| `nat` | boolean | NO | `0`, `1` | National rankings |
| `u20` | boolean | NO | `0`, `1` | Under 20 rankings |
| `start` | integer | NO | - | Starting position |
| `limit` | integer | NO | - | Number of results |

**Response Fields:**
- `team` with: `ranking`, `id`, `name`, `country_iso`, `points`

**Notes:**
- Default: world rankings

---

### 22. Regions (`r=r`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `r` | Request type |
| `regionid` | integer | Optional | - | Individual region |
| `regionids` | string | NO | Comma-separated IDs | Multiple regions |
| `country` | string | NO | Country ISO | Regions for country |

**Response Fields:**
- `id`, `name`, `country_iso`

---

### 23. Reporter's Summary (`r=rs`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `rs` | Request type |
| `fixtureid` | integer | Optional | - | Individual fixture |
| `fixtureids` | string | NO | Comma-separated IDs | Multiple fixtures |
| `youth` | boolean | NO | `0`, `1` | Youth match |
| `nat` | boolean | NO | `0`, `1` | National match |
| `u20` | boolean | NO | `0`, `1` | Under 20 match |

**Response Fields:**
- `reporters_summary` with: `motm` (Man of the Match), home/guest ratings, player ratings

**Notes:**
- Bench ratings only if player entered field
- Legacy ratings may have `_old` suffix

---

### 24. Standings (`r=s`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `s` | Request type |
| `leagueid` | integer | Optional | - | League ID |
| `youth` | boolean | NO | `0`, `1` | Youth league |
| `nat` | boolean | NO | `0`, `1` | National standings |
| `u20` | boolean | NO | `0`, `1` | Under 20 standings |
| `season` | integer | NO | - | Season number |

**Response Fields:**
- `standing` with: `played`, `w`, `l`, `d`, `for`, `against`, `b1`, `b2`, `points`

---

### 25. Team History (`r=th`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `th` | Request type |
| `teamid` | integer | YES | - | Team ID |

**Response Fields:**
- `entry` items with: `id`, `event`, `date`, tags

---

### 26. Teams (`r=t`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `t` | Request type |
| `teamid` | integer | Optional | - | Individual team |
| `teamids` | string | NO | Comma-separated IDs | Multiple teams (max 10) |
| `regionid` | integer | NO | - | All teams in region |
| `leagueid` | integer | NO | - | All teams in league |
| `nat` | boolean | NO | `0`, `1` | National team |
| `u20` | boolean | NO | `0`, `1` | Under 20 team |
| `country` | string | NO | Country ISO | National/U20 team by country |

**Response Fields (Public):**
- `id`, `name`, `country_iso`, `region`, `stadium`, `bot`, `nickname_1`, `nickname_2`, `nickname_3`, `ranking_points`, `regional_rank`, `national_rank`, `world_rank`, `prev_ranking_points`, `prev_regional_rank`, `prev_national_rank`, `prev_world_rank`, `leagueid`, `manager`

**Response Fields (Owner Only - Additional):**
- `drift`, `rush`, `manonman`, `contentment`, `members`, `stadium_capacity`, `stadium_standing`, `stadium_uncovered`, `stadium_covered`, `stadium_members`, `stadium_corporate`, `bank_balance`, `ltnt_effectiveness`, `ltnt_effectiveness_youth`, `major_sponsor`, `minor_sponsors`, `scouting_stars`

**National/U20 Fields:**
- `nat: 1` for national, `u20: 1` for Under 20 team

---

### 27. Team Statistics (`r=ts`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `ts` | Request type |
| `teamid` | integer | Optional | - | Individual team |
| `teamids` | string | NO | Comma-separated IDs | Multiple teams |
| `nat` | boolean | NO | `0`, `1` | National statistics |
| `wc` | boolean | NO | `0`, `1` | World Cup statistics |
| `u20` | boolean | NO | `0`, `1` | Under 20 statistics |
| `season` | integer | NO | - | Season number |

**Response Fields:**
- `teamid`, `tackles`, `metresgained`, `tries`, `conversions`, `dropgoals`, `penalties`, `totalpoints`, `yellowcards`, `redcards`, `linebreaks`, `intercepts`, `kicks`, `knockons`, `forwardpasses`, `phases`, `sevenplusphases`, `turnovers`, `possession`, `territory`, `minutesintwentytwo`, `lineoutswon`, `lineoutslost`, `lineoutsagainstthrow`, `scrumswon`, `scrumslost`, `scrumsagainstputin`, `injuries`, `handlingerrors`, `missedtackles`, `fights`, `kickingmetres`, `ruckswon`, `maulswon`, `matchesplayed`, `avminutesintwentytwo`, `avkickingmetres`, `penaltiesconceded`, `kicksoutonthefull`, `balltime`, `penaltytime`, `missedconversions`, `misseddropgoals`, `missedpenalties`, `goodupandunders`, `badupandunders`, `upandunders`, `goodkicks`, `badkicks`, `penaltieswon`, `turnoversconceded`

---

### 28. Training Instructions (`r=ti`)
**Type:** GET (Read)  
**Supports JSON:** Yes  
**Authentication:** Member key required

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `ti` | Request type |

**Response Fields:**
- `training_instructions` with: `team_training`, `individual_training`, `youth_team_training`
- Each entry has: `teamid`, `s1`, `s2`, `s3`, `s4` (skill names or "none")

**Notes:**
- No additional parameters required (uses member context)
- Returns for authenticated member's team only

---

### 29. Training Reports (`r=tr`)
**Type:** GET (Read)  
**Supports JSON:** Yes  
**Authentication:** Member key required

#### Parameters
| Parameter | Type | Required | Valid Values | Description |
|-----------|------|----------|--------------|-------------|
| `r` | string | YES | `tr` | Request type |
| `season` | integer | NO | - | Season number |
| `round` | integer | NO | - | Round number |

**Response Fields:**
- `training_report` with: `id`, `teamid`, `season`, `round`
- Team training: `skills`, player drops/pops, CSR changes
- Individual training per player

**Notes:**
- Returns latest available if no season/round specified
- Contains: `drops`, `pops`, `csr` (old/new), `energy` (old/new)

---

### 30. Transfer Market (`r=tm`)
**Type:** GET (Read)  
**Supports JSON:** Yes

#### Parameters

##### Sorting/Pagination
| Parameter | Type | Valid Values | Description |
|-----------|------|--------------|-------------|
| `sortby` | string | `name`, `price`, `deadline`, `nationality`, `age`, `ranking` | Sort field |
| `sortorder` | string | `asc`, `desc` | Sort order |
| `offset` | integer | - | Results offset (0-based) |
| `limit` | integer | - | Max results to return |

##### Filtering
| Parameter | Type | Valid Values | Description |
|-----------|------|--------------|-------------|
| `teamid` | integer | - | Players from specific team |
| `priceMin` | integer | - | Minimum asking price |
| `priceMax` | integer | - | Maximum asking price |
| `ageMin` | integer | - | Minimum player age |
| `ageMax` | integer | - | Maximum player age |
| `csrMin` | integer | - | Minimum CSR |
| `csrMax` | integer | - | Maximum CSR |
| `nationality` | string | Country ISO | Player nationality |

##### Attribute Filters
| Parameter | Type | Valid Values | Description |
|-----------|------|--------------|-------------|
| `skill[X]` | string | See notes | Attribute name |
| `skill[X]min` | integer | - | Minimum attribute value |
| `skill[X]max` | integer | - | Maximum attribute value |

**Valid Skill Attributes:**  
`stamina`, `handling`, `defense`, `attack`, `technique`, `strength`, `jumping`, `speed`, `agility`, `kicking`, `form`, `height`, `weight`, `salary`, `discipline`, `aggression`, `leadership`, `experience`

**Response Fields:**
- `auction` with: `playerid`, `deadline`

**Notes:**
- Default: all auctions sorted by nearest deadline
- Use `offset`/`limit` for pagination
- Cannot use search parameters with `teamid`
- Skill levels: 1=Non-existent, 2=Despicable, 3=Horrible... up to 11

---

## Global Response Format

### XML Response Structure
```xml
<blackoutrugby_api_response 
  brt_timestamp="1384889315"
  brt_sql="2013-11-20 02:08:35"
  brt_iso_8601="2013-11-20T02:08:35+12:00"
  season="8"
  round="9"
  day="3">
  <!-- Endpoint-specific content -->
</blackoutrugby_api_response>
```

### JSON Response Structure
```json
{
  "status": "Ok",
  "brt": "2013-11-20T02:08:35+12:00",
  "season": 8,
  "round": 9,
  "day": 3,
  ...endpoint data...
}
```

### Error Response (JSON)
```json
{
  "status": "Failed",
  "error": "Error message here",
  "brt": "2013-11-20T02:08:35+12:00"
}
```

---

## Common Request Patterns

### Multiple IDs Parameter Format
```
&playerids=1,2,3,4,5
&teamids=10,20,30
&fixtureids=100,101,102
```

### Search/Filter Pattern
```
&priceMin=50000&priceMax=500000
&ageMin=25&ageMax=30
&skill1=attack&skill1min=8&skill1max=11
&skill2=defense&skill2min=6&skill2max=11
```

### Pagination Pattern
```
&offset=0&limit=10      # Results 1-10
&offset=10&limit=10     # Results 11-20
&offset=20&limit=10     # Results 21-30
```

---

## Important Notes

1. **Rate Limiting Guidelines for Bulk Operations:**
   - 0-120 seconds: 3 second wait between requests
   - 120-240 seconds: 1 second wait between requests
   - After 240 seconds: No wait required

2. **Caching:** Use caching in your application to minimize server load

3. **Version Control:** Use versioned API URLs (e.g., `/2011-08-13/`) to prevent breaking changes

4. **Member Keys:** Always prompt users for their member key and store securely

5. **Write Requests:** Must use POST and encrypt data when possible

6. **Search Results:** Empty results return no elements (not empty placeholders)

7. **Timestamps:** All dates are ISO 8601 formatted in BRT (GMT+12)

8. **Free vs Premium Members:**
   - Free: Hidden skills, no pops for 7 days
   - Premium: Full player data, scouting stars, immediate pops

9. **JSON Requests:** POST the JSON object with key `json` URL-encoded
