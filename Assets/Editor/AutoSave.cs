using System.Collections;
using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
//tsubakiさん制作の自動保存機能  https://tsubakit1.hateblo.jp/entry/20140131/1391094449
//https://gist.github.com/tsubaki/8709502
public class AutoSave
{
	public static readonly string manualSaveKey = "autosave@manualSave";

	static double nextTime = 0;
	static bool isChangedHierarchy = false;

	static AutoSave ()
	{
		IsManualSave = true;
		EditorApplication.playmodeStateChanged += () =>
		{
			if ( IsAutoSave && !EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode) {

				IsManualSave = false;

				if (IsSavePrefab)
					AssetDatabase.SaveAssets ();
				if (IsSaveScene) {
					Debug.Log ("save scene " + System.DateTime.Now);
					EditorApplication.SaveScene ();
				}
				IsManualSave = true;
			}
			isChangedHierarchy = false;
		};

		nextTime = EditorApplication.timeSinceStartup + Interval;
		EditorApplication.update += () =>
		{
			if (isChangedHierarchy && nextTime < EditorApplication.timeSinceStartup) {
				nextTime = EditorApplication.timeSinceStartup + Interval;

				IsManualSave = false;

				if (IsSaveSceneTimer && IsAutoSave && !EditorApplication.isPlaying) {
					if (IsSavePrefab)
						AssetDatabase.SaveAssets ();
					if (IsSaveScene) {
						Debug.Log ("save scene " + System.DateTime.Now);
						EditorApplication.SaveScene ();
					}
				}
				isChangedHierarchy = false;
				IsManualSave = true;
			}
		};

		EditorApplication.hierarchyWindowChanged += ()=>
		{
			if(! EditorApplication.isPlaying)
				isChangedHierarchy = true;
		};
	}

	public static bool IsManualSave {
		get {
			return EditorPrefs.GetBool (manualSaveKey);
		}
		private set {
			EditorPrefs.SetBool (manualSaveKey, value);
		}
	}


	private static readonly string autoSave = "auto save";
	static bool IsAutoSave {
		get {
			string value = EditorUserSettings.GetConfigValue (autoSave);
			return!string.IsNullOrEmpty (value) && value.Equals ("True");
		}
		set {
			EditorUserSettings.SetConfigValue (autoSave, value.ToString ());
		}
	}

	private static readonly string autoSavePrefab = "auto save prefab";
	static bool IsSavePrefab {
		get {
			string value = EditorUserSettings.GetConfigValue (autoSavePrefab);
			return!string.IsNullOrEmpty (value) && value.Equals ("True");
		}
		set {
			EditorUserSettings.SetConfigValue (autoSavePrefab, value.ToString ());
		}
	}

	private static readonly string autoSaveScene = "auto save scene";
	static bool IsSaveScene {
		get {
			string value = EditorUserSettings.GetConfigValue (autoSaveScene);
			return!string.IsNullOrEmpty (value) && value.Equals ("True");
		}
		set {
			EditorUserSettings.SetConfigValue (autoSaveScene, value.ToString ());
		}
	}

	private static readonly string autoSaveSceneTimer = "auto save scene timer";
	static bool IsSaveSceneTimer {
		get {
			string value = EditorUserSettings.GetConfigValue (autoSaveSceneTimer);
			return!string.IsNullOrEmpty (value) && value.Equals ("True");
		}
		set {
			EditorUserSettings.SetConfigValue (autoSaveSceneTimer, value.ToString ());
		}
	}

	private static readonly string autoSaveInterval = "save scene interval";
	static int Interval {
		get {

			string value = EditorUserSettings.GetConfigValue (autoSaveInterval);
			if (value == null) {
				value = "60";
			}
			return int.Parse (value);
		}
		set {
			if (value < 60)
				value = 60;
			EditorUserSettings.SetConfigValue (autoSaveInterval, value.ToString ());
		}
	}


	[PreferenceItem("Auto Save")] 
	static void ExampleOnGUI ()
	{
		bool isAutoSave = EditorGUILayout.BeginToggleGroup ("auto save", IsAutoSave);

		IsAutoSave = isAutoSave;
		EditorGUILayout.Space ();

		IsSavePrefab = EditorGUILayout.ToggleLeft ("save prefab", IsSavePrefab);
		IsSaveScene = EditorGUILayout.ToggleLeft ("save scene", IsSaveScene);
		IsSaveSceneTimer = EditorGUILayout.BeginToggleGroup ("save scene interval", IsSaveSceneTimer);
		Interval = EditorGUILayout.IntField ("interval(sec) min60sec", Interval);
		EditorGUILayout.EndToggleGroup ();
		EditorGUILayout.EndToggleGroup ();
	}

	[MenuItem("File/Backup/Backup")]
	public static void Backup ()
	{
		string expoertPath = "Backup/" + EditorApplication.currentScene;

		Directory.CreateDirectory (Path.GetDirectoryName (expoertPath));

		if( string.IsNullOrEmpty(EditorApplication.currentScene))
			return;

		byte[] data = File.ReadAllBytes (EditorApplication.currentScene);
		File.WriteAllBytes (expoertPath, data);
	}

	[MenuItem("File/Backup/Rollback")]
	public static void RollBack ()
	{
		string expoertPath = "Backup/" + EditorApplication.currentScene;
		
		byte[] data = File.ReadAllBytes (expoertPath);
		File.WriteAllBytes (EditorApplication.currentScene, data);
		AssetDatabase.Refresh (ImportAssetOptions.Default);
	}

}