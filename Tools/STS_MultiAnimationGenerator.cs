using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class STS_MultiAnimationGenerator : EditorWindow
{
	// --- UI를 위한 변수들 ---
	private string controllerName = "New Animator Controller";
	private int fps = 60;
    
	// [New] 키프레임 간격 (1 = 매 프레임, 2 = 2프레임마다 등)
	private int keyframeInterval = 1; 

	private Object outputFolder;
    
	// 리스트 UI를 위한 SerializedObject
	[SerializeField]
	private List<Object> sourceFolders = new List<Object>();
    
	private SerializedObject serializedObject;
	private SerializedProperty sourceFoldersProperty;
    
	private Vector2 scrollPosition;

	// [New] 메뉴 경로 변경
	[MenuItem("Tools/[STS] Multi-Animation Generator")]
	public static void ShowWindow()
	{
		GetWindow<STS_MultiAnimationGenerator>("STS Generator");
	}

	private void OnEnable()
	{
		serializedObject = new SerializedObject(this);
		sourceFoldersProperty = serializedObject.FindProperty("sourceFolders");
	}

	private void OnGUI()
	{
		serializedObject.Update();

		EditorGUILayout.LabelField("[STS] Multi-Animation Generator", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("여러 시퀀스 폴더를 연결하여 하나의 컨트롤러와 다수의 클립을 생성합니다.", MessageType.Info);
        
		EditorGUILayout.Space();

		// --- 설정 영역 ---
		controllerName = EditorGUILayout.TextField("Animator Controller Name", controllerName);
        
		EditorGUILayout.BeginHorizontal();
		fps = EditorGUILayout.IntField("Sample Rate (FPS)", fps);
		if (fps < 1) fps = 1;
		EditorGUILayout.EndHorizontal();

		// [New] 키프레임 간격 옵션 UI 추가
		EditorGUILayout.BeginHorizontal();
		keyframeInterval = EditorGUILayout.IntField("Keyframe Interval", keyframeInterval);
		if (keyframeInterval < 1) keyframeInterval = 1;
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.LabelField(" ", $"(Actual Speed: {fps / (float)keyframeInterval:0.##} fps)", EditorStyles.miniLabel);

		outputFolder = EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

		EditorGUILayout.Space();

		// --- 소스 폴더 리스트 영역 ---
		EditorGUILayout.LabelField("Source Image Folders", EditorStyles.boldLabel);
        
		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
		EditorGUILayout.PropertyField(sourceFoldersProperty, true);
		EditorGUILayout.EndScrollView();

		serializedObject.ApplyModifiedProperties();

		EditorGUILayout.Space();

		// --- 생성 버튼 ---
		if (GUILayout.Button("Generate Animator Controller", GUILayout.Height(40)))
		{
			GenerateAnimations();
		}
	}

	private void GenerateAnimations()
	{
		// 1. 유효성 검사
		if (string.IsNullOrEmpty(controllerName) || outputFolder == null || sourceFolders.Count == 0)
		{
			EditorUtility.DisplayDialog("Error", "모든 필드를 올바르게 설정해주세요.\n- Controller Name\n- Output Folder\n- At least one Source Folder", "OK");
			return;
		}
		string outputPath = AssetDatabase.GetAssetPath(outputFolder);
		if (!AssetDatabase.IsValidFolder(outputPath))
		{
			EditorUtility.DisplayDialog("Error", "Output Folder가 올바른 폴더가 아닙니다.", "OK");
			return;
		}

		// 2. 애니메이터 컨트롤러 생성
		string controllerPath = Path.Combine(outputPath, $"{controllerName}.controller");
		controllerPath = AssetDatabase.GenerateUniqueAssetPath(controllerPath);
		AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

		// 3. 각 소스 폴더를 순회하며 애니메이션 클립 생성
		foreach (var folderObject in sourceFolders)
		{
			if (folderObject == null) continue;

			string folderPath = AssetDatabase.GetAssetPath(folderObject);
			string[] guids = AssetDatabase.FindAssets("t:sprite", new[] { folderPath });
			if (guids.Length == 0)
			{
				Debug.LogWarning($"No sprites found in folder: {folderObject.name}. Skipping.");
				continue;
			}

			Sprite[] sprites = guids
				.Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
				.OrderBy(s => s.name)
				.ToArray();

			// 애니메이션 클립 생성
			AnimationClip clip = new AnimationClip();
			clip.frameRate = fps; // 클립 자체의 샘플 레이트 (보통 60)

			EditorCurveBinding curveBinding = new EditorCurveBinding
			{
				type = typeof(SpriteRenderer),
				path = "",
				propertyName = "m_Sprite"
			};

			ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[sprites.Length];
            
			// [New] 간격 계산: (1.0 / FPS) * Interval
			float timePerFrame = 1.0f / clip.frameRate;
			float actualTimeStep = timePerFrame * keyframeInterval;

			for (int i = 0; i < sprites.Length; i++)
			{
				keyFrames[i] = new ObjectReferenceKeyframe 
				{ 
					time = i * actualTimeStep, // 간격을 반영한 시간 설정
					value = sprites[i] 
				};
			}

			AnimationUtility.SetObjectReferenceCurve(clip, curveBinding, keyFrames);

			// 클립 저장
			string clipPath = Path.Combine(outputPath, $"{folderObject.name}.anim");
			clipPath = AssetDatabase.GenerateUniqueAssetPath(clipPath);
			AssetDatabase.CreateAsset(clip, clipPath);

			// 컨트롤러에 추가
			controller.AddMotion(clip);
		}

		// 4. 마무리
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();

		Debug.Log($"[STS] Created '{controllerName}' with {sourceFolders.Count} clips in '{outputPath}'.");
		EditorUtility.DisplayDialog("Success", "생성 완료! (STS Generator)", "OK");

		EditorGUIUtility.PingObject(controller);
	}
}