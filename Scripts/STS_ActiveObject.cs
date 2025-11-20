using UnityEngine;

public class STS_ActiveObject : MonoBehaviour
{
	[SerializeField]
	private GameObject targetObject;
	
	public void SetActiveTrue()
	{
		if (targetObject != null)
			targetObject.SetActive(true);
	}
    
	public void SetActiveFalse()
	{
		if (targetObject != null)
			targetObject.SetActive(false);
	}
}
