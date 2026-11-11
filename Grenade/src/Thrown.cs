using System.Collections.Generic;
using HalfGripTools;
using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// A grenade in the world: thrown from the car (with the car's own speed added), it flies, bounces off the road, walls and cars,
	/// rolls to a stop and goes off when its fuse runs out. Its flight is worked out here with sphere casts rather than left to the
	/// physics engine, so it does not depend on which layers the game lets collide, and it cannot pass through a thin wall at speed.
	/// </summary>
	internal sealed class Thrown : MonoBehaviour
	{
		/// <summary>How big it is drawn and how big it is for hitting things, in world units: bigger than life, so it can be followed.</summary>
		private const float Size = 1.1f;
		private const float Radius = 0.45f;
		/// <summary>How much of its speed into a surface it keeps, bouncing back; slower than <see cref="Settle"/> it stops bouncing.</summary>
		private const float Restitution = 0.35f;
		private const float Settle = 4f;
		/// <summary>How much of its speed along a surface it keeps at each strike, and how fast it slows rolling (units a second squared).</summary>
		private const float Scrape = 0.8f;
		private const float Rolling = 12f;

		private static readonly List<Thrown> all = new List<Thrown>();
		private static readonly RaycastHit[] hits = new RaycastHit[16];
		private static Mesh mesh;

		/// <summary>This grenade's number in <see cref="GrenadeNet"/>'s messages, so the other players' ghost of it can be told apart.</summary>
		internal int NetId;
		private Vector3 velocity;
		private float fuse;
		private float gravity;
		private int mask;
		private Vector3 spinAxis;
		private float spin;
		private bool rolling;
		private float lastBounce;

		internal static IReadOnlyList<Thrown> All => all;
		internal Vector3 Velocity => velocity;

		/// <summary>
		/// Throws a grenade out of the player's car for a flick of <paramref name="flick"/> screen heights a second let go at
		/// <paramref name="cursor"/> (screen pixels). False, and nothing thrown, if the flick was too slow or there is no car to throw from.
		/// </summary>
		internal static bool TryThrow(Vector2 flick, Vector2 cursor, float fuseLeft)
		{
			CarControllerNew car = PlayerCar();
			Camera cam = car != null && car.fpvCamera != null ? car.fpvCamera : Camera.main;
			if (car == null || cam == null)
			{
				Mod.Log.LogWarning("Grenade: no car to throw from, so it drops");
				return false;
			}
			Vector3 forward = Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized;
			if (forward.sqrMagnitude < 0.5f)
			{
				forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
			}
			Vector3 right = Vector3.Cross(Vector3.up, forward);
			Ray ray = cam.ScreenPointToRay(cursor);
			Vector3 pointing = Vector3.ProjectOnPlane(ray.direction, Vector3.up);
			float aim = pointing.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(forward, pointing, Vector3.up) : 0f;
			Throw.Settings settings = Mod.ThrowSettings();
			if (!Throw.Velocity(flick, aim, settings, out Vector3 local))
			{
				Mod.Info($"Grenade: let go at {flick.magnitude:0.00} screen heights a second, too slow to throw: it drops");
				return false;
			}
			Vector3 carVelocity = car.rb != null ? car.rb.linearVelocity : Vector3.zero;
			Vector3 velocity = carVelocity + right * local.x + Vector3.up * local.y + forward * local.z;
			// Ahead, it leaves from where the hand let go, a little way out along the line through the windscreen; back, over the roof.
			Vector3 from = local.z >= 0f
				? ray.origin + ray.direction * 3f
				: cam.transform.position + Vector3.up * 1.5f - forward * 1f;
			from.y = Mathf.Max(from.y, cam.transform.position.y - 0.3f);
			Launch(from, velocity, fuseLeft, settings.Gravity);
			Mod.Info($"Grenade: thrown, flick {flick.x:0.00},{flick.y:0.00} pointing {aim:0}° -> {local.x:0.0} right, {local.y:0.0} up, {local.z:0.0} forward on top of the car's {carVelocity.magnitude:0.0}, {fuseLeft:0.0} s left");
			return true;
		}

		/// <summary>The player's car: the race's, or any car that says it is the player's.</summary>
		internal static CarControllerNew PlayerCar()
		{
			TrackManager race = Object.FindFirstObjectByType<TrackManager>();
			if (race != null && race.playerCarlink != null && race.playerCarlink.cc != null)
			{
				return race.playerCarlink.cc;
			}
			foreach (CarControllerNew car in Object.FindObjectsByType<CarControllerNew>(FindObjectsSortMode.None))
			{
				if (car.isPlayer)
				{
					return car;
				}
			}
			return null;
		}

		private static void Launch(Vector3 from, Vector3 velocity, float fuse, float gravity)
		{
			var go = new GameObject("ThrownGrenade");
			go.transform.position = from;
			go.transform.rotation = Random.rotation;
			Thrown grenade = go.AddComponent<Thrown>();
			grenade.velocity = velocity;
			grenade.fuse = fuse;
			grenade.gravity = gravity;
			grenade.mask = HitMask();
			grenade.spinAxis = Random.onUnitSphere;
			grenade.spin = Random.Range(400f, 800f);
			grenade.NetId = GrenadeNet.NewId();
			Dress(go);
			all.Add(grenade);
		}

		/// <summary>Everything but the player's car, the cockpit (which is drawn in the world too), and the layers nothing solid is on.</summary>
		private static int HitMask()
		{
			int mask = ~0;
			foreach (string name in new[] { "Player", "INTERIOR", "Ignore Raycast", "UI", "TransparentFX" })
			{
				int layer = LayerMask.NameToLayer(name);
				if (layer >= 0)
				{
					mask &= ~(1 << layer);
				}
			}
			return mask;
		}

		/// <summary>
		/// The model: the grenade's own pixel art, extruded, twice, crossed, so it looks solid from any side as it tumbles; and the hiss.
		/// Also dresses other players' grenades (<see cref="Ghost"/>), on a machine that may have no grenades fitted.
		/// </summary>
		internal static void Dress(GameObject go)
		{
			Cockpit.EnsureSprites();
			Texture2D texture = Cockpit.BodySprite != null ? Cockpit.BodySprite.texture : null;
			if (texture == null)
			{
				return;
			}
			float pixel = Size / 16f;
			if (mesh == null)
			{
				mesh = VoxelMesh.Build(texture, pixel, 11f * pixel, 12f, "Thrown grenade");
			}
			for (int i = 0; i < 2; i++)
			{
				var holder = new GameObject(i == 0 ? "model" : "model crossed");
				holder.transform.SetParent(go.transform, false);
				holder.transform.localRotation = Quaternion.Euler(0f, 90f * i, 0f);
				// The art runs back from its left edge: move it forward so the body's middle column is at the centre.
				GameObject model = VoxelMesh.MakeObject("mesh", mesh, texture, null, holder.transform, 0);
				model.transform.localPosition = new Vector3(0f, 0f, (Art.BodyX + 0.5f) * pixel);
			}
			Effects.Loop3D(go, Effects.Hiss, 0.6f);
		}

		private void Update()
		{
			float dt = Time.deltaTime;
			if (dt <= 0f)
			{
				return;
			}
			fuse -= dt;
			if (fuse <= 0f)
			{
				Explode();
				return;
			}
			velocity += Vector3.down * (gravity * dt);
			if (rolling)
			{
				// On the ground: rolling friction slows it along the surface.
				Vector3 flat = Vector3.ProjectOnPlane(velocity, Vector3.up);
				float speed = flat.magnitude;
				if (speed > 1e-3f)
				{
					velocity -= flat / speed * Mathf.Min(speed, Rolling * dt);
				}
			}
			rolling = false;
			Move(velocity * dt);
			transform.Rotate(spinAxis, spin * dt, Space.World);
			if (transform.position.y < -5000f)
			{
				Destroy(gameObject);
			}
		}

		/// <summary>Moves by <paramref name="step"/>, bouncing off whatever is in the way, up to three times in one frame.</summary>
		private void Move(Vector3 step)
		{
			Vector3 at = transform.position;
			for (int pass = 0; pass < 3; pass++)
			{
				float distance = step.magnitude;
				if (distance < 1e-5f)
				{
					break;
				}
				Vector3 heading = step / distance;
				int count = Physics.SphereCastNonAlloc(at, Radius, heading, hits, distance, mask, QueryTriggerInteraction.Ignore);
				int best = -1;
				for (int i = 0; i < count; i++)
				{
					// Starting inside something (distance 0) is no surface to bounce off; nor is the player's own car.
					if (hits[i].distance <= 0f || IsPlayer(hits[i].collider))
					{
						continue;
					}
					if (best < 0 || hits[i].distance < hits[best].distance)
					{
						best = i;
					}
				}
				if (best < 0)
				{
					at += step;
					break;
				}
				RaycastHit hit = hits[best];
				at += heading * Mathf.Max(0f, hit.distance - 0.01f);
				Vector3 normal = hit.normal;
				Vector3 surface = hit.rigidbody != null ? hit.rigidbody.GetPointVelocity(hit.point) : Vector3.zero;
				Vector3 relative = velocity - surface;
				float into = Vector3.Dot(relative, normal);
				if (into < 0f)
				{
					Vector3 along = (relative - normal * into) * Scrape;
					float back = -into > Settle ? -into * Restitution : 0f;
					velocity = surface + along + normal * back;
					if (-into > 8f && Time.time - lastBounce > 0.1f)
					{
						lastBounce = Time.time;
						Effects.BounceAt(hit.point, -into);
					}
					spin *= 0.7f;
				}
				if (normal.y > 0.6f)
				{
					rolling = true;
				}
				float left = Mathf.Max(0f, distance - hit.distance);
				step = Vector3.ProjectOnPlane(heading * left, normal);
			}
			transform.position = at;
		}

		private static bool IsPlayer(Collider c)
		{
			CarControllerNew car = c.GetComponentInParent<CarControllerNew>();
			return car != null && car.isPlayer;
		}

		private void Explode()
		{
			Vector3 at = transform.position;
			all.Remove(this);
			Destroy(gameObject);
			Effects.Explosion(at);
			GrenadeNet.SendBlast(NetId, at, false);
			GrenadeNet.AsExplosion(() => Blast.InWorld(at));
		}

		private void OnDestroy()
		{
			all.Remove(this);
		}

		/// <summary>Mods switched off: grenades in the air go with them, unexploded.</summary>
		internal static void DestroyAll()
		{
			foreach (Thrown grenade in all.ToArray())
			{
				if (grenade != null)
				{
					Destroy(grenade.gameObject);
				}
			}
			all.Clear();
		}
	}
}
