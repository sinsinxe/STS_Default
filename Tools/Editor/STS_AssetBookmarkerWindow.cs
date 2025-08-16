// --------------------------------------------------------------------------------
// 파일: STS_AssetBookmarkerWindow.cs
// 역할: Asset Bookmarker 툴의 메인 에디터 창 UI 및 로직 (개선 버전 4)
// --------------------------------------------------------------------------------
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class STS_AssetBookmarkerWindow : EditorWindow
{
	private STS_AssetBookmarkerData data;
	private Vector2 scrollPosition;
	private string newGroupName = "New Group";
	private int selectedTabIndex = 0;

	private GUIStyle dropAreaStyle;
	private GUIStyle deleteButtonStyle;
	private GUIStyle groupTitleStyle; // 그룹 제목 스타일

	[MenuItem("Tools/[STS] Asset Bookmarker")]
	public static void ShowWindow()
	{
		GetWindow<STS_AssetBookmarkerWindow>("[STS] Asset Bookmarker");
	}

	private void OnEnable()
	{
		LoadData();
	}

	private void LoadData()
	{
		string[] guids = AssetDatabase.FindAssets("t:STS_AssetBookmarkerData");
		if (guids.Length > 0)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[0]);
			data = AssetDatabase.LoadAssetAtPath<STS_AssetBookmarkerData>(path);
		}
		else
		{
			data = CreateInstance<STS_AssetBookmarkerData>();
			string path = "Assets/STS_Default/Tools/Editor/STS_AssetBookmarkerData.asset";
			string directory = Path.GetDirectoryName(path);
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			AssetDatabase.CreateAsset(data, path);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			Debug.Log("STS_AssetBookmarkerData.asset 파일이 생성되었습니다: " + path);
		}

		if (data.groups.Count == 0)
		{
			data.groups.Add(new BookmarkGroup("Default"));
			EditorUtility.SetDirty(data);
		}
	}

	private void OnGUI()
	{
		if (data == null)
		{
			EditorGUILayout.LabelField("데이터를 로드할 수 없습니다. 창을 다시 열어주세요.");
			if (GUILayout.Button("데이터 로드 시도"))
			{
				LoadData();
			}
			return;
		}

		InitializeStyles();
		DrawHeaderControls();
		DrawGroupTabs();
        
		GUILayout.Space(3);

		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
		
		HandleContentDragAndDrop();
        
		if (data.groups.Count > 0 && data.groups[selectedTabIndex].items.Count > 0)
		{
			GUILayout.Space(3);
		}

		if (data.groups.Count > 0 && selectedTabIndex < data.groups.Count)
		{
			DrawGroupContent(data.groups[selectedTabIndex]);
		}
		EditorGUILayout.EndScrollView();
        
		DrawFooterControls();
	}

	private void InitializeStyles()
	{
		if (dropAreaStyle == null)
		{
			dropAreaStyle = new GUIStyle(GUI.skin.box);
			Texture2D blueTexture = new Texture2D(1, 1);
			blueTexture.SetPixel(0, 0, new Color(0.2f, 0.3f, 0.5f, 1f));
			blueTexture.Apply();

			dropAreaStyle.normal.background = blueTexture;
			dropAreaStyle.normal.textColor = Color.gray;
			dropAreaStyle.alignment = TextAnchor.MiddleCenter;
			dropAreaStyle.fontStyle = FontStyle.Bold;
		}

		if (deleteButtonStyle == null)
		{
			deleteButtonStyle = new GUIStyle(GUI.skin.button);
			deleteButtonStyle.normal.textColor = Color.white;
			deleteButtonStyle.padding = new RectOffset(0, 0, 0, 0);
			deleteButtonStyle.margin = new RectOffset(0, 4, 2, 2);
		}

		// 그룹 제목 스타일 초기화
		if (groupTitleStyle == null)
		{
			groupTitleStyle = new GUIStyle(EditorStyles.boldLabel)
			{
				alignment = TextAnchor.MiddleCenter,
				normal = { textColor = Color.white }
			};
		}
	}

	private void DrawHeaderControls()
	{
		EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(45));
		GUILayout.FlexibleSpace();
		EditorGUILayout.BeginHorizontal();

		Color originalColor = GUI.backgroundColor;

		GUI.backgroundColor = new Color(0.4f, 0.8f, 0.5f, 1.0f);
		newGroupName = EditorGUILayout.TextField(newGroupName, GUILayout.ExpandWidth(true));
		if (GUILayout.Button("Add Group", GUILayout.Width(80), GUILayout.Height(21)))
		{
			if (!string.IsNullOrEmpty(newGroupName) && !data.groups.Any(g => g.name == newGroupName))
			{
				data.groups.Add(new BookmarkGroup(newGroupName));
				selectedTabIndex = data.groups.Count - 1;
				newGroupName = "New Group";
				EditorUtility.SetDirty(data);
				GUI.FocusControl(null);
			}
			else
			{
				ShowNotification(new GUIContent("유효하지 않거나 중복된 그룹 이름입니다."));
			}
		}
		GUI.backgroundColor = originalColor;

		GUI.enabled = selectedTabIndex > 0;
		if (GUILayout.Button("◀", GUILayout.Width(25), GUILayout.Height(21)))
		{
			selectedTabIndex--;
		}

		GUI.enabled = selectedTabIndex < data.groups.Count - 1;
		if (GUILayout.Button("▶", GUILayout.Width(25), GUILayout.Height(21)))
		{
			selectedTabIndex++;
		}
		GUI.enabled = true;

		if (data.groups.Count > 1)
		{
			GUI.backgroundColor = new Color(1.0f, 0.5f, 0.5f, 1.0f);
			if (GUILayout.Button("Delete Group", GUILayout.Width(90), GUILayout.Height(21)))
			{
				DeleteGroup(selectedTabIndex);
			}
			GUI.backgroundColor = originalColor;
		}

		EditorGUILayout.EndHorizontal();
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndVertical();
	}

	private void DrawGroupTabs()
	{
		if (data.groups.Count == 0) return;

		string[] groupNames = data.groups.Select(g => g.name).ToArray();
		selectedTabIndex = GUILayout.Toolbar(selectedTabIndex, groupNames, GUILayout.Height(25));

		Rect tabAreaRect = GUILayoutUtility.GetLastRect();
		HandleTabAreaDragAndDrop(tabAreaRect);
	}
    
	private void DrawFooterControls()
	{
		EditorGUILayout.BeginVertical(GUI.skin.box);
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace(); 
        
		EditorGUILayout.LabelField("탭 순서 이동", GUILayout.Width(70));

		GUI.enabled = selectedTabIndex > 0;
		if (GUILayout.Button("◀", GUILayout.Width(25)))
		{
			MoveGroup(selectedTabIndex, -1);
		}
        
		GUI.enabled = selectedTabIndex < data.groups.Count - 1;
		if (GUILayout.Button("▶", GUILayout.Width(25)))
		{
			MoveGroup(selectedTabIndex, 1);
		}
		GUI.enabled = true;

		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.EndVertical();
	}

	private void HandleTabAreaDragAndDrop(Rect tabAreaRect)
	{
		Event currentEvent = Event.current;
		if (!tabAreaRect.Contains(currentEvent.mousePosition)) return;

		if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
		{
			if (DragAndDrop.objectReferences.Any(obj => AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj))))
			{
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				if (currentEvent.type == EventType.DragPerform)
				{
					DragAndDrop.AcceptDrag();
					foreach (var obj in DragAndDrop.objectReferences)
					{
						string path = AssetDatabase.GetAssetPath(obj);
						if (AssetDatabase.IsValidFolder(path))
						{
							CreateGroupFromFolder(path);
							break;
						}
					}
					currentEvent.Use();
				}
			}
		}
	}

	private void CreateGroupFromFolder(string folderPath)
	{
		string folderName = Path.GetFileName(folderPath);
		if (data.groups.Any(g => g.name == folderName))
		{
			ShowNotification(new GUIContent($"'{folderName}' 그룹은 이미 존재합니다."));
			return;
		}

		BookmarkGroup newGroup = new BookmarkGroup(folderName);
		var filePaths = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly).Where(p => !p.EndsWith(".meta"));
		var dirPaths = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly);

		foreach (string path in filePaths.Concat(dirPaths))
		{
			Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
			if (asset != null) AddBookmark(asset, newGroup, false);
		}

		data.groups.Add(newGroup);
		selectedTabIndex = data.groups.Count - 1;
		EditorUtility.SetDirty(data);
	}

	private void HandleContentDragAndDrop()
	{
		if (data.groups.Count == 0) return;

		string dropMessage = $"여기로 에셋을 드래그하여 '{data.groups[selectedTabIndex].name}' 그룹에 추가하세요.";
		EditorGUILayout.BeginHorizontal();
		GUILayout.Space(20);

		Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
		GUI.Box(dropArea, dropMessage, dropAreaStyle);

		GUILayout.Space(20);
		EditorGUILayout.EndHorizontal();

		Event currentEvent = Event.current;
		if (!dropArea.Contains(currentEvent.mousePosition)) return;

		switch (currentEvent.type)
		{
		case EventType.DragUpdated:
		case EventType.DragPerform:
			DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
			if (currentEvent.type == EventType.DragPerform)
			{
				DragAndDrop.AcceptDrag();
				foreach (Object draggedObject in DragAndDrop.objectReferences)
				{
					AddBookmark(draggedObject, data.groups[selectedTabIndex]);
				}
				currentEvent.Use();
			}
			break;
		}
	}

	private void DrawGroupContent(BookmarkGroup group)
	{
		// 에셋 리스트 상단에 그룹명 표시
		if (group.items.Count > 0)
		{
			EditorGUILayout.LabelField($"<{group.name}>", groupTitleStyle);
			GUILayout.Space(5); // 제목과 리스트 사이 간격
		}

		for (int i = 0; i < group.items.Count; i++)
		{
			if (i < group.items.Count) DrawBookmarkItem(group.items[i], group);
		}
	}

	private void DrawBookmarkItem(BookmarkItem item, BookmarkGroup parentGroup)
	{
		string path = AssetDatabase.GUIDToAssetPath(item.guid);
		if (string.IsNullOrEmpty(path))
		{
			EditorGUILayout.BeginHorizontal(GUI.skin.box);
			GUI.color = Color.gray;
			EditorGUILayout.LabelField(new GUIContent($" [삭제됨] {item.alias ?? "알 수 없음"}", EditorGUIUtility.IconContent("console.warnicon.sml").image));
			GUI.color = Color.white;
			if (GUILayout.Button("X", GUILayout.Width(20)))
			{
				parentGroup.items.Remove(item);
				EditorUtility.SetDirty(data);
				GUIUtility.ExitGUI();
			}
			EditorGUILayout.EndHorizontal();
			return;
		}

		Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
		if (asset == null) return;

		EditorGUILayout.BeginHorizontal();

		Color originalColor = GUI.backgroundColor;
		
		GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
		if (GUILayout.Button("X", deleteButtonStyle, GUILayout.Width(20), GUILayout.Height(18)))
		{
			parentGroup.items.Remove(item);
			EditorUtility.SetDirty(data);
			GUIUtility.ExitGUI();
		}
		GUI.backgroundColor = originalColor;

		if (asset is GameObject && PrefabUtility.IsPartOfPrefabAsset(asset))
		{
			GUI.backgroundColor = new Color(0.2f, 0.5f, 1.0f, 1.0f);
			if (GUILayout.Button("Edit Prefab", GUILayout.Width(80), GUILayout.Height(18)))
			{
				AssetDatabase.OpenAsset(asset);
				GUIUtility.ExitGUI();
			}
			GUI.backgroundColor = originalColor;
		}
		else if (asset is SceneAsset)
		{
			GUI.backgroundColor = new Color(0.2f, 0.5f, 1.0f, 1.0f);
			if (GUILayout.Button("Open", GUILayout.Width(80), GUILayout.Height(18)))
			{
				if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				{
					EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
					GUIUtility.ExitGUI();
				}
			}
			GUI.backgroundColor = originalColor;
		}
		else if (asset is MonoScript)
		{
			GUI.backgroundColor = new Color(0.2f, 0.5f, 1.0f, 1.0f);
			if (GUILayout.Button("Open", GUILayout.Width(80), GUILayout.Height(18)))
			{
				AssetDatabase.OpenAsset(asset);
				GUIUtility.ExitGUI();
			}
			GUI.backgroundColor = originalColor;
		}

		GUIContent label = new GUIContent(string.IsNullOrEmpty(item.alias) ? asset.name : item.alias, AssetDatabase.GetCachedIcon(path));
		Rect itemRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
		GUI.Label(itemRect, label);

		EditorGUILayout.EndHorizontal();

		Event currentEvent = Event.current;
		if (itemRect.Contains(currentEvent.mousePosition))
		{
			if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
			{
				if (currentEvent.clickCount == 1) EditorGUIUtility.PingObject(asset);
				else if (currentEvent.clickCount == 2) AssetDatabase.OpenAsset(asset);
				currentEvent.Use();
			}
			else if (currentEvent.type == EventType.ContextClick)
			{
				GenericMenu menu = new GenericMenu();
				menu.AddItem(new GUIContent("별칭 변경"), false, () => {
					string newAlias = EditorInputDialog.Show("별칭 변경", "새로운 별칭을 입력하세요:", item.alias ?? asset.name);
					if (newAlias != null)
					{
						item.alias = (newAlias == asset.name) ? "" : newAlias;
						EditorUtility.SetDirty(data);
					}
				});
				menu.ShowAsContext();
				currentEvent.Use();
			}
		}
	}

	private void AddBookmark(Object obj, BookmarkGroup group, bool showNotification = true)
	{
		string path = AssetDatabase.GetAssetPath(obj);
		if (string.IsNullOrEmpty(path)) return;
		string guid = AssetDatabase.AssetPathToGUID(path);
		if (group.items.Any(item => item.guid == guid))
		{
			if (showNotification) ShowNotification(new GUIContent($"'{obj.name}'은(는) 이미 그룹에 존재합니다."));
			return;
		}
		group.items.Add(new BookmarkItem(guid));
		EditorUtility.SetDirty(data);
	}

	private void MoveGroup(int index, int direction)
	{
		BookmarkGroup groupToMove = data.groups[index];
		data.groups.RemoveAt(index);

		int newIndex = index + direction;
		data.groups.Insert(newIndex, groupToMove);

		selectedTabIndex = newIndex;

		EditorUtility.SetDirty(data);
	}

	private void RenameGroup(int index)
	{
		BookmarkGroup group = data.groups[index];
		string newName = EditorInputDialog.Show("그룹 이름 변경", "새로운 그룹 이름을 입력하세요:", group.name);
		if (!string.IsNullOrEmpty(newName) && newName != group.name)
		{
			if (data.groups.Any(g => g.name == newName))
			{
				ShowNotification(new GUIContent("이미 존재하는 그룹 이름입니다."));
			}
			else
			{
				group.name = newName;
				EditorUtility.SetDirty(data);
			}
		}
	}

	private void DeleteGroup(int index)
	{
		if (EditorUtility.DisplayDialog("그룹 삭제", $"'{data.groups[index].name}' 그룹을 정말 삭제하시겠습니까?", "삭제", "취소"))
		{
			data.groups.RemoveAt(index);
			selectedTabIndex = Mathf.Max(0, selectedTabIndex - 1);
			EditorUtility.SetDirty(data);
		}
	}
}

public class EditorInputDialog : EditorWindow
{
	private string title, description, inputText;
	private System.Action<string> onOk;

	public static string Show(string title, string description, string initialText)
	{
		EditorInputDialog window = CreateInstance<EditorInputDialog>();
		window.titleContent = new GUIContent(title);
		window.description = description;
		window.inputText = initialText;
		string result = null;
		window.onOk = (text) => { result = text; };
		window.ShowModal();
		return result;
	}

	private void OnGUI()
	{
		EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
		inputText = EditorGUILayout.TextField(inputText);
		GUILayout.Space(10);
		if (GUILayout.Button("OK"))
		{
			onOk?.Invoke(inputText);
			Close();
		}
		if (GUILayout.Button("Cancel"))
		{
			onOk?.Invoke(null);
			Close();
		}
	}
}
