using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace foll_device_managment.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionConsumerInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_inbox_messages",
                schema: "device",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_inbox_messages", x => x.event_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_inbox_messages_operation_id",
                schema: "device",
                table: "integration_inbox_messages",
                column: "operation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_inbox_messages",
                schema: "device");
        }
    }
}
