using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawsPost.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPetAdStatusCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PetAds_Status_CreatedAt",
                table: "PetAds",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PetAds_Status_CreatedAt",
                table: "PetAds");
        }
    }
}
