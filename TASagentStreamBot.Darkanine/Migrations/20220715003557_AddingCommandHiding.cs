using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TASagentStreamBot.Darkanine.Migrations
{
    public partial class AddingCommandHiding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Shown",
                table: "CustomTextCommands",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Shown",
                table: "CustomTextCommands");
        }
    }
}
