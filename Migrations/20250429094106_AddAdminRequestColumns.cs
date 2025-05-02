using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechShare.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRequestColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AdminRequestPending",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AdminRequestReason",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginTime",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminRequestPending",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AdminRequestReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginTime",
                table: "AspNetUsers");
        }
    }
}
