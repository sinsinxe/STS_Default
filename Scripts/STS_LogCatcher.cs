using UnityEngine;

public class STS_LogCatcher : MonoBehaviour
{
	// ✅ 최근 로그 정보 (Visual Scripting에서 접근 가능)
	public string LastLog { get; private set; }
	public string LastLogType { get; private set; }

	// ✅ 실시간 로그 감지용 (선택적 활용)
	public bool HasNewLog { get; private set; }

	private void OnEnable()
	{
		Application.logMessageReceived += OnLogReceived;
	}

	private void OnDisable()
	{
		Application.logMessageReceived -= OnLogReceived;
	}

	// ✅ Unity 콘솔 로그 수신 처리
	private void OnLogReceived(string logString, string stackTrace, LogType type)
	{
		LastLog = logString;
		LastLogType = type.ToString();
		HasNewLog = true;
	}

	// ✅ 최근 로그 반환
	public string GetLastLog()
	{
		return LastLog;
	}

	// ✅ 특정 타입(Log, Warning, Error)만 반환하는 확장 함수
	public string GetLastLogType(string filterType)
	{
		// 대소문자 무시 비교
		if (LastLogType.Equals(filterType, System.StringComparison.OrdinalIgnoreCase))
		{
			return LastLog;
		}
		else
		{
			return string.Empty;
		}
	}

	// ✅ 로그 플래그 리셋
	public void ConsumeLogFlag()
	{
		HasNewLog = false;
	}

	// ✅ 전체 로그 초기화
	public void ClearLog()
	{
		LastLog = string.Empty;
		LastLogType = string.Empty;
		HasNewLog = false;
	}
}
