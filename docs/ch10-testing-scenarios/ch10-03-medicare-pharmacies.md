# Chapter 10.3 — Medicare Analysis: Pharmacies

> Route: `/medicare-analysis/pharmacies`
> Covers pharmacy selection (FP lookup), legacy nearby pharmacy search, and plan-aware pharmacy search.

---

## 5d. Pharmacy Selection (FP Lookup)

> **Note:** Pharmacy selection now uses `PharmacyStepComponent` with the Financial Planner `GET /api/pharmacy/lookup` API. The old NPI-based "Find Nearby Pharmacies" flow is no longer used in the UI.

| # | Scenario | Precondition | Expected Result |
|---|----------|--------------|-----------------|
| 5d.1 | Pharmacies auto-load | Navigate to `/medicare-analysis/pharmacies` with profile containing lat/lng | Pharmacies loaded from API. Cards displayed with name, address, distance, zipcode. |
| 5d.2 | Filter by name | Type "CVS" in name filter → click Search | Only pharmacies matching "CVS" shown. |
| 5d.3 | Filter by radius | Change radius dropdown to 10 miles | Pharmacies within 10 miles shown. |
| 5d.4 | Clear filters | Click Clear button | Name cleared, radius reset to 25 mi, page reset to 1. |
| 5d.5 | Pagination | 50+ pharmacies, page size 20 | 3 pages shown. Next/Prev buttons. Page number window. |
| 5d.6 | Select pharmacy | Click pharmacy card checkbox | Pharmacy marked selected (emerald highlight + check). Counter updates ("1/5 selected"). System message posted. |
| 5d.7 | Unselect pharmacy | Click selected pharmacy checkbox | Pharmacy unmarked. Counter decreases. System message: "Deselected pharmacy: {name}". |
| 5d.8 | 5-pharmacy cap | Select 5 pharmacies → attempt 6th | 6th click silently ignored. Counter stays at 5/5. |
| 5d.9 | Selected pharmacies review panel | Select 2+ pharmacies | Emerald summary panel shows selected pharmacies with name, address, distance, remove (×) buttons. |
| 5d.10 | Remove from review panel | Click × on a pharmacy in review panel | Pharmacy deselected. Counter decreases. |
| 5d.11 | Google Maps: Spot on Map | Click map icon on pharmacy card | Opens Google Maps centered on pharmacy address in new tab. |
| 5d.12 | Google Maps: Directions | Click directions icon on pharmacy card | Opens Google Maps directions to pharmacy in new tab. |
| 5d.13 | No lat/lng in profile | Profile without latitude/longitude | Pharmacies request fails. Error handled gracefully. |
| 5d.14 | Page size change | Change per-page dropdown from 20 → 50 | More pharmacies shown per page. Pagination updated. |
| 5d.15 | Empty results | Filter by name "XYZNONEXIST" | No pharmacies found. Empty state suggests adjusting filters. |

---

## 5. Nearby Pharmacy Search & AI-Powered Pricing (Legacy Backend-Only)

> **Note:** The `GET /api/pharmacy/search` and `GET /api/pharmacy/nearby` endpoints have been removed from the backend. The analysis wizard uses `GET /api/pharmacy/lookup` (Financial Planner pharmacy lookup) via `PharmacyStepComponent`. These scenarios are retained for reference only.

| # | Scenario | Precondition | Expected Result |
|---|----------|--------------|-----------------|
| 5.1 | Pharmacies found | User has zip "90210". Submit drugs, then click "Find Nearby Pharmacies" button | Pharmacies populated. Cheapest has "Best Price" chip. Button hides after load. |
| 5.2 | No zip code | No address profile. Click "Find Nearby Pharmacies" | Empty pharmacies. Panel not shown. Error handled gracefully. |
| 5.3 | Select pharmacy | Click a pharmacy row | Row highlights. Per-drug price grid expands (Retail/Medicare/Generic). |
| 5.4 | Sort toggle | Click sort icon | Re-sorts by name ↔ price. |
| 5.5 | Collapse/expand | Click panel header | Panel toggles. |
| 5.7 | Chat summary | Check chat message after drug analysis | Summary mentions "Use the buttons below the results to load Medicare plan recommendations or find nearby pharmacies." |
| 5.8 | Standalone search | `GET /api/pharmacy/search?zip=90210&drugs=1364430` | Returns pharmacies with pricing. |
| 5.9 | Missing zip | `GET /api/pharmacy/search?drugs=1364430` (no zip) | Returns 400. |
| 5.10 | Pharmacy API timeout | Pharmacy API down, click "Find Nearby Pharmacies" | Loading spinner stops. No pharmacy panel. No errors. |
| 5.11 | AI pricing fails | IChatClient timeout | Fallback to ParsePriceString prices. |
| 5.12 | Both pricing fail | AI + ParsePriceString fail | Prices null. UI shows "—". |
| 5.13 | Brand-only drug | `"Eliquis 5mg"` | `genericPrice` null. "—" in Generic column. |
| 5.14 | Generic drug | `"Metformin 500mg"` | All three price columns populated. |
| 5.15 | Multiple drugs | `"Eliquis 5mg, Metformin 500mg"` | Per-drug prices summed into TotalRetailCost. Sorted by total. |
| 5.16 | AI pricing cache | Same drugs+zip twice | Second request instant from 30-day cache. |
| 5.17 | NPI cache | Different drugs, same zip | NPI from 7-day cache. Only AI pricing call made. |

---

## 5c. Plan-Aware Pharmacy Search (Legacy Backend-Only)

> **Note:** Plan-aware pharmacy display mode is not currently active in the UI. These scenarios are retained for backend API testing only.

| # | Scenario | Precondition | Expected Result |
|---|----------|--------------|-----------------|
| 5c.1 | Plan pharmacies loaded | Select a plan → click "Find Plan Pharmacies" | Pharmacies shown with copay columns instead of AI-estimated prices. |
| 5c.2 | Preferred network | Plan with preferred pharmacy network | Preferred pharmacies marked with badge. Lower copays for preferred pharmacies. |
| 5c.3 | Non-covered drug | Drug not on plan formulary | Drug row shows "Not Covered" with no copay. |
| 5c.4 | Prior auth required | Drug requires prior authorization | Amber "PA" tag on drug row in pharmacy panel. |
| 5c.5 | Formulary tier display | Select plan with tiered formulary | Tier number (1-5) shown per drug per pharmacy. |
| 5c.6 | No plan selected | Click plan pharmacy search without selecting plan | Button not visible or disabled. |
| 5c.7 | Total plan copay | Multiple drugs with copays | Per-pharmacy total copay aggregated across all drugs. |

---

← [Testing Index](../ch10-testing-scenarios/ch10-testing-scenarios.md)
