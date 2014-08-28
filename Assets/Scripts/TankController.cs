using UnityEngine;
using System.IO;
using System.Collections;

public class TankController : MonoBehaviour {


    public float forceValue;
    public float torqueValue;

    private GameObject cam;

    void Start(){
        cam = GameObject.FindGameObjectWithTag("MainCamera");
    }

    void Update(){
        cam.transform.position = new Vector3(
            transform.position.x,
            transform.position.y,
            cam.transform.position.z
        );
    }
	
	// Update is called once per frame
	void FixedUpdate () {
        rigidbody2D.AddTorque(-Input.GetAxis("Horizontal") * torqueValue);
        rigidbody2D.AddRelativeForce(Vector2.up * Input.GetAxis("Vertical") * forceValue);
	}
    
    public void Save(){
        PlayerPrefs.SetFloat("x", transform.localPosition.x);
        PlayerPrefs.SetFloat("y", transform.localPosition.y);
        PlayerPrefs.SetFloat("r", transform.rotation.eulerAngles.z);
    }

    public void Load(){
        transform.localPosition = new Vector3(
            PlayerPrefs.GetFloat("x", QNode.BLOCK_SIZE * 0.5f),
            PlayerPrefs.GetFloat("y", QNode.BLOCK_SIZE * 0.5f),
            -1
        );
        transform.localRotation = Quaternion.Euler(0, 0, PlayerPrefs.GetFloat("r", 0));
    }

    void OnDestroy(){
        Save();
    }
}
