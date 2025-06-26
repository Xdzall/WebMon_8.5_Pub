using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAdditionalBreakTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdditionalBreakTimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BreakTime1Start = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakTime1End = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakTime2Start = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakTime2End = table.Column<TimeSpan>(type: "time", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdditionalBreakTimes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdditionalBreakTimes");
        }
    }
}
