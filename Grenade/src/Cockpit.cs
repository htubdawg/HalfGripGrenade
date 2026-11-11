using System.Collections.Generic;
using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// Twice a second: grenades fitted and a cockpit without them → builds the rack on the dash; not fitted → takes it away again (a
	/// refit between races, or a scene without the item). Lives for the whole session.
	/// </summary>
	internal sealed class Fitter : MonoBehaviour
	{
		private float next;

		private void Update()
		{
			try
			{
				GrenadeNet.Tick();
			}
			catch (System.Exception e)
			{
				Mod.Log.LogWarning("Grenade: multiplayer update failed: " + e.Message);
			}
			if (Time.unscaledTime < next)
			{
				return;
			}
			next = Time.unscaledTime + 0.5f;
			try
			{
				if (!GrenadeItem.IsEquipped)
				{
					Remove();
					return;
				}
				if (Cockpit.Rack == null)
				{
					Cockpit.TryBuild();
				}
			}
			catch (System.Exception e)
			{
				Mod.Log.LogWarning("Grenade: could not put the grenades on the dash: " + e);
				next = Time.unscaledTime + 5f;
			}
		}

		internal static void Remove()
		{
			if (Cockpit.Rack != null)
			{
				Object.Destroy(Cockpit.Rack.gameObject);
				Cockpit.Rack = null;
				Mod.Info("Grenade: took the grenades off the dash");
			}
		}
	}

	/// <summary>
	/// The grenades' rack on the dash. It is the game's own ammo rack (<see cref="MagazineManager"/>) with <see cref="DashGrenade"/>s in
	/// it, so the game does the rest as it does for magazines: ammo boxes and the Extra Magazines stat add grenades up to the rack's room,
	/// a grenade let go near its place is put back, one dropped out of sight flies back, and the padlock holds them during the opening
	/// ceasefire. It sits where the revolver's magazines sit (the guns' racks are all in that part of the dash), measured off the
	/// revolver's rack in the scene, which is there even though no gun is shown.
	/// </summary>
	internal static class Cockpit
	{
		/// <summary>The rack in the cockpit now, or null.</summary>
		internal static MagazineManager Rack;

		/// <summary>One cockpit pixel, in the cockpit's own units (about 0.05).</summary>
		internal static float Texel = 0.05f;

		/// <summary>The dash sprites' lit material, sorting layer and order: the magazines', so the grenades sit among them.</summary>
		internal static Material Lit;
		internal static int SortingLayer;
		internal static int SortingOrder;
		internal static int Layer;

		/// <summary>Cockpit pixels between two grenades on the dash.</summary>
		internal const int Spacing = 20;

		/// <summary>How far below the dash (in the rack's units) a dropped grenade, pin or spoon is out of sight: the footwell.</summary>
		internal const float FootwellY = -5f;

		// Where the revolver's first magazine sits in the 0.419 cockpit, in CarInterior's space, for when no rack can be measured.
		private static readonly Vector3 FallbackAnchor = new Vector3(6.69f, 1.4f, 0.49f);

		internal static Sprite BodySprite;
		internal static Sprite SpoonSprite;
		internal static Sprite PinSprite;
		private static readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();

		/// <summary>Builds the rack if there is a cockpit to put it in.</summary>
		internal static void TryBuild()
		{
			Grabber grabber = Object.FindFirstObjectByType<Grabber>();
			if (grabber == null || grabber.viewCamera == null)
			{
				return;
			}
			Transform interior = null;
			SpriteRenderer reference = null;
			Vector3 anchor = FallbackAnchor;
			NAmmo ammo = ReferenceAmmo(out Transform gunParent);
			if (ammo != null)
			{
				interior = gunParent;
				reference = Sprite(ammo);
			}
			if (interior == null)
			{
				interior = grabber.transform.parent;
			}
			if (interior == null)
			{
				return;
			}
			if (reference == null)
			{
				reference = AnyLitSprite(interior);
			}
			if (reference != null)
			{
				Lit = reference.sharedMaterial;
				SortingLayer = reference.sortingLayerID;
				SortingOrder = reference.sortingOrder;
				Layer = reference.gameObject.layer;
			}
			else
			{
				Lit = null;
				Layer = interior.gameObject.layer;
			}
			if (ammo != null && reference != null)
			{
				// The magazine's middle across, its foot, at its depth: where the first grenade stands.
				Bounds b = reference.bounds;
				anchor = interior.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
				float scale = Mathf.Max(1e-4f, Mathf.Abs(interior.lossyScale.x));
				float measured = reference.sprite.bounds.size.x * Mathf.Abs(reference.transform.lossyScale.x) / Mathf.Max(1f, reference.sprite.rect.width) / scale;
				Texel = measured > 0.02f && measured < 0.12f ? measured : 0.05f;
			}
			else
			{
				Texel = 0.05f;
			}
			Build(interior, anchor + new Vector3(Mod.RackX.Value, Mod.RackY.Value, 0f));
			Mod.Info($"Grenade: {Rack.Count} grenades on the dash at {anchor} in '{interior.name}' (measured off {(ammo != null ? "'" + ammo.name + "'" : "nothing, so the 0.419 place")}), cockpit pixel {Texel:0.0000}, material '{(Lit != null ? Lit.name : "none")}', sorting {SortingLayer}/{SortingOrder}");
		}

		/// <summary>A magazine in the revolver's rack (else any gun's), and the object the guns hang in: the cockpit.</summary>
		private static NAmmo ReferenceAmmo(out Transform interior)
		{
			interior = null;
			NAmmo best = null;
			foreach (Gun gun in Object.FindObjectsByType<Gun>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				MagazineManager rack = gun.GetComponentInChildren<MagazineManager>(true);
				if (rack == null)
				{
					continue;
				}
				foreach (NAmmo ammo in rack.GetComponentsInChildren<NAmmo>(true))
				{
					// The rack's spare copy is switched off; the authored ones are not.
					if (ammo.gameObject.activeSelf && Sprite(ammo) != null)
					{
						if (best == null || gun.RequiredGunType == GunType.REVOLVER)
						{
							best = ammo;
							interior = gun.transform.parent;
						}
						break;
					}
				}
				if (best != null && gun.RequiredGunType == GunType.REVOLVER)
				{
					break;
				}
			}
			return best;
		}

		private static SpriteRenderer Sprite(Component c)
		{
			foreach (SpriteRenderer r in c.GetComponentsInChildren<SpriteRenderer>(true))
			{
				if (r.sprite != null)
				{
					return r;
				}
			}
			return null;
		}

		private static SpriteRenderer AnyLitSprite(Transform interior)
		{
			foreach (SpriteRenderer r in interior.GetComponentsInChildren<SpriteRenderer>(false))
			{
				if (r.sprite != null && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("SpriteLit"))
				{
					return r;
				}
			}
			return null;
		}

		/// <summary>
		/// The rack object, its grenades, then the game's rack component, all made switched off and switched on together, so the rack wakes
		/// to find its grenades already in place and takes them as its authored ones (it copies the first for the ones it adds later).
		/// </summary>
		private static void Build(Transform interior, Vector3 anchor)
		{
			EnsureSprites();
			var go = new GameObject("GrenadeRack");
			go.SetActive(false);
			go.layer = Layer;
			go.transform.SetParent(interior, false);
			go.transform.localPosition = anchor;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = Vector3.one;
			int count = Mathf.Clamp(Mod.Grenades.Value, 1, 3);
			for (int i = 0; i < count; i++)
			{
				MakeGrenade(go.transform, new Vector3(i * Spacing * Texel, 0f, 0f), i);
			}
			MagazineManager rack = go.AddComponent<MagazineManager>();
			rack.entitiesPerMagazine = 1;
			rack.magazinesPerKill = 0;
			// Spares stand behind the front row, a little up and to the right so their tops show.
			rack.extraSlotOffset = new Vector3(3f * Texel, 2f * Texel, 0.2f);
			rack.snapRadius = Spacing * Texel * 0.45f;
			rack.recoverDelay = 1.5f;
			rack.returnDuration = 0.25f;
			Rack = rack;
			go.SetActive(true);
		}

		/// <summary>One grenade: the body you grab, the spoon on its side and the pin you pull, each a sprite on the same canvas.</summary>
		private static void MakeGrenade(Transform rack, Vector3 at, int index)
		{
			var go = new GameObject("Grenade");
			go.layer = Layer;
			go.transform.SetParent(rack, false);
			go.transform.localPosition = at;
			// Toward the camera is -z on the dash: the parts are kept a little apart in depth, the way the game keeps its own stacked sprites.
			Part(go.transform, "Body", BodySprite, Vector3.zero, 0);
			Part(go.transform, "Spoon", SpoonSprite, new Vector3(0f, 0f, -0.01f), 1);

			var grip = new GameObject("Grip");
			grip.layer = Layer;
			grip.transform.SetParent(go.transform, false);
			grip.transform.localPosition = new Vector3(0f, 7.5f * Texel, -0.1f);
			InteriorSquareCollider body = grip.AddComponent<InteriorSquareCollider>();
			body.width = 13f * Texel;
			body.height = 15f * Texel;
			body.hitBoxExpand = 0.1f;
			body.sortingLayer = 0;

			// The pin's object sits at the ring, so it turns round the ring when it falls; its picture is moved back so the canvases line up.
			// The object (and its collider) stands in front of the body's collider; the picture is drawn just in front of the spoon.
			Vector3 ring = new Vector3((Art.RingX - Art.BodyX) * Texel, Art.RingY * Texel, -0.12f);
			var pin = new GameObject("Pin");
			pin.layer = Layer;
			pin.transform.SetParent(go.transform, false);
			pin.transform.localPosition = ring;
			Part(pin.transform, "Sprite", PinSprite, new Vector3(-ring.x, -ring.y, 0.1f), 2);
			InteriorCircleCollider pull = pin.AddComponent<InteriorCircleCollider>();
			pull.radius = 3.5f * Texel;
			pull.hitBoxExpand = 0.05f;
			// Above the grenade's body (0), so the ring is what the hand takes when it is over it.
			pull.sortingLayer = 1;
			pin.AddComponent<Pin>();

			go.AddComponent<DashGrenade>();
		}

		/// <summary>A sprite of the grenade on its object, lit like the dash.</summary>
		internal static SpriteRenderer Part(Transform parent, string name, Sprite sprite, Vector3 at, int above)
		{
			var go = new GameObject(name);
			go.layer = Layer;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = at;
			SpriteRenderer r = go.AddComponent<SpriteRenderer>();
			r.sprite = sprite;
			Material m = LitFor(sprite.texture);
			if (m != null)
			{
				r.sharedMaterial = m;
			}
			r.sortingLayerID = SortingLayer;
			r.sortingOrder = SortingOrder + above;
			return r;
		}

		/// <summary>
		/// The dash's lit material with this texture in it. The lit shader reads the material's texture, not the sprite's, so each dash
		/// sprite has its own copy of the material (as the game's do); one per texture is enough here.
		/// </summary>
		private static Material LitFor(Texture texture)
		{
			if (Lit == null)
			{
				return Effects.Unlit;
			}
			if (materials.TryGetValue(texture, out Material m) && m != null && m.shader == Lit.shader)
			{
				return m;
			}
			m = new Material(Lit) { name = "Grenade " + texture.name, hideFlags = HideFlags.HideAndDontSave };
			foreach (string property in new[] { "_BaseMap", "_MainTex" })
			{
				if (m.HasProperty(property))
				{
					m.SetTexture(property, texture);
					m.SetTextureScale(property, Vector2.one);
					m.SetTextureOffset(property, Vector2.zero);
				}
			}
			foreach (string property in new[] { "_BaseColor", "_Color" })
			{
				if (m.HasProperty(property))
				{
					m.SetColor(property, Color.white);
				}
			}
			materials[texture] = m;
			return m;
		}

		internal static void EnsureSprites()
		{
			float ppu = 1f / Texel;
			// Every part on the same canvas with the same pivot, the foot of the body, so they line up wherever they are put.
			Vector2 pivot = new Vector2((Art.BodyX + 0.5f) / Art.Width, 0.5f / Art.Height);
			if (BodySprite == null || Mathf.Abs(BodySprite.pixelsPerUnit - ppu) > 0.01f)
			{
				BodySprite = Make("GrenadeBody", Art.Body(), pivot, ppu);
				SpoonSprite = Make("GrenadeSpoon", Art.Spoon(), pivot, ppu);
				PinSprite = Make("GrenadePin", Art.Pin(), pivot, ppu);
			}
		}

		internal static Sprite Make(string name, Color32[] pixels, Vector2 pivot, float ppu, int w = Art.Width, int h = Art.Height)
		{
			var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.HideAndDontSave,
				name = name
			};
			texture.SetPixels32(pixels);
			texture.Apply();
			Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
			sprite.name = name;
			sprite.hideFlags = HideFlags.HideAndDontSave;
			return sprite;
		}
	}
}
