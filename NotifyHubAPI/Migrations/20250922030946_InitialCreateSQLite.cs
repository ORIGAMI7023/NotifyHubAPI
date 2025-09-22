using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyHubAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateSQLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToAddresses = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CcAddresses = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    BccAddresses = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsHtml = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastRetryAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RequestId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
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
