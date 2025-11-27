using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

public class STS_ImageFontGenerator : EditorWindow
{
	// UI 변수
	private Texture2D sourceTexture;
	private string charSequence = "0123456789"; 
	private string fontName = "MyFont";
	private int alphaThreshold = 10;
	private bool fixNumbers = true;
	private bool forceSquarePOT = true; 

	// 내부 처리용 변수
	private List<Rect> detectedRects = new List<Rect>();
    
	[MenuItem("Tools/[STS] ImageFontGenerator")]
	public static void ShowWindow()
	{
		GetWindow<STS_ImageFontGenerator>("[STS] Font Gen");
	}

	void OnGUI()
	{
		GUILayout.Label("[ZP] 이미지 폰트 생성기 (Size Fix)", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox(
			"사용 방법:\n" +
			"1. '원본 이미지'를 넣고 '글자 순서'를 입력하세요.\n" +
			"2. '1. 이미지 분석' -> '2. 폰트 생성' 순서로 진행합니다.\n" +
			"3. 생성된 폰트는 이제 유니티 Text 컴포넌트에서 사이즈 조절이 가능합니다.", 
			MessageType.Info);

		EditorGUILayout.Space();

		GUILayout.Label("1. 리소스 설정", EditorStyles.boldLabel);
		sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
			new GUIContent("원본 이미지", "글자가 나열된 투명 배경의 PNG 이미지"), 
			sourceTexture, typeof(Texture2D), false);
		fontName = EditorGUILayout.TextField("저장할 폰트 이름", fontName);
        
		EditorGUILayout.Space();
        
		GUILayout.Label("2. 세부 설정", EditorStyles.boldLabel);
		GUILayout.Label("글자 순서 입력 (Character Sequence)");
		charSequence = EditorGUILayout.TextArea(charSequence, GUILayout.Height(40));
		alphaThreshold = EditorGUILayout.IntSlider("투명도 민감도", alphaThreshold, 0, 255);
		fixNumbers = EditorGUILayout.Toggle("숫자/기호 정렬 최적화", fixNumbers);
		forceSquarePOT = EditorGUILayout.Toggle("정사각 2의 거듭제곱 (POT)", forceSquarePOT);

		EditorGUILayout.Space(15);
        
		if (GUILayout.Button("1. 이미지 분석 실행 (Analyze)", GUILayout.Height(35)))
		{
			AnalyzeImage();
		}

		if (detectedRects.Count > 0)
		{
			EditorGUILayout.Space();
			GUILayout.Label("3. 분석 결과 확인", EditorStyles.boldLabel);

			bool countMatch = charSequence.Length == detectedRects.Count;
			string statusMsg = $"감지됨: {detectedRects.Count}개 / 입력됨: {charSequence.Length}개";

			if (countMatch)
			{
				EditorGUILayout.HelpBox(statusMsg + "\n일치합니다.", MessageType.Info);
				GUI.backgroundColor = Color.green; 
				if (GUILayout.Button("2. 폰트 파일 생성 및 저장 (Generate)", GUILayout.Height(40)))
				{
					GenerateFont();
				}
				GUI.backgroundColor = Color.white;
			}
			else
			{
				EditorGUILayout.HelpBox(statusMsg + "\n[주의] 개수가 일치하지 않습니다.", MessageType.Error);
			}
		}
	}

	void AnalyzeImage()
	{
		if (sourceTexture == null) return;

		string path = AssetDatabase.GetAssetPath(sourceTexture);
		TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer != null && !importer.isReadable)
		{
			importer.isReadable = true;
			importer.SaveAndReimport();
		}

		detectedRects.Clear();
		Color32[] pixels = sourceTexture.GetPixels32();
		int width = sourceTexture.width;
		int height = sourceTexture.height;

		List<(int start, int end)> rows = new List<(int, int)>();
		bool inRow = false;
		int startY = 0;

		for (int y = 0; y < height; y++)
		{
			bool rowHasPixel = false;
			for (int x = 0; x < width; x++)
			{
				if (pixels[y * width + x].a > alphaThreshold)
				{
					rowHasPixel = true;
					break;
				}
			}

			if (rowHasPixel && !inRow)
			{
				inRow = true;
				startY = y;
			}
			else if (!rowHasPixel && inRow)
			{
				inRow = false;
				rows.Add((startY, y));
			}
		}
		if (inRow) rows.Add((startY, height));

		foreach (var row in rows)
		{
			bool inChar = false;
			int startX = 0;

			for (int x = 0; x < width; x++)
			{
				bool colHasPixel = false;
				for (int y = row.start; y < row.end; y++)
				{
					if (pixels[y * width + x].a > alphaThreshold)
					{
						colHasPixel = true;
						break;
					}
				}

				if (colHasPixel && !inChar)
				{
					inChar = true;
					startX = x;
				}
				else if (!colHasPixel && inChar)
				{
					inChar = false;
					detectedRects.Add(RefineBounds(pixels, width, startX, row.start, x - startX, row.end - row.start));
				}
			}
			if (inChar)
			{
				detectedRects.Add(RefineBounds(pixels, width, startX, row.start, width - startX, row.end - row.start));
			}
		}

		detectedRects = detectedRects
			.OrderByDescending(r => r.y)
			.ThenBy(r => r.x)
			.GroupBy(r => Mathf.FloorToInt(r.y / (r.height * 0.5f))) 
			.SelectMany(g => g.OrderBy(r => r.x))
			.ToList();
            
		detectedRects.Sort((a, b) =>
		{
			if (Mathf.Abs(a.y - b.y) > 10) return b.y.CompareTo(a.y);
			return a.x.CompareTo(b.x);
		});

		Debug.Log($"[ZP] 분석 완료. {detectedRects.Count}개");
	}

	Rect RefineBounds(Color32[] pixels, int texWidth, int x, int y, int w, int h)
	{
		int minX = x + w, maxX = x, minY = y + h, maxY = y;
		bool foundAny = false;
        
		for(int cy = y; cy < y + h; cy++) {
			for(int cx = x; cx < x + w; cx++) {
				if (pixels[cy * texWidth + cx].a > alphaThreshold) {
					if (cx < minX) minX = cx;
					if (cx > maxX) maxX = cx;
					if (cy < minY) minY = cy;
					if (cy > maxY) maxY = cy;
					foundAny = true;
				}
			}
		}
		if (!foundAny) return new Rect(x, y, w, h);
		return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
	}

	void GenerateFont()
	{
		if (detectedRects.Count == 0) return;
        
		int count = Mathf.Min(charSequence.Length, detectedRects.Count);
		var glyphs = new List<GlyphInfo>();
        
		float maxNumWidth = 0;
		float maxNumHeight = 0;
		float overallMaxHeight = 0;

		for (int i = 0; i < count; i++)
		{
			char c = charSequence[i];
			Rect r = detectedRects[i];
			if (r.height > overallMaxHeight) overallMaxHeight = r.height;
			if (fixNumbers && char.IsDigit(c))
			{
				if (r.width > maxNumWidth) maxNumWidth = r.width;
				if (r.height > maxNumHeight) maxNumHeight = r.height;
			}
			glyphs.Add(new GlyphInfo { character = c, originalRect = r });
		}
        
		if (fixNumbers && maxNumHeight == 0) maxNumHeight = overallMaxHeight;

		List<Texture2D> processedTextures = new List<Texture2D>();
        
		foreach (var g in glyphs)
		{
			int targetW = (int)g.originalRect.width;
			int targetH = (int)g.originalRect.height;
			int offsetX = 0;
			int offsetY = 0;

			bool isNum = char.IsDigit(g.character);
			bool isSmallPunc = ".,'\"`".Contains(g.character); 
			bool isMidPunc = "-~=".Contains(g.character);

			if (fixNumbers)
			{
				if (isNum)
				{
					targetW = (int)maxNumWidth;
					offsetX = (targetW - (int)g.originalRect.width) / 2;
				}
				else if (isSmallPunc || isMidPunc)
				{
					targetH = (int)maxNumHeight;
					if (".,_".Contains(g.character)) offsetY = 0;
					else if ("'\"`^°".Contains(g.character)) offsetY = targetH - (int)g.originalRect.height;
					else offsetY = (targetH - (int)g.originalRect.height) / 2;
				}
			}

			Texture2D newTex = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
			Color32[] clearColors = new Color32[targetW * targetH];
			newTex.SetPixels32(clearColors);

			int rectX = (int)g.originalRect.x;
			int rectY = (int)g.originalRect.y;
			int rectW = (int)g.originalRect.width;
			int rectH = (int)g.originalRect.height;

			Color[] srcBlock = sourceTexture.GetPixels(rectX, rectY, rectW, rectH);
			newTex.SetPixels(offsetX, offsetY, rectW, rectH, srcBlock);
			newTex.Apply();

			g.finalTexture = newTex;
			processedTextures.Add(newTex);
		}

		Texture2D tempAtlas = new Texture2D(256, 256, TextureFormat.RGBA32, false);
		Rect[] uvs = tempAtlas.PackTextures(processedTextures.ToArray(), 2, 4096);
		Texture2D finalAtlas = tempAtlas;

		if (forceSquarePOT)
		{
			int usedW = tempAtlas.width;
			int usedH = tempAtlas.height;
			int maxSize = Mathf.Max(usedW, usedH);
			int potSize = Mathf.NextPowerOfTwo(maxSize);
			potSize = Mathf.Max(potSize, 32);

			finalAtlas = new Texture2D(potSize, potSize, TextureFormat.RGBA32, false);
			Color32[] clear = new Color32[potSize * potSize];
			finalAtlas.SetPixels32(clear);
			finalAtlas.SetPixels(0, 0, usedW, usedH, tempAtlas.GetPixels());
			finalAtlas.Apply();

			for (int i = 0; i < uvs.Length; i++)
			{
				float newX = uvs[i].x * ((float)usedW / potSize);
				float newY = uvs[i].y * ((float)usedH / potSize);
				float newW = uvs[i].width * ((float)usedW / potSize);
				float newH = uvs[i].height * ((float)usedH / potSize);
				uvs[i] = new Rect(newX, newY, newW, newH);
			}
			if (tempAtlas != finalAtlas) DestroyImmediate(tempAtlas);
		}

		string saveFolderPath = EditorUtility.SaveFolderPanel("Save Font", Application.dataPath, "");
		if (string.IsNullOrEmpty(saveFolderPath)) return;

		string absolutePath = Path.Combine(saveFolderPath, fontName + ".png");
		string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
        
		File.WriteAllBytes(absolutePath, finalAtlas.EncodeToPNG());
        
		StringBuilder sb = new StringBuilder();
		int lineHeight = fixNumbers ? (int)Mathf.Max(maxNumHeight, overallMaxHeight) : (int)overallMaxHeight;
        
		// FNT 파일 저장 (참고용)
		sb.AppendLine("<?xml version=\"1.0\"?>");
		sb.AppendLine("<font>");
		sb.AppendLine($"  <info face=\"{fontName}\" size=\"{lineHeight}\" bold=\"0\" italic=\"0\" charset=\"\" unicode=\"1\" stretchH=\"100\" smooth=\"1\" aa=\"1\" padding=\"0,0,0,0\" spacing=\"1,1\" outline=\"0\"/>");
		sb.AppendLine($"  <common lineHeight=\"{lineHeight}\" base=\"{(int)(lineHeight * 0.8f)}\" scaleW=\"{finalAtlas.width}\" scaleH=\"{finalAtlas.height}\" pages=\"1\" packed=\"0\" alphaChnl=\"1\" redChnl=\"0\" greenChnl=\"0\" blueChnl=\"0\"/>");
		sb.AppendLine("  <pages>");
		sb.AppendLine($"    <page id=\"0\" file=\"{fontName}.png\" />");
		sb.AppendLine("  </pages>");
		sb.AppendLine($"  <chars count=\"{glyphs.Count}\">");

		for (int i = 0; i < glyphs.Count; i++)
		{
			var g = glyphs[i];
			Rect uv = uvs[i];
			int x = (int)(uv.x * finalAtlas.width);
			int y = (int)(uv.y * finalAtlas.height);
			int w = (int)(uv.width * finalAtlas.width);
			int h = (int)(uv.height * finalAtlas.height);
			int invY = finalAtlas.height - y - h;
			int id = (int)g.character;
			int xadv = g.finalTexture.width; 
			sb.AppendLine($"    <char id=\"{id}\" x=\"{x}\" y=\"{invY}\" width=\"{w}\" height=\"{h}\" xoffset=\"0\" yoffset=\"0\" xadvance=\"{xadv}\" page=\"0\" chnl=\"15\" />");
		}
		sb.AppendLine("  </chars>");
		sb.AppendLine("</font>");
		File.WriteAllText(Path.Combine(saveFolderPath, fontName + ".fnt"), sb.ToString());

		AssetDatabase.Refresh();

		// 3. Unity Font Asset 생성 (FontSize 강제 주입 포함)
		CreateUnityFontAsset(relativePath, fontName, glyphs, uvs, finalAtlas.width, finalAtlas.height, lineHeight);

		Debug.Log($"[ZP] 폰트 생성 완료! 경로: {saveFolderPath}");
        
		foreach (var tex in processedTextures) DestroyImmediate(tex);
	}

	void CreateUnityFontAsset(string texPath, string name, List<GlyphInfo> glyphs, Rect[] uvs, int atlasW, int atlasH, int lineHeight)
	{
		Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
		if (tex == null)
		{
			Debug.LogError("생성된 텍스처를 로드할 수 없습니다.");
			return;
		}

		string dir = Path.GetDirectoryName(texPath);
		string matPath = Path.Combine(dir, name + ".mat");
		Material fontMat = new Material(Shader.Find("UI/Default")); 
		fontMat.mainTexture = tex;
		AssetDatabase.CreateAsset(fontMat, matPath);

		string fontPath = Path.Combine(dir, name + ".fontsettings");
		Font customFont = new Font(name);
		customFont.material = fontMat;

		CharacterInfo[] charInfos = new CharacterInfo[glyphs.Count];

		for (int i = 0; i < glyphs.Count; i++)
		{
			Rect uv = uvs[i];
			float uMin = uv.x;
			float uMax = uv.x + uv.width;
			float vMin = uv.y;
			float vMax = uv.y + uv.height;

			int pW = (int)(uv.width * atlasW);
			int pH = (int)(uv.height * atlasH);

			CharacterInfo info = new CharacterInfo();
			info.index = (int)glyphs[i].character;
            
			info.uvBottomLeft = new Vector2(uMin, vMin);
			info.uvBottomRight = new Vector2(uMax, vMin);
			info.uvTopLeft = new Vector2(uMin, vMax);
			info.uvTopRight = new Vector2(uMax, vMax);

			info.minX = 0;
			info.maxX = pW;
			info.minY = -pH; 
			info.maxY = 0;
            
			info.advance = pW + 1; 

			charInfos[i] = info;
		}

		customFont.characterInfo = charInfos;
        
		// [핵심 수정] SerializedObject를 통해 FontSize 및 LineSpacing 강제 설정
		SerializedObject so = new SerializedObject(customFont);
		so.Update();
		so.FindProperty("m_FontSize").floatValue = lineHeight;      // 기준 폰트 크기 설정
		so.FindProperty("m_LineSpacing").floatValue = lineHeight;   // 줄 간격 설정
		so.FindProperty("m_Ascent").floatValue = lineHeight * 0.8f; // 기준선(Baseline) 대략 설정
		so.ApplyModifiedProperties();

		AssetDatabase.CreateAsset(customFont, fontPath);
		EditorUtility.SetDirty(customFont);
		AssetDatabase.SaveAssets();
	}

	private class GlyphInfo
	{
		public char character;
		public Rect originalRect;
		public Texture2D finalTexture;
	}
}