using System.Net.Http.Json;
using System.Text.Json;

namespace Landweigh.Core;

public sealed record ExtractionResult(string Json, int PromptTokens, int OutputTokens, int ThoughtTokens);

public interface IProductExtractor
{
    Task<ExtractionResult> ExtractAsync(string listingText, CancellationToken ct = default);
}

public sealed class GeminiProductExtractor(HttpClient http, string apiKey, string model) : IProductExtractor
{
    private const string Prompt = """
        You extract product data from the text of one supplier listing on 1688 or Alibaba.
        Return ONLY a JSON object with exactly this shape:
        {
          "title_original": string,
          "title_en": string,
          "category": one of "sports_outdoors", "electronics_accessories", "lighting", "home_kitchen", "apparel_textiles", "beauty_personal_care", "toys_hobbies", "auto_parts", "tools_hardware", "packaging_printing", "other",
          "supplier": { "name": string, "profile_url": string, "location": string, "years_on_platform": number },
          "currency": string (ISO code, e.g. "CNY"),
          "unit_of_measure": string (e.g. "piece", "set", "pair", "carton"),
          "moq": number,
          "dispatch_days": number,
          "domestic_shipping": number,
          "variants": [
            {
              "label": string,
              "attributes": { snake_case_key: string },
              "price_tiers": [ { "min_qty": number, "max_qty": number, "unit_price": number } ],
              "unit_weight_kg": number,
              "package_dims_cm": [number, number, number]
            }
          ],
          "packaging": { "units_per_carton": number, "carton_weight_kg": number, "carton_dims_cm": [number, number, number] },
          "customization": { "oem": boolean, "logo_print": boolean },
          "certifications": [string],
          "attributes": { snake_case_key: string },
          "unresolved_fields": [string]
        }

        Rules:
        - Any value the text does not state is null. Never guess. certifications and unresolved_fields are [] when empty.
        - Describe only the main product. Ignore prices and details of recommended or other products on the page.
        - Every listing has at least one variant. With no SKUs, use one variant labelled "default".
        - Prices live only in variants[].price_tiers. A single price with no quantity range is one tier with min_qty and max_qty null.
        - SKUs become separate variants only if price, weight or size differ. Options that differ only in colour or style go into the top-level attributes as one comma-separated string.
        - unit_weight_kg and package_dims_cm describe one packed unit. Carton data goes in packaging. Convert grams to kg.
        - dispatch_days is the stated time until the supplier ships (48 hours = 2), not production time.
        - domestic_shipping is the stated shipping fee inside China, in the listing currency.
        - attributes: facts about the product only, flat, snake_case keys, string values only. No supplier metrics, reviews, return policy or sales regions.
        - Numbers are plain JSON numbers without currency symbols or units.
        - The page may be machine-translated and append footnote digits to values (e.g. "XY0071", "XY0072" for "XY007"). Record the real value.
        - If the text states a field in contradictory ways, record the best reading and add the field path (e.g. "attributes.material", "customization.oem") to unresolved_fields.

        Listing text:
        """;

    public async Task<ExtractionResult> ExtractAsync(string listingText, CancellationToken ct = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = Prompt + listingText } } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("x-goog-api-key", apiKey);

        using var response = await http.SendAsync(request, ct);
        if(!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Request failed with status code {response.StatusCode}: {errorContent}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = document.RootElement;
        string extractedJson = root
        .GetProperty("candidates")[0]
        .GetProperty("content")
        .GetProperty("parts")[0]
        .GetProperty("text")
        .GetString() ?? "";

        var usage = root.GetProperty("usageMetadata");
        int promptTokens = usage.GetProperty("promptTokenCount").GetInt32();
        int outputTokens = usage.GetProperty("candidatesTokenCount").GetInt32();
        int thoughtTokens = usage.GetProperty("thoughtTokenCount").GetInt32();
        return new ExtractionResult(extractedJson, promptTokens, outputTokens, thoughtTokens);
    }
}