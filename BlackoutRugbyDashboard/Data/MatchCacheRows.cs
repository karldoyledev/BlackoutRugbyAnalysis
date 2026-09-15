using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlackoutRugbyDashboard.Data;

/// <summary>
/// Parsed Match Cache rows (spec §Match Cache / D1): the query-model layer that
/// sits beside the immutable raw XML archive. Stat fields are stored verbatim
/// from the API — the <see cref="ColumnAttribute"/> name is the exact API element
/// name, so the XML field list has one source of truth and unreliable fields
/// (fs *_caps, played) are still persisted, per D1's "verbatim" rule.
/// </summary>

/// <summary>All `f` metadata for one completed Fixture, as fetched.</summary>
public class FixtureRow
{
    [Key]
    public int FixtureId { get; set; }
    public int Season { get; set; }
    public int LeagueId { get; set; }
    public int Round { get; set; }
    public string Competition { get; set; } = string.Empty;
    public int HomeTeamId { get; set; }
    public int GuestTeamId { get; set; }
    public int WeatherId { get; set; }
    public int BotMatch { get; set; }
    public int DataRemoved { get; set; }
    public string Stadium { get; set; } = string.Empty;
    public string CountryIso { get; set; } = string.Empty;
    public long MatchStartUnix { get; set; }
    public long MatchFinishUnix { get; set; }
    public DateTime FetchedAt { get; set; }
}

/// <summary>
/// Per-Fixture Match Summary: points per side, intensity, weather, the five
/// attendance tiers, and injuries/subs as JSON (D1). Scorers are joinable rows
/// (see <see cref="MatchSummaryScorerRow"/>).
/// </summary>
public class MatchSummaryRow
{
    [Key]
    public int FixtureId { get; set; }
    public int HomePoints { get; set; }
    public int GuestPoints { get; set; }
    public int? HomeIntensity { get; set; }
    public int? GuestIntensity { get; set; }
    public int? WeatherId { get; set; }
    public bool? WeatherNight { get; set; }
    public int Standing { get; set; }
    public int Uncovered { get; set; }
    public int Covered { get; set; }
    public int Members { get; set; }
    public int Corporate { get; set; }
    public string InjuriesJson { get; set; } = "[]";
    public string SubstitutionsJson { get; set; } = "[]";
    public DateTime FetchedAt { get; set; }
}

/// <summary>One scorer entry: tries/conversions/penalties/dropgoals per Player with count.</summary>
public class MatchSummaryScorerRow
{
    [Key]
    public int Id { get; set; }
    public int FixtureId { get; set; }
    public string ScorerType { get; set; } = string.Empty;
    public int PlayerId { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// One Player's output in one Fixture, parsed from the live
/// home_player_N/guest_player_N element (R3 F2). Jersey = N; Slot = N (1–15 XV,
/// 16–23 bench). All 50 fs stat fields stored verbatim.
/// </summary>
public class PlayerFixtureRow
{
    [Key]
    public int Id { get; set; }
    public int FixtureId { get; set; }
    public int TeamId { get; set; }
    public int PlayerId { get; set; }
    public string Side { get; set; } = string.Empty;
    public int Jersey { get; set; }
    public int Slot { get; set; }

    [Column("energy_before")] public int EnergyBefore { get; set; }
    [Column("tackles")] public int Tackles { get; set; }
    [Column("metres_gained")] public int MetresGained { get; set; }
    [Column("tries")] public int Tries { get; set; }
    [Column("conversions")] public int Conversions { get; set; }
    [Column("missed_conversions")] public int MissedConversions { get; set; }
    [Column("dropgoals")] public int DropGoals { get; set; }
    [Column("missed_dropgoals")] public int MissedDropGoals { get; set; }
    [Column("penalties")] public int Penalties { get; set; }
    [Column("missed_penalties")] public int MissedPenalties { get; set; }
    [Column("total_points")] public int TotalPoints { get; set; }
    [Column("yellow_cards")] public int YellowCards { get; set; }
    [Column("red_cards")] public int RedCards { get; set; }
    [Column("linebreaks")] public int Linebreaks { get; set; }
    [Column("intercepts")] public int Intercepts { get; set; }
    [Column("kicks")] public int Kicks { get; set; }
    [Column("good_kicks")] public int GoodKicks { get; set; }
    [Column("bad_kicks")] public int BadKicks { get; set; }
    [Column("up_and_unders")] public int UpAndUnders { get; set; }
    [Column("good_up_and_unders")] public int GoodUpAndUnders { get; set; }
    [Column("bad_up_and_unders")] public int BadUpAndUnders { get; set; }
    [Column("knockons")] public int Knockons { get; set; }
    [Column("forward_passes")] public int ForwardPasses { get; set; }
    [Column("try_assists")] public int TryAssists { get; set; }
    [Column("beaten_defenders")] public int BeatenDefenders { get; set; }
    [Column("injuries")] public int Injuries { get; set; }
    [Column("handling_errors")] public int HandlingErrors { get; set; }
    [Column("missed_tackles")] public int MissedTackles { get; set; }
    [Column("fights")] public int Fights { get; set; }
    [Column("kicking_metres")] public int KickingMetres { get; set; }
    [Column("league_caps")] public int LeagueCaps { get; set; }
    [Column("friendly_caps")] public int FriendlyCaps { get; set; }
    [Column("cup_caps")] public int CupCaps { get; set; }
    [Column("under_twenty_caps")] public int UnderTwentyCaps { get; set; }
    [Column("national_caps")] public int NationalCaps { get; set; }
    [Column("under_twenty_world_cup_caps")] public int UnderTwentyWorldCupCaps { get; set; }
    [Column("world_cup_caps")] public int WorldCupCaps { get; set; }
    [Column("other_caps")] public int OtherCaps { get; set; }
    [Column("penalties_conceded")] public int PenaltiesConceded { get; set; }
    [Column("kicks_out_on_the_full")] public int KicksOutOnTheFull { get; set; }
    [Column("ball_time")] public int BallTime { get; set; }
    [Column("played")] public int Played { get; set; }
    [Column("turnovers")] public int Turnovers { get; set; }
    [Column("lineouts_secured")] public int LineoutsSecured { get; set; }
    [Column("lineouts_conceded")] public int LineoutsConceded { get; set; }
    [Column("lineouts_stolen")] public int LineoutsStolen { get; set; }
    [Column("successful_lineout_throws")] public int SuccessfulLineoutThrows { get; set; }
    [Column("unsuccessful_lineout_throws")] public int UnsuccessfulLineoutThrows { get; set; }
    [Column("minutes_played")] public int MinutesPlayed { get; set; }
    [Column("energy_after")] public int EnergyAfter { get; set; }
}

/// <summary>
/// One Team's bare-fs stats for one Fixture: full-time and half-time blocks for
/// both sides (R3 F5). Side = home/guest; Half = full/half1. All 58 team stat
/// fields stored verbatim — the compare row's source.
/// </summary>
public class TeamFixtureStatRow
{
    [Key]
    public int Id { get; set; }
    public int FixtureId { get; set; }
    public int TeamId { get; set; }
    public string Side { get; set; } = string.Empty;
    public string Half { get; set; } = string.Empty;

    [Column("tackles")] public int Tackles { get; set; }
    [Column("metres_gained")] public int MetresGained { get; set; }
    [Column("tries")] public int Tries { get; set; }
    [Column("conversions")] public int Conversions { get; set; }
    [Column("missed_conversions")] public int MissedConversions { get; set; }
    [Column("dropgoals")] public int DropGoals { get; set; }
    [Column("missed_dropgoals")] public int MissedDropGoals { get; set; }
    [Column("penalties")] public int Penalties { get; set; }
    [Column("missed_penalties")] public int MissedPenalties { get; set; }
    [Column("total_points")] public int TotalPoints { get; set; }
    [Column("yellow_cards")] public int YellowCards { get; set; }
    [Column("red_cards")] public int RedCards { get; set; }
    [Column("linebreaks")] public int Linebreaks { get; set; }
    [Column("intercepts")] public int Intercepts { get; set; }
    [Column("kicks")] public int Kicks { get; set; }
    [Column("good_kicks")] public int GoodKicks { get; set; }
    [Column("bad_kicks")] public int BadKicks { get; set; }
    [Column("up_and_unders")] public int UpAndUnders { get; set; }
    [Column("good_up_and_unders")] public int GoodUpAndUnders { get; set; }
    [Column("bad_up_and_unders")] public int BadUpAndUnders { get; set; }
    [Column("knockons")] public int Knockons { get; set; }
    [Column("forward_passes")] public int ForwardPasses { get; set; }
    [Column("phases")] public int Phases { get; set; }
    [Column("sevenplus_phases")] public int SevenplusPhases { get; set; }
    [Column("turnovers")] public int Turnovers { get; set; }
    [Column("possession")] public int Possession { get; set; }
    [Column("territory")] public int Territory { get; set; }
    [Column("minutes_in_22")] public int MinutesIn22 { get; set; }
    [Column("lineouts_won")] public int LineoutsWon { get; set; }
    [Column("lineouts_lost")] public int LineoutsLost { get; set; }
    [Column("lineouts_against_throw")] public int LineoutsAgainstThrow { get; set; }
    [Column("lineouts_thrown")] public int LineoutsThrown { get; set; }
    [Column("lineouts_secured")] public int LineoutsSecured { get; set; }
    [Column("scrums_won")] public int ScrumsWon { get; set; }
    [Column("scrums_lost")] public int ScrumsLost { get; set; }
    [Column("scrums_against_put_in")] public int ScrumsAgainstPutIn { get; set; }
    [Column("scrums_put_in")] public int ScrumsPutIn { get; set; }
    [Column("scrums_secured")] public int ScrumsSecured { get; set; }
    [Column("injuries")] public int Injuries { get; set; }
    [Column("category_1_injuries")] public int Category1Injuries { get; set; }
    [Column("category_2_injuries")] public int Category2Injuries { get; set; }
    [Column("category_3_injuries")] public int Category3Injuries { get; set; }
    [Column("category_4_injuries")] public int Category4Injuries { get; set; }
    [Column("category_5_injuries")] public int Category5Injuries { get; set; }
    [Column("category_6_injuries")] public int Category6Injuries { get; set; }
    [Column("injury_breaks")] public int InjuryBreaks { get; set; }
    [Column("handling_errors")] public int HandlingErrors { get; set; }
    [Column("missed_tackles")] public int MissedTackles { get; set; }
    [Column("fights")] public int Fights { get; set; }
    [Column("kicking_metres")] public int KickingMetres { get; set; }
    [Column("rucks_won")] public int RucksWon { get; set; }
    [Column("mauls_won")] public int MaulsWon { get; set; }
    [Column("matches_played")] public int MatchesPlayed { get; set; }
    [Column("penalties_conceded")] public int PenaltiesConceded { get; set; }
    [Column("penalties_won")] public int PenaltiesWon { get; set; }
    [Column("kicks_out_on_the_full")] public int KicksOutOnTheFull { get; set; }
    [Column("ball_time")] public int BallTime { get; set; }
    [Column("turnovers_conceded")] public int TurnoversConceded { get; set; }
}

/// <summary>
/// Point-in-time Team facts captured when a team read is made (R5): name, bot
/// flag, and strength figures. Append-only — history is the point (D1).
/// </summary>
public class TeamFactRow
{
    [Key]
    public int Id { get; set; }
    public int TeamId { get; set; }
    public DateTime CapturedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CountryIso { get; set; } = string.Empty;
    public bool Bot { get; set; }
    public int AverageTop15Csr { get; set; }
    public double? RankingPoints { get; set; }
    public int LeagueId { get; set; }
    public int RegionalRank { get; set; }
    public int NationalRank { get; set; }
    public int WorldRank { get; set; }
}

/// <summary>
/// One Player's cumulative season statistics (r=ps, R3-verified 48 fields plus
/// identity). Filled when a Player History view fetches ps for that
/// Player+season; fs *_caps aggregation stays out (D4 §5 — ps is the caps source).
/// </summary>
public class PlayerSeasonRow
{
    [Key]
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int Season { get; set; }
    public DateTime FetchedAt { get; set; }

    [Column("tackles")] public int Tackles { get; set; }
    [Column("metresgained")] public int MetresGained { get; set; }
    [Column("tries")] public int Tries { get; set; }
    [Column("conversions")] public int Conversions { get; set; }
    [Column("dropgoals")] public int DropGoals { get; set; }
    [Column("penalties")] public int Penalties { get; set; }
    [Column("totalpoints")] public int TotalPoints { get; set; }
    [Column("yellowcards")] public int YellowCards { get; set; }
    [Column("redcards")] public int RedCards { get; set; }
    [Column("linebreaks")] public int Linebreaks { get; set; }
    [Column("intercepts")] public int Intercepts { get; set; }
    [Column("kicks")] public int Kicks { get; set; }
    [Column("knockons")] public int Knockons { get; set; }
    [Column("forwardpasses")] public int ForwardPasses { get; set; }
    [Column("tryassists")] public int TryAssists { get; set; }
    [Column("beatendefenders")] public int BeatenDefenders { get; set; }
    [Column("injuries")] public int Injuries { get; set; }
    [Column("handlingerrors")] public int HandlingErrors { get; set; }
    [Column("missedtackles")] public int MissedTackles { get; set; }
    [Column("fights")] public int Fights { get; set; }
    [Column("kickingmetres")] public int KickingMetres { get; set; }
    [Column("leaguecaps")] public int LeagueCaps { get; set; }
    [Column("friendlycaps")] public int FriendlyCaps { get; set; }
    [Column("cupcaps")] public int CupCaps { get; set; }
    [Column("undertwentycaps")] public int UnderTwentyCaps { get; set; }
    [Column("nationalcaps")] public int NationalCaps { get; set; }
    [Column("othercaps")] public int OtherCaps { get; set; }
    [Column("avkickingmetres")] public int AvKickingMetres { get; set; }
    [Column("penaltiesconceded")] public int PenaltiesConceded { get; set; }
    [Column("kicksoutonthefull")] public int KicksOutOnTheFull { get; set; }
    [Column("balltime")] public int BallTime { get; set; }
    [Column("penaltytime")] public int PenaltyTime { get; set; }
    [Column("missedconversions")] public int MissedConversions { get; set; }
    [Column("misseddropgoals")] public int MissedDropGoals { get; set; }
    [Column("missedpenalties")] public int MissedPenalties { get; set; }
    [Column("goodupandunders")] public int GoodUpAndUnders { get; set; }
    [Column("badupandunders")] public int BadUpAndUnders { get; set; }
    [Column("upandunders")] public int UpAndUnders { get; set; }
    [Column("goodkicks")] public int GoodKicks { get; set; }
    [Column("badkicks")] public int BadKicks { get; set; }
    [Column("turnoverswon")] public int TurnoversWon { get; set; }
    [Column("lineoutssecured")] public int LineoutsSecured { get; set; }
    [Column("lineoutsconceded")] public int LineoutsConceded { get; set; }
    [Column("lineoutsstolen")] public int LineoutsStolen { get; set; }
    [Column("successfullineoutthrows")] public int SuccessfulLineoutThrows { get; set; }
    [Column("unsuccessfullineoutthrows")] public int UnsuccessfulLineoutThrows { get; set; }
    [Column("worldcupcaps")] public int WorldCupCaps { get; set; }
    [Column("undertwentyworldcupcaps")] public int UnderTwentyWorldCupCaps { get; set; }
}
