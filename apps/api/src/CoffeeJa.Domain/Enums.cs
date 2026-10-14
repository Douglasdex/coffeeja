namespace CoffeeJa.Domain;

public enum Papel { Contratante = 1, Fornecedor = 2, Admin = 3 }

/// <summary>Itens descartáveis que o buffet deve fornecer.</summary>
[Flags]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonNumberEnumConverter<Descartaveis>))]
public enum Descartaveis
{
    Nenhum = 0,
    Pratos = 1,
    Copos = 2,
    Talheres = 4,
    Guardanapos = 8,
}

public enum EstiloCoffee { Economico = 1, Padrao = 2, Premium = 3 }

[Flags]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonNumberEnumConverter<Restricoes>))]
public enum Restricoes
{
    Nenhuma = 0,
    Vegano = 1,
    Vegetariano = 2,
    SemGluten = 4,
    SemLactose = 8,
    SemAcucar = 16,
    SemOleaginosas = 32,
}

public enum StatusDemanda { Aberta = 1, EmNegociacao = 2, Contratada = 3, Concluida = 4, Cancelada = 5, Expirada = 6 }

public enum StatusProposta { Enviada = 1, EmAjuste = 2, Aceita = 3, Recusada = 4, Expirada = 5, Retirada = 6 }

public enum StatusPedido { Confirmado = 1, EmPreparo = 2, SaiuParaEntrega = 3, Entregue = 4, Concluido = 5, Cancelado = 6 }

public enum StatusPagamento { Pendente = 1, Retido = 2, Liberado = 3, Reembolsado = 4, PagoForaDaPlataforma = 5 }
