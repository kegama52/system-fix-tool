using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreasuryFixTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_users_UserId1",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_UserId1",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "refresh_tokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId1",
                table: "refresh_tokens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId1",
                table: "refresh_tokens",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_users_UserId1",
                table: "refresh_tokens",
                column: "UserId1",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
