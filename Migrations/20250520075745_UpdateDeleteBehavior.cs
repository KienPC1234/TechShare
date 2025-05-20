using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechShare.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BorrowOrders_AspNetUsers_DeliveryAgentId",
                table: "BorrowOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_BorrowOrders_ExchangeItems_ItemId",
                table: "BorrowOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeItems_ItemCategories_CategoryId",
                table: "ExchangeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemComments_ExchangeItems_ItemId",
                table: "ItemComments");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemMedia_ExchangeItems_ItemId",
                table: "ItemMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemRatings_ExchangeItems_ItemId",
                table: "ItemRatings");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemReports_ExchangeItems_ItemId",
                table: "ItemReports");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_ReceiverId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationNews_AspNetUsers_AuthorId",
                table: "OrganizationNews");

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowOrders_AspNetUsers_DeliveryAgentId",
                table: "BorrowOrders",
                column: "DeliveryAgentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowOrders_ExchangeItems_ItemId",
                table: "BorrowOrders",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeItems_ItemCategories_CategoryId",
                table: "ExchangeItems",
                column: "CategoryId",
                principalTable: "ItemCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemComments_ExchangeItems_ItemId",
                table: "ItemComments",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemMedia_ExchangeItems_ItemId",
                table: "ItemMedia",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemRatings_ExchangeItems_ItemId",
                table: "ItemRatings",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemReports_ExchangeItems_ItemId",
                table: "ItemReports",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_ReceiverId",
                table: "Messages",
                column: "ReceiverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId",
                table: "Messages",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationNews_AspNetUsers_AuthorId",
                table: "OrganizationNews",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BorrowOrders_AspNetUsers_DeliveryAgentId",
                table: "BorrowOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_BorrowOrders_ExchangeItems_ItemId",
                table: "BorrowOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeItems_ItemCategories_CategoryId",
                table: "ExchangeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemComments_ExchangeItems_ItemId",
                table: "ItemComments");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemMedia_ExchangeItems_ItemId",
                table: "ItemMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemRatings_ExchangeItems_ItemId",
                table: "ItemRatings");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemReports_ExchangeItems_ItemId",
                table: "ItemReports");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_ReceiverId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationNews_AspNetUsers_AuthorId",
                table: "OrganizationNews");

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowOrders_AspNetUsers_DeliveryAgentId",
                table: "BorrowOrders",
                column: "DeliveryAgentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowOrders_ExchangeItems_ItemId",
                table: "BorrowOrders",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeItems_ItemCategories_CategoryId",
                table: "ExchangeItems",
                column: "CategoryId",
                principalTable: "ItemCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemComments_ExchangeItems_ItemId",
                table: "ItemComments",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemMedia_ExchangeItems_ItemId",
                table: "ItemMedia",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemRatings_ExchangeItems_ItemId",
                table: "ItemRatings",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemReports_ExchangeItems_ItemId",
                table: "ItemReports",
                column: "ItemId",
                principalTable: "ExchangeItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_ReceiverId",
                table: "Messages",
                column: "ReceiverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId",
                table: "Messages",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationNews_AspNetUsers_AuthorId",
                table: "OrganizationNews",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
