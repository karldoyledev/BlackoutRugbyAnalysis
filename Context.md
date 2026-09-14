#Blackout Rugby domain overview

Blackout Rugby is a rugby management sim. I am setting up this repository to explore the Api system exposed by the game to be able to get getter analysis of items that I find difficult in the game. Primerly the goal was to get better analysis of the teams performance post game, highlighting issues with certain players abilities , disipline, lineout etc.

## Language

**User**:
A local dashboard account, signed in with email and password, linked to one Member.
_Avoid_: account, member, login

**Member**:
A Blackout Rugby game identity, identified by member ID and authorized by its Member Key.
_Avoid_: user, account

**Member Key**:
The game-issued secret that authorizes a Member's API access; invalidated when the Member changes their in-game password.
_Avoid_: API key, password, access key

**Team**:
The club or national side a Member manages; the root of all analysis.
_Avoid_: club, side

**Player**:
A squad member of a Team with attributes, statistics, and contract data.
_Avoid_: roster member, squad member

**Fixture**:
A scheduled or completed match between two Teams in a competition; the API's unit of match data.
_Avoid_: match, game

**Fixture Statistics**:
A Team's or Player's output within one Fixture.
_Avoid_: match stats, game stats

**Player Statistics**:
A Player's cumulative output over a scoring period (season, competition).
_Avoid_: player stats, career stats

**Standing**:
A Team's league-table position for a season.
_Avoid_: league table, table position

**Ranking**:
A Team's official points-based position, club or national.
_Avoid_: leaderboard, ladder

**Division**:
A competitive tier in a country's hierarchy.
_Avoid_: tier, level

**League**:
A group of Teams within a Division playing a season of Fixtures.
_Avoid_: division, conference

**Region**:
A geographic subdivision of a country that groups Teams.
_Avoid_: area, zone

**Transfer Market**:
The auction market for Players.
_Avoid_: auction house, bidding market

#Important rules

All timestamps are in BRT, which is GMT+12
The API uses a root response format and the r parameter to choose a module
Missing data usually returns no element rather than an empty value
Some endpoints require valid member credentials for owner data
Youth, national, and U20 data are filtered using flags such as youth, nat, and u20
Analysis-oriented conventions
Use team and player IDs as the stable identity keys
Prefer statistics endpoints for performance analysis
Compare across season, fixture, and ranking data rather than raw event logs