// A stand-in for the parts of HalfGripTools 1.0.6 this mod uses, with the same signatures (copied from its source), so the mod can be
// compile-checked where building HalfGripTools itself is not possible (tools/check.sh --stub). It does nothing; never ship it.
using System;
using System.Collections.Generic;
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
		public ModNet Net { get; }
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

	public enum NetPhase { None = 0, Lobby = 1, Loading = 2, Racing = 3, Results = 4 }

	public struct NetPlayer
	{
		public ulong Id;
		public string Name;
		public bool IsHost;
		public bool IsLocal;
	}

	public static class Net
	{
		public const ulong Everyone = 0UL;
		public const int MaxReliable = 3800;
		public const int MaxUnreliable = 1100;
		public static bool Available => false;
		public static bool InRoom { get; private set; }
		public static bool IsHost { get; private set; }
		public static ulong LocalId { get; private set; }
		public static ulong HostId { get; private set; }
		public static NetPhase Phase { get; private set; }
		public static string ModeId { get; private set; }
		public static IList<NetPlayer> Players => new NetPlayer[0];
#pragma warning disable CS0067
		public static event Action<NetPlayer> PlayerJoined;
		public static event Action<NetPlayer> PlayerLeft;
		public static event Action<NetPhase> PhaseChanged;
#pragma warning restore CS0067
	}

	public sealed class ModNet
	{
		public bool Send(byte[] data, ulong to = Net.Everyone, bool reliable = true) => false;
		public bool SendToHost(byte[] data, bool reliable = true) => false;
		public void OnReceive(Action<ulong, byte[]> handler) { }
	}
}
