using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// How a flick of the cockpit hand becomes a throw, as plain arithmetic (so <c>tools/preview</c> can print a table of throws).
	///
	/// The screen is flat, so one of its axes has to stand in for depth. Up and down on the screen is forward and back in the world:
	/// flick up to throw ahead of the car, flick down to toss it back over the roof at whoever is behind. Left and right stay left and
	/// right. How fast the hand moves when you let go is how hard it is thrown. On top of that the throw goes the way you point: let
	/// go with the hand over a car and the grenade heads for it, whichever way you flick. So you aim like a gun (point) and throw like
	/// a ball (flick), and the two do not fight: pointing sets the line, the flick sets the strength and forward or back.
	/// </summary>
	internal static class Throw
	{
		/// <summary>The numbers a throw is made from. Defaults here; <see cref="Mod"/> fills them from the config.</summary>
		internal sealed class Settings
		{
			/// <summary>Throw speed (world units a second, a car is about 13 long) for each screen height a second of flick.</summary>
			public float SpeedPerFlick = 11f;
			/// <summary>How much of a sideways flick counts, against an up or down one: a throw out of the side window is weaker.</summary>
			public float SideShare = 0.6f;
			/// <summary>Slower than this (world units a second) and letting go is a drop, not a throw: the grenade falls in the car.</summary>
			public float MinSpeed = 7f;
			/// <summary>The hardest throw, world units a second, relative to the car.</summary>
			public float MaxSpeed = 60f;
			/// <summary>The angle above the horizontal the grenade leaves at, in degrees.</summary>
			public float LoftDegrees = 22f;
			/// <summary>A little extra upward speed on every throw, so even a soft one clears the car.</summary>
			public float UpBoost = 3f;
			/// <summary>How far to the side pointing can steer a throw, in degrees either way.</summary>
			public float MaxAimDegrees = 50f;
			/// <summary>The grenade's gravity, world units a second squared (the world is about three times life size).</summary>
			public float Gravity = 30f;
		}

		/// <summary>
		/// The throw for a flick of <paramref name="flick"/> screen heights a second (x right, y up) let go while pointing
		/// <paramref name="aimDegrees"/> to the right of straight ahead. <paramref name="velocity"/> is relative to the car, in its own
		/// axes (x right, y up, z forward). False if the flick was too slow to be a throw.
		/// </summary>
		internal static bool Velocity(Vector2 flick, float aimDegrees, Settings s, out Vector3 velocity)
		{
			float forward = flick.y * s.SpeedPerFlick;
			float side = flick.x * s.SpeedPerFlick * s.SideShare;
			velocity = Vector3.zero;
			if (new Vector2(side, forward).magnitude < s.MinSpeed)
			{
				return false;
			}
			// Pointing steers the throw along the line from the car through the hand, forward or back. Back, the line is mirrored the
			// way you would expect: hand on the left, it lands behind you on the left.
			float aim = Mathf.Clamp(aimDegrees, -s.MaxAimDegrees, s.MaxAimDegrees) * Mathf.Deg2Rad;
			side += Mathf.Abs(forward) * Mathf.Tan(aim);
			Vector2 flat = Vector2.ClampMagnitude(new Vector2(side, forward), s.MaxSpeed);
			float up = flat.magnitude * Mathf.Tan(s.LoftDegrees * Mathf.Deg2Rad) + s.UpBoost;
			velocity = new Vector3(flat.x, up, flat.y);
			return true;
		}
	}
}
