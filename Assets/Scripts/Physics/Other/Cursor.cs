using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cursor : MonoBehaviour
{
    const float interval = .05f;

    DynamicObject obj;
    bool setAnchor = false;

    Vector2 prevMousePos;
    Vector2 mouseVelocity;
    float t = 0f;

    private void Start()
    {
        prevMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseVelocity = Vector2.zero;
    }
    private void Update()
    {
        t += Time.deltaTime;
        if (t > interval) {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseVelocity = (mousePos - prevMousePos) / t;
            prevMousePos = mousePos;
            t = 0f;
        }
        Debug.Log(mouseVelocity);

        if (Input.GetMouseButtonDown(0)) {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            Collider2D hit = Physics2D.OverlapPoint(mousePos);

            if (hit != null && hit.gameObject.GetComponent<PhysicsObjectComponent>() != null)
            {
                PhysicsObject physicsObject = hit.gameObject.GetComponent<PhysicsObjectComponent>().self;
                if (physicsObject != null && physicsObject is DynamicObject d)
                {
                    obj = d;

                    if (float.IsNaN(obj.Anchor.x)) {
                        obj.SetAnchor(obj.WorldToLocal_Point(mousePos));
                        setAnchor = true;
                    }
                    else setAnchor = false;
                }
            }
        }
        else if (Input.GetMouseButtonUp(0)) {
            if (obj != null)
            {
                if (setAnchor) obj.RemoveAnchor();
                obj = null;
            }
        }
        else {
            if (obj != null)
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                obj.SetPosition_Anchor(mousePos, true, true);
                obj.SetVelocity_Anchor(mouseVelocity);
                obj.ApplyTransform();
            }
        }
    }
}
