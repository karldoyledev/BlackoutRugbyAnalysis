using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackoutRugbyDashboard.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityAndMatchCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MemberId = table.Column<int>(type: "INTEGER", nullable: true),
                    EncryptedMemberKey = table.Column<string>(type: "TEXT", nullable: true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fixtures",
                columns: table => new
                {
                    FixtureId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Season = table.Column<int>(type: "INTEGER", nullable: false),
                    LeagueId = table.Column<int>(type: "INTEGER", nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    Competition = table.Column<string>(type: "TEXT", nullable: false),
                    HomeTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    GuestTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    WeatherId = table.Column<int>(type: "INTEGER", nullable: false),
                    BotMatch = table.Column<int>(type: "INTEGER", nullable: false),
                    DataRemoved = table.Column<int>(type: "INTEGER", nullable: false),
                    Stadium = table.Column<string>(type: "TEXT", nullable: false),
                    CountryIso = table.Column<string>(type: "TEXT", nullable: false),
                    MatchStartUnix = table.Column<long>(type: "INTEGER", nullable: false),
                    MatchFinishUnix = table.Column<long>(type: "INTEGER", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fixtures", x => x.FixtureId);
                });

            migrationBuilder.CreateTable(
                name: "MatchSummaries",
                columns: table => new
                {
                    FixtureId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HomePoints = table.Column<int>(type: "INTEGER", nullable: false),
                    GuestPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeIntensity = table.Column<int>(type: "INTEGER", nullable: true),
                    GuestIntensity = table.Column<int>(type: "INTEGER", nullable: true),
                    WeatherId = table.Column<int>(type: "INTEGER", nullable: true),
                    WeatherNight = table.Column<bool>(type: "INTEGER", nullable: true),
                    Standing = table.Column<int>(type: "INTEGER", nullable: false),
                    Uncovered = table.Column<int>(type: "INTEGER", nullable: false),
                    Covered = table.Column<int>(type: "INTEGER", nullable: false),
                    Members = table.Column<int>(type: "INTEGER", nullable: false),
                    Corporate = table.Column<int>(type: "INTEGER", nullable: false),
                    InjuriesJson = table.Column<string>(type: "TEXT", nullable: false),
                    SubstitutionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSummaries", x => x.FixtureId);
                });

            migrationBuilder.CreateTable(
                name: "MatchSummaryScorers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FixtureId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScorerType = table.Column<string>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSummaryScorers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerFixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FixtureId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<string>(type: "TEXT", nullable: false),
                    Jersey = table.Column<int>(type: "INTEGER", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    energy_before = table.Column<int>(type: "INTEGER", nullable: false),
                    tackles = table.Column<int>(type: "INTEGER", nullable: false),
                    metres_gained = table.Column<int>(type: "INTEGER", nullable: false),
                    tries = table.Column<int>(type: "INTEGER", nullable: false),
                    conversions = table.Column<int>(type: "INTEGER", nullable: false),
                    missed_conversions = table.Column<int>(type: "INTEGER", nullable: false),
                    dropgoals = table.Column<int>(type: "INTEGER", nullable: false),
                    missed_dropgoals = table.Column<int>(type: "INTEGER", nullable: false),
                    penalties = table.Column<int>(type: "INTEGER", nullable: false),
                    missed_penalties = table.Column<int>(type: "INTEGER", nullable: false),
                    total_points = table.Column<int>(type: "INTEGER", nullable: false),
                    yellow_cards = table.Column<int>(type: "INTEGER", nullable: false),
                    red_cards = table.Column<int>(type: "INTEGER", nullable: false),
                    linebreaks = table.Column<int>(type: "INTEGER", nullable: false),
                    intercepts = table.Column<int>(type: "INTEGER", nullable: false),
                    kicks = table.Column<int>(type: "INTEGER", nullable: false),
                    good_kicks = table.Column<int>(type: "INTEGER", nullable: false),
                    bad_kicks = table.Column<int>(type: "INTEGER", nullable: false),
                    up_and_unders = table.Column<int>(type: "INTEGER", nullable: false),
                    good_up_and_unders = table.Column<int>(type: "INTEGER", nullable: false),
                    bad_up_and_unders = table.Column<int>(type: "INTEGER", nullable: false),
                    knockons = table.Column<int>(type: "INTEGER", nullable: false),
                    forward_passes = table.Column<int>(type: "INTEGER", nullable: false),
                    try_assists = table.Column<int>(type: "INTEGER", nullable: false),
                    beaten_defenders = table.Column<int>(type: "INTEGER", nullable: false),
                    injuries = table.Column<int>(type: "INTEGER", nullable: false),
                    handling_errors = table.Column<int>(type: "INTEGER", nullable: false),
                    missed_tackles = table.Column<int>(type: "INTEGER", nullable: false),
                    fights = table.Column<int>(type: "INTEGER", nullable: false),
                    kicking_metres = table.Column<int>(type: "INTEGER", nullable: false),
                    league_caps = table.Column<int>(type: "INTEGER", nullable: false),
                    friendly_caps = table.Column<int>(type: "INTEGER", nullable: false),
                    cup_caps = table.Column<int>(type: "INTEGER", nullable: false),
                    under_twenty_caps = table.Column<int>(type: "INTEGER", nullable: false),
                    national_caps = table.Column<int>(type: "INTEGER", nullable: false),
                    under_twenty_world_cup_caps = table.Column<int>(type: "INTEGER", nullable: false),
                    world_cup_caps = table.Column<int>(type: "INTEGER", nullable: false),
                    other_caps = table.Column<int>(type: "INTEGER", nullable: false),
                    penalties_conceded = table.Column<int>(type: "INTEGER", nullable: false),
                    kicks_out_on_the_full = table.Column<int>(type: "INTEGER", nullable: false),
                    ball_time = table.Column<int>(type: "INTEGER", nullable: false),
                    played = table.Column<int>(type: "INTEGER", nullable: false),
                    turnovers = table.Column<int>(type: "INTEGER", nullable: false),
                    lineouts_secured = table.Column<int>(type: "INTEGER", nullable: false),
                    lineouts_conceded = table.Column<int>(type: "INTEGER", nullable: false),
                    lineouts_stolen = table.Column<int>(type: "INTEGER", nullable: false),
                    successful_lineout_throws = table.Column<int>(type: "INTEGER", nullable: false),
                    unsuccessful_lineout_throws = table.Column<int>(type: "INTEGER", nullable: false),
                    minutes_played = table.Column<int>(type: "INTEGER", nullable: false),
                    energy_after = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerFixtures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSeasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Season = table.Column<int>(type: "INTEGER", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tackles = table.Column<int>(type: "INTEGER", nullable: false),
                    metresgained = table.Column<int>(type: "INTEGER", nullable: false),
                    tries = table.Column<int>(type: "INTEGER", nullable: false),
                    conversions = table.Column<int>(type: "INTEGER", nullable: false),
                    dropgoals = table.Column<int>(type: "INTEGER", nullable: false),
                    penalties = table.Column<int>(type: "INTEGER", nullable: false),
                    totalpoints = table.Column<int>(type: "INTEGER", nullable: false),
                    yellowcards = table.Column<int>(type: "INTEGER", nullable: false),
                    redcards = table.Column<int>(type: "INTEGER", nullable: false),
                    linebreaks = table.Column<int>(type: "INTEGER", nullable: false),
                    intercepts = table.Column<int>(type: "INTEGER", nullable: false),
                    kicks = table.Column<int>(type: "INTEGER", nullable: false),
                    knockons = table.Column<int>(type: "INTEGER", nullable: false),
                    forwardpasses = table.Column<int>(type: "INTEGER", nullable: false),
                    tryassists = table.Column<int>(type: "INTEGER", nullable: false),
                    beatendefenders = table.Column<int>(type: "INTEGER", nullable: false),
                    injuries = table.Column<int>(type: "INTEGER", nullable: false),
                    handlingerrors = table.Column<int>(type: "INTEGER", nullable: false),
                    missedtackles = table.Column<int>(type: "INTEGER", nullable: false),
                    fights = table.Column<int>(type: "INTEGER", nullable: false),
                    kickingmetres = table.Column<int>(type: "INTEGER", nullable: false),
                    leaguecaps = table.Column<int>(type: "INTEGER", nullable: false),
                    friendlycaps = table.Column<int>(type: "INTEGER", nullable: false),
                    cupcaps = table.Column<int>(type: "INTEGER", nullable: false),
                    undertwentycaps = table.Column<int>(type: "INTEGER", nullable: false),
                    nationalcaps = table.Column<int>(type: "INTEGER", nullable: false),
                    othercaps = table.Column<int>(type: "INTEGER", nullable: false),
                    avkickingmetres = table.Column<int>(type: "INTEGER", nullable: false),
                    penaltiesconceded = table.Column<int>(type: "INTEGER", nullable: false),
                    kicksoutonthefull = table.Column<int>(type: "INTEGER", nullable: false),
                    balltime = table.Column<int>(type: "INTEGER", nullable: false),
                    penaltytime = table.Column<int>(type: "INTEGER", nullable: false),
                    missedconversions = table.Column<int>(type: "INTEGER", nullable: false),
                    misseddropgoals = table.Column<int>(type: "INTEGER", nullable: false),
                    missedpenalties = table.Column<int>(type: "INTEGER", nullable: false),
                    goodupandunders = table.Column<int>(type: "INTEGER", nullable: false),
                    badupandunders = table.Column<int>(type: "INTEGER", nullable: false),
                    upandunders = table.Column<int>(type: "INTEGER", nullable: false),
                    goodkicks = table.Column<int>(type: "INTEGER", nullable: false),
                    badkicks = table.Column<int>(type: "INTEGER", nullable: false),
                    turnoverswon = table.Column<int>(type: "INTEGER", nullable: false),
                    lineoutssecured = table.Column<int>(type: "INTEGER", nullable: false),
                    lineoutsconceded = table.Column<int>(type: "INTEGER", nullable: false),
                    lineoutsstolen = table.Column<int>(type: "INTEGER", nullable: false),
                    successfullineoutthrows = table.Column<int>(type: "INTEGER", nullable: false),
                    unsuccessfullineoutthrows = table.Column<int>(type: "INTEGER", nullable: false),
                    worldcupcaps = table.Column<int>(type: "INTEGER", nullable: false),
                    undertwentyworldcupcaps = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSeasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamFacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CountryIso = table.Column<string>(type: "TEXT", nullable: false),
                    Bot = table.Column<bool>(type: "INTEGER", nullable: false),
                    AverageTop15Csr = table.Column<int>(type: "INTEGER", nullable: false),
                    RankingPoints = table.Column<double>(type: "REAL", nullable: true),
                    LeagueId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegionalRank = table.Column<int>(type: "INTEGER", nullable: false),
                    NationalRank = table.Column<int>(type: "INTEGER", nullable: false),
                    WorldRank = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamFacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamFixtureStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FixtureId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<string>(type: "TEXT", nullable: false),
                    Half = table.Column<string>(type: "TEXT", nullable: false),
                    tackles = table.Column<int>(type: "INTEGER", nullable: false),
                    metres_gained = table.Column<int>(type: "INTEGER", nullable: false),
                    tries = table.Column<int>(type: "INTEGER", nullable: false),
                    conversions = table.Column<int>(type: "INTEGER", nullable: false),
                    missed_conversions = table.Column<int>(type: "INTEGER", nullable: false),
                    dropgoals = table.Column<int>(type: "INTEGER", nullable: false),
                    missed_dropgoals = table.Column<int>(type: "INTEGER", nullable: false),
                    penalties = table.Column<int>(type: "INTEGER", nullable: false),
                    missed_penalties = table.Column<int>(type: "INTEGER", nullable: false),
                    total_points = table.Column<int>(type: "INTEGER", nullable: false),
                    yellow_cards = table.Column<int>(type: "INTEGER", nullable: false),
                    red_cards = table.Column<int>(type: "INTEGER", nullable: false),
                    linebreaks = table.Column<int>(type: "INTEGER", nullable: false),
                    intercepts = table.Column<int>(type: "INTEGER", nullable: false),
                    kicks = table.Column<int>(type: "INTEGER", nullable: false),
                    good_kicks = table.Column<int>(type: "INTEGER", nullable: false),
                    bad_kicks = table.Column<int>(type: "INTEGER", nullable: false),
                    up_and_unders = table.Column<int>(type: "INTEGER", nullable: false),
                    good_up_and_unders = table.Column<int>(type: "INTEGER", nullable: false),
                    bad_up_and_unders = table.Column<int>(type: "INTEGER", nullable: false),
                    knockons = table.Column<int>(type: "INTEGER", nullable: false),
                    forward_passes = table.Column<int>(type: "INTEGER", nullable: false),
                    phases = table.Column<int>(type: "INTEGER", nullable: false),
                    sevenplus_phases = table.Column<int>(type: "INTEGER", nullable: false),
                    turnovers = table.Column<int>(type: "INTEGER", nullable: false),
                    possession = table.Column<int>(type: "INTEGER", nullable: false),
                    territory = table.Column<int>(type: "INTEGER", nullable: false),
                    minutes_in_22 = table.Column<int>(type: "INTEGER", nullable: false),
                    lineouts_won = table.Column<int>(type: "INTEGER", nullable: false),
                    lineouts_lost = table.Column<int>(type: "INTEGER", nullable: false),
                    lineouts_against_throw = table.Column<int>(type: "INTEGER", nullable: false),
                    lineouts_thrown = table.Column<int>(type: "INTEGER", nullable: false),
                    lineouts_secured = table.Column<int>(type: "INTEGER", nullable: false),
                    scrums_won = table.Column<int>(type: "INTEGER", nullable: false),
                    scrums_lost = table.Column<int>(type: "INTEGER", nullable: false),
                    scrums_against_put_in = table.Column<int>(type: "INTEGER", nullable: false),
                    scrums_put_in = table.Column<int>(type: "INTEGER", nullable: false),
                    scrums_secured = table.Column<int>(type: "INTEGER", nullable: false),
                    injuries = table.Column<int>(type: "INTEGER", nullable: false),
                    category_1_injuries = table.Column<int>(type: "INTEGER", nullable: false),
                    category_2_injuries = table.Column<int>(type: "INTEGER", nullable: false),
                    category_3_injuries = table.Column<int>(type: "INTEGER", nullable: false),
                    category_4_injuries = table.Column<int>(type: "INTEGER", nullable: false),
                    category_5_injuries = table.Column<int>(type: "INTEGER", nullable: false),
                    category_6_injuries = table.Column<int>(type: "INTEGER", nullable: false),
                    injury_breaks = table.Column<int>(type: "INTEGER", nullable: false),
                    handling_errors = table.Column<int>(type: "INTEGER", nullable: false),
                    missed_tackles = table.Column<int>(type: "INTEGER", nullable: false),
                    fights = table.Column<int>(type: "INTEGER", nullable: false),
                    kicking_metres = table.Column<int>(type: "INTEGER", nullable: false),
                    rucks_won = table.Column<int>(type: "INTEGER", nullable: false),
                    mauls_won = table.Column<int>(type: "INTEGER", nullable: false),
                    matches_played = table.Column<int>(type: "INTEGER", nullable: false),
                    penalties_conceded = table.Column<int>(type: "INTEGER", nullable: false),
                    penalties_won = table.Column<int>(type: "INTEGER", nullable: false),
                    kicks_out_on_the_full = table.Column<int>(type: "INTEGER", nullable: false),
                    ball_time = table.Column<int>(type: "INTEGER", nullable: false),
                    turnovers_conceded = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamFixtureStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_MemberId",
                table: "AspNetUsers",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Fixtures");

            migrationBuilder.DropTable(
                name: "MatchSummaries");

            migrationBuilder.DropTable(
                name: "MatchSummaryScorers");

            migrationBuilder.DropTable(
                name: "PlayerFixtures");

            migrationBuilder.DropTable(
                name: "PlayerSeasons");

            migrationBuilder.DropTable(
                name: "TeamFacts");

            migrationBuilder.DropTable(
                name: "TeamFixtureStats");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
