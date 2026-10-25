using GBV_Shield.Services;
using System.Net;
using System.Text.Json;

void Assert(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
bool Relevant(object tags) { using var d = JsonDocument.Parse(JsonSerializer.Serialize(tags)); return OSMService.IsRelevantSupport(d.RootElement); }
Assert(!Relevant(new { name="1st Benoni Sea Scouts", amenity="community_centre" }), "Exclude Scouts");
Assert(!Relevant(new { name="The Magic Company", amenity="community_centre" }), "Exclude entertainment");
Assert(!Relevant(new { name="Jupiter's", office="ngo" }), "NGO tag alone is insufficient");
Assert(Relevant(new { name="Kidz Clinic Boksburg", amenity="clinic" }), "Include Kidz Clinic");
Assert(Relevant(new { name="Childline Gauteng" }), "Include Childline");
Assert(Relevant(new { name="Boksburg Child Welfare" }), "Include Child Welfare");
Assert(Relevant(new { name="Child & Family Welfare" }), "Include ampersand family welfare");
Assert(Relevant(new { name="Hope Child Protection Centre" }), "Include child protection centres");
Assert(Relevant(new { name="The Teddy Bear Clinic" }), "Include specialist abuse clinics");
Assert(Relevant(new { name="Hope Child and Youth Care Centre" }), "Include child and youth care");
Assert(Relevant(new { name="Hope Children's Home" }), "Include public children's homes");
Assert(Relevant(new { name="Family Office", social_work="child_protection" }), "Include explicitly tagged child protection");
Assert(Relevant(new Dictionary<string,string> { ["name"]="Hope", ["social_facility"]="group_home", ["social_facility:for"]="orphans; children" }), "Include child-care facility tags without amenity tag");
Assert(!Relevant(new { name="Children's Toy Shop", shop="toys" }), "Exclude generic child businesses");
Assert(!Relevant(new { name="Teddy Bear Preschool", amenity="kindergarten" }), "Exclude ordinary preschools");
Assert(Relevant(new Dictionary<string,string> { ["name"]="Hope Centre", ["amenity"]="social_facility", ["social_facility:for"]="abused;women" }), "Include tagged abuse support");
var handler = new FixtureHandler();
var service = new OSMService(new HttpClient(handler));
var police = await service.GetNearbySupportAsync(0,0,SupportCategory.Police,10000);
Assert(police.Count==2, "Keep stations, exclude academy and outside-radius station, deduplicate objects");
Assert(police[0].Name=="Near Police" && police[1].Name=="Far Police", "Sort by distance");
Assert(police.All(x=>x.Address!="Address not supplied"), "Recover missing street address");
Assert(handler.Calls==2, "Retry incomplete Overpass response rather than treating it as empty success");
var local = await service.GetNearbySupportAsync(-26.192579,28.256326,SupportCategory.Police,10000);
Assert(local.Any(x=>x.Name=="Boksburg North SAPS"), "Official station survives missing OSM address");
Assert(local.All(x=>x.Distance<=10000) && local.SequenceEqual(local.OrderBy(x=>x.Distance)), "Merged records respect radius and ordering");
Console.WriteLine("All directory checks passed.");
var ngoHandler = new NgoFixtureHandler();
var ngoService = new OSMService(new HttpClient(ngoHandler));
var ngos = await ngoService.GetNearbySupportAsync(0,0,SupportCategory.NgoNpo);
Assert(ngos.Count == 61, "NGO search has no 50-result cap and keeps address-only offices");
Assert(ngos.All(x => x.Distance <= 50000) && ngos.SequenceEqual(ngos.OrderBy(x=>x.Distance)), "NGOs default to 50km and sort nearest first");
Assert(ngos.Any(x=>x.Name=="Far Child Protection Centre") && !ngos.Any(x=>x.Name.StartsWith("Outside")), "Include between 10km and 50km; exclude beyond 50km");
Assert(!ngos.Any(x=>x.Name.StartsWith("Private") || x.Name.StartsWith("Closed")), "Exclude private or disused facilities");
Assert(ngoHandler.Query.Contains("safeguarding") && ngoHandler.Query.Contains("child_protection"), "Live query requests expanded child protection candidates");
Assert(SupportDirectory.Entries.Count(x=>x.Category==SupportCategory.NgoNpo) == 19, "Expanded saved directory includes 19 support contacts");
Assert(SupportDirectory.Entries.Any(x=>x.ServiceLabel.Contains("shelter") && x.ServiceLabel.Contains("children")),
    "Directory includes shelters for women and children");
Assert(SupportDirectory.Entries.Any(x=>x.ServiceLabel.Contains("rape") || x.ServiceLabel.Contains("sexual")),
    "Directory includes rape and sexual-abuse support");
Assert(ngoService.SearchNotice.Contains("incomplete"), "Do not promise exhaustive coverage");
Assert(SupportDirectory.Entries.Count(x=>x.Category==SupportCategory.Hospital) == 5,
    "Verified public 24-hour hospital directory includes five Ekurhuleni hospitals");

sealed class NgoFixtureHandler : HttpMessageHandler
{
    public string Query = "";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Query = Uri.UnescapeDataString(await request.Content!.ReadAsStringAsync(token));
        var elements = Enumerable.Range(1,60).Select(i => new { lat=0.001*i, lon=0d,
            tags=new Dictionary<string,string> { ["name"]=$"Child Protection Centre {i}", ["addr:street"]=$"{i} Test Street" } }).ToList();
        elements.Add(new { lat=0.4, lon=0d, tags=new Dictionary<string,string> { ["name"]="Far Child Protection Centre", ["addr:street"]="Far Street" } });
        elements.Add(new { lat=0.46, lon=0d, tags=new Dictionary<string,string> { ["name"]="Outside Child Protection Centre", ["addr:street"]="Outside Street" } });
        elements.Add(new { lat=0.1, lon=0d, tags=new Dictionary<string,string> { ["name"]="Private Children's Home", ["access"]="private" } });
        elements.Add(new { lat=0.1, lon=0d, tags=new Dictionary<string,string> { ["name"]="Closed Child Welfare", ["disused"]="yes" } });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { elements })) };
    }
}

sealed class FixtureHandler : HttpMessageHandler
{
    public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        Calls++;
        var json = Calls==1 ? "{\"remark\":\"runtime error: timeout\",\"elements\":[]}" : """
        {"elements":[
        {"lat":0.02,"lon":0,"tags":{"name":"Far Police","amenity":"police","addr:street":"Second Street"}},
        {"lat":0.01,"lon":0,"tags":{"name":"Near Police","amenity":"police"}},
        {"lat":0.01,"lon":0,"tags":{"name":"Near Police","amenity":"police"}},
        {"lat":0.01,"lon":0,"tags":{"name":"Police Academy","amenity":"police"}},
        {"lat":0.5,"lon":0,"tags":{"name":"Outside Police","amenity":"police","addr:street":"Third Street"}}
        ]}
        """;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(json)});
    }
}
