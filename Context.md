#Blackout Rugby domain overview

Blackout Rugby is a rugby management sim. I am setting up this repository to explor the Api system exposed by the game to be able to get getter analysis of items that I find difficult in the game. Primerly the goal was to get better analysis of the teams performance post game, highlighting issues with certain players abilities , disipline, lineout etc. \

I

#Core entities

Team: a club or national side, identified by team id
Player: a team member with attributes, stats, and contract/training data
Fixture: a match between teams in a competition
Division / League / Region: organisational hierarchy
Standing: league table results
Ranking: club or national team position
Player statistics: per-player output across matches
Transfer market: auctioned players and market filter

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