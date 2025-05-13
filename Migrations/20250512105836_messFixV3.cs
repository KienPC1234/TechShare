using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechShare.Migrations
{
    /// <inheritdoc />
    public partial class messFixV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OrganizationId",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true,
                oldDefaultValue: "Không Tổ Chức");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OrganizationId",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: true,
                defaultValue: "Không Tổ Chức",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
