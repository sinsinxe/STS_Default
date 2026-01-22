using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 지원을 위해 추가

public class STS_CountNum : MonoBehaviour
{
	[Header("UI Settings")]
	[Tooltip("기본 UI Text 컴포넌트 (사용할 경우 할당)")]
	public Text legacyText;
	[Tooltip("TextMeshPro 컴포넌트 (사용할 경우 할당)")]
	public TMP_Text tmpText;

	[Header("Counting Settings")]
	public float duration = 2.0f;    // 카운팅에 걸리는 총 시간
	public long startNumber = 0;     // 시작 숫자 (큰 숫자를 대비해 long 사용)
	public long endNumber = 10000;   // 목표 숫자
    
	[Header("Format Settings")]
	[Tooltip("N0: 천단위 콤마, D: 일반 숫자, C: 통화 표시(이미지폰트에 기호가 있어야 함)")]
	public string numberFormat = "N0"; 

	[Header("State")]
	public bool isCounting = false;
	private float timer = 0.0f;

	void Update()
	{
		if (isCounting)
		{
			timer += Time.deltaTime;

			// 1. 진행 비율 계산 (0에서 1 사이)
			float progress = Mathf.Clamp01(timer / duration);

			// 2. 숫자를 보간하여 계산
			long currentNumber = (long)Mathf.Lerp(startNumber, endNumber, progress);

			// 3. UI 업데이트
			UpdateText(currentNumber.ToString(numberFormat));

			// 4. 종료 체크
			if (timer >= duration)
			{
				FinishCounting();
			}
		}
	}

	/// <summary>
	/// 외부 버튼이나 스크립트에서 카운팅을 시작할 때 호출합니다.
	/// </summary>
	public void StartCounting()
	{
		if (duration <= 0) duration = 0.1f; // 0초로 설정 시 에러 방지
        
		timer = 0.0f;
		isCounting = true;
		Debug.Log($"카운팅 시작: {startNumber} -> {endNumber} ({duration}초)");
	}

	/// <summary>
	/// 강제로 카운팅을 중단하거나 즉시 완료합니다.
	/// </summary>
	public void FinishCounting()
	{
		isCounting = false;
		timer = 0.0f;
		UpdateText(endNumber.ToString(numberFormat)); // 최종 숫자 확정
		Debug.Log("카운팅 완료");
	}

	/// <summary>
	/// 할당된 텍스트 컴포넌트에 값을 출력합니다.
	/// </summary>
	private void UpdateText(string message)
	{
		if (legacyText != null) legacyText.text = message;
		if (tmpText != null) tmpText.text = message;
	}
}