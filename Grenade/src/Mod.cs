using BepInEx.Configuration;
using BepInEx.Logging;
using HalfGripTools;
using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// Hand grenades as a gun item. Fitted, they take the gun's place: no gun hangs by the visor, and up to three grenades sit on the dash
	/// where the ammo goes. Pull a pin with the cockpit hand (the spoon flies off and the fuse is lit), then grab that grenade and flick
	/// it out of the car: up throws it ahead, down tosses it back, and it goes the way the hand points (see <see cref="Throw"/>). It
	/// bounces, rolls and goes off when the fuse runs out, wherever it is, in the car included.
	/// </summary>
	public class Mod : HalfGripMod
	{
		internal static ManualLogSource Log;

		internal static ConfigEntry<float> Damage;
		internal static ConfigEntry<float> FullDamageRadius;
		internal static ConfigEntry<float> BlastRadius;
		internal static ConfigEntry<float> FuseSeconds;
		internal static ConfigEntry<bool> SelfDamage;
		internal static ConfigEntry<float> CockpitDamage;
		internal static ConfigEntry<int> Grenades;
		internal static ConfigEntry<int> Cost;
		internal static ConfigEntry<float> SpeedPerFlick;
		internal static ConfigEntry<float> MaxThrowSpeed;
		internal static ConfigEntry<float> MinThrowSpeed;
		internal static ConfigEntry<float> Loft;
		internal static ConfigEntry<float> Gravity;
		internal static ConfigEntry<float> PinPull;
		internal static ConfigEntry<float> RackX;
		internal static ConfigEntry<float> RackY;
		internal static ConfigEntry<bool> Verbose;
		internal static ConfigEntry<int> ConfigVersion;

		/// <summary>The config's layout now; <see cref="Migrate"/> brings an older file up to it once.</summary>
		private const int CurrentConfig = 1;

		private static GameObject fitter;

		/// <summary>A detail line for the log, only written with Debug.VerboseLog on. Warnings and errors are always written.</summary>
		internal static void Info(string text)
		{
			if (Verbose != null && Verbose.Value)
			{
				Log.LogInfo(text);
			}
		}

		/// <summary>The throw's numbers from the config, read afresh for each throw so they can be tuned between races.</summary>
		internal static Throw.Settings ThrowSettings()
		{
			return new Throw.Settings
			{
				SpeedPerFlick = SpeedPerFlick.Value,
				MinSpeed = MinThrowSpeed.Value,
				MaxSpeed = MaxThrowSpeed.Value,
				LoftDegrees = Loft.Value,
				Gravity = Gravity.Value,
			};
		}

		public override void Load(ModContext ctx)
		{
			Log = ctx.Log;
			// Distances and speeds are in the game's own units: a car is about 13 long, and the world is about three times life size.
			Damage = ctx.Config.Bind("Grenade", "Damage", 80f, "Damage to a car close to the blast. It falls off to nothing at BlastRadius.");
			FullDamageRadius = ctx.Config.Bind("Grenade", "FullDamageRadius", 13f, "Cars this close to the blast (game units, a car is about 13 long) take the full Damage.");
			BlastRadius = ctx.Config.Bind("Grenade", "BlastRadius", 32f, "Cars further from the blast than this take nothing. In between, the damage falls off evenly.");
			FuseSeconds = ctx.Config.Bind("Grenade", "FuseSeconds", 3.5f, new ConfigDescription("Seconds from pulling the pin to the blast. There is no holding the spoon down: once the pin is out, the fuse burns.", new AcceptableValueRange<float>(1f, 10f)));
			SelfDamage = ctx.Config.Bind("Grenade", "SelfDamage", true, "A thrown grenade also hurts your own car if it goes off close to you.");
			CockpitDamage = ctx.Config.Bind("Grenade", "CockpitDamage", 80f, "Damage to your car when a grenade goes off inside it (held too long, dropped in the footwell, or left on the dash with its pin out). 0: none.");
			Grenades = ctx.Config.Bind("Grenade", "Grenades", 3, new ConfigDescription("Grenades on the dash at the start of a race. Ammo boxes top them up to this.", new AcceptableValueRange<int>(1, 3)));
			Cost = ctx.Config.Bind("Grenade", "Cost", 40, "The item's price in the shop. Takes effect the next time the game starts.");
			SpeedPerFlick = ctx.Config.Bind("Throw", "SpeedPerFlick", 11f, "Throw speed (game units a second) for each screen height a second the hand moves as you let go. Higher: the same flick throws further.");
			MinThrowSpeed = ctx.Config.Bind("Throw", "MinThrowSpeed", 7f, "Let go slower than this and the grenade drops in the car instead of being thrown (you can catch it again before it falls out of sight).");
			MaxThrowSpeed = ctx.Config.Bind("Throw", "MaxThrowSpeed", 60f, "The hardest throw, game units a second, on top of the car's own speed.");
			Loft = ctx.Config.Bind("Throw", "LoftDegrees", 22f, new ConfigDescription("The angle above level the grenade leaves at.", new AcceptableValueRange<float>(0f, 60f)));
			Gravity = ctx.Config.Bind("Throw", "Gravity", 30f, "How fast a thrown grenade falls, game units a second squared.");
			PinPull = ctx.Config.Bind("Grenade", "PinPull", 0.5f, new ConfigDescription("How far the hand has to pull the ring before the pin comes out, in cockpit units (a cockpit pixel is 0.05).", new AcceptableValueRange<float>(0.1f, 2f)));
			RackX = ctx.Config.Bind("Dash", "OffsetX", 0f, "Moves the grenades sideways on the dash (cockpit units, right is positive). Takes effect at the next race.");
			RackY = ctx.Config.Bind("Dash", "OffsetY", 0f, "Moves the grenades up or down on the dash (cockpit units, up is positive). Takes effect at the next race.");
			Verbose = ctx.Config.Bind("Debug", "VerboseLog", false, "Write what the mod finds and does (the dash, pins, throws, blasts) to the BepInEx log.");
			ConfigVersion = ctx.Config.Bind("Debug", "ConfigVersion", 0, "The settings' layout, for moving old values on once. Leave it alone.");
			Migrate();

			GrenadeItem.Register(ctx);
			ctx.OnSceneLoaded(scene => Effects.OnSceneLoaded());

			fitter = new GameObject("GrenadeFitter") { hideFlags = HideFlags.HideAndDontSave };
			Object.DontDestroyOnLoad(fitter);
			fitter.AddComponent<Fitter>();
		}

		public override void Unload()
		{
			if (fitter != null)
			{
				Object.Destroy(fitter);
				fitter = null;
			}
			Fitter.Remove();
			Thrown.DestroyAll();
		}

		/// <summary>BepInEx keeps a saved value over a new default, so a changed default is moved on here, once. 1: the first layout.</summary>
		private static void Migrate()
		{
			if (ConfigVersion.Value >= CurrentConfig)
			{
				return;
			}
			ConfigVersion.Value = CurrentConfig;
		}
	}
}
