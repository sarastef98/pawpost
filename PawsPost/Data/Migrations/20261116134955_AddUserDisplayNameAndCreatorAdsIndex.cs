using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawsPost.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDisplayNameAndCreatorAdsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedDisplayName",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // One-time backfill (plan #44, decision D1, accepted exception to the migration rules, like #32).
            // Every existing user gets "Pet lover NNNN", numbered by position in the order of Id, so names are
            // unique by construction. Constant SQL, no user input. Runs before the unique index is created.
            migrationBuilder.Sql(
                """
                UPDATE AspNetUsers
                SET DisplayName = 'Pet lover ' || printf('%04d',
                        (SELECT COUNT(*) FROM AspNetUsers AS other WHERE other.Id <= AspNetUsers.Id)),
                    NormalizedDisplayName = 'PET LOVER ' || printf('%04d',
                        (SELECT COUNT(*) FROM AspNetUsers AS other WHERE other.Id <= AspNetUsers.Id))
                WHERE DisplayName = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PetAds_OwnerId_Status_CreatedAt",
                table: "PetAds",
                columns: new[] { "OwnerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NormalizedDisplayName",
                table: "AspNetUsers",
                column: "NormalizedDisplayName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PetAds_OwnerId_Status_CreatedAt",
                table: "PetAds");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NormalizedDisplayName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedDisplayName",
                table: "AspNetUsers");
        }
    }
}
