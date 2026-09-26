# Labelling rules (schema version 2)

Each fixture folder holds the input (`listing.html` and/or `screenshot.png`) and `expected.json`, the answer key: what a perfect extraction of **that input file** returns. The key describes the saved file, not the live page. If the saved page was logged out, the key only covers what a logged-out page shows.

## Capturing a listing

- Open the listing in your browser the way a user would: logged out, on the 1688 global site (English auto-translation).
- Save it with **Save as > Webpage, Complete** and keep only the `.html`, renamed to `listing.html`. Delete or ignore the `_files` folder.
- This gives the rendered page (after JavaScript ran), which is what Landweigh will receive.
- Pages that are not product pages (captcha, login wall) go in `fixtures/blocked/` with a descriptive name. They have no answer key; they test block detection.

## Structure

- **Listing level:** platform, URL, titles, category, supplier, currency, unit of measure, MOQ, dispatch days, domestic shipping, carton packaging, customization, certifications, attributes.
- **Variant level:** anything that changes between SKUs: price tiers, unit weight, packed dimensions, and the attributes that tell the SKUs apart.
- Every listing has at least one variant. A listing without SKUs has exactly one, labelled `default`.
- Prices always live in `variants[].price_tiers`, never at listing level.

## When SKUs become separate variants

Only when they differ in price, weight or dimensions. SKUs that differ only in colour or style at the same price collapse into one variant, and the options go into listing-level `attributes` as a comma-separated string.

## Values

- `null` means the input file does not state it. Never guess.
- `unresolved_fields` is only for values the file states in contradictory ways. Record the best reading, list the field path, and explain in `labelling_notes`.
- Numbers are JSON numbers, without currency symbols.
- Record what is written. Do not correct typos or convert units in the key.
- Exception: the translated 1688 page sometimes appends footnote digits to values (`XY0071`, `No3`). Record the real value, list the field in `unresolved_fields`, and explain it in the notes.
- `dispatch_days` is the stated time until the supplier ships, not production lead time.
- `domestic_shipping` is the stated shipping fee inside China, in the listing currency.
- `certifications` is an array of names (e.g. `["CE"]`) that the file claims. Whether they are genuine is outside the key's scope.

## Attributes

- Flat map, snake_case keys, **string values only**. No arrays, booleans, numbers or nested objects.
- Only facts about the product. Supplier metrics, reviews, return policy and sales regions do not go in.
- No business logic reads attributes. If a value is needed for cost or sorting, it belongs in the core schema.

## Category list

`sports_outdoors`, `electronics_accessories`, `lighting`, `home_kitchen`, `apparel_textiles`, `beauty_personal_care`, `toys_hobbies`, `auto_parts`, `tools_hardware`, `packaging_printing`, `other`

## Notes

`labelling_notes` explains labelling decisions only: why a value was chosen, what was ambiguous, what the input does not cover. Sourcing advice goes somewhere else.

## Fixture metadata

`_fixture` is ignored by the eval. Set `human_verified` to `true` only after checking every field against the input file yourself.
