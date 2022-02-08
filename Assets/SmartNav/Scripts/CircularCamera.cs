using UnityEngine;
using System.Collections.Generic;


public class CircularCamera : MonoBehaviour
{

    public List<GameObject> targets;

    GameObject target;

    Vector3 offset;
    float latitude = 0f;
    float longitude = 0f;
    float follow_target_longitude = 0f;
    bool update_target_longitude = false;
    
    float dist_to_target = 6.0f;
    float h_direction = 0.0f;
    float v_direction = 0.0f;

    float rotation_speed = Mathf.PI;
    float zoom_speed = 10f;

    // Used for the debug display TODO: Not have one per agent
    private static GUIStyle style = new GUIStyle();
    private static Rect rect;
    private static string helpMessage;
    private bool useMouseMovement = false;


    private void Start()
    {
        if (targets.Count > 0)
            target = targets[0];
        offset = new Vector3(dist_to_target * Mathf.Sin(longitude + follow_target_longitude) * Mathf.Cos(latitude), dist_to_target * Mathf.Sin(latitude), dist_to_target * Mathf.Cos(longitude + follow_target_longitude) * Mathf.Cos(latitude));

        // GUI init
        int w = Screen.width, h = Screen.height;
        style.alignment = TextAnchor.UpperRight;
        style.fontSize = h * 3 / 100;
        style.normal.textColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        rect = new Rect(-20, 20, w, h * 2 / 100);
        helpMessage = "Camera commands\n\n*** Rotations ***\n\n[arrow up] UP\n[arrow down] DOWN\n[arrow right] RIGHT\n[arrow left] LEFT\n\n*** Zoom ***\n\n[page up] ZOOM IN\n[page down] ZOOM OUT\n\n*** Lock target ***\n\n[right ctrl] LOCK / UNLOCK\n\n*** Target selection ***\n\n[1] Agent\n[2] Goal";
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void OnGUI()
    {
        if (Input.GetKey(KeyCode.F2))
        {
            GUI.Label(rect, helpMessage, style);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
            useMouseMovement = !useMouseMovement;

        if (Input.GetKey(KeyCode.Alpha1))
            target = targets[0];
        if (Input.GetKey(KeyCode.Alpha2))
            target = targets[1];

        if (target)
        { 
            // Update cam orientation
            h_direction = Input.GetAxis("CameraHorizontal") + (useMouseMovement ? Input.GetAxis("MouseHorizontal") : 0);
            v_direction = Input.GetAxis("CameraVertical") + (useMouseMovement ? Input.GetAxis("MouseVertical") : 0);
            longitude = (longitude + h_direction * rotation_speed * Time.deltaTime) % (2 * Mathf.PI);
            latitude = (latitude + v_direction * rotation_speed * Time.deltaTime) % (2 * Mathf.PI);

            // Lock / unlock the target angle
            if (Input.GetKey(KeyCode.RightControl))
                update_target_longitude = !update_target_longitude;

            if (update_target_longitude)
                follow_target_longitude = (Mathf.PI + target.transform.eulerAngles.y * Mathf.PI / 180f) % (2 * Mathf.PI);

            // Update zoom
            if (Input.GetKey(KeyCode.PageDown))
                dist_to_target = Mathf.Min(dist_to_target + zoom_speed * Time.deltaTime, 20f);
            if (Input.GetKey(KeyCode.PageUp))
                dist_to_target = Mathf.Max(dist_to_target - zoom_speed * Time.deltaTime, 1f);

            // Update cam position and orientation
            offset = new Vector3(dist_to_target * Mathf.Sin(longitude + follow_target_longitude) * Mathf.Cos(latitude), dist_to_target * Mathf.Sin(latitude), dist_to_target * Mathf.Cos(longitude + follow_target_longitude) * Mathf.Cos(latitude));
            transform.position = target.transform.position + offset;
            transform.LookAt(target.transform.position);
        }
    }
}
