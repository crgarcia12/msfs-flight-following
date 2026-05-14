using System;
using System.Collections.Generic;
using System.Linq;

namespace MSFSFlightFollowing.Vatsim;

/// <summary>
/// Combines the VATSIM data feed + transceivers feed + the aircraft's current
/// position into an ordered list of controllers/ATIS stations whose coverage
/// area contains the aircraft. Pure function — no I/O, easy to unit-test.
/// </summary>
internal static class VatsimMatcher
{
    public static List<NearbyController> Match(
        VatsimDataDto data,
        IReadOnlyDictionary<string, (double Lat, double Lon)> stationPositions,
        double aircraftLat,
        double aircraftLon,
        int rangeBufferNm)
    {
        var facilityShort = data.Facilities.ToDictionary(
            f => f.Id,
            f => string.IsNullOrWhiteSpace(f.Short) ? "ATC" : f.Short);

        var result = new List<NearbyController>();

        foreach (var c in data.Controllers)
            result.AddRange(BuildIfInRange(c, facilityShort.GetValueOrDefault(c.Facility, "ATC"), stationPositions, aircraftLat, aircraftLon, rangeBufferNm));
        foreach (var a in data.Atis)
            result.AddRange(BuildIfInRange(a, "ATIS", stationPositions, aircraftLat, aircraftLon, rangeBufferNm));

        return result
            .OrderBy(c => FacilityOrder(c.FacilityShort))
            .ThenBy(c => c.DistanceNm)
            .ToList();
    }

    private static IEnumerable<NearbyController> BuildIfInRange(
        VatsimControllerDto c,
        string facilityShort,
        IReadOnlyDictionary<string, (double Lat, double Lon)> stationPositions,
        double aircraftLat,
        double aircraftLon,
        int rangeBufferNm)
    {
        if (!stationPositions.TryGetValue(c.Callsign, out var pos))
            yield break; // No transceiver position published — skip.

        var distance = HaversineNm(aircraftLat, aircraftLon, pos.Lat, pos.Lon);
        if (distance > c.VisualRange + rangeBufferNm)
            yield break;

        yield return new NearbyController(
            Callsign:        c.Callsign,
            Frequency:       c.Frequency,
            FacilityShort:   facilityShort,
            ControllerName:  c.Name,
            VisualRangeNm:   c.VisualRange,
            DistanceNm:      System.Math.Round(distance, 1),
            LatitudeDeg:     pos.Lat,
            LongitudeDeg:    pos.Lon,
            AtisCode:        string.IsNullOrWhiteSpace(c.AtisCode) ? null : c.AtisCode,
            AtisText:        (c.TextAtis is { Count: > 0 }) ? string.Join('\n', c.TextAtis) : null);
    }

    /// <summary>
    /// Given the transceivers feed, return the average position of every
    /// callsign so we can place the controller on the map and measure distance.
    /// </summary>
    public static Dictionary<string, (double Lat, double Lon)> BuildStationPositions(
        IReadOnlyList<VatsimTransceiverGroupDto> transceivers)
    {
        var dict = new Dictionary<string, (double Lat, double Lon)>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in transceivers)
        {
            if (group.Transceivers.Count == 0) continue;
            double lat = 0, lon = 0;
            foreach (var t in group.Transceivers) { lat += t.LatDeg; lon += t.LonDeg; }
            lat /= group.Transceivers.Count;
            lon /= group.Transceivers.Count;
            dict[group.Callsign] = (lat, lon);
        }
        return dict;
    }

    private static int FacilityOrder(string s) => s switch
    {
        "DEL"  => 0,
        "GND"  => 1,
        "TWR"  => 2,
        "DEP"  => 3,
        "APP"  => 3,
        "CTR"  => 4,
        "FSS"  => 5,
        "ATIS" => 9,
        _      => 6
    };

    // Great-circle distance between two lat/lon points, in nautical miles.
    private static double HaversineNm(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusNm = 3440.065;
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusNm * c;
    }

    private static double ToRadians(double deg) => deg * Math.PI / 180.0;
}
