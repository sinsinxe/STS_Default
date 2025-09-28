#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class STS_ClearPlayerPrefs
{
	[MenuItem("Tools/[STS] Clear PlayerPrefs")]
	public static void ClearPlayerPrefs()
	{
		PlayerPrefs.DeleteAll();
		PlayerPrefs.Save();
		Debug.Log("✅ PlayerPrefs 모두 초기화 완료");
	}
}
#endif
