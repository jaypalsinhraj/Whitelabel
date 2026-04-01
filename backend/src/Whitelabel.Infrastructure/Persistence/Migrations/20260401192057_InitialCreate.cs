using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Whitelabel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationAdmins",
                columns: table => new
                {
                    ObjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationAdmins", x => x.ObjectId);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PrimaryColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SecondaryColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EntraTenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "TenantAdmins",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "character varying(128)", nullable: false),
                    ObjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantAdmins", x => new { x.TenantId, x.ObjectId });
                    table.ForeignKey(
                        name: "FK_TenantAdmins_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantEmailDomains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<string>(type: "character varying(128)", nullable: false),
                    Domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantEmailDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantEmailDomains_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantHostNames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<string>(type: "character varying(128)", nullable: false),
                    HostName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantHostNames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantHostNames_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantUserGrants",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "character varying(128)", nullable: false),
                    ObjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUserGrants", x => new { x.TenantId, x.ObjectId });
                    table.ForeignKey(
                        name: "FK_TenantUserGrants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantEmailDomains_Domain",
                table: "TenantEmailDomains",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantEmailDomains_TenantId",
                table: "TenantEmailDomains",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantHostNames_HostName",
                table: "TenantHostNames",
                column: "HostName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantHostNames_TenantId",
                table: "TenantHostNames",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationAdmins");

            migrationBuilder.DropTable(
                name: "TenantAdmins");

            migrationBuilder.DropTable(
                name: "TenantEmailDomains");

            migrationBuilder.DropTable(
                name: "TenantHostNames");

            migrationBuilder.DropTable(
                name: "TenantUserGrants");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
