using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechShare.Migrations
{
    /// <inheritdoc />
    public partial class messFixv6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Messages");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Messages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Messages");

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Messages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
