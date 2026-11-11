using UnityEngine;

namespace HalfGripGrenade
{
	/// <summary>
	/// Sounds and pictures. The blast is the game's own: the big explosion an enemy car makes when it is wrecked
	/// (<c>EnemyCarEffectController.bigExplosionEffect</c>) and the wrecked car's sound (<c>EnemyController.destroySound</c>), found on the
	/// race's cars. Where there are none (an empty playground), a small one made here stands in. The pin's pull is the SMG's slide;
	/// the spoon's ping, the fuse's hiss and the bounce are made here.
	/// </summary>
	internal static class Effects
	{
		private static GameObject explosionPrefab;
		private static AudioClip destroySound;
		private static AudioClip[] slideSounds;
		private static bool searched;
		private static Material unlit;
		private static Material soft;
		private static Sprite lightPixel;
		private static Sprite darkPixel;
		private static Sprite flash;
		private static AudioClip ping;
		private static AudioClip hiss;
		private static AudioClip clink;
		private static AudioClip boom;

		/// <summary>A new scene: the cars (and their explosion) are new too.</summary>
		internal static void OnSceneLoaded()
		{
			explosionPrefab = null;
			destroySound = null;
			slideSounds = null;
			searched = false;
		}

		/// <summary>The plain sprite material, for things that glow or fade (smoke, the flash): unlit, and its colour is the sprite's.</summary>
		internal static Material Unlit
		{
			get
			{
				if (unlit == null)
				{
					Shader shader = Shader.Find("Sprites/Default");
					if (shader != null)
					{
						unlit = new Material(shader) { name = "Grenade unlit", hideFlags = HideFlags.HideAndDontSave };
					}
				}
				return unlit;
			}
		}

		/// <summary>A one-pixel sprite of smoke, light or dark; scaled to the size wanted.</summary>
		internal static Sprite Pixel(bool light)
		{
			if (lightPixel == null)
			{
				lightPixel = Cockpit.Make("GrenadeSmokeLight", new[] { Art.Smoke(true) }, new Vector2(0.5f, 0.5f), 1f, 1, 1);
				darkPixel = Cockpit.Make("GrenadeSmokeDark", new[] { Art.Smoke(false) }, new Vector2(0.5f, 0.5f), 1f, 1, 1);
			}
			return light ? lightPixel : darkPixel;
		}

		/// <summary>The in-car blast's flash, 40 pixels square at one unit across.</summary>
		internal static Sprite Flash
		{
			get
			{
				if (flash == null)
				{
					flash = Cockpit.Make("GrenadeFlash", Art.Flash(40), new Vector2(0.5f, 0.5f), 40f, 40, 40);
				}
				return flash;
			}
		}

		private static void Search()
		{
			if (searched)
			{
				return;
			}
			searched = true;
			foreach (EnemyCarEffectController car in Object.FindObjectsByType<EnemyCarEffectController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (car.bigExplosionEffect != null)
				{
					explosionPrefab = car.bigExplosionEffect;
					break;
				}
			}
			foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (enemy.destroySound != null)
				{
					destroySound = enemy.destroySound;
					break;
				}
			}
			foreach (NAutomaticGun gun in Object.FindObjectsByType<NAutomaticGun>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (gun.slidePullSounds != null && gun.slidePullSounds.Length > 0)
				{
					slideSounds = gun.slidePullSounds;
					break;
				}
			}
			Mod.Info($"Grenade: explosion '{(explosionPrefab != null ? explosionPrefab.name : "none, a small one of its own")}', sound '{(destroySound != null ? destroySound.name : "none, its own")}', pin sound '{(slideSounds != null ? slideSounds[0].name : "its own")}'");
		}

		internal static AudioClip PinPull
		{
			get
			{
				Search();
				return slideSounds != null && slideSounds.Length > 0 ? slideSounds[Random.Range(0, slideSounds.Length)] : Clink;
			}
		}

		/// <summary>The spoon leaving: a bright ring of thin steel.</summary>
		internal static AudioClip Ping => ping != null ? ping : ping = Synth("grenade ping", 0.6f, (t, noise, s) =>
			(Mathf.Sin(6.2831853f * 2900f * t) * 0.6f + Mathf.Sin(6.2831853f * 4170f * t) * 0.35f) * Mathf.Exp(-t * 9f) + noise * Mathf.Exp(-t * 300f) * 0.6f);

		/// <summary>The fuse burning: a soft hiss with a flutter in it, made to loop.</summary>
		internal static AudioClip Hiss => hiss != null ? hiss : hiss = Synth("grenade fuse", 1f, (t, noise, s) =>
		{
			s[0] += (noise - s[0]) * 0.35f;
			return (noise - s[0]) * 0.5f * (0.8f + 0.2f * Mathf.Sin(6.2831853f * 13f * t));
		}, false);

		/// <summary>The grenade striking the road: a short dull clank.</summary>
		internal static AudioClip Clink => clink != null ? clink : clink = Synth("grenade bounce", 0.25f, (t, noise, s) =>
			(Mathf.Sin(6.2831853f * 820f * t) * 0.5f + Mathf.Sin(6.2831853f * 1460f * t) * 0.3f) * Mathf.Exp(-t * 28f) + noise * Mathf.Exp(-t * 200f) * 0.5f);

		private static AudioClip Boom => boom != null ? boom : boom = Synth("grenade boom", 2.2f, (t, noise, s) =>
		{
			float crack = noise * Mathf.Exp(-t * 60f) * 1.6f;
			s[0] += (noise - s[0]) * Mathf.Lerp(0.4f, 0.015f, Mathf.Clamp01(t / 1.2f));
			float rumble = s[0] * 3f * Mathf.Exp(-t * 2f);
			float thump = Mathf.Sin(6.2831853f * (70f - 30f * Mathf.Min(t, 1f)) * t) * Mathf.Exp(-t * 4f) * 1.2f;
			return crack + rumble + thump;
		});

		internal static void Play2D(AudioClip clip, float volume, float pitch = 1f)
		{
			if (clip == null)
			{
				return;
			}
			var go = new GameObject("Grenade sound");
			AudioSource source = go.AddComponent<AudioSource>();
			source.outputAudioMixerGroup = SoundController.SfxGroup;
			source.spatialBlend = 0f;
			source.clip = clip;
			source.volume = volume;
			source.pitch = pitch;
			source.Play();
			Object.Destroy(go, clip.length / Mathf.Max(0.05f, pitch) + 0.1f);
		}

		/// <summary>A looping sound heard as if in the car (the cockpit is far from the world's listener, so not placed in the world).</summary>
		internal static AudioSource Loop2D(GameObject on, AudioClip clip, float volume)
		{
			if (clip == null || on == null)
			{
				return null;
			}
			AudioSource source = on.AddComponent<AudioSource>();
			source.outputAudioMixerGroup = SoundController.SfxGroup;
			source.spatialBlend = 0f;
			source.loop = true;
			source.clip = clip;
			source.volume = volume;
			source.Play();
			return source;
		}

		/// <summary>A looping sound that follows a thing in the world.</summary>
		internal static AudioSource Loop3D(GameObject on, AudioClip clip, float volume)
		{
			AudioSource source = Loop2D(on, clip, volume);
			if (source != null)
			{
				source.spatialBlend = 1f;
				source.minDistance = 8f;
				source.maxDistance = 150f;
			}
			return source;
		}

		internal static void BounceAt(Vector3 at, float hardness)
		{
			SoundController.PlaySoundAtPosition(Clink, at, Mathf.Clamp01(0.3f + hardness * 0.02f), 6f, 160f);
		}

		/// <summary>The blast in the world: the game's car explosion and its sound.</summary>
		internal static void Explosion(Vector3 at)
		{
			Search();
			if (explosionPrefab != null)
			{
				GameObject made = Object.Instantiate(explosionPrefab, at, Quaternion.identity);
				Object.Destroy(made, 10f);
			}
			else
			{
				SmallExplosion(at);
			}
			SoundController.PlaySoundAtPosition(destroySound != null ? destroySound : Boom, at, 1f, 25f, 500f);
		}

		/// <summary>The blast in the car: the explosion's sound, right here.</summary>
		internal static void BoomInCar()
		{
			Search();
			Play2D(destroySound != null ? destroySound : Boom, 1f);
		}

		private static Material Soft()
		{
			if (soft == null && Unlit != null)
			{
				const int size = 32;
				var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
				var pixels = new Color32[size * size];
				float half = (size - 1) * 0.5f;
				for (int y = 0; y < size; y++)
				{
					for (int x = 0; x < size; x++)
					{
						float r = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
						// A hard-edged puff, in three steps, to keep to the game's pixel look.
						byte a = (byte)(r < 0.55f ? 255 : r < 0.8f ? 170 : r < 1f ? 80 : 0);
						pixels[y * size + x] = new Color32(255, 255, 255, a);
					}
				}
				texture.SetPixels32(pixels);
				texture.Apply();
				soft = new Material(Unlit) { name = "Grenade puff", hideFlags = HideFlags.HideAndDontSave, mainTexture = texture };
			}
			return soft;
		}

		/// <summary>When the race has no wrecked-car explosion to borrow: a flash, a fireball and smoke, about a car length across.</summary>
		private static void SmallExplosion(Vector3 at)
		{
			Material material = Soft();
			if (material == null)
			{
				return;
			}
			var root = new GameObject("GrenadeExplosion");
			root.transform.position = at;
			Burst(root.transform, material, 40, 0.9f, 22f, 7f, new Color(1f, 0.85f, 0.4f, 1f), new Color(0.6f, 0.15f, 0.05f, 0f), -0.2f);
			Burst(root.transform, material, 30, 3.5f, 9f, 9f, new Color(0.3f, 0.28f, 0.26f, 0.9f), new Color(0.15f, 0.15f, 0.15f, 0f), -0.05f);
			var light = new GameObject("flash").AddComponent<Light>();
			light.transform.SetParent(root.transform, false);
			light.type = LightType.Point;
			light.color = new Color(1f, 0.65f, 0.3f);
			light.range = 60f;
			light.intensity = 30f;
			Object.Destroy(light.gameObject, 0.15f);
			Object.Destroy(root, 6f);
		}

		private static void Burst(Transform parent, Material material, int count, float life, float speed, float size, Color from, Color to, float gravity)
		{
			var go = new GameObject("puff");
			go.transform.SetParent(parent, false);
			ParticleSystem ps = go.AddComponent<ParticleSystem>();
			ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			ParticleSystem.MainModule main = ps.main;
			main.playOnAwake = false;
			main.loop = false;
			main.duration = 0.1f;
			main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
			main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
			main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
			main.gravityModifier = gravity;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.maxParticles = count;
			ParticleSystem.EmissionModule emission = ps.emission;
			emission.rateOverTime = 0f;
			emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
			ParticleSystem.ShapeModule shape = ps.shape;
			shape.shapeType = ParticleSystemShapeType.Sphere;
			shape.radius = 1f;
			ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
			colour.enabled = true;
			var gradient = new Gradient();
			gradient.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) }, new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(to.a, 1f) });
			colour.color = gradient;
			go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
			ps.Play();
		}

		private delegate float Voice(float t, float noise, float[] state);

		/// <summary>A sound made from a formula, mono at 44.1 kHz, softly clipped; faded at both ends unless it is to loop.</summary>
		private static AudioClip Synth(string name, float seconds, Voice voice, bool fade = true)
		{
			const int rate = 44100;
			int count = (int)(rate * seconds);
			var data = new float[count];
			var state = new float[2];
			var rng = new System.Random(name.GetHashCode());
			for (int i = 0; i < count; i++)
			{
				float t = i / (float)rate;
				float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
				float edge = fade ? Mathf.Clamp01(t / 0.003f) * Mathf.Clamp01((seconds - t) / 0.08f) : 1f;
				data[i] = (float)System.Math.Tanh(voice(t, noise, state) * edge);
			}
			AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
			clip.SetData(data, 0);
			clip.hideFlags = HideFlags.HideAndDontSave;
			return clip;
		}
	}
}
