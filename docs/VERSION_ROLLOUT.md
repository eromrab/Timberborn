# Tidetails version rollout

This document is the **release plan** for *Eromrab's Tidetails* (and related mods). It uses **mod semver** (`major.minor.patch` in each `manifest.json`), not the game version.

**North star:** **`1.0.0`** = the Tidetails faction is **feature-complete for a first public “full faction” release**: playable start-to-wonder with all intended custom buildings, beaver identity, water-centric rules, and breeding via Romance Retreat (Iron Teeth pods hidden). Vanilla Iron Teeth buildings remain available but are **Tidetails-tuned** via blueprint modifiers where you have already defined them.

**Today:** monolithic `Tidetails` at `0.1.0` + optional separate `BadwaterRush` at `0.1.0`.

---

## Version rules (keep releases predictable)

| Bump | When |
|------|------|
| **Patch** `x.y.Z` | Bug fixes, balance tweaks, localization, art polish, no new buildings/systems |
| **Minor** `x.Y.0` | New building, new good/recipe, new optional behavior, or **splitting out a new sub-mod** |
| **Major** `X.0.0` | Breaking saves, removing/replacing buildings, or changing core faction rules in a way old saves cannot tolerate |

**Exit criteria for any release:** everything in that version’s checklist is **verified in-game** (move items from `docs/things-to-fix.txt` VERIFY → DONE). Steam Workshop builds can stay **Hidden** until you hit `1.0.0` if you prefer link-only testing before that.

---

## Optional multi-mod layout (piecemeal + faction bundles all)

Goal: players (or you) can enable **slices** for testing; the **faction mod** only registers Tidetails when **all required slices** are present.

Suggested packages (names are placeholders; `Id` must stay stable once published):

| Mod `Id` | Contains | Depends on |
|----------|----------|------------|
| `Tidetails.Core` | Harmony entry, beaver adult/child modifiers, path speed, starting location, shared C# utilities | Harmony |
| `Tidetails.NaturalResources` | Live oak, bald cypress, water tupelo; shallow/deep crop placement modifiers; succulent forester hide | Core |
| `Tidetails.Paths` | Mud block, mud slide, wet-path behavior & materials | Core |
| `Tidetails.Landscaping` | Blockage, natural dam, dam modifier, flood/walk-on-top behavior | Core |
| `Tidetails.VanillaTweaks` | Pump/tank/farmhouse/fermenter/water wheel/shower modifiers, glass lid, placement validators, description patches (partial solid, terrain bullets) | Core |
| `Tidetails.Housing` | Romance need, Romance Retreat building + C# visit logic, breeding pod hide | Core |
| `Tidetails.Goods` | Fermented cattail/spadderdock, recipes, need collection hooks | Core (Housing if Romance consumes goods later) |
| **`Tidetails`** (meta / faction) | `Faction.Tidetails` blueprint, avatars, localization umbrella, **no duplicate content** | **All of the above** at `MinimumVersion` matching the meta release |
| `BadwaterRush` (optional) | Contaminated rush behavior | Harmony only; **not** required for Tidetails |

**Rollout note:** you do **not** have to split at `0.1`. A practical approach is **stay monolithic through `0.3.x`**, then **extract slices for `0.5.0`** once Romance and verify work is stable (splitting early costs a lot of manifest and path churn).

**Meta manifest pattern (example):**

```json
"RequiredMods": [
  { "Id": "Harmony", "MinimumVersion": "2.4.0" },
  { "Id": "Tidetails.Core", "MinimumVersion": "0.5.0" },
  { "Id": "Tidetails.NaturalResources", "MinimumVersion": "0.5.0" },
  ...
]
```

Each slice bumps its own version; the faction meta mod bumps when you raise **minimum** versions or change faction-only data.

---

## Stage checklist by mod version

Statuses: **Done (in repo)** = implemented, may still need VERIFY. **Target** = should be true before you ship that version.

### `0.1.0` — Prototype (current)

**Theme:** “Playable oddball faction,” not polished.

| Area | Done (in repo) | Target before calling 0.1 “shipped” |
|------|----------------|-------------------------------------|
| Faction | Tidetails faction, Iron Teeth building set + modifiers | Faction picker name **Eromrab's Tidetails** |
| Beaver | Swim fast / walk slow, teal recolor (verify) | VERIFY fur color + tail size |
| Paths | Reversed path costs; mud block/slide | — |
| Landscaping | Blockage, natural dam, dam tweak, walk on top | Confirm natural dam **0.9 vs 0.65** hold height intent |
| Food / power | Aquatic farmhouse tweak, compact wheel ground-only, crop placement | Tree display names in forester UI |
| Water | Deep Water Pump unified (water / badwater / both); Deep Badwater Pump hidden for Tidetails; mechanical pump modifier unchanged | — |
| Trees | Live oak, bald cypress, water tupelo | — |
| Breeding | Romance Retreat prototype; pods hidden | VERIFY retreat builds, floods, no dried-out, visit → kit flow |
| Optional | Badwater Rush separate mod | — |

---

### `0.2.0` — Paths & presentation

**Theme:** Movement and UI read as “water beavers,” not placeholder.

| Deliverable |
|-------------|
| Wet path look finalized (building mode muddy tint, normal view reads as road) |
| VERIFY items for trees, succulents/forester crash, mod display names |
| Romance retreat **art pass**: boarded windows, custom icon (behavior can still be basic) |
| Localization pass on new strings (title case, pump wording if not deferred to 0.4) |

---

### `0.3.0` — Housing & population (baseline breeding)

**Theme:** Tidetails has a **distinct, shippable** population loop.

| Deliverable |
|-------------|
| Romance Retreat **baseline** locked: dry-land placement, stock water + berries, pair visit, Romance 0→1 only inside, one kit, stock cleared |
| Iron Teeth breeding pods remain hidden/unusable for Tidetails |
| No animation-controller crash on build; rebuild note documented for save migration |
| `docs/things-to-fix.txt` VERIFY for romance **moved to DONE** |

**Not in 0.3:** wine/meat retreats, multi-kit variants (post-1.0 or `1.1.0`).

---

### `0.4.0` — Building clarity & water UX

**Theme:** Players understand **where** and **why** they can build.

| Deliverable |
|-------------|
| Deep water pump (and similar) show **Partially Solid** when appropriate |
| Stat bullets: **Deep Water**, **Shallow / Land**, **Dry Land Only** from placement specs |
| Natural dam hold height decision documented and aligned with design |
| Optional: **homeless sleep prefers water** (Tidetails-only patch) |

---

### `0.5.0` — Modular packaging (optional milestone)

**Theme:** Same gameplay as `0.4.x`, but **split mods** for easier testing and smaller downloads.

| Deliverable |
|-------------|
| Extract slices per table above; monolithic folder becomes meta + slices OR monolithic remains as “all-in-one” convenience pack |
| CI/build script produces N mod folders with aligned versions |
| Single “install all” zip or meta mod that pulls minimum versions |
| No gameplay change intended (only packaging); if gameplay changes, bump minor on affected slices |

**Skip this version** if you prefer one Workshop item until `1.0.0`; jump from `0.4` → `0.9` and split at `1.0.1` instead.

---

### `0.9.0` — Release candidate

**Theme:** Nothing left on the **1.0** list except deliberate “later” features.

| Deliverable |
|-------------|
| Full playthrough: new game → district center → water economy → fermenter goods → wonder (vanilla IT wonder OK as placeholder) |
| Balance pass: aquatic farmhouse costs, blockage/dam ordering, path costs |
| Save load: old saves from `0.3+` load without forester/menu crashes |
| README / Workshop description lists **required mods** and optional Badwater Rush |
| All OPEN items in `docs/things-to-fix.txt` either **DONE** or explicitly deferred to post-1.0 below |

---

### `1.0.0` — Full faction (your definition)

**Theme:** **All buildings and faction systems done** for the first complete Tidetails experience.

**Includes everything from `0.1`–`0.9`, plus:**

| Category | 1.0 complete means |
|----------|-------------------|
| **Custom buildings** | Mud block, mud slide, blockage, natural dam, aquatic farmhouse (Tidetails rules), Romance Retreat (baseline) — all shippable art and behavior |
| **Vanilla buildings (faction modifiers)** | Dam, path, farmhouse, fermenter, compact water wheel, unified Deep Water Pump (water / badwater / both), Deep Mechanical Pump, Deep Badwater Pump hidden, tanks, double shower, shallow & deep crops, breeding pod hide — placement and costs intentional |
| **Natural resources** | Three water trees + forester/gatherer integration |
| **Needs & goods** | Romance need; fermented cattail & spadderdock line playable |
| **Beaver identity** | Teal fur, enlarged tail models, swim/land speeds, starting location |
| **Optional companion** | Badwater Rush documented as optional; not required |
| **Quality** | No known crashers on VERIFY list; faction name branding consistent |

**Explicitly not required for 1.0** (track as **1.x / 2.0**):

| Feature | Suggested version |
|---------|-------------------|
| Advanced Romance Retreats (wine, meat, 2–3 kits) | `1.1.0` |
| Beaver gender / pairing rules | `1.2.0` or `2.0.0` if saves break |
| Custom Tidetails wonder (replace Iron Teeth placeholder) | `1.3.0` |
| Additional custom buildings beyond current set | minor bumps per building |
| Monolithic → split mods (if skipped at 0.5) | `1.0.1` packaging only |

---

## Quick map: `docs/things-to-fix.txt` → version

| Item | Target version |
|------|----------------|
| Wet path look | `0.2.0` |
| Romance polish (windows, icon) | `0.2.0` |
| Romance VERIFY (build, flood, kit flow) | `0.3.0` |
| Later romance retreats (goods, multi-kit) | `1.1.0` |
| Natural dam height decision | `0.4.0` |
| Sleep in water if homeless | `0.4.0` |
| Partially Solid + terrain stat bullets | `0.4.0` |
| Gender | `1.2.0+` |
| Beaver fur / tail / trees / succulents VERIFY | `0.2.0` |
| Mod names VERIFY | `0.2.0` |

---

## After you ship `1.0.0`

- **`1.0.x`:** hotfixes only.
- **`1.y.0`:** new content (advanced retreats, new buildings) without breaking core saves.
- **`2.0.0`:** reserved for faction rule overhauls (gender, wonder replacement, removing Iron Teeth building inheritance, etc.).

Update this file when you add buildings or change what “complete faction” means.
