namespace CoffeeJa.Domain;

public static class Geo
{
    private const double RaioTerraKm = 6371.0088;

    public static double DistanciaKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double g) => g * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * RaioTerraKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Caixa delimitadora usada como pré-filtro indexável antes do cálculo exato.</summary>
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) CaixaDelimitadora(double lat, double lon, double raioKm)
    {
        var dLat = raioKm / 111.32;
        var dLon = raioKm / (111.32 * Math.Max(0.01, Math.Cos(lat * Math.PI / 180)));
        return (lat - dLat, lat + dLat, lon - dLon, lon + dLon);
    }
}
