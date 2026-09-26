using System.Text;
using System.Text.Json;
using Landweigh.Core;
using Microsoft.Extensions.Configuration;

// Usage: dotnet run --project src/Landweigh.Cli -- <path to listing.html>
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

// Chinese titles and the ¥ sign print correctly only with UTF-8.
Console.OutputEncoding = Encoding.UTF8;

// Secrets come from user-secrets locally and from environment variables (Gemini__ApiKey) in CI or on a server.
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

// 1. Prune
var html = await File.ReadAllTextAsync(filePath);
var pruned = ListingPruner.Prune(html);
var keptPercent = html.Length == 0 ? 0 : 100.0 * pruned.Length / html.Length;

Console.WriteLine($"Original length: {html.Length:N0} chars");
Console.WriteLine($"Pruned length:   {pruned.Length:N0} chars ({keptPercent:F1}% kept)");
Console.WriteLine($"Preview: {pruned[..Math.Min(500, pruned.Length)]}");
Console.WriteLine();

// 2. Extract
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
Console.WriteLine($"Tokens: {result.PromptTokens:N0} in, {result.OutputTokens:N0} out");
Console.WriteLine();

// 3. Print the JSON. Parsing it also tells us whether the model returned valid JSON at all.
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
