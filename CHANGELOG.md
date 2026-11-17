# Changelog

## 1.3.1
- 4 more ocean species, and the first FishMon for four types: **Bonefin** (Ghost), **Cindergill** (Fire), **Kelpling** (Grass) and **Shellmite** (Bug)
- The host's preset and balance settings now apply to the whole lobby. Guests see them greyed out in the Mod Menu and get their own settings back when they leave (the host and guests need this version)
- Fixed: boss badges were never given, even when your team was under the level cap
- Fixed: beating a boss didn't give your FishMon its EXP, or count towards their bosses defeated and the Fishdex
- Fixed: most ocean species were invisible when they were a Drip
- Ocean species no longer bite on quest and special baits: Leech Bait, Crab Carcass, Carrot, the Chum Bucket, Boss Lures, Coconut and the Empty Beer Can
- Game fish types now match their names more often, e.g. Stonefish is Water/Ground, Flying Fish and Parrotfish are Water/Flying, Clownfish is Water/Fairy and Oarfish is Water/Dragon. FishMon you already have update too
- Fixed: Sengarat was being treated as a Gar (Water/Steel)

## 1.3.0
- Presets: **Normal**, **Challenge** and **Custom** at the top of the Mod Menu. Normal and Challenge set the balance for you, and boss, wild creature and catch levels now go up island by island (main bosses Lv. 10 on the first island to Lv. 50 on the last). Custom lets you change everything, and keeps your values while you try a preset
- Boss badges: on the Challenge preset, beat a boss with a team at or under its level (Bronze), 80% of it (Silver) or 60% (Gold). Badge points unlock perks like +10% EXP, half-price Heal All and an extra party slot. See them in **Fishdex → Badges**
- Ocean species: 10 brand new FishMon you can catch, each with its own model, types and evolutions: **Coralit**, **Sparkfin**, **Inkling**, **Urchip**, **Driftray**, **Tidefang**, **Frostfin**, **Wavelet**, **Hullback** and the legendary **Leviathorn**
- Each ocean species bites on certain baits and islands only. Its Fishdex entry tells you where to find it (e.g. "Islands 3–5 · Professional Lure, Scientific Lure")
- **Hullback** is huge (about 4.5 m), hostile and has a lot of HP. Bring your party
- New **Custom species** section in the Mod Menu: turn the ocean species on or off, change how often they bite and how bright they are, and a test button to make the next bite a chosen species. Everyone in a lobby needs the same setup
- For modders: other mods can add their own FishMon species with their own models, types, stats, evolutions, islands and baits (`HowToFishMon.Api.FishMonAPI`). Adapted from MoreFishAPI by hiccup444, and works alongside it
- Fixed: the Fishdex count now updates when new species are added

## 1.2.0
- Catch difficulty: rarer and higher-level fish can break free when you pick them up (one try per fish, you keep it as a normal fish). The "hooked!" message shows the catch chance. Adjust or turn it off with **Catch difficulty** in the Mod Menu
- Fishdex rewards: money for every new species you catch, plus milestones (5 and 10 species, then 25 / 50 / 75 / 100% of the Fishdex) with money and held items. Claim them from **Fishdex → Rewards**

## 1.1.1
- Battles: challenge another player from the new **Players** tab. Your FishMon fight it out halfway between you while you walk around and watch. HP and items go back to normal afterwards, and both FishMon get EXP (both players and the host need this version)
- The **Trade** tab is now the **Players** tab, with both trading and battling
- The FishMon info box (HP, EXP, controls) moved to the top right so it no longer covers the game's money, health and hunger

## 1.1.0
- Abilities: every FishMon rolls an ability (Common to Legendary). Switch it off or reroll it in the party menu
- First ability: **Merchant** picks up fish you drop and sells them for you
- All the other abilities work now: **Scholar**, **Thick Scales**, **Speedster**, **Guard**, **Angler**, **Berserker**, **Mentor**, **Medic**, **Treasure Hunter**, **Leech**, **Haggler** and **Lucky Charm**
- FishMon now roam around you, mostly in front of your camera, instead of trailing behind you. Toggle it and set the distance in the Mod Menu (on by default, 7 m)
- Rebind every FishMon key from the Mod Menu (new **Controls** section), with swapping and a warning when a key is also one of the game's keys
- Key names in messages are easier to read (e.g. "1" instead of "Alpha1")
- Status conditions: Burn, Poison, Paralysis, Sleep and Freeze. Moves can inflict them on foes, and bosses can inflict them on your FishMon
- Other players now see status conditions on your FishMon and on what it's fighting
- Trading: swap FishMon with other players from the new **Trade** tab in the party menu (both players and the host need this version)
- Held items: buy items like Lucky Egg, Leftovers, Choice Band and Sitrus Berry, and give one to each FishMon from the new **Item** tab
- Fresher look for the menus: rounded panels and buttons, soft shadows, a header bar, and the game is dimmed behind open menus
- Fixed: pressing Esc in a FishMon menu also opened the game's pause menu behind it (with its hover sounds)

## 1.0.0
First release!
- Catch fish with your rod and turn them into FishMon with stats, IVs, natures, types and moves
- Party, Box, Fishdex with 3D models, evolutions
- FishMon fight bosses and hostile creatures with you and deal real damage
- Spread moves that hit every foe nearby (great vs piranha swarms)
- Multiplayer: see other players' FishMon (the host needs the mod)
- Keep your FishMon across worlds, or start fresh in a new world
- Works with the latest How to Fish update
