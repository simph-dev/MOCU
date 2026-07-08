using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MatchFov : MonoBehaviour
{
    public Camera vrCamera; // Перетащите вашу VR-камеру сюда в инспекторе
    private Camera uiCamera;

    void Start()
    {

        vrCamera = GameObject.Find("VrHelmetCamera").GetComponent<Camera>();
        uiCamera = this.GetComponent<Camera>();

        uiCamera.fieldOfView = 80;  // vrCamera.fieldOfView looks not real. 80 is just "hand peaked"

    }
}
