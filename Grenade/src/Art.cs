using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// The grenade's pixel art, drawn in code so <c>tools/preview</c> can draw it outside the game: an olive "pineapple" hand grenade
	/// with its fuse head on top, the spoon (the lever) down its right side, and the pin through the fuse head with its ring out to the
	/// left. The three parts are separate pictures on one canvas of the same size, so laid over each other at the same place they are the
	/// whole grenade; the pin and the spoon can then come away on their own. Pixel arrays only, rows from the bottom; the caller makes
	/// the textures. Lit from the upper left, like the rest of the cockpit's art.
	/// </summary>
	internal static class Art
	{
		/// <summary>The canvas every part is drawn on, in cockpit pixels.</summary>
		internal const int Width = 22;
		internal const int Height = 20;

		/// <summary>The body's middle column and the ring's middle, on the canvas.</summary>
		internal const int BodyX = 12;
		internal const int RingX = 4;
		internal const int RingY = 16;

		/// <summary>The fuse head's middle row: where the smoke comes out once the pin is pulled.</summary>
		internal const int FuseTopY = 18;

		private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
		private static readonly Color32 Outline = new Color32(22, 26, 16, 255);
		private static readonly Color32[] Olive =
		{
			new Color32(44, 52, 26, 255),
			new Color32(66, 78, 38, 255),
			new Color32(92, 106, 52, 255),
			new Color32(122, 136, 70, 255),
			new Color32(158, 170, 98, 255),
		};
		private static readonly Color32[] Steel =
		{
			new Color32(70, 72, 70, 255),
			new Color32(112, 114, 108, 255),
			new Color32(156, 158, 150, 255),
			new Color32(206, 206, 196, 255),
		};

		/// <summary>A blank canvas.</summary>
		internal static Color32[] Blank(int w = Width, int h = Height)
		{
			Color32[] px = new Color32[w * h];
			for (int i = 0; i < px.Length; i++)
			{
				px[i] = Clear;
			}
			return px;
		}

		private static void Put(Color32[] px, int x, int y, Color32 c, int w = Width, int h = Height)
		{
			if (x >= 0 && y >= 0 && x < w && y < h)
			{
				px[y * w + x] = c;
			}
		}

		private static bool Solid(Color32[] px, int x, int y, int w = Width, int h = Height)
		{
			return x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a > 0;
		}

		/// <summary>Rings every drawn shape in <paramref name="shape"/> with the dark outline, on the outside.</summary>
		private static void Outlined(Color32[] px, Color32[] shape)
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					if (Solid(shape, x, y))
					{
						px[y * Width + x] = shape[y * Width + x];
						continue;
					}
					if (Solid(shape, x - 1, y) || Solid(shape, x + 1, y) || Solid(shape, x, y - 1) || Solid(shape, x, y + 1))
					{
						px[y * Width + x] = Outline;
					}
				}
			}
		}

		/// <summary>
		/// The body: an egg of olive squares parted by grooves, shaded round from the upper left, and the steel fuse head on top (a neck,
		/// then a wider cap). This part stays whole from the rack to the blast.
		/// </summary>
		internal static Color32[] Body()
		{
			Color32[] shape = Blank();
			const float cx = BodyX, cy = 6.5f, rx = 5.6f, ry = 6.2f;
			for (int y = 1; y <= 13; y++)
			{
				for (int x = 6; x <= 18; x++)
				{
					float nx = (x - cx) / rx, ny = (y - cy) / ry;
					float r = nx * nx + ny * ny;
					if (r > 1f)
					{
						continue;
					}
					// Round shading: light from the upper left, darkest at the lower right rim.
					float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - r));
					float light = -0.55f * nx + 0.6f * ny + 0.6f * nz;
					int level = Mathf.Clamp(Mathf.RoundToInt(1f + light * 2.4f), 1, 4);
					// The grooves of the pineapple: every third column and row, a step darker.
					bool groove = (x - BodyX + 30) % 3 == 1 || (y + 30) % 3 == 2;
					if (groove)
					{
						level = Mathf.Max(0, level - 2);
					}
					shape[y * Width + x] = Olive[level];
				}
			}
			// The fuse: a neck two pixels high, then the cap.
			for (int y = 13; y <= 14; y++)
			{
				for (int x = BodyX - 2; x <= BodyX + 2; x++)
				{
					shape[y * Width + x] = Steel[x < BodyX ? 2 : (x == BodyX ? 1 : 0)];
				}
			}
			for (int y = 15; y <= FuseTopY - 1; y++)
			{
				for (int x = BodyX - 3; x <= BodyX + 2; x++)
				{
					int level = y == FuseTopY - 1 ? 3 : (x <= BodyX - 2 ? 2 : (x <= BodyX ? 1 : 0));
					shape[y * Width + x] = Steel[level];
				}
			}
			Color32[] px = Blank();
			Outlined(px, shape);
			return px;
		}

		/// <summary>The spoon: a thin steel lever from the top of the fuse head, over to the right and down the body's side.</summary>
		internal static Color32[] Spoon()
		{
			Color32[] shape = Blank();
			// Over the top of the cap and out to the right.
			for (int x = BodyX; x <= BodyX + 5; x++)
			{
				shape[FuseTopY * Width + x] = Steel[x < BodyX + 3 ? 3 : 2];
			}
			// Down the side, following the body's curve: one column out at the shoulder, back in toward the bottom.
			int[] column = { 18, 18, 19, 19, 19, 19, 19, 19, 18, 18, 18, 17 };
			for (int i = 0; i < column.Length; i++)
			{
				int y = FuseTopY - 1 - i;
				shape[y * Width + column[i]] = Steel[i < 4 ? 2 : 1];
			}
			Color32[] px = Blank();
			Outlined(px, shape);
			return px;
		}

		/// <summary>The pin: a short rod into the fuse head and the ring round (<see cref="RingX"/>, <see cref="RingY"/>).</summary>
		internal static Color32[] Pin()
		{
			Color32[] shape = Blank();
			for (int x = RingX + 4; x <= BodyX - 3; x++)
			{
				shape[RingY * Width + x] = Steel[3];
			}
			// A ring of radius 2.5: its pixels are those between 1.9 and 3.1 from the middle.
			for (int y = RingY - 3; y <= RingY + 3; y++)
			{
				for (int x = RingX - 3; x <= RingX + 3; x++)
				{
					float d = Mathf.Sqrt((x - RingX) * (x - RingX) + (y - RingY) * (y - RingY));
					if (d >= 1.9f && d < 3.1f)
					{
						shape[y * Width + x] = Steel[(x - RingX) - (y - RingY) < 0 ? 3 : 1];
					}
				}
			}
			Color32[] px = Blank();
			Outlined(px, shape);
			return px;
		}

		/// <summary>The three parts laid over each other: the whole grenade as it sits on the dash.</summary>
		internal static Color32[] Whole()
		{
			Color32[] px = Body();
			foreach (Color32[] part in new[] { Spoon(), Pin() })
			{
				for (int i = 0; i < px.Length; i++)
				{
					if (part[i].a > 0 && (px[i].a == 0 || part[i].r != Outline.r || part[i].g != Outline.g))
					{
						px[i] = part[i];
					}
				}
			}
			return px;
		}

		/// <summary>One grey pixel of smoke or a spark from the fuse: two of them, a light and a dark.</summary>
		internal static Color32 Smoke(bool light)
		{
			return light ? new Color32(190, 188, 180, 255) : new Color32(120, 118, 112, 255);
		}

		/// <summary>The cockpit flash of a grenade going off in the car: a rough star of hot colours, <paramref name="size"/> pixels square.</summary>
		internal static Color32[] Flash(int size)
		{
			Color32[] px = Blank(size, size);
			float c = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = x - c, dy = y - c;
					float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
					float a = Mathf.Atan2(dy, dx);
					// Eight ragged spikes.
					float reach = 0.62f + 0.38f * Mathf.Abs(Mathf.Cos(a * 4f)) * (0.7f + 0.3f * Mathf.Abs(Mathf.Sin(a * 7f + 1f)));
					if (d > reach)
					{
						continue;
					}
					float t = d / reach;
					Color32 col = t < 0.35f ? new Color32(255, 250, 220, 255) : t < 0.65f ? new Color32(255, 200, 70, 255) : t < 0.85f ? new Color32(240, 120, 30, 255) : new Color32(150, 50, 20, 255);
					Put(px, x, y, col, size, size);
				}
			}
			return px;
		}
	}
}
