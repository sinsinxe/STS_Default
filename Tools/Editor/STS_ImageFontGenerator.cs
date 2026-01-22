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
	private DefaultAsset saveFolder; // [개선 1] 저장될 폴더 객체
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
		GUILayout.Label("[STS] 이미지 폰트 생성기 (v2.0)", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox(
			"사용 방법:\n" +
			"1. '원본 이미지'를 넣고 '저장 폴더'를 지정하세요.\n" +
			"2. '1. 이미지 분석' 버튼을 눌러 글자를 인식시킵니다.\n" +
			"3. 감지된 개수와 입력한 글자 수가 같으면 '2. 폰트 생성' 버튼을 누르세요.", 
			MessageType.Info);

		EditorGUILayout.Space();

		// 1. 리소스 설정 섹션
		GUILayout.Label("1. 리소스 설정", EditorStyles.boldLabel);
        
		sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
			new GUIContent("원본 이미지 (Texture)", "글자가 나열된 투명 배경의 PNG 이미지를 넣어주세요."), 
			sourceTexture, typeof(Texture2D), false);

		fontName = EditorGUILayout.TextField(
			new GUIContent("저장할 폰트 이름", "생성될 .fnt 파일과 .png 파일의 이름입니다."), 
			fontName);

		// [개선 1] 저장할 폰트 폴더 옵션 추가
		saveFolder = (DefaultAsset)EditorGUILayout.ObjectField(
			new GUIContent("저장 폴더 (Option)", "지정하면 저장 창 없이 이 폴더에 바로 저장됩니다."), 
			saveFolder, typeof(DefaultAsset), false);
        
		EditorGUILayout.Space();
        
		// 2. 세부 설정 섹션
		GUILayout.Label("2. 세부 설정", EditorStyles.boldLabel);

		GUILayout.Label(new GUIContent("글자 순서 입력 (Character Sequence)", "이미지에 보이는 글자 순서대로 빠짐없이 입력해주세요."));
		charSequence = EditorGUILayout.TextArea(charSequence, GUILayout.Height(40));

		alphaThreshold = EditorGUILayout.IntSlider(
			new GUIContent("투명도 민감도 (Alpha)", "배경과 글자를 구분하는 기준값입니다."), 
			alphaThreshold, 0, 255);

		fixNumbers = EditorGUILayout.Toggle(
			new GUIContent("숫자/기호 정렬 최적화", "체크: 숫자 너비 통일 및 기호 위치 보정\n미체크: 모든 글자 높이값 동일 유지"), 
			fixNumbers);

		forceSquarePOT = EditorGUILayout.Toggle(
			new GUIContent("정사각 2의 거듭제곱 (POT)", "체크 시 이미지를 256, 512 등 정사각 규격으로 생성합니다."),
			forceSquarePOT);

		EditorGUILayout.Space(15);
        
		// 3. 액션 버튼 섹션
		if (GUILayout.Button("1. 이미지 분석 실행 (Analyze)", GUILayout.Height(35)))
		{
			AnalyzeImage();
		}

		if (detectedRects.Count > 0)
		{
			EditorGUILayout.Space();
			GUILayout.Label("3. 분석 결과 확인", EditorStyles.boldLabel);

			bool countMatch = charSequence.Length == detectedRects.Count;
			string statusMsg = $"감지된 글자 영역: {detectedRects.Count}개\n입력한 글자 개수: {charSequence.Length}개";

			if (countMatch)
			{
				EditorGUILayout.HelpBox(statusMsg + "\n\n개수가 일치합니다! 폰트를 생성할 수 있습니다.", MessageType.Info);
				GUI.backgroundColor = Color.green; 
				if (GUILayout.Button("2. 폰트 파일 생성 및 저장 (Generate)", GUILayout.Height(40)))
				{
					GenerateFont();
				}
				GUI.backgroundColor = Color.white;
			}
			else
			{
				EditorGUILayout.HelpBox(statusMsg + "\n\n[주의] 개수가 일치하지 않습니다.", MessageType.Error);
			}
		}
	}

	void AnalyzeImage()
	{
		if (sourceTexture == null) 
		{
			EditorUtility.DisplayDialog("알림", "원본 이미지를 먼저 넣어주세요.", "확인");
			return;
		}

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

			if (rowHasPixel && !inRow) { inRow = true; startY = y; }
			else if (!rowHasPixel && inRow) { inRow = false; rows.Add((startY, y)); }
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
					if (pixels[y * width + x].a > alphaThreshold) { colHasPixel = true; break; }
				}

				if (colHasPixel && !inChar) { inChar = true; startX = x; }
				else if (!colHasPixel && inChar) { inChar = false; detectedRects.Add(RefineBounds(pixels, width, startX, row.start, x - startX, row.end - row.start)); }
			}
			if (inChar) detectedRects.Add(RefineBounds(pixels, width, startX, row.start, width - startX, row.end - row.start));
		}

		detectedRects.Sort((a, b) =>
		{
			if (Mathf.Abs(a.y - b.y) > 10) return b.y.CompareTo(a.y);
			return a.x.CompareTo(b.x);
		});
	}

	Rect RefineBounds(Color32[] pixels, int texWidth, int x, int y, int w, int h)
	{
		int minX = x + w, maxX = x, minY = y + h, maxY = y;
		bool foundAny = false;
		for(int cy = y; cy < y + h; cy++) {
			for(int cx = x; cx < x + w; cx++) {
				if (pixels[cy * texWidth + cx].a > alphaThreshold) {
					if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
					if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;
					foundAny = true;
				}
			}
		}
		return foundAny ? new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1) : new Rect(x, y, w, h);
	}

	void GenerateFont()
	{
		if (detectedRects.Count == 0) return;
        
		int count = Mathf.Min(charSequence.Length, detectedRects.Count);
		var glyphs = new List<GlyphInfo>();
        
		float maxNumWidth = 0;
		float maxNumHeight = 0;
		float overallMaxHeight = 0;

		// 1. 최대 값 사전 계산
		for (int i = 0; i < count; i++)
		{
			char c = charSequence[i];
			Rect r = detectedRects[i];
			if (r.height > overallMaxHeight) overallMaxHeight = r.height;
			if (char.IsDigit(c))
			{
				if (r.width > maxNumWidth) maxNumWidth = r.width;
				if (r.height > maxNumHeight) maxNumHeight = r.height;
			}
			glyphs.Add(new GlyphInfo { character = c, originalRect = r });
		}
        
		if (maxNumHeight == 0) maxNumHeight = overallMaxHeight;

		// 2. 개별 글자 텍스처 생성
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
				// 최적화 모드: 숫자 너비 통일, 기호 정렬
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
			else
			{
				// [개선 2] 최적화 해제 시: 모든 문자의 높이를 동일하게 유지
				targetH = (int)overallMaxHeight;
				offsetY = 0; // 하단 정렬
			}

			Texture2D newTex = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
			newTex.SetPixels32(new Color32[targetW * targetH]);
			newTex.SetPixels(offsetX, offsetY, (int)g.originalRect.width, (int)g.originalRect.height, 
				sourceTexture.GetPixels((int)g.originalRect.x, (int)g.originalRect.y, (int)g.originalRect.width, (int)g.originalRect.height));
			newTex.Apply();

			g.finalTexture = newTex;
			processedTextures.Add(newTex);
		}

		// 3. 아틀라스 패킹
		Texture2D tempAtlas = new Texture2D(256, 256, TextureFormat.RGBA32, false);
		Rect[] uvs = tempAtlas.PackTextures(processedTextures.ToArray(), 2, 4096);
		Texture2D finalAtlas = tempAtlas;

		if (forceSquarePOT)
		{
			int potSize = Mathf.Max(Mathf.NextPowerOfTwo(Mathf.Max(tempAtlas.width, tempAtlas.height)), 32);
			finalAtlas = new Texture2D(potSize, potSize, TextureFormat.RGBA32, false);
			finalAtlas.SetPixels32(new Color32[potSize * potSize]);
			finalAtlas.SetPixels(0, 0, tempAtlas.width, tempAtlas.height, tempAtlas.GetPixels());
			finalAtlas.Apply();

			for (int i = 0; i < uvs.Length; i++)
			{
				uvs[i] = new Rect(uvs[i].x * ((float)tempAtlas.width / potSize), uvs[i].y * ((float)tempAtlas.height / potSize), 
					uvs[i].width * ((float)tempAtlas.width / potSize), uvs[i].height * ((float)tempAtlas.height / potSize));
			}
		}

		// 4. 데이터 생성
		StringBuilder sb = new StringBuilder();
		int lineHeight = (int)overallMaxHeight;
		sb.AppendLine("<?xml version=\"1.0\"?>\n<font>");
		sb.AppendLine($"  <info face=\"{fontName}\" size=\"{lineHeight}\" bold=\"0\" italic=\"0\" charset=\"\" unicode=\"1\" stretchH=\"100\" smooth=\"1\" aa=\"1\" padding=\"0,0,0,0\" spacing=\"1,1\" outline=\"0\"/>");
		sb.AppendLine($"  <common lineHeight=\"{lineHeight}\" base=\"{(int)(lineHeight * 0.8f)}\" scaleW=\"{finalAtlas.width}\" scaleH=\"{finalAtlas.height}\" pages=\"1\" packed=\"0\" alphaChnl=\"1\" redChnl=\"0\" greenChnl=\"0\" blueChnl=\"0\"/>");
		sb.AppendLine($"  <pages>\n    <page id=\"0\" file=\"{fontName}.png\" />\n  </pages>");
		sb.AppendLine($"  <chars count=\"{glyphs.Count}\">");

		for (int i = 0; i < glyphs.Count; i++)
		{
			var g = glyphs[i]; Rect uv = uvs[i];
			int x = (int)(uv.x * finalAtlas.width); int y = (int)(uv.y * finalAtlas.height);
			int w = (int)(uv.width * finalAtlas.width); int h = (int)(uv.height * finalAtlas.height);
			sb.AppendLine($"    <char id=\"{(int)g.character}\" x=\"{x}\" y=\"{finalAtlas.height - y - h}\" width=\"{w}\" height=\"{h}\" xoffset=\"0\" yoffset=\"0\" xadvance=\"{g.finalTexture.width}\" page=\"0\" chnl=\"15\" />");
		}
		sb.AppendLine("  </chars>\n</font>");

		// 5. 저장 로직 (개선 1 반영)
		string savePath = "";
		if (saveFolder != null) savePath = AssetDatabase.GetAssetPath(saveFolder);
		else savePath = EditorUtility.SaveFolderPanel("Save Font", Application.dataPath, "");

		if (!string.IsNullOrEmpty(savePath))
		{
			File.WriteAllBytes(Path.Combine(savePath, fontName + ".png"), finalAtlas.EncodeToPNG());
			File.WriteAllText(Path.Combine(savePath, fontName + ".fnt"), sb.ToString());
			AssetDatabase.Refresh();
			Debug.Log($"[ZP] 폰트 저장 완료: {savePath}");
		}

		foreach (var tex in processedTextures) DestroyImmediate(tex);
	}

	private class GlyphInfo { public char character; public Rect originalRect; public Texture2D finalTexture; }
}