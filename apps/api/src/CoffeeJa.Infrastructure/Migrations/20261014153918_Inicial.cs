using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeJa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Avaliacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PedidoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContratanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nota = table.Column<int>(type: "int", nullable: false),
                    Pontual = table.Column<bool>(type: "bit", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avaliacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contratantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Empresa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NomeFantasia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    Cpf = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    NotaFiscalMesmoEndereco = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Endereco_Bairro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Endereco_Cep = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Endereco_Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Endereco_Complemento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Endereco_Logradouro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Endereco_Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Endereco_Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    EnderecoNotaFiscal_Bairro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnderecoNotaFiscal_Cep = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    EnderecoNotaFiscal_Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnderecoNotaFiscal_Complemento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnderecoNotaFiscal_Logradouro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnderecoNotaFiscal_Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EnderecoNotaFiscal_Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contratantes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Convites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConvidanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Telefone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NomeConvidado = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CadastradoUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CadastradoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Convites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fornecedores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeFantasia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RazaoSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    RaioAtendimentoKm = table.Column<int>(type: "int", nullable: false),
                    AtendeRestricoes = table.Column<int>(type: "int", nullable: false),
                    Aprovado = table.Column<bool>(type: "bit", nullable: false),
                    Fundador = table.Column<bool>(type: "bit", nullable: false),
                    NotaMedia = table.Column<double>(type: "float", nullable: false),
                    TotalAvaliacoes = table.Column<int>(type: "int", nullable: false),
                    Endereco_Bairro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Endereco_Cep = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Endereco_Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Endereco_Complemento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Endereco_Logradouro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Endereco_Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Endereco_Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fornecedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notificacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Lida = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushInscricoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    P256dh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushInscricoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Demandas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContratanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DataEvento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "time", nullable: false),
                    Endereco = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    NumeroPessoas = table.Column<int>(type: "int", nullable: false),
                    Restricoes = table.Column<int>(type: "int", nullable: false),
                    Descartaveis = table.Column<int>(type: "int", nullable: false),
                    Estilo = table.Column<int>(type: "int", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PrazoPropostas = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SolicitanteNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SolicitanteSetor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Demandas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Demandas_Contratantes_ContratanteId",
                        column: x => x.ContratanteId,
                        principalTable: "Contratantes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ModelosCardapio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estilo = table.Column<int>(type: "int", nullable: false),
                    Itens = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ValorPorPessoa = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MinimoPessoas = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelosCardapio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelosCardapio_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cpf = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Whatsapp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Cargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Setor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SenhaHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Papel = table.Column<int>(type: "int", nullable: false),
                    Funcao = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SuperiorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SuperiorWhatsapp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AprovacaoTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AprovacaoExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AprovadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRecusa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AtivacaoTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AtivacaoExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodigoConvite = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    IndicadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContratanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Contratantes_ContratanteId",
                        column: x => x.ContratanteId,
                        principalTable: "Contratantes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Usuarios_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Propostas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DemandaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Cardapio = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AjusteSolicitado = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContratanteLeuEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FornecedorLeuEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Propostas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Propostas_Demandas_DemandaId",
                        column: x => x.DemandaId,
                        principalTable: "Demandas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Propostas_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Mensagens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemetenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mensagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mensagens_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DemandaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContratanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ComissaoPercentual = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusPagamento = table.Column<int>(type: "int", nullable: false),
                    ProvedorPagamento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ContratadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContratadoPorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ContratadoPorSetor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NotaFiscalNumero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NotaFiscalArquivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NotaFiscalNomeOriginal = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    NotaFiscalEnviadaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pedidos_Demandas_DemandaId",
                        column: x => x.DemandaId,
                        principalTable: "Demandas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pedidos_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EventosPedido",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PedidoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Avaliacoes_FornecedorId",
                table: "Avaliacoes",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Avaliacoes_PedidoId",
                table: "Avaliacoes",
                column: "PedidoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contratantes_Cnpj",
                table: "Contratantes",
                column: "Cnpj",
                unique: true,
                filter: "[Cnpj] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Convites_Codigo",
                table: "Convites",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Convites_ConvidanteId",
                table: "Convites",
                column: "ConvidanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Demandas_ContratanteId",
                table: "Demandas",
                column: "ContratanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Demandas_Latitude_Longitude",
                table: "Demandas",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Demandas_Status_DataEvento",
                table: "Demandas",
                columns: new[] { "Status", "DataEvento" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosPedido_PedidoId",
                table: "EventosPedido",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_Cnpj",
                table: "Fornecedores",
                column: "Cnpj",
                unique: true,
                filter: "[Cnpj] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_Latitude_Longitude",
                table: "Fornecedores",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_PropostaId_CriadoEm",
                table: "Mensagens",
                columns: new[] { "PropostaId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelosCardapio_FornecedorId",
                table: "ModelosCardapio",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_UsuarioId_Lida",
                table: "Notificacoes",
                columns: new[] { "UsuarioId", "Lida" });

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ContratanteId",
                table: "Pedidos",
                column: "ContratanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_DemandaId",
                table: "Pedidos",
                column: "DemandaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_FornecedorId",
                table: "Pedidos",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_PropostaId",
                table: "Pedidos",
                column: "PropostaId");

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_DemandaId_FornecedorId",
                table: "Propostas",
                columns: new[] { "DemandaId", "FornecedorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_FornecedorId",
                table: "Propostas",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_PushInscricoes_Endpoint",
                table: "PushInscricoes",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushInscricoes_UsuarioId",
                table: "PushInscricoes",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_AprovacaoTokenHash",
                table: "Usuarios",
                column: "AprovacaoTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_AtivacaoTokenHash",
                table: "Usuarios",
                column: "AtivacaoTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CodigoConvite",
                table: "Usuarios",
                column: "CodigoConvite",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ContratanteId",
                table: "Usuarios",
                column: "ContratanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Cpf",
                table: "Usuarios",
                column: "Cpf",
                unique: true,
                filter: "[Cpf] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_FornecedorId",
                table: "Usuarios",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_IndicadoPorId",
                table: "Usuarios",
                column: "IndicadoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Avaliacoes");

            migrationBuilder.DropTable(
                name: "Convites");

            migrationBuilder.DropTable(
                name: "EventosPedido");

            migrationBuilder.DropTable(
                name: "Mensagens");

            migrationBuilder.DropTable(
                name: "ModelosCardapio");

            migrationBuilder.DropTable(
                name: "Notificacoes");

            migrationBuilder.DropTable(
                name: "PushInscricoes");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Pedidos");

            migrationBuilder.DropTable(
                name: "Propostas");

            migrationBuilder.DropTable(
                name: "Demandas");

            migrationBuilder.DropTable(
                name: "Fornecedores");

            migrationBuilder.DropTable(
                name: "Contratantes");
        }
    }
}
