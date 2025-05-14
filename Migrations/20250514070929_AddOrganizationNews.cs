using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechShare.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationNews",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", nullable: true),
                    AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationNews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationNews_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationNews_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationNewsComments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    NewsId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationNewsComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationNewsComments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationNewsComments_OrganizationNews_NewsId",
                        column: x => x.NewsId,
                        principalTable: "OrganizationNews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationNews_AuthorId",
                table: "OrganizationNews",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationNews_OrganizationId",
                table: "OrganizationNews",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationNewsComments_NewsId",
                table: "OrganizationNewsComments",
                column: "NewsId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationNewsComments_UserId",
                table: "OrganizationNewsComments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationNewsComments");

            migrationBuilder.DropTable(
                name: "OrganizationNews");
        }
    }
}
