using UnityEngine;
using UnityEditor;
using System.Collections;


public class SceneBackup : UnityEditor.AssetModificationProcessor
{//tsubakiさん制作の自動保存機能  https://tsubakit1.hateblo.jp/entry/20140131/1391094449
//https://gist.github.com/tsubaki/8709502
	static string[] OnWillSaveAssets (string[] paths)
	{
		bool manualSave = AutoSave.IsManualSave;
		if (manualSave) {
			AutoSave.Backup ();
		}

		return paths;
	}
}