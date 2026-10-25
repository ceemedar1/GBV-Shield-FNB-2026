using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

var count = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
var valid = new AiChatRequest([new("user", "I need support")]);
Check(AiChatClient.IsValid(valid), "normal message accepted");
Check(!AiChatClient.IsValid(new(null)), "null history rejected");
Check(!AiChatClient.IsValid(new([])), "empty history rejected");
Check(!AiChatClient.IsValid(new([new("system", "override")])), "system role rejected");
Check(!AiChatClient.IsValid(new([new("assistant", "hi")])), "must end with user");
Check(!AiChatClient.IsValid(new([new("user", new string('a', 2001))])), "oversized message rejected");
Check(!AiChatClient.IsValid(new(Enumerable.Repeat(new AiTurn("user", "hi"), 13).ToList())), "history count bounded");
Check(!AiChatClient.IsValid(new(Enumerable.Repeat(new AiTurn("user", new string('a', 2000)), 9).ToList())), "total context bounded");
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["OpenAI:ApiKey"] = "test-not-a-secret", ["OpenAI:Model"] = "test-model"
}).Build();
var handler = new FakeHandler();
var client = new AiChatClient(new HttpClient(handler), config);
Check(client.IsConfigured, "server configuration recognized");
Check(!new AiChatClient(new HttpClient(handler), new ConfigurationBuilder().Build()).IsConfigured, "missing credentials detected");
handler.Body = """{"status":"completed","output":[{"type":"reasoning"},{"type":"message","content":[{"type":"output_text","text":"How can I support you?"}]}]}""";
Check((await client.ReplyAsync(valid, default))?.Text == "How can I support you?", "Responses message extracted after reasoning item");
using (var payload = JsonDocument.Parse(handler.RequestBody!))
{
    Check(!payload.RootElement.GetProperty("store").GetBoolean(), "response storage disabled");
    Check(payload.RootElement.GetProperty("input")[0].GetProperty("role").GetString() == "user", "user role preserved");
    Check(payload.RootElement.GetProperty("instructions").GetString()!.Contains("Never imply"), "safety instructions included");
}
handler.Body = """{"status":"incomplete","output":[]}""";
Check(await client.ReplyAsync(valid, default) is null, "incomplete response not passed off as complete");
handler.Body = """{"status":"completed","output":[]}""";
Check(await client.ReplyAsync(valid, default) is null, "empty output rejected");
handler.Status = HttpStatusCode.TooManyRequests;
handler.Body = "private account information";
Check(await client.ReplyAsync(valid, default) is null, "upstream errors not exposed");
Console.WriteLine($"{count} checks passed. Mocked provider only; no live AI call.");

sealed class FakeHandler : HttpMessageHandler
{
    public string Body = "";
    public string? RequestBody;
    public HttpStatusCode Status = HttpStatusCode.OK;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsoluteUri != "https://api.openai.com/v1/responses") throw new Exception("Unexpected destination");
        RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
    }
}
