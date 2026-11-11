using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// A loose piece in the cockpit that nobody grabs: the spoon flying off, a pixel of smoke from the fuse, the flash and smoke of a
	/// grenade going off in the car. It moves in its parent's plane (the dash's: x right, y up), with gravity, a spin and a life, and
	/// shrinks away at the end of it.
	/// </summary>
	internal sealed class Bit : MonoBehaviour
	{
		private Vector2 velocity;
		private float spin;
		private float gravity;
		private float life;
		private float age;
		private float grow;
		private Vector3 size;

		/// <summary>A copy of <paramref name="like"/> (same sprite, material, sorting) that flies off from where it is.</summary>
		internal static void Spawn(SpriteRenderer like, Vector2 velocity, float spin, float life)
		{
			Transform parent = like.transform.parent != null ? like.transform.parent.parent : null;
			var go = new GameObject(like.name + " (flying)");
			go.layer = like.gameObject.layer;
			go.transform.SetParent(parent, false);
			// It turns round the middle of its picture, not round the canvas's pivot (the grenade's foot), so the picture hangs off a child.
			go.transform.SetPositionAndRotation(like.bounds.center, like.transform.rotation);
			var picture = new GameObject("Sprite");
			picture.layer = go.layer;
			picture.transform.SetParent(go.transform, false);
			picture.transform.position = like.transform.position;
			SpriteRenderer r = picture.AddComponent<SpriteRenderer>();
			r.sprite = like.sprite;
			r.sharedMaterial = like.sharedMaterial;
			r.sortingLayerID = like.sortingLayerID;
			r.sortingOrder = like.sortingOrder + 1;
			Bit bit = go.AddComponent<Bit>();
			bit.Init(velocity, spin, 30f, life, 0f);
		}

		/// <summary>One pixel of smoke rising from the fuse, drifting, growing a little and gone in under a second.</summary>
		internal static void Smoke(Transform parent, Vector3 at, float texel)
		{
			if (parent == null)
			{
				return;
			}
			var go = new GameObject("FuseSmoke");
			go.layer = Cockpit.Layer;
			go.transform.SetParent(parent, false);
			go.transform.position = at;
			SpriteRenderer r = go.AddComponent<SpriteRenderer>();
			r.sprite = Effects.Pixel(Random.value < 0.5f);
			r.sharedMaterial = Effects.Unlit;
			r.sortingLayerID = Cockpit.SortingLayer;
			r.sortingOrder = Cockpit.SortingOrder + 3;
			go.transform.localScale = Vector3.one * texel * (Random.value < 0.3f ? 2f : 1f);
			go.AddComponent<Bit>().Init(new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1.2f, 2.2f)), 0f, -1f, Random.Range(0.4f, 0.8f), 0.8f);
		}

		/// <summary>A sprite that bursts out of a point in the cockpit: the in-car blast's flash and smoke.</summary>
		internal static void Burst(Transform parent, Vector3 at, Sprite sprite, float scale, Vector2 velocity, float life, float grow, int above)
		{
			var go = new GameObject("Burst");
			go.layer = Cockpit.Layer;
			go.transform.SetParent(parent, false);
			go.transform.position = at;
			go.transform.localScale = Vector3.one * scale;
			SpriteRenderer r = go.AddComponent<SpriteRenderer>();
			r.sprite = sprite;
			r.sharedMaterial = Effects.Unlit;
			r.sortingLayerID = Cockpit.SortingLayer;
			r.sortingOrder = Cockpit.SortingOrder + above;
			go.AddComponent<Bit>().Init(velocity, Random.Range(-30f, 30f), 0f, life, grow);
		}

		private void Init(Vector2 v, float degreesPerSecond, float g, float seconds, float growth)
		{
			velocity = v;
			spin = degreesPerSecond;
			gravity = g;
			life = Mathf.Max(0.05f, seconds);
			grow = growth;
			size = transform.localScale;
		}

		private void Update()
		{
			float dt = Time.deltaTime;
			age += dt;
			if (age >= life || transform.localPosition.y < Cockpit.FootwellY - 5f)
			{
				Destroy(gameObject);
				return;
			}
			velocity.y -= gravity * dt;
			transform.localPosition += new Vector3(velocity.x, velocity.y, 0f) * dt;
			transform.Rotate(0f, 0f, spin * dt, Space.Self);
			float t = age / life;
			// Grows as it goes, then shrinks away over the last third of its life.
			float shrink = t < 0.66f ? 1f : Mathf.Clamp01((1f - t) / 0.34f);
			transform.localScale = size * (1f + grow * t) * shrink;
		}
	}
}
