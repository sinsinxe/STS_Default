#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class ComponentQuickMenu
{
	private const string PREF_ENABLED = "ComponentQuickMenu.Enabled";
	private const string MARK_NAME    = "ComponentQuickMenu__MARK";
	private const string BAR_NAME     = "ComponentQuickMenu__BAR";

	// 요청사항 반영
	private const int   WIDTH_SHRINK_PX = 10;   // 버튼 가로 10px 축소
	private const float FONT_SCALE      = 0.9f; // 폰트 10% 감소(= 90%)

	// 컬러(0~1 float)
	private static readonly Color COLOR_RED_BG   = new Color(0.7962264f, 0.1046818f, 0f, 1f); // Delete
	private static readonly Color COLOR_GREEN_BG = new Color(0.2735667f, 0.6377357f, 0.253891f, 1f); // Copy
	private static readonly Color COLOR_BLUE_BG  = new Color(0.2969099f, 0.5639141f, 0.7056604f, 1f); // Paste
	private static readonly Color COLOR_WHITE_FG = new Color(1f, 1f, 1f, 1f);

	// ✅ Button 상태 저장용
	private sealed class BtnUserData
	{
		public bool fontScaled;
		public bool hover;
		public bool down;
	}

	private static BtnUserData GetBtnData(Button b)
	{
		if (b.userData is not BtnUserData d)
		{
			d = new BtnUserData();
			b.userData = d;
		}
		return d;
	}

	private static bool Enabled
	{
		get => EditorPrefs.GetBool(PREF_ENABLED, true);
		set => EditorPrefs.SetBool(PREF_ENABLED, value);
	}

	static ComponentQuickMenu()
	{
		EditorApplication.update -= PatchAllInspectors_Update;
		EditorApplication.update += PatchAllInspectors_Update;

		Editor.finishedDefaultHeaderGUI -= OnFinishedDefaultHeaderGUI;
		Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
	}

	[MenuItem("Tools/Component Quick Menu/Enabled")]
	private static void ToggleEnabled()
	{
		Enabled = !Enabled;
		RepaintAll();
	}

	[MenuItem("Tools/Component Quick Menu/Enabled", true)]
	private static bool ToggleEnabled_Validate()
	{
		Menu.SetChecked("Tools/Component Quick Menu/Enabled", Enabled);
		return true;
	}

	private static void RepaintAll()
	{
		try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
			catch { /* ignore */ }
	}

	private static void OnFinishedDefaultHeaderGUI(Editor editor) { }

	// ===== UI Toolkit Patch =====
	private static double _nextPatchTime;

	private static void PatchAllInspectors_Update()
	{
		if (!Enabled) return;

		if (EditorApplication.timeSinceStartup < _nextPatchTime) return;
		_nextPatchTime = EditorApplication.timeSinceStartup + 0.3f;

		try
		{
			var allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
			foreach (var w in allWindows)
			{
				if (w == null) continue;
				if (w.GetType().FullName != "UnityEditor.InspectorWindow") continue;
				PatchInspectorWindow(w);
			}
		}
			catch (Exception e)
			{
				Debug.LogError($"[ComponentQuickMenu] Inspector patch update failed.\n{e}");
			}
	}

	private static void PatchInspectorWindow(EditorWindow inspectorWindow)
	{
		var root = inspectorWindow.rootVisualElement;
		if (root == null) return;

		var tracker = ActiveEditorTracker.sharedTracker;
		var editors = tracker?.activeEditors;
		if (editors == null || editors.Length == 0) return;

		var candidates = root.Query<VisualElement>().Where(ve =>
			ve.ClassListContains("unity-inspector-element") ||
			ve.ClassListContains("unity-editor-element") ||
			ve.ClassListContains("unity-inspector-editor")
		).ToList();

		if (candidates == null || candidates.Count == 0) return;

		foreach (var container in candidates)
		{
			if (container == null) continue;
			if (container.Q<VisualElement>(MARK_NAME) != null) continue;

			if (!TryResolveTargetComponent(container, editors, out var comp, out var compEditor))
				continue;

			var mark = new VisualElement { name = MARK_NAME };
			mark.style.display = DisplayStyle.None;
			container.Add(mark);

			var quickBar = BuildQuickBar(comp, compEditor);
			container.Insert(0, quickBar);
		}
	}

	private static bool TryResolveTargetComponent(VisualElement container, Editor[] editors, out Component comp, out Editor compEditor)
	{
		comp = null;
		compEditor = null;

		try
		{
			object maybeEditor =
				GetAnyFieldOrPropertyValue(container, "editor") ??
				GetAnyFieldOrPropertyValue(container, "m_Editor") ??
				GetAnyFieldOrPropertyValue(container, "m_InspectorEditor");

			if (maybeEditor is Editor ed && ed.target is Component c1)
			{
				comp = c1;
				compEditor = ed;
				return true;
			}

			string headerText = FindLikelyHeaderText(container);
			if (!string.IsNullOrEmpty(headerText))
			{
				foreach (var ed2 in editors)
				{
					if (ed2 == null || ed2.target == null) continue;
					if (ed2.target is not Component c2) continue;

					if (string.Equals(headerText, c2.GetType().Name, StringComparison.OrdinalIgnoreCase))
					{
						comp = c2;
						compEditor = ed2;
						return true;
					}
				}
			}
		}
			catch { }

		return false;
	}

	private static string FindLikelyHeaderText(VisualElement container)
	{
		try
		{
			var labels = container.Query<Label>().ToList();
			if (labels == null || labels.Count == 0) return null;

			foreach (var lb in labels)
			{
				var t = lb?.text;
				if (string.IsNullOrWhiteSpace(t)) continue;
				t = t.Trim();
				if (t.Length > 40) continue;
				if (t.Contains("(") || t.Contains(")")) continue;
				return t;
			}
		}
			catch { }
		return null;
	}

	private static object GetAnyFieldOrPropertyValue(object obj, string name)
	{
		if (obj == null) return null;
		var t = obj.GetType();

		var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (p != null) { try { return p.GetValue(obj); } catch { } }

		var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (f != null) { try { return f.GetValue(obj); } catch { } }

		return null;
	}

	// ======================================================================
	// ✅ Multi-selection helpers
	// ======================================================================
	private static Component[] GetTargetComponents(Component comp, Editor compEditor)
	{
		if (comp == null) return Array.Empty<Component>();

		try
		{
			if (compEditor != null && compEditor.targets != null && compEditor.targets.Length > 0)
			{
				var comps = compEditor.targets.OfType<Component>().Where(c => c != null).ToArray();
				if (comps.Length > 0)
				{
					var type = comps[0].GetType();
					if (comps.All(c => c.GetType() == type))
						return comps;
				}
			}
		}
			catch { /* ignore */ }

		return new[] { comp };
	}

	private static GameObject[] GetTargetGameObjects(Component[] comps)
	{
		if (comps == null || comps.Length == 0) return Array.Empty<GameObject>();

		var set = new HashSet<GameObject>();
		foreach (var c in comps)
		{
			if (c == null) continue;
			if (c.gameObject == null) continue;
			set.Add(c.gameObject);
		}
		return set.ToArray();
	}

	// ======================================================================
	// QuickBar
	// ======================================================================
	private static VisualElement BuildQuickBar(Component comp, Editor compEditor)
	{
		var bar = new VisualElement { name = BAR_NAME };

		// 오른쪽 정렬(공백 제거)
		bar.style.flexDirection = FlexDirection.Row;
		bar.style.alignItems = Align.Center;
		bar.style.justifyContent = Justify.FlexEnd;
		bar.style.flexGrow = 1;
		bar.style.width = new Length(100, LengthUnit.Percent);

		bar.style.paddingLeft = 0;
		bar.style.paddingRight = 0;
		bar.style.paddingTop = 0;
		bar.style.paddingBottom = 0;
		bar.style.marginBottom = 0;

		bar.style.borderTopWidth = 0;
		bar.style.borderBottomWidth = 0;
		bar.style.borderLeftWidth = 0;
		bar.style.borderRightWidth = 0;
		bar.style.borderTopLeftRadius = 0;
		bar.style.borderTopRightRadius = 0;
		bar.style.borderBottomLeftRadius = 0;
		bar.style.borderBottomRightRadius = 0;

		// 버튼 가로 10px 축소
		int W(int original) => Mathf.Max(16, original - WIDTH_SHRINK_PX);

		// ===== 색상 유틸: 비활성화 시에도 컬러감 유지 + 톤다운 + Hover/Pressed 반응 =====
		Color Darken(Color c, float factor)
			=> new Color(c.r * factor, c.g * factor, c.b * factor, c.a);

		Color Brighten(Color c, float t) // t: 0~1, white로 lerp
			=> Color.Lerp(c, Color.white, Mathf.Clamp01(t));

		void ApplyButtonColor(Button b, bool enabled, Color bg, Color fg)
		{
			var d = GetBtnData(b);

			if (!enabled)
			{
				b.style.backgroundColor = new StyleColor(Darken(bg, 0.55f));
				b.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.65f));
				b.style.opacity = 0.9f;
				return;
			}

			Color finalBg = bg;
			if (d.down)       finalBg = Darken(bg, 0.82f);
			else if (d.hover) finalBg = Brighten(bg, 0.18f);

			b.style.backgroundColor = new StyleColor(finalBg);
			b.style.color = new StyleColor(fg);
			b.style.opacity = 1f;
		}

		void HookHoverPress(Button b, Action refresh)
		{
			var d = GetBtnData(b);

			b.RegisterCallback<PointerEnterEvent>(_ => { d.hover = true;  refresh?.Invoke(); });
			b.RegisterCallback<PointerLeaveEvent>(_ => { d.hover = false; d.down = false; refresh?.Invoke(); });
			b.RegisterCallback<PointerDownEvent>(_  => { d.down  = true;  refresh?.Invoke(); });
			b.RegisterCallback<PointerUpEvent>(_    => { d.down  = false; refresh?.Invoke(); });
			b.RegisterCallback<PointerCaptureOutEvent>(_ => { d.down = false; refresh?.Invoke(); });
		}

		// ===== Buttons (라벨 극단 축약) =====
		var btnReset = MakeBtn("R", W(28), () =>
		{
			var targets = GetTargetComponents(comp, compEditor);
			if (targets.Length == 0) return;

			ResetComponents(targets, "Reset Component(s)");
			RepaintAll();
		});

		var btnCopy = MakeBtn("C", W(28), () =>
		{
			if (comp == null) return;
			ComponentUtilityCopy(comp);
			RepaintAll();
		});

		var btnPasteNew = MakeBtn("PN", W(34), () =>
		{
			var targets = GetTargetComponents(comp, compEditor);
			var gos = GetTargetGameObjects(targets);

			foreach (var go in gos)
			{
				if (go == null) continue;
				if (!CanPasteAsNewOrAssumeTrue(go)) continue;
				ComponentUtilityPasteAsNew(go);
			}
			RepaintAll();
		});

		var btnPasteValues = MakeBtn("PV", W(34), () =>
		{
			var targets = GetTargetComponents(comp, compEditor);
			foreach (var c in targets)
			{
				if (c == null) continue;
				if (!CanPasteValuesOrAssumeTrue(c)) continue;
				ComponentUtilityPasteValues(c);
			}
			RepaintAll();
		});

		var btnUp = MakeBtn("▲", W(28), () =>
		{
			if (comp == null) return;
			ComponentUtilityMoveUp(comp);
			RepaintAll();
		});

		var btnDown = MakeBtn("▼", W(28), () =>
		{
			if (comp == null) return;
			ComponentUtilityMoveDown(comp);
			RepaintAll();
		});

		var btnRemove = MakeBtn("X", W(28), () =>
		{
			if (comp == null) return;
			RemoveComponents(new[] { comp }, "Remove Component");
			RepaintAll();
		});

		// 마지막 버튼은 오른쪽 끝에 딱 붙게
		btnRemove.style.marginRight = 0;

		void UpdateStates()
		{
			if (comp == null) return;

			var targets = GetTargetComponents(comp, compEditor);
			var gos = GetTargetGameObjects(targets);

			btnReset.SetEnabled(targets.Length > 0);
			btnCopy.SetEnabled(true);

			bool	bool canPasteNewAny = false;
			foreach (var go in gos)
			{
				if (go == null) continue;
				if (CanPasteAsNewOrAssumeTrue(go)) { canPasteNewAny = true; break; }
			}

			bool canPasteValuesAny = false;
			foreach (var c in targets)
			{
				if (c == null) continue;
				if (CanPasteValuesOrAssumeTrue(c)) { canPasteValuesAny = true; break; }
			}

			btnPasteNew.SetEnabled(canPasteNewAny);
			btnPasteValues.SetEnabled(canPasteValuesAny);

			bool canUp = CanMoveUp(comp);
			bool canDown = CanMoveDown(comp);
			bool canRemove = (comp is not Transform);

			btnUp.SetEnabled(canUp);
			btnDown.SetEnabled(canDown);
			btnRemove.SetEnabled(canRemove);

			ApplyButtonColor(btnCopy, btnCopy.enabledSelf, COLOR_GREEN_BG, COLOR_WHITE_FG);
			ApplyButtonColor(btnPasteNew, btnPasteNew.enabledSelf, COLOR_BLUE_BG, COLOR_WHITE_FG);
			ApplyButtonColor(btnPasteValues, btnPasteValues.enabledSelf, COLOR_BLUE_BG, COLOR_WHITE_FG);
			ApplyButtonColor(btnRemove, btnRemove.enabledSelf, COLOR_RED_BG, COLOR_WHITE_FG);

			// Reset/Up/Down은 기본 스타일 유지
			btnReset.style.backgroundColor = StyleKeyword.Null;
			btnReset.style.color = StyleKeyword.Null;
			btnReset.style.opacity = btnReset.enabledSelf ? 1f : 0.9f;

			btnUp.style.backgroundColor = StyleKeyword.Null;
			btnUp.style.color = StyleKeyword.Null;
			btnUp.style.opacity = btnUp.enabledSelf ? 1f : 0.9f;

			btnDown.style.backgroundColor = StyleKeyword.Null;
			btnDown.style.color = StyleKeyword.Null;
			btnDown.style.opacity = btnDown.enabledSelf ? 1f : 0.9f;
		}

		// 컬러 버튼 hover/pressed
		HookHoverPress(btnCopy, UpdateStates);
		HookHoverPress(btnPasteNew, UpdateStates);
		HookHoverPress(btnPasteValues, UpdateStates);
		HookHoverPress(btnRemove, UpdateStates);

		bar.Add(btnReset);
		bar.Add(btnCopy);
		bar.Add(btnPasteNew);
		bar.Add(btnPasteValues);
		bar.Add(btnUp);
		bar.Add(btnDown);
		bar.Add(btnRemove);

		bar.RegisterCallback<AttachToPanelEvent>(_ =>
		{
			UpdateStates();
			bar.schedule.Execute(UpdateStates).Every(200);
		});

		bar.RegisterCallback<MouseEnterEvent>(_ => UpdateStates());

		return bar;
	}

	private static Button MakeBtn(string text, int width, Action onClick)
	{
		var b = new Button(() =>
		{
			try { onClick?.Invoke(); }
				catch (Exception e)
				{
					Debug.LogError($"[ComponentQuickMenu] Action '{text}' failed.\n{e}");
				}
		})
		{ text = text };

		b.style.width = width;
		b.style.height = 18;

		// 버튼 간 간격 2
		b.style.marginRight = 2;
		b.style.unityTextAlign = TextAnchor.MiddleCenter;

		// 폰트 사이즈 10% 감소
		b.RegisterCallback<AttachToPanelEvent>(_ =>
		{
			var d = GetBtnData(b);
			if (d.fontScaled) return;

			float baseSize = b.resolvedStyle.fontSize;
			if (baseSize > 0f)
			{
				b.style.fontSize = baseSize * FONT_SCALE;
				d.fontScaled = true;
			}
		});

		return b;
	}

	// ===== Can... =====
	private static bool CanMoveUp(Component c)
	{
		if (c == null) return false;
		if (c is Transform) return false;

		var list = c.gameObject.GetComponents<Component>();
		int idx = Array.IndexOf(list, c);
		return idx > 1;
	}

	private static bool CanMoveDown(Component c)
	{
		if (c == null) return false;
		if (c is Transform) return false;

		var list = c.gameObject.GetComponents<Component>();
		int idx = Array.IndexOf(list, c);
		return idx >= 1 && idx < list.Length - 1;
	}

	private static bool CanPasteAsNewOrAssumeTrue(GameObject go)
	{
		try
		{
			var t = GetComponentUtilityType();
			var mi = t?.GetMethod("CanPasteComponentAsNew",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
				null, new[] { typeof(GameObject) }, null);

			if (mi == null) return true;
			return (bool)mi.Invoke(null, new object[] { go });
		}
			catch
			{
				return true;
			}
	}

	private static bool CanPasteValuesOrAssumeTrue(Component c)
	{
		try
		{
			var t = GetComponentUtilityType();
			var mi = t?.GetMethod("CanPasteComponentValues",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
				null, new[] { typeof(Component) }, null);

			if (mi == null) return true;
			return (bool)mi.Invoke(null, new object[] { c });
		}
			catch
			{
				return true;
			}
	}

	// ===== Actions =====
	private static void RemoveComponents(Component[] comps, string undoName)
	{
		foreach (var c in comps)
		{
			if (c is Transform) continue;
			Undo.DestroyObjectImmediate(c);
		}
	}

	private static void ResetComponents(Component[] comps, string undoName)
	{
		if (comps == null || comps.Length == 0) return;

		if (TrySmartReset(comps, undoName))
			return;

		foreach (var comp in comps)
		{
			if (comp == null) continue;

			var type = comp.GetType();
			var tempGO = new GameObject("~ResetTemp~") { hideFlags = HideFlags.HideAndDontSave };

			try
			{
				var tempComp = tempGO.AddComponent(type);
				Undo.RecordObject(comp, undoName);
				EditorUtility.CopySerialized(tempComp, comp);
				EditorUtility.SetDirty(comp);
			}
				finally
			{
				UnityEngine.Object.DestroyImmediate(tempGO);
			}
		}
	}

	private static bool TrySmartReset(Component[] comps, string undoName)
	{
		try
		{
			var unsupportedType = typeof(Editor).Assembly.GetType("UnityEditor.Unsupported");
			var smartReset = unsupportedType?.GetMethod("SmartReset",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
				null, new[] { typeof(UnityEngine.Object) }, null);

			if (smartReset == null) return false;

			foreach (var comp in comps)
			{
				if (comp == null) continue;
				Undo.RecordObject(comp, undoName);
				smartReset.Invoke(null, new object[] { comp });
				EditorUtility.SetDirty(comp);
			}
			return true;
		}
			catch
			{
				return false;
			}
	}

	// ===== ComponentUtility wrappers =====
	private static Type GetComponentUtilityType()
	=> typeof(Editor).Assembly.GetType("UnityEditorInternal.ComponentUtility");

	private static void ComponentUtilityCopy(Component c)
	=> InvokeComponentUtility("CopyComponent", new[] { typeof(Component) }, new object[] { c }, "CopyComponent");

	private static void ComponentUtilityPasteAsNew(GameObject go)
	=> InvokeComponentUtility("PasteComponentAsNew", new[] { typeof(GameObject) }, new object[] { go }, "PasteComponentAsNew");

	private static void ComponentUtilityPasteValues(Component c)
	=> InvokeComponentUtility("PasteComponentValues", new[] { typeof(Component) }, new object[] { c }, "PasteComponentValues");

	private static void ComponentUtilityMoveUp(Component c)
	=> InvokeComponentUtility("MoveComponentUp", new[] { typeof(Component) }, new object[] { c }, "MoveComponentUp");

	private static void ComponentUtilityMoveDown(Component c)
	=> InvokeComponentUtility("MoveComponentDown", new[] { typeof(Component) }, new object[] { c }, "MoveComponentDown");

	private static void InvokeComponentUtility(string method, Type[] sig, object[] args, string label)
	{
		var t = GetComponentUtilityType();
		if (t == null)
		{
			Debug.LogError("[ComponentQuickMenu] UnityEditorInternal.ComponentUtility type not found.");
			return;
		}

		var mi = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, sig, null);
		if (mi == null)
		{
			Debug.LogError($"[ComponentQuickMenu] ComponentUtility.{method} not found. (Unity version diff)");
			return;
		}

		try
		{
			mi.Invoke(null, args);
		}
			catch (TargetInvocationException tie)
			{
				Debug.LogError($"[ComponentQuickMenu] {label} failed.\n{tie.InnerException}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[ComponentQuickMenu] {label} failed.\n{ex}");
			}
	}
}
#endif
