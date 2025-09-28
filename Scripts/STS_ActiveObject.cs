using UnityEngine;

public class STS_ActiveObject : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
	public GameObject ActiveObject;
	
	void Start()
    {
	   
    }
    
	public void SetActiveTrue()
	{
		ActiveObject.SetActive(true);
	}
    
	public void SetActiveFalse()
	{
		ActiveObject.SetActive(false);
	}
    

}
