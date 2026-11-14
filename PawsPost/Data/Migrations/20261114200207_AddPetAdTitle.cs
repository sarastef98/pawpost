using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawsPost.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPetAdTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "PetAds",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "PetAds");
        }
    }
}
