using System.Text;
using System.Text.Json;
using Landweigh.Core;
using Microsoft.Extensions.Configuration;


if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Landweigh.Cli <path to listing.html>");
    return 1;
}

var filePath = args[0];
if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"File not found: {filePath}");
    return 1;
}

Console.OutputEncoding = Encoding.UTF8;
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var apiKey = config["Gemini:ApiKey"];
var model = config["Gemini:Model"];
if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
{
    Console.Error.WriteLine("Missing Gemini:ApiKey or Gemini:Model. Set them with:");
    Console.Error.WriteLine("  dotnet user-secrets set \"Gemini:ApiKey\" \"<your key>\" --project src/Landweigh.Cli");
    Console.Error.WriteLine("  dotnet user-secrets set \"Gemini:Model\" \"<model code>\" --project src/Landweigh.Cli");
    return 1;
}
var html = await File.ReadAllTextAsync(filePath);
var pruned = ListingPruner.Prune(html);
var keptPercent = html.Length == 0 ? 0 : 100.0 * pruned.Length / html.Length;

Console.WriteLine($"Original length: {html.Length:N0} chars");
Console.WriteLine($"Pruned length:   {pruned.Length:N0} chars ({keptPercent:F1}% kept)");
Console.WriteLine($"Preview: {pruned[..Math.Min(500, pruned.Length)]}");
Console.WriteLine();

using var http = new HttpClient();
IProductExtractor extractor = new GeminiProductExtractor(http, apiKey, model);

ExtractionResult result;
try
{
    result = await extractor.ExtractAsync(pruned);
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Gemini call failed: {ex.Message}");
    return 1;
}

Console.WriteLine($"Model: {model}");
Console.WriteLine($"Tokens: {result.PromptTokens:N0} in, {result.OutputTokens:N0} out, {result.ThoughtTokens:N0} thought");
Console.WriteLine();

try
{
    using var doc = JsonDocument.Parse(result.Json);
    Console.WriteLine(JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }));
}
catch (JsonException)
{
    Console.Error.WriteLine("The model did not return valid JSON. Raw output:");
    Console.WriteLine(result.Json);
    return 1;
}

return 0;
