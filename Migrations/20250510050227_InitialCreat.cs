using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechShare.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "ExchangeItems");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "ExchangeItems");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Notifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "Notifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "Notifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "ItemTags",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ItemRatings",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "ItemRatings",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ItemComments",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "ItemComments",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ItemCategories",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationId",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CategoryId",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentInfo",
                table: "BorrowOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "BorrowOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeliveryAgentId",
                table: "BorrowOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BorrowerId",
                table: "BorrowOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ItemMedia",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemMedia_ExchangeItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ExchangeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationReports_OrganizationId",
                table: "OrganizationReports",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationReports_UserId",
                table: "OrganizationReports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRatings_OrganizationId",
                table: "OrganizationRatings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRatings_UserId",
                table: "OrganizationRatings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_OrganizationId",
                table: "OrganizationMembers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_UserId",
                table: "OrganizationMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationJoinRequests_OrganizationId",
                table: "OrganizationJoinRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationJoinRequests_UserId",
                table: "OrganizationJoinRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationComments_OrganizationId",
                table: "OrganizationComments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationComments_UserId",
                table: "OrganizationComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_TOCHUC_UserId",
                table: "Notification_TOCHUC",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMedia_ItemId",
                table: "ItemMedia",
                column: "ItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeItems_Organizations_OrganizationId",
                table: "ExchangeItems",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_TOCHUC_AspNetUsers_UserId",
                table: "Notification_TOCHUC",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationComments_AspNetUsers_UserId",
                table: "OrganizationComments",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationComments_Organizations_OrganizationId",
                table: "OrganizationComments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationJoinRequests_AspNetUsers_UserId",
                table: "OrganizationJoinRequests",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationJoinRequests_Organizations_OrganizationId",
                table: "OrganizationJoinRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationMembers_AspNetUsers_UserId",
                table: "OrganizationMembers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationMembers_Organizations_OrganizationId",
                table: "OrganizationMembers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationRatings_AspNetUsers_UserId",
                table: "OrganizationRatings",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationRatings_Organizations_OrganizationId",
                table: "OrganizationRatings",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationReports_AspNetUsers_UserId",
                table: "OrganizationReports",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationReports_Organizations_OrganizationId",
                table: "OrganizationReports",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeItems_Organizations_OrganizationId",
                table: "ExchangeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Notification_TOCHUC_AspNetUsers_UserId",
                table: "Notification_TOCHUC");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationComments_AspNetUsers_UserId",
                table: "OrganizationComments");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationComments_Organizations_OrganizationId",
                table: "OrganizationComments");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationJoinRequests_AspNetUsers_UserId",
                table: "OrganizationJoinRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationJoinRequests_Organizations_OrganizationId",
                table: "OrganizationJoinRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationMembers_AspNetUsers_UserId",
                table: "OrganizationMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationMembers_Organizations_OrganizationId",
                table: "OrganizationMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationRatings_AspNetUsers_UserId",
                table: "OrganizationRatings");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationRatings_Organizations_OrganizationId",
                table: "OrganizationRatings");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationReports_AspNetUsers_UserId",
                table: "OrganizationReports");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationReports_Organizations_OrganizationId",
                table: "OrganizationReports");

            migrationBuilder.DropTable(
                name: "ItemMedia");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationReports_OrganizationId",
                table: "OrganizationReports");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationReports_UserId",
                table: "OrganizationReports");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationRatings_OrganizationId",
                table: "OrganizationRatings");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationRatings_UserId",
                table: "OrganizationRatings");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMembers_OrganizationId",
                table: "OrganizationMembers");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMembers_UserId",
                table: "OrganizationMembers");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationJoinRequests_OrganizationId",
                table: "OrganizationJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationJoinRequests_UserId",
                table: "OrganizationJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationComments_OrganizationId",
                table: "OrganizationComments");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationComments_UserId",
                table: "OrganizationComments");

            migrationBuilder.DropIndex(
                name: "IX_Notification_TOCHUC_UserId",
                table: "Notification_TOCHUC");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Notifications",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "Notifications",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "Notifications",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "ItemTags",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ItemRatings",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "ItemRatings",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ItemComments",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "ItemComments",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ItemCategories",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationId",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "CategoryId",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "ExchangeItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentInfo",
                table: "BorrowOrders",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "BorrowOrders",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "DeliveryAgentId",
                table: "BorrowOrders",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "BorrowerId",
                table: "BorrowOrders",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
