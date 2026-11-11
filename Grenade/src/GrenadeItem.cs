using System.Collections.Generic;
using System.Linq;
using HalfGripTools;
using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// The grenades as an item: a gun card cloned from the game's revolver, so it has the gun slot, a place in the shop and the save
	/// for free, but with gun type <see cref="GunType.NONE"/>. The game shows the gun whose type the fitted card names
	/// (<c>GunLoadoutActivator</c>), and none has type NONE, so with grenades fitted every gun (and its rack) stays switched off and
	/// nothing hangs by the visor. The grenades on the dash are the <see cref="Fitter"/>'s business.
	/// </summary>
	internal static class GrenadeItem
	{
		/// <summary>Saved in runs: never change it.</summary>
		internal const string Id = "grenade.frag";

		private static GunUpgrade item;

		/// <summary>True while grenades are the player's fitted gun.</summary>
		internal static bool IsEquipped => item != null && PlayerStats.gunUpgrade == item;

		internal static void Register(ModContext ctx)
		{
			ctx.Items.Add(Id, catalog => Create(ctx, catalog));
		}

		private static GunUpgrade Create(ModContext ctx, ItemCatalog catalog)
		{
			List<GunUpgrade> guns = catalog.items.OfType<GunUpgrade>().Where(g => g != null).ToList();
			GunUpgrade revolver = guns.FirstOrDefault(g => g.gunType == GunType.REVOLVER) ?? guns.FirstOrDefault(g => g.gunType != GunType.NONE);
			if (revolver == null)
			{
				Mod.Log.LogError("No gun item in the catalog to base the grenades on, so there are no grenades.");
				return null;
			}

			GunUpgrade made = ctx.Items.Clone(revolver, Id);
			made.description = "Grenade\n<color=#7e7e7e>Pull pin. Throw. Do not hold.</color>\n<color=#6fb7b4>Pull a pin on the dash, then flick the grenade out of the car: up throws it ahead, down tosses it back</color>";
			made.secondaryDescription = "";
			made.gunType = GunType.NONE;
			made.tier = ItemTier.RaceSpec;
			made.disabled = false;
			made.startsLocked = false;
			made.cost = Mathf.Max(1, Mod.Cost.Value);

			// The card's damage is the damage close to the blast; "magazines" is the grenades on the dash.
			made.damage = Mathf.Max(1f, Mod.Damage.Value);
			made.projectilesPerShot = 1;
			made.projectileSpread = 0f;
			made.inaccuracy = 0f;
			made.fireRate = 0f;
			made.capacity = 0;
			made.magazines = Mathf.Clamp(Mod.Grenades.Value, 1, 3);
			made.ammoWord = "grenade|grenades";
			made.muzzleFlash = null;
			made.shakeIntensity = 0f;
			made.gunTexture1 = null;
			made.gunTexture2 = null;
			// Other cards' ammo lines ("+1 spare clip") read this gun's words: say grenades.
			made.ammoWording = new List<GunUpgrade.AmmoWording>();
			if (revolver.ammoWording != null)
			{
				foreach (GunUpgrade.AmmoWording wording in revolver.ammoWording)
				{
					if (wording != null)
					{
						made.ammoWording.Add(new GunUpgrade.AmmoWording { stat = wording.stat, scale = wording.scale, label = Grenadify(wording.label) });
					}
				}
			}
			made.forceHide = new List<string>(revolver.forceHide ?? new List<string>()) { "fire rate", "accuracy" };

			Sprite icon = Icon(revolver.image);
			if (icon != null)
			{
				made.image = icon;
			}
			item = made;
			Mod.Info($"Grenade: made the item '{Id}' from '{revolver.id}': damage {made.damage}, {made.magazines} grenades, cost {made.cost}");
			return made;
		}

		/// <summary>"spare clips" becomes "spare grenades", and so on.</summary>
		private static string Grenadify(string label)
		{
			if (string.IsNullOrEmpty(label))
			{
				return label;
			}
			foreach (string word in new[] { "magazines", "clips", "shells", "rounds", "bullets" })
			{
				label = label.Replace(word, "grenades");
			}
			foreach (string word in new[] { "magazine", "clip", "shell", "round", "bullet" })
			{
				label = label.Replace(word, "grenade");
			}
			return label;
		}

		/// <summary>The shop card's picture: the whole grenade, at the gun icons' pixel density.</summary>
		private static Sprite Icon(Sprite like)
		{
			var texture = new Texture2D(Art.Width, Art.Height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.HideAndDontSave,
				name = "GrenadeIcon"
			};
			texture.SetPixels32(Art.Whole());
			texture.Apply();
			float ppu = like != null ? like.pixelsPerUnit : 100f;
			Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Art.Width, Art.Height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
			sprite.name = "GrenadeIcon";
			sprite.hideFlags = HideFlags.HideAndDontSave;
			return sprite;
		}
	}
}
