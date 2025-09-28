using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

[CustomEditor(typeof(STS_AnimationPlayer))]
public class AnimatorButtonGeneratorEditor : Editor
{
	public override void OnInspectorGUI()
	{
		// 기본 인스펙터 UI를 그립니다.
		DrawDefaultInspector();

		// 현재 스크립트의 타겟을 가져옵니다.
		STS_AnimationPlayer script = (STS_AnimationPlayer)target;

		// "Fetch..." 버튼을 추가합니다.
		if (GUILayout.Button("Fetch Animator States/Parameters"))
		{
			if (script.targetAnimator != null && script.targetAnimator.runtimeAnimatorController != null)
			{
				// 데이터를 가져와서 script.parameters 리스트에 저장합니다.
				FetchDataFromController(script);
				// 변경된 데이터를 저장하기 위해 오브젝트를 'dirty'로 표시합니다.
				EditorUtility.SetDirty(script);
			}
			else
			{
				Debug.LogWarning("Target Animator가 할당되지 않았거나 Animator Controller가 없습니다.");
			}
		}
	}

	/// <summary>
	/// Animator Controller에서 상태 또는 파라미터 목록을 가져옵니다.
	/// </summary>
	private void FetchDataFromController(STS_AnimationPlayer script)
	{
		// 기존 데이터 초기화
		script.parameters.Clear();

		AnimatorController controller = script.targetAnimator.runtimeAnimatorController as AnimatorController;
		if (controller == null) return;

		// 파라미터 모드일 때: 모든 파라미터(Trigger, Bool, Int 등)를 가져옵니다.
		if (script.useParameterMode)
		{
			foreach (var param in controller.parameters)
			{
				script.parameters.Add(new AnimatorParameterInfo
				{
					name = param.name,
					type = param.type,
					boolValue = param.defaultBool, // 기본값도 함께 가져오기
					intValue = param.defaultInt
					// TODO: Float 타입이 필요한 경우 param.defaultFloat 추가
				});
			}
			Debug.Log($"{script.parameters.Count}개의 파라미터를 가져왔습니다.");
		}
		// 상태 재생 모드일 때: 모든 레이어의 모든 상태 이름을 가져옵니다.
		else
		{
			foreach (var layer in controller.layers)
			{
				foreach (var childState in layer.stateMachine.states)
				{
					// 상태 재생은 Trigger 타입으로 간주하여 버튼으로 생성되도록 설정
					script.parameters.Add(new AnimatorParameterInfo
					{
						name = childState.state.name,
						type = AnimatorControllerParameterType.Trigger 
					});
				}
			}
			Debug.Log($"{script.parameters.Count}개의 상태(State)를 가져왔습니다.");
		}
	}
}
