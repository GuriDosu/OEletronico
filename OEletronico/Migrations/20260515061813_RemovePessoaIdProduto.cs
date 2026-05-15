using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OEletronico.Migrations
{
    /// <inheritdoc />
    public partial class RemovePessoaIdProduto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Produtos_Pessoas_PessoaId",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_PessoaId",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "PessoaId",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "ProdutoId",
                table: "Produtos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PessoaId",
                table: "Produtos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProdutoId",
                table: "Produtos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_PessoaId",
                table: "Produtos",
                column: "PessoaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Produtos_Pessoas_PessoaId",
                table: "Produtos",
                column: "PessoaId",
                principalTable: "Pessoas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
