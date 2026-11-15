using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawsPost.Data.Migrations
{
    /// <inheritdoc />
    public partial class LimitPetAdSpeciesToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old rows hold free text. Keep the four known species (any case, surrounding spaces ignored)
            // and map everything else to Other. Constant SQL, no user input.
            migrationBuilder.Sql(
                """
                UPDATE PetAds
                SET Species = CASE lower(trim(Species))
                    WHEN 'dog' THEN 'Dog'
                    WHEN 'cat' THEN 'Cat'
                    WHEN 'bird' THEN 'Bird'
                    WHEN 'rabbit' THEN 'Rabbit'
                    ELSE 'Other'
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The species values stay as text. The old free text cannot be restored.
        }
    }
}
