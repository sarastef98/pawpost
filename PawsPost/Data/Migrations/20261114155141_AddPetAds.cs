using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawsPost.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPetAds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PetAds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Species = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Breed = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AgeInMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetAds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PetAds_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PetAds_CreatedAt",
                table: "PetAds",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PetAds_OwnerId",
                table: "PetAds",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PetAds_Species",
                table: "PetAds",
                column: "Species");

            migrationBuilder.CreateIndex(
                name: "IX_PetAds_Status",
                table: "PetAds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PetAds_Type",
                table: "PetAds",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PetAds");
        }
    }
}
