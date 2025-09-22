using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyHubAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToAddresses = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CcAddresses = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BccAddresses = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsHtml = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecords_ApiKey",
                table: "EmailRecords",
                column: "ApiKey");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecords_Category",
                table: "EmailRecords",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecords_CreatedAt",
                table: "EmailRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecords_Status",
                table: "EmailRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecords_Status_RetryCount",
                table: "EmailRecords",
                columns: new[] { "Status", "RetryCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailRecords");
        }
    }
}