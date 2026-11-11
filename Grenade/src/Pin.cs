using UnityEngine;
using UnityEngine.Events;

namespace HalfGripGrenade
{
	/// <summary>
	/// The pin and its ring. It is held in the fuse head (the game's stuck-and-break-off, as magazines are held in their racks), with a
	/// soft spring so it gives a little as you tug it: hold the left button on the ring and pull it away until it comes out
	/// (<c>Grenade.PinPull</c> cockpit units). Out, it arms the grenade; let go, it falls out of sight and is gone.
	/// </summary>
	internal sealed class Pin : FallingObject
	{
		private DashGrenade grenade;
		private bool pulled;
		private bool loose;
		private float looseFor;

		protected override void Awake()
		{
			isPhysicsControlled = false;
			mass = 1f;
			angularInertia = 1f;
			CDRatio = 1f;
			handSpringForce = 1f;
			positionSmoothing = 30f;
			releasedDamping = 0.5f;
			directSpeedKeptOnRelease = 0.9f;
			keepPhysicalCursorClose = false;
			releaseCursorBlend = 0.25f;
			handIndicatorCameraOffset = 0.1f;
			gravity = new Vector2(0f, -90.81f);
			softAngleLock = false;
			angleLock = false;
			dragTiltPerSpeed = 0.15f;
			dragTiltDir = new Vector2(-1f, 0.5f);
			maxDragTilt = 0.6f;
			dragTiltSmoothing = 12f;
			stuck = true;
			// Against the hand's weight of 1: the pin moves a third of the way toward the hand while it is still in.
			stuckSpring = 2f;
			stuckDampen = 10f;
			breakOffForce = Mod.PinPull != null ? Mod.PinPull.Value : 0.5f;
			attachedCollider = GetComponent<InteriorCollider>();
			onGrabbed = new UnityEvent();
			onReleased = new UnityEvent();
			onRightClicked = new UnityEvent();
			onBrokenOff = new UnityEvent();
			onBrokenOff.AddListener(Pulled);
			base.Awake();
		}

		private void Pulled()
		{
			if (pulled)
			{
				return;
			}
			pulled = true;
			Effects.Play2D(Effects.PinPull, 0.6f, Random.Range(1.1f, 1.25f));
			if (grenade == null)
			{
				grenade = GetComponentInParent<DashGrenade>();
			}
			if (grenade != null)
			{
				grenade.Arm();
			}
		}

		protected override void Update()
		{
			if (grenade == null && !loose)
			{
				grenade = GetComponentInParent<DashGrenade>();
			}
			if (!pulled)
			{
				// Not while the grenade itself is in the hand, not during the opening ceasefire (the rack's padlock), and not once the
				// grenade has gone back to the rack's pile of spares.
				MagazineManager rack = grenade != null ? grenade.Rack : null;
				canBeGrabbed = grenade != null && !grenade.IsGrabbed && grenade.canBeGrabbed && (rack == null || !rack.Locked);
			}
			base.Update();
			if (pulled && !IsGrabbed && !loose)
			{
				Loosen();
			}
			if (loose)
			{
				looseFor += Time.deltaTime;
				if (position.y < Cockpit.FootwellY || looseFor > 4f)
				{
					Destroy(gameObject);
				}
			}
		}

		/// <summary>
		/// Let go once it is out: it leaves the grenade (which may be thrown at any moment) for the rack, keeping where it is and how it
		/// moves, and falls.
		/// </summary>
		private void Loosen()
		{
			loose = true;
			Transform rack = grenade != null ? grenade.transform.parent : transform.parent;
			if (rack == null || rack == transform.parent)
			{
				return;
			}
			Vector2 keep = velocity;
			float spin = angularVelocity;
			transform.SetParent(rack, true);
			ResetRestPose(transform.localPosition, transform.localRotation);
			velocity = keep;
			angularVelocity = spin + Random.Range(-8f, 8f);
		}
	}
}
