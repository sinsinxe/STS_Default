using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 애니메이터의 파라미터 정보를 저장하기 위한 데이터 클래스입니다.
/// 파라미터의 이름, 타입, 그리고 UI에서 제어할 값을 담습니다.
/// </summary>
[System.Serializable]
public class AnimatorParameterInfo
{
	public string name;
	public AnimatorControllerParameterType type;

	// UI에서 값을 입력받고 상태를 저장하기 위한 변수들
	public bool boolValue;
	public int intValue;
	// TODO: Float 타입이 필요한 경우 public float floatValue; 추가
}


public class STS_AnimationPlayer : MonoBehaviour
{
	[Header("Animator Settings")]
	[Tooltip("애니메이션 상태(노드) 또는 파라미터를 제어할 Animator 컴포넌트입니다.")]
	public Animator targetAnimator;

	[Header("Mode Selection")]
	[Tooltip("체크하면 애니메이션 상태 재생 대신 파라미터(Trigger, Bool, Int)를 제어합니다.")]
	public bool useParameterMode = false;

	[Header("UI Prefab Settings")]
	[Tooltip("상태 재생 및 트리거 발동을 위한 버튼 프리팹입니다.")]
	public GameObject buttonPrefab;
	[Tooltip("Bool 파라미터 제어를 위한 토글(Toggle) 프리팹입니다.")]
	public GameObject togglePrefab;
	[Tooltip("Int 파라미터 제어를 위한 입력 필드(Input Field) 프리팹입니다. 자식으로 InputField와 Button이 있어야 합니다.")]
	public GameObject intInputFieldPrefab;
	[Tooltip("생성된 UI 컨트롤들이 배치될 부모 UI 객체입니다.")]
	public Transform buttonParent;

	[Header("Execution")]
	[Tooltip("true로 설정하면 게임 시작 시 자동으로 UI 컨트롤을 생성합니다.")]
	public bool initializeOnStart = true;

	[Header("Animator Data (Read-Only)")]
	[Tooltip("에디터에서 가져온 상태 또는 파라미터 목록입니다. 직접 수정하지 마세요.")]
	public List<AnimatorParameterInfo> parameters = new List<AnimatorParameterInfo>();

	void Start()
	{
		if (initializeOnStart)
		{
			GenerateControls();
		}
	}

	/// <summary>
	/// 저장된 파라미터 목록을 기반으로 UI 컨트롤(버튼, 토글 등)을 생성합니다.
	/// </summary>
	public void GenerateControls()
	{
		if (targetAnimator == null || buttonParent == null)
		{
			Debug.LogError("필수 컴포넌트(Animator, Button Parent)가 할당되지 않았습니다.", this);
			return;
		}

		// 기존 UI 컨트롤 삭제
		foreach (Transform child in buttonParent)
		{
			Destroy(child.gameObject);
		}

		if (parameters == null || parameters.Count == 0)
		{
			Debug.LogWarning("가져올 파라미터 목록이 비어있습니다. 에디터에서 'Fetch Data' 버튼을 클릭했는지 확인해주세요.", this);
			return;
		}

		// 저장된 각 파라미터에 대해 UI 생성
		foreach (var paramInfo in parameters)
		{
			GameObject newUIElement = null;

			// 파라미터 모드일 때만 타입별 UI를 생성
			if (useParameterMode)
			{
				switch (paramInfo.type)
				{
				case AnimatorControllerParameterType.Bool:
					if (togglePrefab == null) continue;
					newUIElement = Instantiate(togglePrefab, buttonParent);
					// 자식의 Text/TMP_Text 컴포넌트를 찾아 라벨 설정
					var toggleLabel = newUIElement.GetComponentInChildren<TMP_Text>() ?? (Component)newUIElement.GetComponentInChildren<Text>();
					if (toggleLabel is TMP_Text tmp) tmp.text = paramInfo.name;
					else if (toggleLabel is Text legacy) legacy.text = paramInfo.name;

					var toggle = newUIElement.GetComponent<Toggle>();
					if(toggle != null)
					{
						toggle.isOn = paramInfo.boolValue; // Inspector에 저장된 값으로 초기화
						toggle.onValueChanged.AddListener((value) => {
							paramInfo.boolValue = value; // 변경된 값을 다시 저장
							ActivateAnimation(paramInfo);
						});
					}
					break;

				case AnimatorControllerParameterType.Int:
					if (intInputFieldPrefab == null) continue;
					newUIElement = Instantiate(intInputFieldPrefab, buttonParent);
					// 자식의 Text/TMP_Text 컴포넌트를 찾아 라벨 설정
					var inputLabel = newUIElement.GetComponentInChildren<TMP_Text>() ?? (Component)newUIElement.GetComponentInChildren<Text>();
					if (inputLabel is TMP_Text tmpInput) tmpInput.text = paramInfo.name;
					else if (inputLabel is Text legacyInput) legacyInput.text = paramInfo.name;
                        
					var inputField = newUIElement.GetComponentInChildren<TMP_InputField>();
					var setButton = newUIElement.GetComponentInChildren<Button>();

					if(inputField != null && setButton != null)
					{
						inputField.text = paramInfo.intValue.ToString();
						setButton.onClick.AddListener(() => {
							if (int.TryParse(inputField.text, out int value))
							{
								paramInfo.intValue = value;
								ActivateAnimation(paramInfo);
							}
						});
					}
					break;

				case AnimatorControllerParameterType.Trigger:
					// Trigger 타입은 아래 default에서 buttonPrefab으로 처리
				default:
					break; // Float 등 다른 타입은 현재 미지원
				}
			}

			// UI가 생성되지 않았다면 (상태 재생 모드 또는 Trigger 파라미터) 버튼 생성
			if (newUIElement == null)
			{
				if (buttonPrefab == null) continue;
				newUIElement = Instantiate(buttonPrefab, buttonParent);
				var buttonText = newUIElement.GetComponentInChildren<TMP_Text>();
				if (buttonText != null) buttonText.text = paramInfo.name;

				var buttonComponent = newUIElement.GetComponent<Button>();
				if (buttonComponent != null)
				{
					// 리스너에 추가할 파라미터 정보를 복사해 둠 (클로저 문제 방지)
					AnimatorParameterInfo currentParam = paramInfo;
					buttonComponent.onClick.AddListener(() => ActivateAnimation(currentParam));
				}
			}
		}
		Debug.Log($"{parameters.Count}개의 UI 컨트롤을 생성했습니다.", this);
	}

	/// <summary>
	/// 파라미터 타입에 따라 애니메이션 상태를 재생하거나 파라미터 값을 변경합니다.
	/// </summary>
	public void ActivateAnimation(AnimatorParameterInfo paramInfo)
	{
		if (targetAnimator == null) return;

		// 파라미터 모드일 때
		if (useParameterMode)
		{
			switch (paramInfo.type)
			{
			case AnimatorControllerParameterType.Bool:
				targetAnimator.SetBool(paramInfo.name, paramInfo.boolValue);
				Debug.Log($"Bool Parameter '{paramInfo.name}' set to: {paramInfo.boolValue}");
				break;

			case AnimatorControllerParameterType.Int:
				targetAnimator.SetInteger(paramInfo.name, paramInfo.intValue);
				Debug.Log($"Int Parameter '{paramInfo.name}' set to: {paramInfo.intValue}");
				break;

			case AnimatorControllerParameterType.Trigger:
				targetAnimator.SetTrigger(paramInfo.name);
				Debug.Log($"Trigger Fired: {paramInfo.name}");
				break;
                
				// TODO: Float 타입이 필요한 경우 여기에 SetFloat 로직 추가
			}
		}
		// 상태 재생 모드일 때
		else
		{
			targetAnimator.Play(paramInfo.name, 0, 0f);
			Debug.Log($"State Played: {paramInfo.name}");
		}
	}
}
