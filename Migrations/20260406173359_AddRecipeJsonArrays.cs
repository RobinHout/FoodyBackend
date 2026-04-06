using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodyBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeJsonArrays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "DirectionSteps",
                table: "Recipes",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "IngredientItems",
                table: "Recipes",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "Ner",
                table: "Recipes",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DirectionSteps",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "IngredientItems",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Ner",
                table: "Recipes");
        }
    }
}
