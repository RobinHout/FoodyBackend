using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FoodyBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistedDinnerParticipationAndUserLabelCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLabels_UserId",
                table: "UserLabels");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "UserLabels",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DinnerParticipations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DinnerId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Attending = table.Column<string>(type: "text", nullable: false),
                    Q1Choice = table.Column<string>(type: "text", nullable: true),
                    Q2Choice = table.Column<string>(type: "text", nullable: true),
                    Q3Choice = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DinnerParticipations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DinnerParticipations_Dinners_DinnerId",
                        column: x => x.DinnerId,
                        principalTable: "Dinners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DinnerParticipations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLabels_UserId_LabelId",
                table: "UserLabels",
                columns: new[] { "UserId", "LabelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DinnerParticipations_DinnerId_UserId",
                table: "DinnerParticipations",
                columns: new[] { "DinnerId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DinnerParticipations_UserId",
                table: "DinnerParticipations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DinnerParticipations");

            migrationBuilder.DropIndex(
                name: "IX_UserLabels_UserId_LabelId",
                table: "UserLabels");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "UserLabels");

            migrationBuilder.CreateIndex(
                name: "IX_UserLabels_UserId",
                table: "UserLabels",
                column: "UserId");
        }
    }
}
