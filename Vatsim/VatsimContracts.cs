using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MSFSFlightFollowing.Vatsim;

// -----------------------------------------------------------------------------
// Public types — what the agent bus and the front-end actually see
// -----------------------------------------------------------------------------

/// <summary>One ATC station the aircraft is currently within range of.</summary>
public sealed record NearbyController(
    string Callsign,
    string Frequency,
    string FacilityShort,   // e.g. "TWR", "APP", "CTR", "ATIS", "GND", "DEL"
    string ControllerName,
    int    VisualRangeNm,
    double DistanceNm,
    double LatitudeDeg,
    double LongitudeDeg,
    string? AtisCode,       // null for non-ATIS positions
    string? AtisText        // null for non-ATIS positions
);

// -----------------------------------------------------------------------------
// Wire-level DTOs — mirror the JSON returned by data.vatsim.net
// -----------------------------------------------------------------------------

internal sealed class VatsimDataDto
{
    [JsonPropertyName("controllers")] public List<VatsimControllerDto> Controllers { get; set; } = new();
    [JsonPropertyName("atis")]        public List<VatsimControllerDto> Atis { get; set; } = new();
    [JsonPropertyName("facilities")]  public List<VatsimFacilityDto>   Facilities { get; set; } = new();
}

internal sealed class VatsimControllerDto
{
    [JsonPropertyName("cid")]           public int    Cid { get; set; }
    [JsonPropertyName("name")]          public string Name { get; set; } = "";
    [JsonPropertyName("callsign")]      public string Callsign { get; set; } = "";
    [JsonPropertyName("frequency")]     public string Frequency { get; set; } = "";
    [JsonPropertyName("facility")]      public int    Facility { get; set; }
    [JsonPropertyName("rating")]        public int    Rating { get; set; }
    [JsonPropertyName("visual_range")]  public int    VisualRange { get; set; }
    [JsonPropertyName("atis_code")]     public string? AtisCode { get; set; }
    [JsonPropertyName("text_atis")]     public List<string>? TextAtis { get; set; }
}

internal sealed class VatsimFacilityDto
{
    [JsonPropertyName("id")]    public int    Id { get; set; }
    [JsonPropertyName("short")] public string Short { get; set; } = "";
    [JsonPropertyName("long_name")] public string LongName { get; set; } = "";
}

internal sealed class VatsimTransceiverGroupDto
{
    [JsonPropertyName("callsign")]     public string Callsign { get; set; } = "";
    [JsonPropertyName("transceivers")] public List<VatsimTransceiverDto> Transceivers { get; set; } = new();
}

internal sealed class VatsimTransceiverDto
{
    [JsonPropertyName("id")]          public int    Id { get; set; }
    [JsonPropertyName("frequency")]   public long   FrequencyHz { get; set; }
    [JsonPropertyName("latDeg")]      public double LatDeg { get; set; }
    [JsonPropertyName("lonDeg")]      public double LonDeg { get; set; }
    [JsonPropertyName("heightMslM")]  public double HeightMslM { get; set; }
    [JsonPropertyName("heightAglM")]  public double HeightAglM { get; set; }
}
