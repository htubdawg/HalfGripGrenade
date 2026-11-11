using System.Collections.Generic;
using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// What a grenade does when it goes off: in the world, damage to every car in reach (full close in, falling off evenly to nothing at
	/// <c>BlastRadius</c>), credited to the player as a kill, a shove, and barrels set off; in the car, damage to the player's car.
	/// </summary>
	internal static class Blast
	{
		/// <summary>How fast the blast throws a car at full strength, units a second; heavier cars are pushed harder, so all go alike.</summary>
		private const float Knock = 10f;

		private static readonly HashSet<Object> done = new HashSet<Object>();

		/// <summary>How much of the full damage reaches something this far from the blast.</summary>
		internal static float Share(float distance) => Share(distance, Mod.FullDamageRadius.Value, Mod.BlastRadius.Value);

		/// <summary>The same for another player's blast, with that player's radii.</summary>
		internal static float Share(float distance, float full, float reach)
		{
			full = Mathf.Max(0f, full);
			reach = Mathf.Max(full + 0.01f, reach);
			return distance <= full ? 1f : Mathf.Clamp01(1f - (distance - full) / (reach - full));
		}

		internal static void InWorld(Vector3 at)
		{
			float reach = Mathf.Max(0.1f, Mod.BlastRadius.Value);
			float damage = Mathf.Max(0f, Mod.Damage.Value);
			done.Clear();
			int cars = 0;
			foreach (Collider collider in Physics.OverlapSphere(at, reach, ~0, QueryTriggerInteraction.Ignore))
			{
				Vector3 closest = collider.ClosestPoint(at);
				float share = Share(Vector3.Distance(closest, at));
				if (share <= 0f)
				{
					continue;
				}
				EnemyController enemy = collider.GetComponentInParent<EnemyController>();
				if (enemy != null)
				{
					Shove(collider.attachedRigidbody, at, reach, share);
					if (done.Add(enemy))
					{
						Vector3 away = closest - at;
						Vector3 normal = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.up;
						enemy.TakeDamage(LapShield.Scale(enemy, damage * share), closest, normal, DamageMarkKind.Impact);
						cars++;
					}
					continue;
				}
				CarControllerNew car = collider.GetComponentInParent<CarControllerNew>();
				if (car != null && car.isPlayer)
				{
					if (Mod.SelfDamage.Value && car.carLink != null && done.Add(car))
					{
						car.carLink.TakeCrashDamage(damage * share, true, DamageKind.Crash);
						Mod.Info($"Grenade: caught your own car for {damage * share:0}");
					}
					continue;
				}
				DestructibleObject breakable = collider.GetComponentInParent<DestructibleObject>();
				if (breakable != null && share >= 0.5f && done.Add(breakable))
				{
					breakable.TakeDamageFromBullet();
				}
				Shove(collider.attachedRigidbody, at, reach, share);
			}
			done.Clear();
			Shake(at);
			Mod.Info($"Grenade: went off at {at}, {cars} car(s) in reach");
		}

		private static void Shove(Rigidbody body, Vector3 at, float reach, float share, bool player = false)
		{
			if (body == null || body.isKinematic || !done.Add(body))
			{
				return;
			}
			CarControllerNew car = body.GetComponentInParent<CarControllerNew>();
			if (car != null && car.isPlayer && !player)
			{
				return;
			}
			body.AddExplosionForce(body.mass * Knock * share * 2f, at, reach, 0.5f, ForceMode.Impulse);
		}

		/// <summary>
		/// Another player's grenade went off at <paramref name="at"/>: the shove only (the damage came from the thrower's machine). Every
		/// machine pushes the bodies it simulates itself, and Multiplayer sends where they went: this player's own car, the host's real
		/// AI cars (on a client they are kinematic stand-ins and skipped, as are other players' cars), loose things.
		/// </summary>
		internal static void ShoveFromAfar(Vector3 at, float full, float reach)
		{
			reach = Mathf.Max(0.1f, reach);
			done.Clear();
			int pushed = 0;
			foreach (Collider collider in Physics.OverlapSphere(at, reach, ~0, QueryTriggerInteraction.Ignore))
			{
				Rigidbody body = collider.attachedRigidbody;
				if (body == null || body.isKinematic || done.Contains(body))
				{
					continue;
				}
				float share = Share(Vector3.Distance(collider.ClosestPoint(at), at), full, reach);
				if (share > 0f)
				{
					Shove(body, at, reach, share, player: true);
					pushed++;
				}
			}
			done.Clear();
			Mod.Info($"Grenade: another player's blast at {at} pushed {pushed} bod(ies) here");
		}

		internal static void Shake(Vector3 at)
		{
			if (Shaker.instance == null)
			{
				return;
			}
			Camera cam = Camera.main;
			float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, at) / (Mod.BlastRadius.Value * 5f)) : 0.3f;
			if (near > 0f)
			{
				Shaker.instance.Shake(1f + 3f * near, 6f, 0.01f, 1f, 2);
			}
		}

		/// <summary>
		/// It went off in the car: <c>CockpitDamage</c> to the player's car, a hard shake, a flash and smoke where it was (unless it was in
		/// the footwell, out of sight, where only the smoke comes up).
		/// </summary>
		internal static void InCockpit(Vector3 at, Transform parent, bool footwell)
		{
			Effects.BoomInCar();
			if (Shaker.instance != null)
			{
				Shaker.instance.Shake(6f, 9f, 0.01f, 1.2f, 2);
			}
			CarControllerNew car = Thrown.PlayerCar();
			float damage = Mathf.Max(0f, Mod.CockpitDamage.Value);
			if (damage > 0f && car != null && car.carLink != null)
			{
				car.carLink.TakeCrashDamage(damage, true, DamageKind.Crash);
			}
			if (car != null)
			{
				GrenadeNet.SendBlast(0, car.transform.position + Vector3.up * 1.5f, true); // the others see our car go bang
			}
			TextController.EnqueueRandomText(1.2f, "OOPS", "IN THE CAR!", "THROW IT!");
			Mod.Info($"Grenade: went off in the car{(footwell ? " (in the footwell)" : "")} for {damage:0}");
			if (parent == null)
			{
				return;
			}
			float texel = Cockpit.Texel;
			Vector3 middle = footwell ? parent.TransformPoint(new Vector3(0f, -1.5f, -0.3f)) : at + new Vector3(0f, 8f * texel, -0.3f);
			if (!footwell && Effects.Flash != null)
			{
				Bit.Burst(parent, middle, Effects.Flash, 3f, Vector2.zero, 0.3f, 1.5f, 6);
			}
			for (int i = 0; i < 16; i++)
			{
				Vector2 v = Random.insideUnitCircle * 4f + Vector2.up * 2.5f;
				Bit.Burst(parent, middle, Effects.Pixel(Random.value < 0.5f), texel * Random.Range(4f, 9f), v, Random.Range(0.6f, 1.2f), 1.5f, 5);
			}
		}
	}
}
