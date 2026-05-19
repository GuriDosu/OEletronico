using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OEletronico.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAtestado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EhAtestado",
                table: "RegistrosPonto",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ObservacaoAtestado",
                table: "RegistrosPonto",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EhAtestado",
                table: "RegistrosPonto");

            migrationBuilder.DropColumn(
                name: "ObservacaoAtestado",
                table: "RegistrosPonto");
        }
    }
}
