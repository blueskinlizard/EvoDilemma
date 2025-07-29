using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MousePan : MonoBehaviour
{
    private Vector3 dragOrigin;
    [SerializeField] private float panSpeed = 0.5f;
    [SerializeField] float minZoom = 0.1f;
    [SerializeField] float maxZoom = 100f;
    [SerializeField] float zoomSpeed = 0.7f;

    void Update()
    {
        if(Input.GetMouseButtonDown(0)){
            dragOrigin = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        }

        if(Input.GetMouseButton(0)){
            Vector3 currentPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3 difference = dragOrigin - currentPos;
            transform.position += difference;
        }
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Camera.main.orthographicSize -= scroll * zoomSpeed;
        Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize, minZoom, maxZoom);
    }
}
