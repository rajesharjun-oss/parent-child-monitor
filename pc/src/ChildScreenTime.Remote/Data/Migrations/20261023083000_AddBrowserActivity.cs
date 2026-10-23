using ChildScreenTime.Remote.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // Generated migration column arrays are intentionally local.

namespace ChildScreenTime.Remote.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261023083000_AddBrowserActivity")]
public partial class AddBrowserActivity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BrowserActivityAggregates",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                VideoId = table.Column<string>(type: "character(11)", fixedLength: true, maxLength: 11, nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Channel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                WatchedSeconds = table.Column<long>(type: "bigint", nullable: false),
                LastSeenUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BrowserActivityAggregates", x => x.Id);
                table.ForeignKey(
                    name: "FK_BrowserActivityAggregates_ChildDevices_DeviceId",
                    column: x => x.DeviceId,
                    principalTable: "ChildDevices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BrowserActivityAggregates_DeviceId_LocalDate",
            table: "BrowserActivityAggregates",
            columns: new[] { "DeviceId", "LocalDate" });

        migrationBuilder.CreateIndex(
            name: "IX_BrowserActivityAggregates_DeviceId_LocalDate_VideoId",
            table: "BrowserActivityAggregates",
            columns: new[] { "DeviceId", "LocalDate", "VideoId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "BrowserActivityAggregates");
    }
}
