# Changelog

## 1.1.1

- **Support for ground vehicles added by other mods** (tested with Aryx Weapons Pack and MC-260 Chimera): mod-added
  trucks, APCs, artillery and launchers get the same playable treatment — own running gear with ride height matched
  to the game AI, the fire control computer (B), working fold / unfold of vertical-launch launcher arms, and correct
  radar-link behavior for radar-dependent launchers. Compatibility with mods other than these two still needs
  verification.
- The R9 Stratolance and RAM-45 missiles get their own images (in the depot purchase panel and in the vehicle
  weapon list) instead of the game's generic missile icon.
- Fixed: parked vehicles slowly creeping or sliding on slopes and when standing still (issue #8).
- Fixed: LCV45 Recon Truck right-side wheels did not steer.

## 1.1.0

- **Own running gear for vehicles:** wheels and road wheels with suspension (progressive bump stop, separate bump
  and rebound damping, anti-roll bars), tyre grip by surface, Ackermann steering, tracks with pivot turns, engine
  and gearbox from real prototypes. Realistic cannon recoil.
- **New ground camera**, armour zones and internal modules, instant cannon shots for client drivers.
- **Weapon wheel and radial menu in vehicles**, the game's own: hold Weapon Wheel to pick a weapon station (with
  ammo), hold Radial Menu for vehicle actions — exit, deploy / stow (hitch, load), fire control computer, night
  vision. Works with a gamepad, mouse or keys, as set in the game's options.
- **Drive vehicle** in the aircraft radial menu: gets you into the selected vehicle or opens your depot's menu, like F7.
- **Gamepad in the vehicle purchase menu:** left stick picks a vehicle, LB / RB switch tabs, X switches to the next
  depot, A deploys and confirms, B goes back (the game's default gamepad bindings for Move, Throttle, Target Select,
  Fire and Target Cancel).
- **Clearer barrel marker** (where the shell will land): yellow with "BLOCKED 340m" when a hill, tree or building is
  in the way, red with "OUT OF REACH" when the shell won't get there, and the distance to the impact point when you
  aim at the sky.
- **Projectile camera (V):** press once to arm — the next shell, bullet or missile fired from your vehicle takes the
  camera (the first round of a burst), press again to cancel. Shells aren't game objects, so the camera flies the
  same ballistic model the game simulates; missiles are followed for real. The camera returns to the vehicle when the
  projectile's flight ends. While watching, firing and turret aiming by the camera are suppressed; the vehicle HUD
  hides (only a small hint panel with the exit key and missile warnings stay). The camera rides right behind the
  projectile with the view locked to the trajectory (free look is disabled while watching).
- Fixed: while you drove a vehicle, the radial menu still worked on your parked aircraft, so it could eject the
  pilot or shut down the engine.
- **Towing trailers fixed.** Trailers now ride on the same per-wheel suspension and tyre physics as driven wheeled
  vehicles, with a low centre of mass, instead of a single plane under the chassis — no more swaying, tipping over
  or pushing the tractor sideways (jackknifing). The hitch is a damped spring coupling like a real tow ball instead
  of a rigid joint, so trailer tug is absorbed rather than thrown at the tractor. Support legs fold away while the
  trailer is hitched and come back out on unhitching, and trailers can be hitched on factory ramps instead of only
  on perfectly flat ground.
- Fixed: in Free Flight no vehicles could be bought at all. Those missions disable faction donations, and the shop
  treated that as "purchasing disabled", while the game's own aircraft requisition at airbases ignores that flag.
  Buying at your own airbases now follows the aircraft rules; the donation restriction still applies to vehicle
  depots, like the vanilla convoy button.
- Fixed: loaded containers and towed trailers stayed combat-ready — the launcher kept tracking and firing through
  the truck bed and the radar kept spinning its antenna. Cargo and the hitch now stow the load's weapons and radar
  (like a helicopter sling load) and un-stow them when it deploys after unloading / unhitching. The launcher is
  raised back to its firing pose, so a towed SAM trailer no longer stays folded and shoots into the ground.
  Synced to all players in multiplayer.
- **Fifth-wheel hitching.** Trailers now sit on a saddle over the tractor's rear axle like a real semi (was: tow ball
  at the bumper), so the trailer rests on the tractor instead of pushing it sideways.
- **Multiplayer protocol 10: the host and everyone who drives must update together.** The host now steers
  client-driven vehicles' barrels parallel to the driver's camera ray (stabilizer-style aiming), same as the local
  driver - before, remote clients' barrels chased the terrain point and visibly lagged behind the crosshair when
  driving. Server-side rate limits on driver input, deploy, towing and purchase requests, mirroring the game's own
  RPC rate limiting. The stowed state of loaded containers and towed trailers is synced to all players.
- **Host controls:** a master switch `[Server] Enabled` turns all vehicle features off (no entering, buying or
  selling, the GROUND spawn mode is hidden), and `VehicleAccess` now defaults to `OwnVehicles` — players drive the
  vehicles they bought; the host sets `Open` to allow any faction vehicle.

## 1.0.3

- **Multiplayer protocol 6: the host and everyone who drives must update together.**
- **Buy vehicles at your own airbases**, not only at vehicle depots. On the spawn screen in GROUND mode your ground
  airbases are shown and can be selected, F7 works with an airbase selected on the map, and the arrows in the shop
  cycle through airbases and depots. The vehicle appears on free ground near the control tower, away from runways and
  taxiways. Carriers are not purchase points.
- **Sell vehicles you bought.** Bring the vehicle to a friendly depot (within 200 m) or airbase (within 800 m of the
  tower), stop and get out, open the shop there and press SELL VEHICLE: the shop switches to your vehicles at that
  point, the camera shows the selected one, and SELL returns money. You get back the purchase price reduced by hull
  damage and spent ammunition; an undamaged vehicle with full ammo returns the full price.
- **Target lock range** now matches the vehicle's longest-range weapon (at least 10 km), so long-range SAMs can lock
  targets as far as their missiles reach.
- Compatibility with the game update of 2026-10-28 (map marker colours of your own vehicles).

## 1.0.2

- **Multiplayer (protocol 5, host and clients must update together)**: the host now measures each driver's ping and
  places their vehicle ahead by the delivery delay, so AI aims at and hits where the vehicle really is on the
  driver's screen. When the host registers a bullet hit on a client's vehicle, the driver sees the impact effect at
  that spot even if their local copy of the bullet missed.
- **Multiplayer: weapon panel and crosshair for clients.** The host now sends the driver the real state of their
  weapons: rounds loaded, total ammo and reloading. Before, a client saw a full magazine that never emptied, ammo
  draining from the reserve and no reload bar.
- **Multiplayer: clients rearm at bunkers, ammo containers and supply trucks.** The host takes the vehicle's height
  above the ground from the client's own physics, so a stopped client vehicle counts as standing on the ground.
- **Laser point defence** (the Spearhead MBT's secondary laser turret) shoots down incoming missiles on its own, like
  on AI vehicles, until you select its weapon station.

## 1.0.0

First public release.

- **Drive your faction's ground vehicles** in third person: tanks, IFVs, trucks, air defence, artillery and missile
  launchers. Driving uses a port of the game's own AI vehicle physics; turrets aim with the mouse, weapon stations
  switch with the game's weapon keys.
- **Buy vehicles at vehicle depots** at the game's prices, from the map (F7) or from the GROUND mode of the spawn
  screen, with a menu styled like the aircraft selection menu. Nuclear launchers follow the game's escalation and
  rank rules.
- **HUD built from the game's own HUD elements**: compass, speed and range boxes, turret crosshair with reload ring,
  weapon panels, hull integrity, target lock, missile warning with alarms, hit direction, minimap.
- **Artillery and missiles**: fire control computer with target selection on the map, direct and indirect fire,
  ballistic and air-defence missiles with the game's radar link rules.
- **Fits into the campaign**: capture airbases, resupply vehicles with a supply truck for the game's reward, earn
  money and rank for kills, get credit for kills by installations you bought.
- **Towing and cargo**: tow wheeled launchers with a tractor, carry containers on a flatbed truck.
- **Multiplayer**: clients drive without input lag, the host validates their input; the host and everyone who drives
  need the mod (same version). Server option to let players drive only their own vehicles.
- Configurable keys, dead zones, driving model, camera and camera shake in
  `BepInEx\config\PlayableVehicles.cfg`.
