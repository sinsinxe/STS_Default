using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class STS_OptimizeImageSize : EditorWindow
{
	// =========================================================
	// 탭 관리
	// =========================================================
	private int tabIndex = 0;
	private string[] tabNames = { "이미지 최적화", "미사용 정리", "리소스 분석" };

	// =========================================================
	// [Tab 1] 최적화 툴 변수
	// =========================================================
	private enum CompressionLevel { Fast_Lv2 = 2, Balanced_Lv4 = 4, Max_Lv7 = 7 }
	private enum ResizeMode { Ratio = 0, Pixel = 1 }
	private enum TargetMode { Folder = 0, ManualList = 1 }

	// 대상 설정
	private TargetMode optTargetMode = TargetMode.Folder;
	private DefaultAsset optTargetFolder = null;

	[SerializeField] 
	private List<Texture2D> manualImageList = new List<Texture2D>(); 
    
	// [New] 매뉴얼 리스트용 스크롤 포지션
	private Vector2 manualListScrollPos;

	// 옵션
	private bool enableResizing = false;
	private bool enableCompression = true;
	private ResizeMode resizeMode = ResizeMode.Ratio;
	private float resizeRatio = 50f;
	private int targetWidth = 1024;
	private int targetHeight = 1024;
	private CompressionLevel compressionLevel = CompressionLevel.Balanced_Lv4;
	private bool includeSubfolders = true;
	private bool createBackup = false;
    
	// 통계
	private int totalPngCount = 0;
	private long totalOriginalSizeBytes = 0;
	private bool isStatsDirty = false;

	// =========================================================
	// [Tab 2] 미사용 리소스 정리 변수
	// =========================================================
	private DefaultAsset unusedTargetFolder = null;
	private Vector2 unusedListScrollPos; 
    
	private class AssetData
	{
		public string path;
		public bool isSelected;
	}
	private List<AssetData> foundAssets = new List<AssetData>();

	private enum CleanupMode { MoveToIsolation = 0, DeletePermanently = 1 }
	private CleanupMode cleanupMode = CleanupMode.MoveToIsolation;

	// =========================================================
	// [Tab 3] 리소스 분석 변수
	// =========================================================
	private DefaultAsset analyzeTargetFolder = null;
	private Vector2 analyzeScrollPos;
    
	private class ExtensionStat
	{
		public string extension;
		public int count;
		public long totalSize;
	}
	private List<ExtensionStat> extensionStats = new List<ExtensionStat>();
    
	private class FileInfoData
	{
		public string path;
		public long size;
	}
	private List<FileInfoData> topLargeFiles = new List<FileInfoData>();
    
	private long grandTotalSize = 0;
	private int grandTotalCount = 0;

	// =========================================================
	// 초기화 및 GUI
	// =========================================================
	// [Name Change] 툴 이름 변경
	[MenuItem("Tools/[STS] Resource Master")]
	public static void ShowWindow()
	{
		GetWindow<STS_OptimizeImageSize>("[STS] Resource Master");
	}

	private void OnGUI()
	{
		GUILayout.Space(10);
		tabIndex = GUILayout.Toolbar(tabIndex, tabNames, GUILayout.Height(30));
		GUILayout.Space(10);

		if (tabIndex == 0) DrawOptimizationTab();
		else if (tabIndex == 1) DrawUnusedFinderTab();
		else DrawAnalysisTab();
	}

	// =================================================================================
	// TAB 1 : 이미지 최적화 기능
	// =================================================================================
	private void DrawOptimizationTab()
	{
		GUILayout.Label("[STS] 이미지 용량 & 사이즈 최적화", EditorStyles.boldLabel);
        
		EditorGUI.BeginChangeCheck();

		// 1. 대상 선택 (폴더 vs 개별 리스트)
		GUILayout.BeginVertical("box");
		GUILayout.Label("1. 대상 선택 방식 (Target Mode)", EditorStyles.boldLabel);
		optTargetMode = (TargetMode)EditorGUILayout.EnumPopup("선택 방식", optTargetMode);

		if (optTargetMode == TargetMode.Folder)
		{
			optTargetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
				new GUIContent("대상 폴더", "최적화할 이미지가 포함된 폴더를 연결하세요."), 
				optTargetFolder, typeof(DefaultAsset), false);

			if (optTargetFolder == null)
				EditorGUILayout.HelpBox("작업할 프로젝트 폴더를 연결하세요.", MessageType.Info);
			else
				EditorGUILayout.LabelField("경로:", AssetDatabase.GetAssetPath(optTargetFolder), EditorStyles.miniLabel);
		}
		else // ManualList
		{
			GUILayout.Label("최적화할 이미지를 아래 리스트에 추가하세요.", EditorStyles.miniLabel);
            
			// [Improvement] 스크롤 뷰 추가 (리스트가 길어질 경우 대비)
			manualListScrollPos = GUILayout.BeginScrollView(manualListScrollPos, "box", GUILayout.Height(200));
            
			ScriptableObject target = this;
			SerializedObject so = new SerializedObject(target);
			SerializedProperty stringsProperty = so.FindProperty("manualImageList");

			EditorGUILayout.PropertyField(stringsProperty, new GUIContent("이미지 리스트"), true); 
			so.ApplyModifiedProperties();

			if (manualImageList.Count == 0)
			{
				GUILayout.Space(10);
				EditorGUILayout.HelpBox("리스트가 비어있습니다. Texture(PNG)를 드래그하여 추가하세요.", MessageType.Info);
			}
            
			GUILayout.EndScrollView();
		}
		GUILayout.EndVertical();
		GUILayout.Space(5);

		// 2. 리사이징 옵션
		GUILayout.BeginVertical("box");
		enableResizing = EditorGUILayout.ToggleLeft(
			new GUIContent(" 2. 이미지 물리 사이즈 변환 (Resizing)", "체크 시, 이미지의 물리적인 픽셀 크기(가로x세로)를 줄입니다."), 
			enableResizing, EditorStyles.boldLabel);
        
		if (enableResizing) {
			EditorGUI.indentLevel++;
			resizeMode = (ResizeMode)EditorGUILayout.EnumPopup("변환 방식", resizeMode);
			if (resizeMode == ResizeMode.Ratio) {
				resizeRatio = EditorGUILayout.Slider(new GUIContent("비율 (%)", "원본 크기 대비 줄어들 비율입니다."), resizeRatio, 1f, 99f);
				EditorGUILayout.HelpBox($"가로/세로를 {resizeRatio:0}% 크기로 줄입니다.\n(전체 면적은 약 {(resizeRatio*resizeRatio)/100f:0}%로 감소)", MessageType.Info);
			} else {
				targetWidth = EditorGUILayout.IntField("가로 (px)", targetWidth);
				targetHeight = EditorGUILayout.IntField("세로 (px)", targetHeight);
				EditorGUILayout.HelpBox("입력한 픽셀 크기로 강제 변환합니다. (비율이 다를 경우 찌그러질 수 있습니다)", MessageType.Warning);
			}
			EditorGUI.indentLevel--;
		}
		GUILayout.EndVertical();
		GUILayout.Space(5);

		// 3. 압축 옵션
		GUILayout.BeginVertical("box");
		enableCompression = EditorGUILayout.ToggleLeft(
			new GUIContent(" 3. OptiPNG 압축 실행 (Compression)", "체크 시, OptiPNG 툴을 사용하여 이미지 용량을 무손실 압축합니다."), 
			enableCompression, EditorStyles.boldLabel);
        
		if (enableCompression) {
			EditorGUI.indentLevel++;
			compressionLevel = (CompressionLevel)EditorGUILayout.EnumPopup(new GUIContent("압축 강도", "Lv 2(빠름) ~ Lv 7(최대 압축/느림)"), compressionLevel);
            
			string levelDesc = "";
			switch (compressionLevel) {
			case CompressionLevel.Fast_Lv2: levelDesc = "빠름 / 낮은 효율"; break;
			case CompressionLevel.Balanced_Lv4: levelDesc = "균형 (권장 기본값)"; break;
			case CompressionLevel.Max_Lv7: levelDesc = "매우 느림 / 최대 효율 (빌드 전 추천)"; break;
			}
			EditorGUILayout.HelpBox($"설정: {levelDesc}", MessageType.None);
			EditorGUI.indentLevel--;
		}
		GUILayout.EndVertical();
		GUILayout.Space(5);

		// 4. 공통 옵션
		GUILayout.BeginVertical("box");
		if (optTargetMode == TargetMode.Folder) 
		{
			includeSubfolders = EditorGUILayout.Toggle(
				new GUIContent("하위 폴더 포함", "체크 시, 선택한 폴더 내부의 모든 하위 폴더까지 검색합니다."), 
				includeSubfolders);
		}
		createBackup = EditorGUILayout.Toggle(
			new GUIContent("백업 생성 (.bak)", "체크 시, 원본 이미지를 덮어쓰기 전에 .bak 파일로 백업합니다.\n(복구가 필요하면 .bak을 지우고 다시 .png로 이름을 바꾸시면 됩니다.)"), 
			createBackup);
		GUILayout.EndVertical();

		if (EditorGUI.EndChangeCheck()) isStatsDirty = true;
        
		if (isStatsDirty) { CalculateStats(); isStatsDirty = false; }

		GUILayout.Space(10);
        
		bool hasTarget = (optTargetMode == TargetMode.Folder && optTargetFolder != null) || 
		(optTargetMode == TargetMode.ManualList && manualImageList.Count > 0);
        
		if (hasTarget && totalPngCount > 0) DrawEstimationBox();
        
		GUILayout.Space(10);

		bool isReady = hasTarget && totalPngCount > 0 && (enableResizing || enableCompression);
		GUI.enabled = isReady;
		if (GUILayout.Button(isReady ? "최적화 시작 (Optimize Start)" : "대상을 선택하고 기능을 활성화하세요", GUILayout.Height(40))) {
			RunOptimization();
			CalculateStats();
		}
		GUI.enabled = true;
	}

	// =================================================================================
	// TAB 2 : 미사용 리소스 정리 기능
	// =================================================================================
	private void DrawUnusedFinderTab()
	{
		GUILayout.Label("[STS] 미사용 이미지 정리 (Local Cleanup)", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("※ 주의: 코드로 로드(Resources.Load)하거나 어드레서블로 사용하는 파일은 감지되지 않을 수 있습니다.\n'격리 모드'를 사용하여 먼저 테스트하는 것을 권장합니다.", MessageType.Warning);
        
		GUILayout.Space(5);

		// 1. 폴더 선택
		GUILayout.BeginVertical("box");
		GUILayout.Label("검사할 대상 폴더", EditorStyles.boldLabel);
		unusedTargetFolder = (DefaultAsset)EditorGUILayout.ObjectField("폴더 연결", unusedTargetFolder, typeof(DefaultAsset), false);
        
		string targetPath = "";
		if (unusedTargetFolder != null) 
		{
			targetPath = AssetDatabase.GetAssetPath(unusedTargetFolder);
			EditorGUILayout.LabelField("경로:", targetPath, EditorStyles.miniLabel);
		}
		GUILayout.EndVertical();

		GUILayout.Space(10);

		// 2. 스캔 버튼
		GUI.enabled = (unusedTargetFolder != null);
		if (GUILayout.Button("미사용 리소스 스캔 (Scan Unused Assets)", GUILayout.Height(35)))
		{
			FindUnusedAssetsInFolder();
		}
		GUI.enabled = true;

		GUILayout.Space(10);

		// 3. 결과 리스트 및 실행 옵션
		if (foundAssets.Count > 0)
		{
			GUILayout.Label($"검색 결과: {foundAssets.Count}개 파일 발견", EditorStyles.boldLabel);
            
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("전체 선택", GUILayout.Width(100))) SetAllSelection(true);
			if (GUILayout.Button("전체 해제", GUILayout.Width(100))) SetAllSelection(false);
			GUILayout.FlexibleSpace(); 
			GUILayout.EndHorizontal();

			GUILayout.Space(5);

			DrawAssetList(); 

			GUILayout.Space(10);
			DrawCleanupOptions(targetPath);
		}
		else if (unusedTargetFolder != null)
		{
			GUILayout.Label("스캔 결과가 없거나, 스캔하지 않았습니다.", EditorStyles.miniLabel);
		}
	}

	private void DrawAssetList()
	{
		unusedListScrollPos = GUILayout.BeginScrollView(unusedListScrollPos, "box", GUILayout.Height(300));

		for (int i = 0; i < foundAssets.Count; i++)
		{
			var item = foundAssets[i];
			GUILayout.BeginHorizontal();
            
			item.isSelected = EditorGUILayout.Toggle(item.isSelected, GUILayout.Width(20));

			Texture2D icon = AssetDatabase.GetCachedIcon(item.path) as Texture2D;
			if (icon != null) GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

			if (GUILayout.Button(item.path, EditorStyles.label))
			{
				Object obj = AssetDatabase.LoadAssetAtPath<Object>(item.path);
				EditorGUIUtility.PingObject(obj);
				Selection.activeObject = obj;
			}
			GUILayout.EndHorizontal();
		}

		GUILayout.EndScrollView();
	}

	private void DrawCleanupOptions(string rootPath)
	{
		GUILayout.BeginVertical("box");
        
		GUILayout.Label("처리 옵션 (Cleanup Options)", EditorStyles.boldLabel);

		string[] modeLabels = new string[] { "안전 격리 모드 (권장)", "영구 삭제 모드 (주의)" };
		cleanupMode = (CleanupMode)EditorGUILayout.Popup("처리 방식", (int)cleanupMode, modeLabels);

		string unusedFolderName = "_Unused_Assets";
		string fullIsolationPath = $"{rootPath}/{unusedFolderName}";

		if (cleanupMode == CleanupMode.MoveToIsolation)
		{
			EditorGUILayout.HelpBox($"[안전 격리 모드]\n선택한 파일들을 아래 위치로 이동시킵니다 (폴더 자동 생성):\n📂 {fullIsolationPath}", MessageType.Info);
		}
		else
		{
			EditorGUILayout.HelpBox("[영구 삭제 모드]\n선택한 파일들을 즉시 삭제합니다.\n이 작업은 되돌릴 수 없습니다!", MessageType.Error);
		}

		GUILayout.Space(10);

		int selectedCount = foundAssets.Count(x => x.isSelected);
		GUI.enabled = selectedCount > 0;
        
		string btnLabel = cleanupMode == CleanupMode.MoveToIsolation 
			? $"선택된 {selectedCount}개 파일 격리 폴더로 이동" 
			: $"선택된 {selectedCount}개 파일 영구 삭제 (주의)";

		Color originalColor = GUI.backgroundColor;
		GUI.backgroundColor = cleanupMode == CleanupMode.MoveToIsolation ? Color.cyan : new Color(1f, 0.5f, 0.5f); 

		if (GUILayout.Button(btnLabel, GUILayout.Height(40)))
		{
			ExecuteCleanup(rootPath, unusedFolderName);
		}

		GUI.backgroundColor = originalColor;
		GUI.enabled = true;

		GUILayout.EndVertical();
	}

	// =================================================================================
	// TAB 3 : 리소스 분석 기능
	// =================================================================================
	private void DrawAnalysisTab()
	{
		GUILayout.Label("[STS] 폴더 리소스 분석 (Analyze)", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("선택한 폴더 내의 파일들을 분석하여 확장자별 통계와\n용량이 큰 파일 순위를 보여줍니다.", MessageType.None);
		GUILayout.Space(5);

		// 폴더 선택
		GUILayout.BeginVertical("box");
		GUILayout.Label("분석할 대상 폴더", EditorStyles.boldLabel);
		analyzeTargetFolder = (DefaultAsset)EditorGUILayout.ObjectField("폴더 연결", analyzeTargetFolder, typeof(DefaultAsset), false);
		GUILayout.EndVertical();

		GUILayout.Space(10);

		// 분석 버튼
		GUI.enabled = (analyzeTargetFolder != null);
		if (GUILayout.Button("리소스 분석 시작 (Analyze Start)", GUILayout.Height(35)))
		{
			AnalyzeFolder();
		}
		GUI.enabled = true;

		GUILayout.Space(10);

		if (extensionStats.Count > 0)
		{
			analyzeScrollPos = GUILayout.BeginScrollView(analyzeScrollPos);

			// 1. 확장자별 요약 통계
			GUILayout.BeginVertical("box");
			GUILayout.Label("📂 확장자별 요약 (Summary by Type)", EditorStyles.boldLabel);
            
			// 헤더
			GUILayout.BeginHorizontal();
			GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(80));
			GUILayout.Label("Count", EditorStyles.boldLabel, GUILayout.Width(60));
			GUILayout.Label("Total Size", EditorStyles.boldLabel);
			GUILayout.EndHorizontal();
			GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // 구분선

			// 리스트 출력
			foreach (var stat in extensionStats)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(stat.extension, GUILayout.Width(80));
				GUILayout.Label(stat.count.ToString(), GUILayout.Width(60));
				GUILayout.Label(EditorUtility.FormatBytes(stat.totalSize));
				GUILayout.EndHorizontal();
			}

			// [New] 총 합계 (Grand Total)
			GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // 구분선
			GUILayout.BeginHorizontal();
			GUILayout.Label("TOTAL", EditorStyles.boldLabel, GUILayout.Width(80));
			GUILayout.Label(grandTotalCount.ToString(), EditorStyles.boldLabel, GUILayout.Width(60));
			GUILayout.Label(EditorUtility.FormatBytes(grandTotalSize), EditorStyles.boldLabel);
			GUILayout.EndHorizontal();

			GUILayout.EndVertical();

			GUILayout.Space(10);

			// 2. 용량 Top 20 리스트
			GUILayout.BeginVertical("box");
			GUILayout.Label("🔥 용량 Top 20 리소스 (Largest Files)", EditorStyles.boldLabel);
            
			for (int i = 0; i < topLargeFiles.Count; i++)
			{
				var file = topLargeFiles[i];
				GUILayout.BeginHorizontal();
				GUILayout.Label($"{i + 1}.", GUILayout.Width(25));
                
				if (GUILayout.Button(Path.GetFileName(file.path), EditorStyles.label, GUILayout.Width(250)))
				{
					Object obj = AssetDatabase.LoadAssetAtPath<Object>(file.path);
					EditorGUIUtility.PingObject(obj);
				}
                
				GUILayout.FlexibleSpace();
				GUILayout.Label(EditorUtility.FormatBytes(file.size), EditorStyles.boldLabel);
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();

			GUILayout.EndScrollView();
		}
	}

	// ---------------------------------------------------------
	// [Logic] 리소스 분석 로직
	// ---------------------------------------------------------
	private void AnalyzeFolder()
	{
		if (analyzeTargetFolder == null) return;
		string path = AssetDatabase.GetAssetPath(analyzeTargetFolder);
        
		extensionStats.Clear();
		topLargeFiles.Clear();
		grandTotalSize = 0; // 초기화
		grandTotalCount = 0; // 초기화

		string fullPath = Path.GetFullPath(path);
		if (!Directory.Exists(fullPath)) return;

		string[] files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
        
		Dictionary<string, ExtensionStat> statsMap = new Dictionary<string, ExtensionStat>();
		List<FileInfoData> allFiles = new List<FileInfoData>();

		foreach (var file in files)
		{
			if (file.EndsWith(".meta")) continue; 

			FileInfo info = new FileInfo(file);
			string ext = info.Extension.ToLower();
			long size = info.Length;

			// 총합 계산
			grandTotalSize += size;
			grandTotalCount++;

			// 확장자별 통계
			if (!statsMap.ContainsKey(ext))
			{
				statsMap[ext] = new ExtensionStat { extension = ext, count = 0, totalSize = 0 };
			}
			statsMap[ext].count++;
			statsMap[ext].totalSize += size;

			// 전체 파일 리스트 (정렬용)
			string relativePath = "Assets" + file.Substring(Application.dataPath.Length).Replace("\\", "/");
			allFiles.Add(new FileInfoData { path = relativePath, size = size });
		}

		// 리스트로 변환 및 정렬
		extensionStats = statsMap.Values.OrderByDescending(x => x.totalSize).ToList();
		topLargeFiles = allFiles.OrderByDescending(x => x.size).Take(20).ToList();
	}

	// ---------------------------------------------------------
	// [Logic] 통계 및 최적화 (Tab 1)
	// ---------------------------------------------------------
	private void CalculateStats()
	{
		totalPngCount = 0;
		totalOriginalSizeBytes = 0;

		List<string> fileList = new List<string>();

		if (optTargetMode == TargetMode.Folder)
		{
			if (optTargetFolder == null) return;
			string folderPath = AssetDatabase.GetAssetPath(optTargetFolder);
			if (!Directory.Exists(folderPath)) return;
			SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			string fullFolderPath = Path.GetFullPath(folderPath);
			fileList.AddRange(Directory.GetFiles(fullFolderPath, "*.png", searchOption));
		}
		else // Manual List
		{
			foreach (var tex in manualImageList)
			{
				if (tex != null)
				{
					string path = AssetDatabase.GetAssetPath(tex);
					if (path.ToLower().EndsWith(".png")) 
					{
						fileList.Add(Path.GetFullPath(path));
					}
				}
			}
		}

		totalPngCount = fileList.Count;
		foreach (string file in fileList)
		{
			if(File.Exists(file)) 
				totalOriginalSizeBytes += new FileInfo(file).Length;
		}
	}

	private void DrawEstimationBox()
	{
		GUILayout.BeginVertical("HelpBox");
		GUILayout.Label("예상 결과", EditorStyles.boldLabel);
		string currentSizeStr = EditorUtility.FormatBytes(totalOriginalSizeBytes);
		GUILayout.Label($"• 파일 수: {totalPngCount}개 / 현재 용량: {currentSizeStr}");
        
		float compressionReduction = 0f;
		if (enableCompression) {
			if (compressionLevel == CompressionLevel.Fast_Lv2) compressionReduction = 0.10f;
			else if (compressionLevel == CompressionLevel.Balanced_Lv4) compressionReduction = 0.30f;
			else compressionReduction = 0.50f;
		}
		float resizeFactor = 1.0f;
		if (enableResizing) resizeFactor = (resizeMode == ResizeMode.Ratio) ? Mathf.Pow(resizeRatio / 100f, 2) : 0.5f;
		long estimatedFinalBytes = (long)(totalOriginalSizeBytes * resizeFactor * (1.0f - compressionReduction));
		GUILayout.Label($"• 예상 최종 용량: {EditorUtility.FormatBytes(estimatedFinalBytes)}");
		GUILayout.EndVertical();
	}

	private void RunOptimization()
	{
		List<string> targetFiles = new List<string>();

		// 1. 대상 파일 수집
		if (optTargetMode == TargetMode.Folder)
		{
			string folderPath = AssetDatabase.GetAssetPath(optTargetFolder);
			string toolRelativePath = "Effect/Tools/optipng.exe";
			string toolFullPath = Path.Combine(Application.dataPath, toolRelativePath);
            
			if (enableCompression && !File.Exists(toolFullPath)) {
				EditorUtility.DisplayDialog("오류", $"optipng.exe 없음.\n경로: {toolFullPath}", "확인");
				return;
			}

			SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			string fullFolderPath = Path.GetFullPath(folderPath);
			targetFiles.AddRange(Directory.GetFiles(fullFolderPath, "*.png", searchOption));
		}
		else // Manual List
		{
			foreach (var tex in manualImageList) {
				if (tex != null) {
					string path = AssetDatabase.GetAssetPath(tex);
					if (path.ToLower().EndsWith(".png")) targetFiles.Add(Path.GetFullPath(path));
				}
			}
		}

		// 2. 최적화 실행
		string toolExePath = Path.Combine(Application.dataPath, "Effect/Tools/optipng.exe");
		if (enableCompression && !File.Exists(toolExePath)) {
			EditorUtility.DisplayDialog("오류", "optipng.exe를 찾을 수 없습니다.", "확인");
			return;
		}

		int successCount = 0;
		try {
			for (int i = 0; i < targetFiles.Count; i++) {
				string file = targetFiles[i];
				if (EditorUtility.DisplayCancelableProgressBar("[STS] 최적화 중...", Path.GetFileName(file), (float)i / targetFiles.Count)) break;
                
				if (createBackup) {
					string bak = file + ".bak";
					if (!File.Exists(bak)) File.Copy(file, bak);
				}
                
				bool ok = true;
				if (enableResizing) ok = PerformResizing(file);
				if (enableCompression && ok) ok = RunOptiPngProcess(toolExePath, file);
                
				if (ok) successCount++;
			}
		}
			catch (System.Exception e) { Debug.LogError(e.Message); }
			finally {
				EditorUtility.ClearProgressBar();
				AssetDatabase.Refresh();
				EditorUtility.DisplayDialog("완료", $"작업 완료: {successCount}개 파일 처리됨", "확인");
			}
	}

	private bool PerformResizing(string filePath)
	{
		try {
			byte[] fileData = File.ReadAllBytes(filePath);
			Texture2D tex = new Texture2D(2, 2);
			if (!tex.LoadImage(fileData)) return false;
			int w = (resizeMode == ResizeMode.Ratio) ? Mathf.RoundToInt(tex.width * (resizeRatio / 100f)) : targetWidth;
			int h = (resizeMode == ResizeMode.Ratio) ? Mathf.RoundToInt(tex.height * (resizeRatio / 100f)) : targetHeight;
			if (w < 1) w = 1; if (h < 1) h = 1;
			Texture2D newTex = ResizeTextureBilinear(tex, w, h);
			File.WriteAllBytes(filePath, newTex.EncodeToPNG());
			DestroyImmediate(tex); DestroyImmediate(newTex);
			return true;
		} catch { return false; }
	}

	private Texture2D ResizeTextureBilinear(Texture2D source, int newWidth, int newHeight)
	{
		Texture2D result = new Texture2D(newWidth, newHeight, source.format, false);
		float incX = (1.0f / (float)newWidth);
		float incY = (1.0f / (float)newHeight);
		for (int y = 0; y < newHeight; y++) {
			for (int x = 0; x < newWidth; x++) {
				float u = x * incX;
				float v = y * incY;
				result.SetPixel(x, y, source.GetPixelBilinear(u, v));
			}
		}
		result.Apply();
		return result;
	}

	private bool RunOptiPngProcess(string exe, string file)
	{
		try {
			System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
			psi.FileName = exe;
			psi.Arguments = $"-o{(int)compressionLevel} -clobber -strip all \"{file}\"";
			psi.UseShellExecute = false;
			psi.CreateNoWindow = true;
			using (var p = System.Diagnostics.Process.Start(psi)) {
				p.WaitForExit();
				return p.ExitCode == 0;
			}
		} catch { return false; }
	}

	// ---------------------------------------------------------
	// [Logic] 미사용 리소스 찾기 (Tab 2)
	// ---------------------------------------------------------
	private void FindUnusedAssetsInFolder()
	{
		if (unusedTargetFolder == null) return;
		string targetPath = AssetDatabase.GetAssetPath(unusedTargetFolder);
		foundAssets.Clear();
		EditorUtility.DisplayProgressBar("스캔 중...", "폴더 내 의존성 분석 중...", 0.2f);
		try {
			string[] textureGuids = AssetDatabase.FindAssets("t:Texture", new[] { targetPath });
			HashSet<string> candidatePaths = new HashSet<string>();
			foreach (string guid in textureGuids) candidatePaths.Add(AssetDatabase.GUIDToAssetPath(guid));

			string[] userAssetGuids = AssetDatabase.FindAssets("t:Scene t:Prefab t:Material t:AnimationClip t:AnimatorController", new[] { targetPath });
			HashSet<string> usedPaths = new HashSet<string>();
			int count = 0;
			foreach (string guid in userAssetGuids) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (count++ % 10 == 0) EditorUtility.DisplayProgressBar("스캔 중...", $"참조 확인: {Path.GetFileName(path)}", (float)count / userAssetGuids.Length);
				string[] dependencies = AssetDatabase.GetDependencies(path, false);
				foreach (string dep in dependencies) usedPaths.Add(dep);
			}

			foreach (string candidate in candidatePaths) {
				if (!usedPaths.Contains(candidate)) foundAssets.Add(new AssetData { path = candidate, isSelected = false });
			}
		}
			catch (System.Exception e) { Debug.LogError(e.Message); }
			finally { EditorUtility.ClearProgressBar(); }
	}

	private void SetAllSelection(bool select)
	{
		foreach (var item in foundAssets) item.isSelected = select;
	}

	private void ExecuteCleanup(string rootPath, string folderName)
	{
		var targets = foundAssets.Where(x => x.isSelected).ToList();
		if (targets.Count == 0) return;
		if (cleanupMode == CleanupMode.DeletePermanently) {
			if (!EditorUtility.DisplayDialog("영구 삭제 확인", $"정말로 {targets.Count}개의 파일을 삭제하시겠습니까?", "삭제", "취소")) return;
		}

		string destFolder = Path.Combine(rootPath, folderName);
		if (cleanupMode == CleanupMode.MoveToIsolation && !AssetDatabase.IsValidFolder(destFolder)) AssetDatabase.CreateFolder(rootPath, folderName);

		int successCount = 0;
		foreach (var item in targets) {
			if (cleanupMode == CleanupMode.MoveToIsolation) {
				string newPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(destFolder, Path.GetFileName(item.path)));
				if (string.IsNullOrEmpty(AssetDatabase.MoveAsset(item.path, newPath))) successCount++;
			} else {
				if (AssetDatabase.DeleteAsset(item.path)) successCount++;
			}
		}
		AssetDatabase.Refresh();
		FindUnusedAssetsInFolder();
		EditorUtility.DisplayDialog("완료", $"총 {successCount}개 파일 처리 완료.", "확인");
	}
}