using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HalfGripTools;
using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// Multiplayer (4l3x4nd3r0's Multiplayer mod, through HalfGripTools' <c>ctx.Net</c>). Without it, or outside a race, nothing here does
	/// anything.
	///
	/// <para><b>Damage needs nothing from this file.</b> In a room every other player's car is a stand-in <see cref="EnemyController"/> on
	/// this machine, and Multiplayer patches <see cref="EnemyController.TakeDamage(float, bool)"/>: a hit on a stand-in is scaled by the
	/// host's PvP setting and sent to the car's owner, whose game applies it to the real car (an AI car's hit goes to the host). So
	/// <see cref="Blast.InWorld"/> hurts other players as it is, worked out on the thrower's machine only, the way the RPG's rockets do.
	/// What this file adds to it: <see cref="AsExplosion"/> tells Multiplayer the hit is an explosion, so the host's ExplosionDamage
	/// applies rather than ItemDamage.</para>
	///
	/// <para><b>What the others see</b> is sent here. While a grenade of ours is in the air, its place and speed go out fifteen times a
	/// second (unreliable); when it goes off, where (reliable), also when it goes off in our car. The other players draw a
	/// <see cref="Ghost"/> grenade that hisses and follows those reports, and set off the explosion's look and sound, with no damage: the
	/// damage was already done by the thrower. The blast also carries the thrower's radii, and each machine shoves the bodies it simulates
	/// itself (its own car, the host's real AI cars) with <see cref="Blast.ShoveFromAfar"/>; Multiplayer sends where they went.</para>
	/// </summary>
	internal static class GrenadeNet
	{
		/// <summary>Our grenades in flight: count, then for each its id, place and speed.</summary>
		private const byte MsgFlying = 1;
		/// <summary>One went off: id (0 for one in the car), place, in the car or not, the thrower's FullDamageRadius and BlastRadius.</summary>
		private const byte MsgBlast = 2;

		private const int MaxPerMessage = 8;
		private const float SendEvery = 1f / 15f;

		private static ModNet net;
		private static float nextSend;
		private static int lastId;

		internal static void Install(ModContext ctx)
		{
			net = ctx.Net;
			net.OnReceive(Receive);
			Net.PhaseChanged += OnPhase;
			Net.PlayerLeft += OnLeft;
		}

		internal static void Remove()
		{
			Net.PhaseChanged -= OnPhase;
			Net.PlayerLeft -= OnLeft;
			Ghost.ClearAll();
			net = null;
		}

		/// <summary>In a Multiplayer room, racing: the only time anything is sent or drawn.</summary>
		internal static bool Racing => net != null && Net.InRoom && Net.Phase == NetPhase.Racing;

		internal static int NewId() => ++lastId == 0 ? ++lastId : lastId;

		private static void OnPhase(NetPhase phase) => Ghost.ClearAll();

		private static void OnLeft(NetPlayer player) => Ghost.Clear(player.Id);

		/// <summary>Every frame (from <see cref="Fitter"/>): our grenades' places, fifteen times a second; the others' ghosts moved.</summary>
		internal static void Tick()
		{
			Ghost.TickAll();
			if (!Racing || Time.unscaledTime < nextSend)
			{
				return;
			}
			nextSend = Time.unscaledTime + SendEvery;
			IReadOnlyList<Thrown> flying = Thrown.All;
			int count = Math.Min(flying.Count, MaxPerMessage);
			if (count == 0)
			{
				return;
			}
			using var stream = new MemoryStream(2 + count * 28);
			using var w = new BinaryWriter(stream);
			w.Write(MsgFlying);
			w.Write((byte)count);
			for (int i = 0; i < count; i++)
			{
				Thrown t = flying[i];
				w.Write(t.NetId);
				Vec(w, t.transform.position);
				Vec(w, t.Velocity);
			}
			w.Flush();
			net.Send(stream.ToArray(), Net.Everyone, reliable: false);
		}

		/// <summary>One of ours went off at <paramref name="at"/> (<paramref name="id"/> 0 and <paramref name="inCar"/>: in our car).</summary>
		internal static void SendBlast(int id, Vector3 at, bool inCar)
		{
			if (!Racing)
			{
				return;
			}
			using var stream = new MemoryStream(26);
			using var w = new BinaryWriter(stream);
			w.Write(MsgBlast);
			w.Write(id);
			Vec(w, at);
			w.Write(inCar);
			w.Write(Mod.FullDamageRadius.Value);
			w.Write(Mod.BlastRadius.Value);
			w.Flush();
			net.Send(stream.ToArray(), Net.Everyone, reliable: true);
		}

		private static void Receive(ulong from, byte[] data)
		{
			if (data == null || data.Length == 0 || !Racing)
			{
				return;
			}
			try
			{
				using var r = new BinaryReader(new MemoryStream(data));
				switch (r.ReadByte())
				{
					case MsgFlying:
						int count = Math.Min((int)r.ReadByte(), MaxPerMessage);
						for (int i = 0; i < count; i++)
						{
							int id = r.ReadInt32();
							Vector3 position = Vec(r);
							Vector3 velocity = Vec(r);
							if (Sane(position) && Sane(velocity))
							{
								Ghost.Report(from, id, position, velocity);
							}
						}
						break;
					case MsgBlast:
						int which = r.ReadInt32();
						Vector3 at = Vec(r);
						bool inCar = r.ReadBoolean();
						float full = r.ReadSingle();
						float reach = r.ReadSingle();
						if (Sane(at))
						{
							Ghost.GoOff(from, which, at, inCar);
							// A blast in the thrower's car pushes nobody else; out in the world it shoves what this machine simulates.
							if (!inCar && ExplosionsHurt() && !float.IsNaN(full + reach) && reach > 0f && reach < 1000f)
							{
								Blast.ShoveFromAfar(at, full, reach);
							}
						}
						break;
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning($"Grenade: a message from player {from} did not read: {e.Message}");
			}
		}

		private static bool Sane(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z) && v.sqrMagnitude < 1e10f;

		private static void Vec(BinaryWriter w, Vector3 v)
		{
			w.Write(v.x);
			w.Write(v.y);
			w.Write(v.z);
		}

		private static Vector3 Vec(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

		// ------------------------------------------------------------------ the hit is an explosion, for Multiplayer's PvP settings

		private static bool looked;
		private static FieldInfo combatCurrent;
		private static object explosion;

		/// <summary>
		/// Runs <paramref name="blast"/> with Multiplayer's <c>Combat.Current</c> set to <c>Explosion</c>, as it sets it itself around the
		/// game's own <c>AreaBlast.Apply</c>, so a hit on another player is scaled by the host's ExplosionDamage. Without Multiplayer, or
		/// if its insides have changed, the blast runs as it is (then it counts as ItemDamage, which is still damage).
		/// </summary>
		internal static void AsExplosion(Action blast)
		{
			Look();
			object before = null;
			if (combatCurrent != null && Net.InRoom)
			{
				try
				{
					before = combatCurrent.GetValue(null);
					combatCurrent.SetValue(null, explosion);
				}
				catch (Exception)
				{
					before = null;
				}
			}
			try
			{
				blast();
			}
			finally
			{
				if (before != null)
				{
					try { combatCurrent.SetValue(null, before); } catch (Exception) { }
				}
			}
		}

		private static FieldInfo roomRace;
		private static FieldInfo raceExplosion;

		/// <summary>
		/// False when the host has set explosions to do no damage to players (Multiplayer's <c>Room.Race.Explosion</c> is 0): then other
		/// players' grenades do not shove this player's car either. True if it cannot be read.
		/// </summary>
		private static bool ExplosionsHurt()
		{
			Look();
			try
			{
				object race = roomRace?.GetValue(null);
				return race == null || raceExplosion == null || (float)raceExplosion.GetValue(race) > 0f;
			}
			catch (Exception)
			{
				return true;
			}
		}

		private static void Look()
		{
			if (looked)
			{
				return;
			}
			looked = true;
			try
			{
				Type combat = null;
				foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					combat = assembly.GetType("Multiplayer.Combat", false);
					if (combat != null)
					{
						break;
					}
				}
				if (combat == null)
				{
					return;
				}
				const BindingFlags any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
				roomRace = combat.Assembly.GetType("Multiplayer.Room", false)?.GetField("Race", any);
				raceExplosion = roomRace?.FieldType.GetField("Explosion", any);
				if (raceExplosion != null && raceExplosion.FieldType != typeof(float))
				{
					raceExplosion = null;
				}
				FieldInfo field = combat.GetField("Current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
				if (field == null || !field.FieldType.IsEnum || !Enum.IsDefined(field.FieldType, "Explosion"))
				{
					Mod.Log.LogWarning("Grenade: Multiplayer looks different from what the Grenade expects; grenade hits on players count as items, not explosions.");
					return;
				}
				explosion = Enum.Parse(field.FieldType, "Explosion");
				combatCurrent = field;
				Mod.Info("Grenade: Multiplayer found; grenade hits on players count as explosions");
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Grenade: could not look for Multiplayer: " + e.Message);
			}
		}
	}

	/// <summary>
	/// Another player's grenade on this machine: the model and the hiss, following that player's reports (between two, it carries on at the
	/// last speed for up to a quarter of a second). It never hits or hurts anything. No word for a while and no blast: it is dropped.
	/// </summary>
	internal sealed class Ghost : MonoBehaviour
	{
		private const float Timeout = 0.6f;

		private static readonly Dictionary<(ulong, int), Ghost> all = new Dictionary<(ulong, int), Ghost>();

		private Vector3 position;
		private Vector3 velocity;
		private float stamp;
		private Vector3 spinAxis;
		private float spin;

		internal static void Report(ulong from, int id, Vector3 position, Vector3 velocity)
		{
			if (!all.TryGetValue((from, id), out Ghost ghost) || ghost == null)
			{
				var go = new GameObject("GrenadeGhost");
				go.transform.SetPositionAndRotation(position, UnityEngine.Random.rotation);
				ghost = go.AddComponent<Ghost>();
				ghost.spinAxis = UnityEngine.Random.onUnitSphere;
				ghost.spin = UnityEngine.Random.Range(400f, 800f);
				Thrown.Dress(go);
				all[(from, id)] = ghost;
				Mod.Info($"Grenade: player {from} threw one, at {position}");
			}
			ghost.position = position;
			ghost.velocity = velocity;
			ghost.stamp = Time.unscaledTime;
		}

		internal static void GoOff(ulong from, int id, Vector3 at, bool inCar)
		{
			if (all.TryGetValue((from, id), out Ghost ghost))
			{
				all.Remove((from, id));
				if (ghost != null)
				{
					Destroy(ghost.gameObject);
				}
			}
			Effects.Explosion(at);
			Blast.Shake(at);
			Mod.Info($"Grenade: player {from}'s went off{(inCar ? " in their car" : "")} at {at}");
		}

		internal static void TickAll()
		{
			if (all.Count == 0)
			{
				return;
			}
			float now = Time.unscaledTime;
			List<(ulong, int)> gone = null;
			foreach (KeyValuePair<(ulong, int), Ghost> pair in all)
			{
				Ghost ghost = pair.Value;
				if (ghost == null || now - ghost.stamp > Timeout)
				{
					if (ghost != null)
					{
						Destroy(ghost.gameObject);
					}
					(gone ??= new List<(ulong, int)>()).Add(pair.Key);
					continue;
				}
				ghost.Follow(now);
			}
			if (gone != null)
			{
				foreach ((ulong, int) key in gone)
				{
					all.Remove(key);
				}
			}
		}

		private void Follow(float now)
		{
			Vector3 target = position + velocity * Mathf.Min(now - stamp, 0.25f);
			transform.position = Vector3.Lerp(transform.position, target, 0.5f);
			transform.Rotate(spinAxis, spin * Time.deltaTime, Space.World);
		}

		internal static void Clear(ulong from)
		{
			List<(ulong, int)> gone = new List<(ulong, int)>();
			foreach (KeyValuePair<(ulong, int), Ghost> pair in all)
			{
				if (pair.Key.Item1 == from)
				{
					gone.Add(pair.Key);
				}
			}
			foreach ((ulong, int) key in gone)
			{
				if (all[key] != null)
				{
					Destroy(all[key].gameObject);
				}
				all.Remove(key);
			}
		}

		internal static void ClearAll()
		{
			foreach (Ghost ghost in all.Values)
			{
				if (ghost != null)
				{
					Destroy(ghost.gameObject);
				}
			}
			all.Clear();
		}
	}
}
