# Landweigh: project brief

Status: session 1 complete when every box in section 11 is ticked.
This file is the source of truth for scope. The general guide describes the method; this brief describes this project.

---

## 1. Problem

Importers sourcing from China end up with dozens of product listings across 1688 and Alibaba that cannot be compared with each other. Every listing uses a different layout, many are only in Chinese, prices come in quantity tiers and SKU variants quoted per piece, set or carton, the minimum order is buried in the description, and weights and packaging are missing or hidden. Today this gets copied by hand into notes or spreadsheets, which is slow, error-prone and repeated for every product in every category. Better looks like this: paste a product link and get back one comparable record with the real price at the quantity you would order and a landed cost per unit.

## 2. Who it is for

- **Primary user:** me, sourcing products from 1688 and Alibaba for resale in Romania and the EU.
- **Secondary users:** small importers doing the same in any product category.
- **Not for (yet):** sourcing agents managing many clients, large importers with ERP systems.

## 3. What version 1 does

1. Accepts a **product link** from 1688 or Alibaba as the main input.
2. Accepts a **saved HTML file** of a listing as the fallback when a link cannot be fetched, and as the input for every test.
3. Detects when a fetched page is not a product page (login wall, captcha, error page) and says so clearly instead of extracting garbage.
4. Strips the page down to the content that matters before it reaches the model, and logs tokens before and after.
5. Extracts one structured record per listing: a universal core, a list of variants with their own price tiers, weights and sizes, and a flat map of category-specific attributes.
6. Works for **any product category** through one code path.
7. Shows which fields the model could not determine and which ones the listing contradicts.
8. Lets me correct any field, and the correction sticks.
9. Computes landed cost per unit at an order quantity I choose (formula in section 8).
10. Shows every saved product, across categories and both platforms, in one sortable list.
11. Runs somewhere I can reach from a browser.

## 4. What version 1 does NOT do

| Not in v1 | Why, or when |
|---|---|
| Screenshot or image input | Deferred to M5. Links are the main use case. |
| CSV or supplier spreadsheet import | No real CSV to design against yet. |
| Logged-in scraping of 1688 or Alibaba | Needs account credentials and breaks often. v1 works from the public, logged-out page. |
| Automatic re-fetching on a schedule, price history | Needs a stable v1 first. |
| Any code specific to one product category | Category variation lives in the attributes map. |
| Category taxonomy beyond a short fixed list | The list grows from data, not in advance. |
| Automatic HS code classification or duty lookup | Duty is a manual override with a default. Candidate for v2. |
| Live currency rates | One fixed rate I set. |
| Product images | Not needed to compare prices. |
| Supplier rating or reputation scoring | Not a buying signal I use yet. |
| Messaging suppliers, placing orders | Out of scope for a comparison tool. |
| Multiple user accounts | One user until M4. |
| Excel export, mobile layout | Later, if I actually miss them. |

## 5. Capabilities (these become GitHub issues)

Ordered by priority. Each has acceptance criteria written in its issue before any code.

1. As a user, I can paste a 1688 or Alibaba product link and get back a structured product record, so that I stop copying fields by hand.
2. As a user, I am told clearly when a link could not be read and can upload the saved page instead, so that a blocked fetch never ends in silence or a wrong record.
3. As a user, I can see every variant and every price tier, so that I know the real unit price at the quantity I would order.
4. As a user, I can see category-specific details next to the core fields, so that I do not need to reopen the original page.
5. As a user, I can see which fields are missing or contradictory, so that I know what to check with the supplier.
6. As a user, I can correct any field and the correction sticks, so that the record is trustworthy.
7. As a user, I can set freight, VAT and exchange-rate assumptions once, and override duty per product, so that landed cost is correct in any category.
8. As a user, I can compare all saved products in one sortable list, filtered by category and platform, so that I can pick a supplier.

## 6. Decisions so far

These become the first files in `docs/adr/`.

| ADR | Decision | Reason |
|---|---|---|
| 001 | A product link is the main input. Saved HTML is the fallback and the only input used by tests and evals. | Links are how I actually work. Live pages change and cost money to fetch, so tests must run on fixed files. |
| 002 | The reference page state is **logged out**. | It is what an automated fetcher sees, so answer keys labelled from logged-out pages match production input exactly. |
| 003 | Page fetching sits behind an interface (`IListingFetcher`), separate from extraction. | Fetching is the most fragile part. It must be swappable (direct HTTP, headless browser, a scraping service) without touching extraction. |
| 004 | Schema: universal core, `variants[]` with their own price tiers, weight and size, and a flat string-only `attributes` map. | One code path for every category. Prices and weights change per SKU, so they live on variants. |
| 005 | SKUs become separate variants only when price, weight or size differ. | Keeps records small. Colour-only differences go into attributes. |
| 006 | Duty is a default rate with a per-product override, not a lookup. | HS classification is its own problem. |
| 007 | Stack: .NET 10 (C#), Gemini API free tier behind `IProductExtractor`, Postgres from M3, Angular from M4. | Matches my experience and the target job market. Model provider must be swappable for the eval comparison. |
| 008 | Screenshot input deferred to M5. | Links are the main use case. The image path reuses the same schema and evals later. |

## 7. The link pipeline (v1)

```
product URL
  -> validate          is it a 1688 or Alibaba product URL? strip tracking params
  -> fetch             IListingFetcher, logged-out page
  -> check page        product page, or login wall / captcha / error?
       blocked  -> clear message: "Could not read this page. Save it and upload the HTML."
  -> prune             strip scripts, styles, nav, footer; keep title, price, SKU and spec areas
  -> extract           one model call: core + variants + attributes
  -> validate          schema check, one retry, then a typed failure
  -> output            print JSON (M0-M2), store in Postgres (M3+)
  -> price             landed cost at a chosen quantity
```

The saved-HTML path joins at **prune**. Everything after that is shared.

## 8. Landed cost, version 1

Simplified formula. Check it with a customs broker before relying on it for real orders, and do not hardcode thresholds, because EU rules for low-value imports are changing.

```
unit_price        = price of the chosen variant at the tier matching order_qty
goods_value       = unit_price x order_qty                     (listing currency)
china_shipping    = domestic_shipping                          (per order, if stated)
freight           = shipping_per_kg x total_chargeable_weight  (my assumption)
customs_value     = goods_value + china_shipping + freight     (converted to EUR)
duty              = customs_value x duty_rate                  (override or default)
vat               = (customs_value + duty) x vat_rate
landed_cost_total = customs_value + duty + vat
landed_cost_unit  = landed_cost_total / order_qty
margin_unit       = my_selling_price - landed_cost_unit        (only if I enter a price)
```

Every input the listing does not state stays visible as an assumption, never a silent default.

## 9. Test data

| Fixture | Platform | Category | Input | Key status |
|---|---|---|---|---|
| 001-1688-fish-bucket | 1688 | sports_outdoors | HTML (logged out) | written, not human-verified |
| 002-1688-solar-garden-light | 1688 | lighting | HTML (logged out) | written, not human-verified |
| 003-alibaba-(tbd) | Alibaba | a new category | HTML (logged out) | to do |

Rules live in `fixtures/LABELLING.md`. Target for M2: about 100 listings, both platforms, at least five categories, including awkward cases (no English at all, prices only in an image, contradictory MOQ, carton pricing, many tiers).

## 10. Milestones

| Milestone | Goal | Done when |
|---|---|---|
| **Spike** | Can I fetch a 1688 and an Alibaba product page at all? | Timeboxed to one evening. Try plain HTTP, a headless browser and a scraping service on the three fixture URLs. Result written as an ADR: what worked, what got blocked, what it costs. |
| **M0** Walking skeleton | Something runs end to end. | Console app reads fixture HTML, prunes it, calls the model, prints JSON. Token counts before and after pruning printed. CI green. |
| **M1** Links and trustworthy extraction | The main use case works and fails cleanly. | URL input through `IListingFetcher`, blocked-page detection with a clear message, full schema (core, variants, tiers, attributes), validation with one retry, cost and tokens logged per call, unit tests for pruning, tier selection and validation. |
| **M2** Evals | I can prove extraction works. | About 100 labelled fixtures, harness reporting accuracy overall and per category, running in CI with a threshold. At least three models compared on accuracy and cost. |
| **M3** Service | It runs on a server and stores results. | Minimal API, Postgres, background jobs with retries, landed cost, deployed with HTTPS and a health endpoint. |
| **M4** Usable | I use it for real sourcing. | Angular UI, auth, README with results and the AI workflow section. At least one real order sourced with it. |
| **M5** Screenshots | Image input. | Screenshot path into the same schema, image size controlled for token cost, screenshot fixtures added to the evals. |

Why the spike comes first: fetching is both the main use case and the biggest unknown. One evening now tells me whether links are realistic, before I build anything that depends on them. Why M0 still starts from a saved file: extraction and fetching are separate risks, and fixtures already exist, so the skeleton can prove extraction without waiting on fetching.

## 11. Session 1 checklist

- [ ] Problem paragraph rewritten in my own words (section 1)
- [x] Project name chosen: **Landweigh**
- [ ] "Not in v1" list reviewed and agreed (section 4)
- [ ] Fixtures 001 and 002 checked field by field in a browser, `human_verified` set to `true`
- [ ] Fixture 003 created from an Alibaba listing in a new category
- [ ] Capabilities from section 5 ready to become GitHub issues
