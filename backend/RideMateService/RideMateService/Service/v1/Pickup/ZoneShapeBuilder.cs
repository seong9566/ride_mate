using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// WKT 도형을 단속 구역 면으로 변환한다
/// </summary>
public static class ZoneShapeBuilder
{
    public const string FormatVersion = "1";

    /// <summary>
    /// 경도·위도 순서의 WKT를 버퍼가 적용된 단속 구역 면으로 변환한다
    /// </summary>
    public static ZoneShapeResult Build(string wktLngLat, short bufferM, double originLat, double originLng)
    {
        Geometry geometry;

        try
        {
            geometry = new WKTReader().Read(wktLngLat);
        }
        catch (Exception)
        {
            return new ZoneShapeResult { FailureReason = "wkt" };
        }

        try
        {
            var metersPerDegreeLat = MetersPerDegreeLat(originLat);
            var metersPerDegreeLng = MetersPerDegreeLng(originLat);

            geometry.Apply(new LocalProjectionFilter(
                originLat,
                originLng,
                metersPerDegreeLat,
                metersPerDegreeLng));
            geometry.GeometryChanged();

            var isSurface = geometry.Dimension == Dimension.Surface;
            if (isSurface)
                geometry = GeometryFixer.Fix(geometry);

            if (bufferM > 0)
                geometry = geometry.Buffer(bufferM, 8);
            else if (!isSurface)
                geometry = geometry.Buffer(1, 8);

            if (geometry.NumPoints > 500)
                geometry = DouglasPeuckerSimplifier.Simplify(geometry, 0.5);

            var polygons = new List<ZonePolygon>();
            AddPolygons(
                geometry,
                polygons,
                originLat,
                originLng,
                metersPerDegreeLat,
                metersPerDegreeLng);

            if (polygons.Count == 0)
                throw new InvalidOperationException();

            return new ZoneShapeResult { Polygons = polygons };
        }
        catch (Exception exception)
        {
            return new ZoneShapeResult { FailureReason = exception.GetType().Name };
        }
    }

    /// <summary>
    /// 주어진 위도에서 위도 1도의 길이(m)를 계산한다
    /// </summary>
    public static double MetersPerDegreeLat(double lat)
    {
        var phi = lat * Math.PI / 180;
        return 111132.954 - 559.822 * Math.Cos(2 * phi) + 1.175 * Math.Cos(4 * phi);
    }

    /// <summary>
    /// 주어진 위도에서 경도 1도의 길이(m)를 계산한다
    /// </summary>
    public static double MetersPerDegreeLng(double lat)
    {
        var phi = lat * Math.PI / 180;
        return 111412.84 * Math.Cos(phi) - 93.5 * Math.Cos(3 * phi);
    }

    /// <summary>
    /// 경도·위도 순서의 WKT에서 첫 좌표를 반환한다
    /// </summary>
    public static (double Lat, double Lng)? FirstCoordinate(string wktLngLat)
    {
        try
        {
            var coordinate = new WKTReader().Read(wktLngLat).Coordinate;
            return coordinate is null ? null : (coordinate.Y, coordinate.X);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void AddPolygons(
        Geometry geometry,
        List<ZonePolygon> polygons,
        double originLat,
        double originLng,
        double metersPerDegreeLat,
        double metersPerDegreeLng)
    {
        if (geometry is Polygon polygon)
        {
            var holes = new List<List<(double Lat, double Lng)>>(polygon.NumInteriorRings);
            for (var i = 0; i < polygon.NumInteriorRings; i++)
            {
                holes.Add(ToLngLatRing(
                    polygon.GetInteriorRingN(i).Coordinates,
                    originLat,
                    originLng,
                    metersPerDegreeLat,
                    metersPerDegreeLng));
            }

            polygons.Add(new ZonePolygon
            {
                Outer = ToLngLatRing(
                    polygon.ExteriorRing.Coordinates,
                    originLat,
                    originLng,
                    metersPerDegreeLat,
                    metersPerDegreeLng),
                Holes = holes,
            });
            return;
        }

        if (geometry is not GeometryCollection collection)
            return;

        for (var i = 0; i < collection.NumGeometries; i++)
        {
            AddPolygons(
                collection.GetGeometryN(i),
                polygons,
                originLat,
                originLng,
                metersPerDegreeLat,
                metersPerDegreeLng);
        }
    }

    private static List<(double Lat, double Lng)> ToLngLatRing(
        Coordinate[] coordinates,
        double originLat,
        double originLng,
        double metersPerDegreeLat,
        double metersPerDegreeLng)
    {
        var count = coordinates.Length;
        if (count > 1 && coordinates[0].Equals2D(coordinates[^1]))
            count--;

        var result = new List<(double Lat, double Lng)>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add((
                Math.Round(originLat + coordinates[i].Y / metersPerDegreeLat, 6),
                Math.Round(originLng + coordinates[i].X / metersPerDegreeLng, 6)));
        }

        return result;
    }

    private sealed class LocalProjectionFilter(
        double originLat,
        double originLng,
        double metersPerDegreeLat,
        double metersPerDegreeLng) : ICoordinateFilter
    {
        public void Filter(Coordinate coordinate)
        {
            coordinate.X = (coordinate.X - originLng) * metersPerDegreeLng;
            coordinate.Y = (coordinate.Y - originLat) * metersPerDegreeLat;
        }
    }
}

/// <summary>
/// 단속 구역 면 변환 결과
/// </summary>
public sealed class ZoneShapeResult
{
    public List<ZonePolygon> Polygons { get; init; } = new();

    public string? FailureReason { get; init; }
}

/// <summary>
/// 외곽 링과 내부 링으로 구성된 단속 구역 면
/// </summary>
public sealed class ZonePolygon
{
    public List<(double Lat, double Lng)> Outer { get; init; } = new();

    public List<List<(double Lat, double Lng)>> Holes { get; init; } = new();
}
