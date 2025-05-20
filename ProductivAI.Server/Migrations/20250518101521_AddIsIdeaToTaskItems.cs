using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductivAI.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddIsIdeaToTaskItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsIdea",
                table: "TaskItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsIdea",
                table: "TaskItems");
        }
    }
}
