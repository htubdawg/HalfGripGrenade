// A stand-in for the parts of HalfGripTools 1.0.6 this mod uses, with the same signatures (copied from its source), so the mod can be
// compile-checked where building HalfGripTools itself is not possible (tools/check.sh --stub). It does nothing; never ship it.
using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfGripTools
{
	public abstract class HalfGripMod
	{
		public abstract void Load(ModContext ctx);
		public virtual void Unload() { }
	}

	public sealed class ModContext
	{
		public string Folder { get; }
		public ManualLogSource Log { get; }
		public ConfigFile Config { get; }
		public ModItems Items { get; }
		public void PatchAll() { }
		public void OnSceneLoaded(Action<Scene> callback) { }
		public void OnUnload(Action callback) { }
	}

	public sealed class ModItems
	{
		public void Add(string id, Func<ItemCatalog, UpgradeComponent> create) { }
		public void OnInject(Action<ItemCatalog> hook) { }
		public UpgradeComponent Get(string id) => null;
		public T Clone<T>(T basis, string id) where T : UpgradeComponent => basis;
	}

	public static class VoxelMesh
	{
		public static Mesh Build(Texture2D texture, float pixel, float thickness, float axisRow, string name) => null;
		public static GameObject MakeObject(string name, Mesh mesh, Texture2D texture, Material like, Transform parent, int layer) => null;
	}
}
