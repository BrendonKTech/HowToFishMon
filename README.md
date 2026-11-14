# FishMon — I made catching fish into Pokemon

Catch a fish, beat it up and then you can use it to fight enemies and potentially other players...

# FishMon

Ever wanted to have your own fish fight alongside you? Well, now you can!

FishMon adds a Pokemon-like system to the game where you can catch fish, level them up, evolve them, and have them fight alongside you against bosses and other hostile creatures.

There are a LOT of features included, from catching and evolving fish to a full party system, different moves, stats, and even multiplayer support!

## How To Use FishMon

| Feature | How it works |
|---|---|
| **Catching FishMon** | The catching mechanic is pretty simple. You play the game normally, catch a fish using your own fishing rod, and then beat it up until it's no longer flopping around. Once it's dead, you can pick it up with **E** to add it to your collection! You'll get a little "Gotcha!" message showing its level, IVs, and nature. Fish you buy or find won't count unless you change `OnlyCatchHooked = false` in the config. |
| **F6 - Main Menu** | Pressing F6 opens the main menu where you'll find your party, Fishdex, moves, and other features. You can check your FishMon's stats, change your party around, rename them, heal them, and more. |
| **F7 - Send Out / Recall** | This lets you throw out or recall your currently selected FishMon. It'll follow you around and even swim when you're in the water! |
| **F8 - Switch FishMon** | Using F8 switches to your next FishMon in order from top to bottom. It'll automatically skip any FishMon that have fainted. |
| **F9 - Settings Menu** | I added a settings menu that lets you mess around with a LOT of the mod's features. You can change things like HP, damage, EXP rates, boss levels, and even the size of your FishMon. Want a massive fish following you around? You can do that too! |
| **G - Attack** | Look at a creature and press G to tell your FishMon to attack it. You'll see a red target marker appear over whatever you're looking at. Press G again while looking at nothing to call it back. |
| **H - Stay / Follow** | Press H to make your FishMon stay in one place. It'll still attack anything you specifically target with G. Press H again to have it follow you. |
| **Automatic Fighting** | You don't actually have to do anything for this one! Your FishMon will automatically attack bosses when they're nearby, or hostile creatures that get too close. Just fight alongside your little buddy! |

## FishMon Stats

Every FishMon has its own stats, and depending on what you catch, some can be a LOT stronger than others.

Each FishMon has:

- **Base Stats:** HP, Attack, Defense, Special Attack, Special Defense, and Speed. These are based on the fish species. Generally, more expensive and tougher fish have higher stats, while lighter fish tend to be faster.
- **IVs:** These are randomly generated when you catch a FishMon. Each stat gets a value between 0 and 31. Drip (shiny) fish always have 3 perfect IVs and get a special ★.
- **Nature:** There are 25 different natures. Each one increases a stat by 10% while lowering another by 10%.
- **EVs:** These are earned by defeating bosses and help improve your FishMon.
- **Types:** Every FishMon gets a type based on its species. For example, Eels are Electric, Sharks are Dark, Pufferfish are Poison, and Seahorses are Dragon. There's also a full type effectiveness system with all 18 types!
- **Moves:** Your FishMon learns new moves as it levels up. By default, it'll automatically keep its 4 strongest moves.

## Evolution

Every FishMon can evolve twice, meaning you'll eventually have some pretty crazy-looking fish following you around.

Here are a few examples of evolution chains:

| Caught as | First Evolution | Final Evolution |
|---|---|---|
| Shrimp | Prawn (Lv 20) | Lobster (Lv 40, gains Fighting type) |
| Crab | King Crab (Lv 25) | Titan Crab (Lv 45, gains Steel type) |
| Cod | Big Cod (Lv 20) | The Codfather (Lv 40, gains Dark type) |
| Mackerel | Mackerelite (Lv 22) | Mackerel Monarch (Lv 42) |
| Shark | Great Shark (Lv 30) | Megalodon (Lv 50, gains Dragon type) |
| Eel | Volt Eel (Lv 25) | Thunder Eel (Lv 45) |
| Seahorse | Sea Dragon (Lv 30) | Leviathan (Lv 50) |
| Everything Else | Big ___ (Lv 25) | Mega ___ (Lv 45) |

Evolving your FishMon increases all of its base stats by around 20% and makes its model about 30% bigger.

Fully evolved FishMon also get a glow effect based on their type, which makes them stand out a little more.

Don't worry about nicknames either! They'll carry over when your FishMon evolves.

If you catch a FishMon that's already past its evolution level, it'll evolve the next time you send it out. Higher-level catches will also come already evolved.

## Fighting With Your FishMon

Here's how the combat works:

- **Automatic Attacks:** Your FishMon will automatically attack bosses within 60 meters. If there isn't a boss nearby, it'll attack hostile creatures like crabs and piranhas within 20 meters.
- **Move Selection:** Every time your FishMon attacks, it'll use one of its learned moves. It usually picks whichever move has the best type matchup against the enemy.
- **Real Boss Damage:** The damage your FishMon deals actually affects the boss's real health bar! You'll also see the normal hitmarkers, damage numbers, and kill score.
- **Attack Speed:** The higher your FishMon's Speed stat, the more frequently it'll attack.
- **Attack Effects:** Every move has its own effects based on its type. Water moves splash, Fire moves spit lava, Electric moves call down lightning, Poison moves leave clouds, and so on. Strong attacks and critical hits also get bigger effects. You can turn these off using `AttackEffects` in the config.
- **Bosses Fight Back:** Bosses have their own levels, types, and moves. They'll actually hit your FishMon back, knocking them around and taking away their HP.
- **EXP and EVs:** Your FishMon gains EXP every time it lands a hit. Once a boss is defeated, every FishMon that participated gets a bonus and earns EVs.
- **Automatic Switching:** If your active FishMon faints, the next healthy one in your party will automatically come out.
- **HP Display:** Your FishMon has a nameplate with its HP bar, and there's also a panel in the bottom-left corner showing its HP, EXP, and current action.

Your lead FishMon also gets a little EXP boost every time you catch a fish!

### Healing and Reviving

Your FishMon can take quite a beating, so you'll need to keep an eye on their HP.

- **Natural Healing:** Injured FishMon recover 10% HP every minute.
- **Heal All:** For $25, you can heal all your injured FishMon through the F6 menu.
- **Revive:** If one of your FishMon faints, it'll stay fainted until you revive it. Reviving costs $100 and brings it back with 50% HP.

One thing to keep in mind: Heal All won't revive fainted FishMon. You'll have to use the Revive option separately.

If you're playing as a guest in someone else's game, revive payments go through the bait shop, and you'll also receive 1 cheap bait each time.

## Multiplayer

FishMon also supports multiplayer!

If other players have the mod installed, you'll actually be able to see their FishMon running around in the world.

You'll see:
- Their FishMon model.
- The owner's name above it.
- Their level and current HP.
- Their attacks, move names, trails, and effects.

There are a couple of things to know:

- **The host MUST have the mod installed.** Multiplayer messages go through the host, so without it, other players won't see anyone else's FishMon.
- Players who don't have the mod installed won't be affected. They simply won't see any FishMon.
- Other players' FishMon are just copies on your screen. You can't attack or control someone else's FishMon.
- All combat happens on the owner's game, but damage dealt to bosses still counts for everyone.

## Moves

Your FishMon can learn moves as they level up.

Every FishMon can learn all moves associated with its own types, along with Normal-type moves, as long as it reaches the required level.

By default, your FishMon automatically chooses its 4 strongest moves. However, you can change these yourself!

To do this:

1. Press **F6** to open the menu.
2. Select the FishMon you want to change.
3. Go to the **Moves** tab.
4. Click the move slot you want to change.
5. Pick a move from the list.

You can also use **Back to Auto** if you want your FishMon to go back to choosing its own best 4 moves.

Once you've manually selected moves, the game won't automatically replace them anymore. Instead, you'll get a notification whenever a new move becomes available so you can decide whether you want to switch it out.

One more thing! Moves that match your FishMon's type get a **50% damage bonus**, so keep that in mind when building your team.

Your FishMon will still automatically choose which of its selected moves to use during combat based on the enemy's type.

## Fishdex

I also added a full Fishdex system!

The Fishdex contains every creature found in the game's journal, including their stats, evolutions, and moves.

You can access it by pressing **F6** and opening the Fishdex tab.

Each entry has a few different states:

- **???** - You haven't discovered this creature yet.
- **Seen** - You've been close to it, hooked it, or fought it. Creatures you've already killed in the game's normal journal will also count.
- **Caught** - You've successfully caught this FishMon!

Once you've caught a creature, you'll unlock even more information about it, including:

- Base stats.
- Full list of learnable moves.
- Evolution chain.
- Number of times you've caught it.
- Your best IVs.
- Highest level you've caught.
- Whether you've caught a Drip (shiny) version.

You can also view a spinning 3D model of each creature! Just drag it around to get a better look.

Bosses are included at the very end of the Fishdex, along with how many times you've defeated each one.

## Settings / Config

The Mod Menu can be opened by pressing **F9**, or by clicking the Settings button inside the F6 menu.

There's quite a bit you can change here, including:

- EXP rate.
- FishMon HP.
- Damage dealt and received.
- Attack speed.
- Boss and creature levels.
- Detection ranges.
- Healing and revive costs.
- Catch levels.
- Party size.
- Attack effects.

FishMon HP is set to **2x by default**. If your FishMon are getting knocked out too quickly, you can increase their HP or lower the Damage Taken setting.

You can also manually edit the config file here:

`BepInEx/config/com.howtofish.fishmon.cfg`

Your FishMon data is saved separately here:

`BepInEx/config/FishMon_save.json`

By default your FishMon are shared between all your worlds. The first time you load a world you're hosting, you'll be asked if you want to **bring your FishMon** or **start fresh**. Starting fresh gives that world its own party, Box and Fishdex, saved in `BepInEx/config/FishMon_worlds/`. You only get asked once per world. If you want to be asked again, delete that world's line from `FishMon_worlds/choices.json`.

When you join someone else's world, you always bring your shared FishMon.

## Building the Mod

If you want to build the mod yourself, you'll need the .NET SDK (version 6 or higher).

Run the following command:

```bash
dotnet build -c Release
```

The build should automatically copy `HowToFishMon.dll` into:

`<game>/BepInEx/plugins/HowToFishMon/`

If your game is installed somewhere else, you may need to change `GameDir` inside `HowToFishMod.csproj`.

## Project Files

Here's a quick breakdown of what each file does:

- `src/Plugin.cs` - Main BepInEx entry point and configuration.
- `src/Patches.cs` - Handles catching when picking up fish, input blocking, and cursor behavior.
- `src/FishMon.cs` - FishMon data, stats, and species/type generation.
- `src/Companion.cs` - Handles your FishMon in the world, including following and attacking bosses.
- `src/Combat.cs` - Handles combat calculations, boss stats, and damage.
- `src/FishMonController.cs` - Handles hotkeys, switching, healing, menus, and HUD elements.
- `src/ModMenu.cs` - The in-game settings menu.
- `src/ModelPreview.cs` - Handles the 3D models in the Fishdex.
- `src/Fishdex.cs` - Handles the Fishdex and seen/caught records.
- `src/Net.cs` - Handles multiplayer communication.
- `src/RemoteCompanion.cs` - Handles displaying other players' FishMon.
- `src/Fx.cs` - Handles attack effects, trails, sounds, and other visual effects.
- `src/Styles.cs` - UI drawing helpers.
- `src/Data/` - Contains the type chart, moves, natures, and evolution chains.

---

README brought to you by Claude because I hate writing README's lol