using System.Collections.Generic;
using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// A grenade on the dash. Until its pin is pulled it is ammo like a magazine: grab it, carry it, let it go and the rack puts it back.
	/// Pulling the pin (<see cref="Pin"/>) arms it: the spoon flies off and the fuse burns, and nothing stops it. Grab it then and flick
	/// it out of the car (<see cref="Thrown"/>); let go too slowly and it drops in the car, where you can still catch it until it falls
	/// out of sight into the footwell. When the fuse runs out anywhere in the car, it goes off in the car (<see cref="Blast.InCockpit"/>).
	/// Everything is set up in code in <see cref="Awake"/>, so the copies the rack makes for ammo boxes work the same.
	/// </summary>
	internal sealed class DashGrenade : NAmmo
	{
		/// <summary>The hand's flick is measured over about this long before it lets go.</summary>
		private const float FlickWindow = 0.09f;

		private struct Sample
		{
			public float time;
			public Vector2 cursor;
		}

		private readonly List<Sample> samples = new List<Sample>();
		private static float lastHint = -100f;
		private Grabber grabber;
		private SpriteRenderer spoon;
		private bool armed;
		private bool inFootwell;
		private bool gone;
		private float fuseLeft;
		private float nextSmoke;
		private AudioSource hiss;

		/// <summary>Armed: the rack leaves it alone (no putting it back) and counts its place as free.</summary>
		public override bool busy => armed;

		internal bool Armed => armed;

		internal MagazineManager Rack => GetComponentInParent<MagazineManager>(true);

		protected override void Awake()
		{
			// The feel of the game's own magazines (read off the revolver's in the 0.419 dumps): moved directly, a soft angle lock, a lean
			// as it is dragged, and held in its place on the rack until pulled clear of it.
			isPhysicsControlled = false;
			mass = 1f;
			angularInertia = 1f;
			CDRatio = 1f;
			handSpringForce = 1f;
			positionSmoothing = 25f;
			releasedDamping = 1f;
			directSpeedKeptOnRelease = 0.9f;
			keepPhysicalCursorClose = false;
			releaseCursorBlend = 0.25f;
			handIndicatorCameraOffset = 0.1f;
			gravity = new Vector2(0f, -90.81f);
			softAngleLock = true;
			angleLockSpring = 20f;
			angleLockDampen = 5f;
			dragTiltPerSpeed = 0.1f;
			dragTiltDir = new Vector2(-1f, 0f);
			maxDragTilt = 0.5f;
			dragTiltSmoothing = 10f;
			stuck = true;
			stuckSpring = 100f;
			stuckDampen = 10f;
			breakOffForce = 1f;
			killHeight = Cockpit.FootwellY;
			loadZone = null;
			Transform grip = transform.Find("Grip");
			attachedCollider = grip != null ? grip.GetComponent<InteriorCollider>() : null;
			Transform spoonPart = transform.Find("Spoon");
			spoon = spoonPart != null ? spoonPart.GetComponent<SpriteRenderer>() : null;
			base.Awake();
		}

		/// <summary>The pin came out: off goes the spoon, and the fuse is lit.</summary>
		internal void Arm()
		{
			if (armed)
			{
				return;
			}
			armed = true;
			fuseLeft = Mathf.Max(0.5f, Mod.FuseSeconds.Value);
			if (spoon != null && spoon.enabled)
			{
				spoon.enabled = false;
				// The lever springs up and away to the right, turning over.
				Bit.Spawn(spoon, new Vector2(Random.Range(2.5f, 4f), Random.Range(5f, 7f)), Random.Range(-900f, -600f), 2f);
			}
			Effects.Play2D(Effects.Ping, 0.7f, Random.Range(0.95f, 1.1f));
			hiss = Effects.Loop2D(gameObject, Effects.Hiss, 0.25f);
			Mod.Info($"Grenade: pin pulled, {fuseLeft:0.0} s on the fuse");
		}

		protected override void Update()
		{
			if (grabber == null)
			{
				grabber = FindFirstObjectByType<Grabber>();
			}
			if (armed)
			{
				fuseLeft -= Time.deltaTime;
				if (fuseLeft <= 0f)
				{
					GoOffInCar();
					return;
				}
				if (!inFootwell && Time.time >= nextSmoke)
				{
					nextSmoke = Time.time + 0.07f;
					Vector3 top = transform.TransformPoint(new Vector3(0f, (Art.FuseTopY + 1) * Cockpit.Texel, -0.03f));
					Bit.Smoke(transform.parent, top, Cockpit.Texel);
				}
			}
			if (IsGrabbed && grabber != null)
			{
				samples.Add(new Sample { time = Time.unscaledTime, cursor = grabber.cursor });
				while (samples.Count > 2 && Time.unscaledTime - samples[1].time > FlickWindow)
				{
					samples.RemoveAt(0);
				}
			}
			if (inFootwell)
			{
				return;
			}
			base.Update();
			if (armed)
			{
				if (!IsGrabbed && position.y < killHeight)
				{
					// Out of sight under the dash: it is in the footwell now, out of reach, and the fuse is still burning.
					inFootwell = true;
					canBeGrabbed = false;
					foreach (Renderer r in GetComponentsInChildren<Renderer>())
					{
						r.enabled = false;
					}
					Mod.Info("Grenade: dropped in the footwell");
				}
				return;
			}
			FellOutOfTheWorld();
		}

		protected override void OnGrab()
		{
			base.OnGrab();
			samples.Clear();
			// A live grenade comes away from the dash at once: there is no time to tug it clear.
			if (armed && stuck)
			{
				BreakOff();
			}
		}

		protected override void OnRelease()
		{
			base.OnRelease();
			if (gone)
			{
				return;
			}
			Vector2 flick = Flick();
			Vector2 cursor = samples.Count > 0 ? samples[samples.Count - 1].cursor : (grabber != null ? grabber.cursor : Vector2.zero);
			samples.Clear();
			if (!armed)
			{
				if (flick.magnitude * Mod.SpeedPerFlick.Value >= Mod.MinThrowSpeed.Value && Time.time - lastHint > 20f)
				{
					lastHint = Time.time;
					TextController.EnqueueRandomText(1.2f, "PULL THE PIN!");
				}
				return;
			}
			if (Thrown.TryThrow(flick, cursor, fuseLeft))
			{
				// Out of the window: the grenade on the dash is gone, its place is free for the next one.
				if (hiss != null)
				{
					hiss.Stop();
				}
				MagazineManager rack = Rack;
				if (rack != null)
				{
					rack.Take(this);
				}
				else
				{
					Destroy(gameObject);
				}
			}
		}

		/// <summary>How fast the hand was moving as it let go, in screen heights a second (x right, y up).</summary>
		private Vector2 Flick()
		{
			if (samples.Count < 2)
			{
				return Vector2.zero;
			}
			Sample last = samples[samples.Count - 1];
			Sample first = samples[0];
			for (int i = samples.Count - 2; i >= 0; i--)
			{
				first = samples[i];
				if (last.time - first.time >= FlickWindow)
				{
					break;
				}
			}
			float dt = last.time - first.time;
			if (dt <= 1e-4f)
			{
				return Vector2.zero;
			}
			return (last.cursor - first.cursor) / dt / Mathf.Max(1f, Screen.height);
		}

		private void GoOffInCar()
		{
			armed = false;
			gone = true;
			if (IsGrabbed)
			{
				blockRelease = false;
				Release();
			}
			Blast.InCockpit(transform.position, transform.parent, inFootwell);
			MagazineManager rack = Rack;
			if (rack != null)
			{
				rack.Take(this);
			}
			else
			{
				Destroy(gameObject);
			}
		}
	}
}
