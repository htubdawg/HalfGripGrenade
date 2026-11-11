# HalfGripGrenade

A Half Grip (Steam app 4163270, Unity, Windows) mod: hand grenades as a gun item. Fitted, no gun hangs by the visor; up to three
grenades sit on the dash where the ammo goes. Pull a pin with the cockpit hand (arms it, the spoon pops off, the fuse burns), grab the
grenade and flick it out of the car. Current release: see `Grenade/mod.json`.

## Always be ready for a context clear

This file is the next session's only memory. After every step that changes the state of things: update Status in the same commit,
commit and push to the branch, and write down what was learned the hard way under *Game facts* and *Owner's wishes*. A fresh session
starts by reading this file, `git log --oneline -10`, `git status`, and the files named in Status.

## How the mod runs

- A **source mod** for [HalfGripTools](https://github.com/4l3x4nd3r0/HalfGripTools) (BepInEx 5): the loader compiles `Grenade/src/*.cs`
  at game start (**C# 10**, netstandard2.1). What ships is the `Grenade/` folder (`mod.json`, `icon.png`, `src/` only), installed to
  `<game>\BepInEx\HalfGripMods\Grenade\`. Loader API: HalfGripTools' `docs/WRITING_MODS.md`, `docs/GAME_NOTES.md`. Game DLLs and scene
  dumps: private `htubdawg/HalfGripRefs`. Siblings for reference: `htubdawg/HalfGripStereo`, `htubdawg/HalfGripMiniMap` (dash sprites,
  lit material copies), `4l3x4nd3r0/HalfGripMods` `mods/RPG` (projectile and blast; this mod does its own).

## Layout of src/

| File | Role |
|---|---|
| `Mod.cs` | Config, registers the item, the persistent `Fitter` object; `Info` (verbose-only log); `ThrowSettings()` |
| `GrenadeItem.cs` | The item `grenade.frag` (saved in runs: never change it): a clone of the revolver's `GunUpgrade` with `gunType = NONE`, RaceSpec, `magazines` = grenades, ammo words "grenade(s)", drawn icon |
| `Cockpit.cs` | `Fitter` (twice a second: equipped and no rack → build; not equipped → remove) and `Cockpit`: measures the revolver's first magazine (place, cockpit pixel, lit material, sorting), builds `GrenadeRack` (a game `MagazineManager`) with the grenades made inactive first so the rack adopts them; sprites and per-texture lit material copies |
| `DashGrenade.cs` | `NAmmo` subclass: magazine feel (values from the 0.419 dump), `Arm()` (spoon `Bit`, ping, hiss), fuse, flick sampling of `Grabber.cursor` (last ~0.09 s, unscaled), release → `Thrown.TryThrow` or a drop; footwell below `Cockpit.FootwellY`; goes off in the car at 0 |
| `Pin.cs` | `FallingObject`, stuck in the fuse head (`stuckSpring` 2, `breakOffForce` = `PinPull`); break-off arms the grenade; once let go it is reparented to the rack and falls away |
| `Throw.cs` | Pure arithmetic: flick (screen heights/s) + pointing angle → velocity in the car's axes. `tools/preview` prints a table |
| `Thrown.cs` | The world grenade: own ballistics with sphere casts (bounce, scrape, rolling friction, rides moving cars' point velocity), crossed voxel meshes of the body art, 3D hiss; `PlayerCar()` |
| `Blast.cs` | `InWorld` (falloff damage with `LapShield.Scale`, kill credit, shove, `DestructibleObject`s, self damage, shake) and `InCockpit` (damage, shake, flash/smoke `Bit`s, text) |
| `Effects.cs` | Borrowed: enemy cars' `bigExplosionEffect` and `destroySound`, the SMG's `slidePullSounds` (pin); synthesized ping, hiss, bounce, boom; a small fallback explosion; the unlit sprite material |
| `Bit.cs` | Unhandled cockpit pieces: the flying spoon, fuse smoke pixels, in-car flash and smoke |
| `Net.cs` | Multiplayer. `GrenadeNet`: `ctx.Net` messages (our grenades in flight 15/s unreliable, blasts reliable, in-car blasts too), `AsExplosion` (sets Multiplayer's `Combat.Current` to Explosion by reflection around `Blast.InWorld`). `Ghost`: another player's grenade, model + hiss, follows reports, no damage; a received blast shoves this machine's own bodies (`Blast.ShoveFromAfar`) |
| `Art.cs` | Pixel art (managed only, `tools/preview` draws it): body, spoon, pin on one 22x20 canvas; flash; smoke colours |

## Design (from the owner's brief, 0.1.0)

- A weapon like the guns (gun slot) but **not in the visor area**; up to **3 grenades where the ammo goes** on the dash.
- **Pull the pin** by holding LMB on it and pulling it out; that arms it, the **spoon pops off**. **No holding the spoon**: the fuse burns.
- Throwing simulates 3D with the 2D screen: **vertical motion = forward/back**, lateral stays lateral for aiming (owner asked for
  thoughts). Built as: release speed of the flick = throw strength, its up/down sign = ahead/back, and the line from the car through
  the hand at release steers it (pointing), plus a weaker sideways component from a sideways flick. Too slow = a drop in the car.
- **80 damage.** The RPG mod was a reference only.

## Game facts

- `GunType` is NONE, SHOTGUN, REVOLVER, SMG_RIFLE, SAWED_OFF. `GunLoadoutActivator` activates the gun whose `RequiredGunType` equals
  `PlayerStats.gun` and switches every other gun (and its rack, a child) off; a card with `gunType = NONE` therefore shows no gun.
  Nothing in 0.419 tests `is GunUpgrade` or reads `gunType` beyond `Gun.Fitted` and `PlayerStats.gun`.
- Gun assemblies hang high (RevolverAssembly at (-2.36, 7.84, 1.31) in CarInterior); their racks (`MagazineHolder`) reach down to the
  dash: the revolver's magazines at about (6.69, 1.74, 0.49), scale 0.4; the shotgun's shells at x 5.1 to 6.5.
- `MagazineManager` adopts every `NAmmo` under it in `Awake` (and copies the first as the template for later spawns), handles
  `AmmoPickedUp`, `EnemyKilled` (`magazinesPerKill`), `Stat.ExtraMagazines`, the ceasefire padlock (`AmmoLockSwitch.Instance`),
  releases near a slot, recovery of ammo that fell below `killHeight`. `Take(item)` removes and destroys. `busy` items are skipped by
  release-catching and their slot counts as free. `Stat.HpPerShot < 0` ("blood fed") empties the rack.
- Magazines (revolver, 0.419 dump): direct mode, gravity (0, -90.81), soft angle lock, drag tilt 0.1, stuck with spring 100 and
  break-off 1 (direct mode: pull distance × `handSpringForce`).
- The Grabber picks the hovered `InteriorCollider` with the highest `sortingLayer`; `IsPointOn` tolerance is `hitBoxExpand ×
  GrabTolerance (1.5)`. On release it moves `cursor` toward the grip point before calling `Release`, so the flick is sampled before.
- `AreaBlast.Apply` exists (barrels, enemy rockets) but always hurts the player and skips `LapShield`; not used.
- **Multiplayer** (4l3x4nd3r0/HalfGripMods `mods/Multiplayer` 1.1.0): every other player's car (and the host's AI cars on a client)
  is a kinematic stand-in `EnemyController` + `RemoteCar` on this machine, in shared world coordinates. Multiplayer patches
  `EnemyController.TakeDamage(float, bool)` (the 4-argument overload calls it): a `byPlayer` hit on a human stand-in is multiplied by
  the host's PvP setting for `Combat.Current` (Gun / Explosion / anything else = Item) and sent (`Msg.Hit`) to its owner, who applies it
  with `TakeCrashDamage`; AI stand-ins go to the host (`Msg.AiHit`). Stand-ins never die from damage, only by the owner's word; kills
  credit through the game's own path. Stand-ins are kinematic, so pushing one does nothing; push the owner's real car on the owner's
  machine (a message) and the movement replicates. `LapShield.Scale` is bypassed for humans. So **damage is the thrower's machine's job**, nothing
  extra to send. Multiplayer shows the RPG's rockets by hard-coded reflection (`Cues.cs`); other mods get `ctx.Net` (HalfGripTools
  1.0.6 `Net.cs`, channel = mod id, everybody in a room has the same mods). The Grenade is the first mod to use `ctx.Net`.
- Cars are about 13 units long; top speeds near 100 units/s. The cockpit is drawn by `InteriorCamera`; the world by `CarControllerNew.fpvCamera`.

## Checking changes

Repos side by side in `/home/user`: `HalfGripRefs`, `HalfGripTools`.

- Compile (before every push): build HalfGripTools against the refs, then `tools/check.sh <HalfGripRefs>/game <HalfGripTools.dll>`.
  Where building HalfGripTools is refused (it was in the 0.1.0 session), `tools/check.sh <HalfGripRefs>/game --stub` compiles against
  `tools/stub/HalfGripTools.cs` (the members the mod uses, signatures from HalfGripTools 1.0.6); keep it in step when the mod uses more.
- Pictures and throw table: `dotnet run -c Release --project tools/preview -p:Refs=<HalfGripRefs> -- <out dir>` writes the parts at
  8x, `rack-4x.png` (copy to `docs/rack.png`), `icon.png` (copy to `Grenade/icon.png`), and prints throws with their range at rest.
- Game code: `DOTNET_ROLL_FORWARD=Major ilspycmd -p -o <dir> <refs>/game/HalfGrip_Data/Managed/Assembly-CSharp.dll`.
- In game (the owner tests): copy `Grenade/` to `<game>\BepInEx\HalfGripMods\`, `Debug.VerboseLog=true`, `giveitem grenade` with
  cheats, read `BepInEx\LogOutput.log` (`made the item`, `grenades on the dash`, `pin pulled`, `thrown`, `went off`).

## Status (keep current)

- **0.1.0** (branch `claude/sharp-knuth-bi1hb5`, first version, not released, **not run in the game yet**). Checked: compiles against
  the 0.419 refs with the stub HalfGripTools (0 warnings); art and throw table from `tools/preview`.
- **Untested guesses to check first in game:** the rack's place and the grenades' size next to the dash; the pin's tug (PinPull 0.5);
  the throw's feel (SpeedPerFlick 11, MinThrowSpeed 7, ranges in the preview table); Gravity 30 against the world's scale; whether the
  thrown grenade's sphere casts hit the road (layer mask) and its unlit voxel model shows; the borrowed explosion's size; that ammo
  boxes refill grenades; that the padlock sits on the grenades; the shop card's text and icon size.
- **0.2.0** (branch `claude/vigilant-wozniak-8p3p78`, on top of 0.1.0): multiplayer, `Net.cs`. Compiles with the stub (Net/ModNet
  added to it from HalfGripTools 1.0.6). **Not run in a room yet.** To check with two players: both see the other's grenade fly and
  hiss (`player N threw one`), the blast where it lands (`went off`), a hit lands on the other's car with the host's ExplosionDamage
  (Multiplayer logs `Hit by <name>: N damage`), a grenade in the thrower's car shows as a blast at their car for the others.
- **Shove in multiplayer:** the blast message carries the thrower's radii; every receiving machine runs `Blast.ShoveFromAfar`, pushing
  the non-kinematic bodies it simulates (its own car, the host's real AI cars, loose things), and Multiplayer replicates where they
  went. Skipped for in-car blasts and when the host set ExplosionDamage to 0 (`Room.Race.Explosion`, by reflection).
- **Not handled yet:** "blood fed" (HpPerShot) leaves no grenades. Each thrower's own config (Damage, radii) decides their blasts.
- **Next step:** the owner tests in a race (0.1.0 single player, then 0.2.0 in a Multiplayer room) and reports (screenshot of the dash, the log with VerboseLog on, how the throw feels).
