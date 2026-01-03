using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySerializationBridge.Interfaces;

namespace UnitySerializationBridge
{
	[BepInPlugin(GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class BridgeManager : BaseUnityPlugin
	{
		const string GUID = "pixelguy.pixelmodding.unity.bridgemanager";
		internal ConfigEntry<bool> enableDebugLogs;
		public static BridgeManager Instance { get; private set; }
		void Awake()
		{
			// Init
			Instance = this;
			var h = new Harmony(GUID);
			h.PatchAll();

			// Config
			enableDebugLogs = Config.Bind("Debugging", "Enable Debug Logs", true, "If True, the library will log all the registered types on initialization.");

			// DEBUG
			RegisterNamespace(Assembly.GetExecutingAssembly(), "UnitySerializationBridge.Test");
			StartCoroutine(WaitForGameplay());
		}

		IEnumerator WaitForGameplay()
		{
			while (SceneManager.GetActiveScene().name != "MainMenu") yield return null;

			var newObject = new GameObject("OurTestSubject").AddComponent<Test.TestComponentToSerialize>();
		}

		/// <summary>
		/// Call this to scan a namespace for classes that need bridging.
		/// <param name="assembly">The assembly to be scanned.</param>
		/// <param name="targetNamespace">The namespace to reduce the search time.</param>
		/// <remarks>Namespace format is what you use in your code (eg. "MyPlugin.Core.Components").</remarks>
		/// </summary>
		public void RegisterNamespace(Assembly assembly, string targetNamespace)
		{
			// Get all the types needed from the assembly
			foreach (var type in AccessTools.GetTypesFromAssembly(assembly).Where(t => t.Namespace == targetNamespace))
			{
				// If it is a MonoBehaviour
				if (typeof(MonoBehaviour).IsAssignableFrom(type))
				{
					// Check if this MonoBehaviour has any IAutoSerializable fields
					if (AccessTools.GetDeclaredFields(type)
						.Any(f => typeof(IAutoSerializable).IsAssignableFrom(f.FieldType)))
					{
						Core.SerializationRegistry.componentTypesToAddBridgeSerializer.Add(type);
						Core.SerializationRegistry.Register(type);
					}
				}
			}
		}

		// Internal methods
		internal void Log(object message)
		{
			if (!enableDebugLogs.Value) return;
			Debug.Log(message, this);
		}
	}
}
