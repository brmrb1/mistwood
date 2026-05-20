using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Drag2DSprite : MonoBehaviour
{

    private void Start()
    {
        startPos = transform.position;
    }
    private void OnMouseDrag()
    {
        Vector3 cursorPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = new Vector3(cursorPos.x, cursorPos.y, transform.position.z);
    }
    private void OnMouseEnter()
    {
        transform.localScale += Vector3.one * 1.8f;
    }
    private void OnMouseExit()
    {
        transform.localScale -= Vector3.one * 1.8f;
    }
}
