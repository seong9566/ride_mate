namespace RideMateService.Comm.Map;

/// <summary>
/// 좌표 사이의 거리 계산
/// </summary>
public static class GeoDistance
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>
    /// 두 좌표 사이의 직선(대원) 거리를 미터로 돌려준다. 하버사인 공식을 쓴다.
    /// </summary>
    public static double Meters(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        var phi1 = double.DegreesToRadians((double)lat1);
        var phi2 = double.DegreesToRadians((double)lat2);
        var deltaPhi = double.DegreesToRadians((double)(lat2 - lat1));
        var deltaLambda = double.DegreesToRadians((double)(lng2 - lng1));

        var sinPhi = Math.Sin(deltaPhi / 2);
        var sinLambda = Math.Sin(deltaLambda / 2);
        var a = sinPhi * sinPhi + Math.Cos(phi1) * Math.Cos(phi2) * sinLambda * sinLambda;

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
