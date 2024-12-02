using System.Collections.Generic;

namespace MSFSFlightFollowing;
public class FmcDisplay
{
    public List<List<string>> Lines { get; set; }
    public string Scratchpad { get; set; }
    public string Title { get; set; }
    public string TitleLeft { get; set; }
    public string Page { get; set; }
    public List<bool> Arrows { get; set; }
    public double IntegralBrightness { get; set; }
    public FmcAnnunciators Annunciators { get; set; }
    public double DisplayBrightness { get; set; }
}
