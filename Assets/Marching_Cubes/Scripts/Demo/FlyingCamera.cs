using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyingCamera : MonoBehaviour
{
    public float _speed = 10;
    public float _rotationSpeed = 10;
    public bool _hideMouse = true;
    private float yaw = 0.0f;
    private float pitch = 0.0f;
    private Vector2 pitchClamp = new(-70, 80);

    public bool InputLocked { get; set; } = false;

    void Start()
    {
        if (_hideMouse)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.Locked;
        }

        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    // Update is called once per frame
    void Update()
    {
        // Only allow movement and rotation if mouse is locked AND not externally locked
        if (Cursor.lockState != CursorLockMode.Locked || InputLocked)
            return;

        // Shift to move faster
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? _speed * 2f : _speed;

        var dir = Input.GetAxis("Vertical") * transform.forward + Input.GetAxis("Horizontal") * transform.right;
        transform.position += (dir * currentSpeed * Time.deltaTime);

        // Q and E for up/down
        if (Input.GetKey(KeyCode.E))
            transform.position += Vector3.up * currentSpeed * Time.deltaTime;
        else if (Input.GetKey(KeyCode.Q))
            transform.position -= Vector3.up * currentSpeed * Time.deltaTime;

        yaw += _rotationSpeed * Input.GetAxis("Mouse X");
        pitch -= _rotationSpeed * Input.GetAxis("Mouse Y");
        pitch = Mathf.Clamp(pitch, pitchClamp.x, pitchClamp.y);

        transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);
    }
}