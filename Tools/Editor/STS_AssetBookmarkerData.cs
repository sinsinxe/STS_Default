// --------------------------------------------------------------------------------
// 파일: STS_AssetBookmarkerData.cs
// 역할: 북마크 데이터를 저장하고 관리하는 ScriptableObject 및 관련 데이터 클래스
// --------------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 개별 북마크 항목을 나타내는 클래스입니다.
/// 에셋의 GUID를 저장하여 파일 이름이나 위치가 변경되어도 안전하게 참조를 유지합니다.
/// </summary>
[System.Serializable]
public class BookmarkItem
{
	public string guid;
	public string alias; // 사용자가 지정하는 별칭

	// 생성자
	public BookmarkItem(string guid)
	{
		this.guid = guid;
		this.alias = null;
	}
}

/// <summary>
/// 북마크 항목들을 그룹화하는 클래스입니다.
/// </summary>
[System.Serializable]
public class BookmarkGroup
{
	public string name;
	public List<BookmarkItem> items = new List<BookmarkItem>();

	// 생성자
	public BookmarkGroup(string name)
	{
		this.name = name;
	}
}

/// <summary>
/// 모든 북마크 데이터를 담는 ScriptableObject 클래스입니다.
/// 이 클래스를 통해 '.asset' 파일로 데이터가 저장됩니다.
/// </summary>
// ✨ 개선: [CreateAssetMenu] 속성을 추가하여 우클릭 메뉴로 생성할 수 있게 합니다.
[CreateAssetMenu(fileName = "STS_AssetBookmarkerData", menuName = "STS Tools/Asset Bookmarker Data")]
public class STS_AssetBookmarkerData : ScriptableObject
{
	public List<BookmarkGroup> groups = new List<BookmarkGroup>();
}
