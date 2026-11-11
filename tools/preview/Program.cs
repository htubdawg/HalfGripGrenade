using System;
using System.IO;
using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// Draws the grenade's art with the mod's own <see cref="Art"/> and checks the throw arithmetic in <see cref="Throw"/>, without the game.
	///   dotnet run -c Release --project tools/preview -p:Refs=&lt;HalfGripRefs&gt; -- &lt;out dir&gt;
	/// Writes body/spoon/pin/whole at 8x, flash-4x.png, icon.png (copy to Grenade/icon.png) and prints a table of throws.
	/// </summary>
	internal static class Program
	{
		private static int Main(string[] args)
		{
			string dir = args.Length > 0 ? args[0] : ".";
			Directory.CreateDirectory(dir);
			Png.Save(Path.Combine(dir, "body-8x.png"), Art.Body(), Art.Width, Art.Height, 8);
			Png.Save(Path.Combine(dir, "spoon-8x.png"), Art.Spoon(), Art.Width, Art.Height, 8);
			Png.Save(Path.Combine(dir, "pin-8x.png"), Art.Pin(), Art.Width, Art.Height, 8);
			Png.Save(Path.Combine(dir, "whole-8x.png"), Art.Whole(), Art.Width, Art.Height, 8);
			Png.Save(Path.Combine(dir, "flash-4x.png"), Art.Flash(40), 40, 40, 4);
			Png.Save(Path.Combine(dir, "rack-4x.png"), Rack(), RackWidth, Art.Height + 4, 4);
			Png.Save(Path.Combine(dir, "icon.png"), Icon(), 64, 64, 2);

			// The throw table: flick (screen heights a second, right and up) -> velocity relative to the car (right, up, forward).
			var settings = new Throw.Settings();
			Console.WriteLine("flick x,y  aim   -> right   up  forward  (speed)  ground range at rest");
			foreach (var (fx, fy, aim) in new[] { (0f, 0.5f, 0f), (0f, 1.5f, 0f), (0f, 3f, 0f), (0f, 5f, 0f), (0f, 9f, 0f), (0f, 4f, 20f), (0f, -3f, 0f), (0f, -3f, -20f), (3f, 0.5f, 0f), (-4f, 1f, 0f) })
			{
				if (!Throw.Velocity(new Vector2(fx, fy), aim, settings, out Vector3 v))
				{
					Console.WriteLine($"{fx,4} {fy,4} {aim,4}  -> a drop, no throw");
					continue;
				}
				float flat = new Vector2(v.x, v.z).magnitude;
				float time = 2f * v.y / settings.Gravity;
				Console.WriteLine($"{fx,4} {fy,4} {aim,4}  -> {v.x,6:0.0} {v.y,5:0.0} {v.z,7:0.0}  ({v.magnitude,5:0.0})  {flat * time,6:0} units, {time:0.00} s");
			}
			return 0;
		}

		private const int RackWidth = 3 * 20 + 6;

		/// <summary>Three grenades as they sit on the dash, 20 cockpit pixels apart (Cockpit.Spacing), the middle one without its pin.</summary>
		private static Color32[] Rack()
		{
			int h = Art.Height + 4;
			Color32[] px = Art.Blank(RackWidth, h);
			for (int i = 0; i < 3; i++)
			{
				Color32[] g = i == 1 ? Art.Body() : Art.Whole();
				Blit(px, RackWidth, h, g, Art.Width, Art.Height, i * 20, 2, 1);
			}
			return px;
		}

		/// <summary>The mod's picture: the whole grenade at twice its size, in the middle of a 64 pixel square.</summary>
		private static Color32[] Icon()
		{
			Color32[] px = Art.Blank(64, 64);
			Blit(px, 64, 64, Art.Whole(), Art.Width, Art.Height, 32 - Art.Width, 32 - Art.Height, 2);
			return px;
		}

		private static void Blit(Color32[] to, int tw, int th, Color32[] from, int fw, int fh, int ox, int oy, int scale)
		{
			for (int y = 0; y < fh * scale; y++)
			{
				for (int x = 0; x < fw * scale; x++)
				{
					Color32 c = from[(y / scale) * fw + x / scale];
					int tx = ox + x, ty = oy + y;
					if (c.a > 0 && tx >= 0 && ty >= 0 && tx < tw && ty < th)
					{
						to[ty * tw + tx] = c;
					}
				}
			}
		}
	}
}
