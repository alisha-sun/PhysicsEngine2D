using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cursor : MonoBehaviour
{
    DynamicObject obj;
    bool setAnchor = false;
    Vector2 prevObjPos;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            Collider2D hit = Physics2D.OverlapPoint(mousePos);

            if (hit != null && hit.gameObject.GetComponent<PhysicsObjectComponent>() != null)
            {
                PhysicsObject physicsObject = hit.gameObject.GetComponent<PhysicsObjectComponent>().self;
                if (physicsObject != null && physicsObject is DynamicObject d)
                {
                    obj = d;
                    prevObjPos = d.Position;

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
                obj.SetVelocity((obj.Position - prevObjPos) / Time.deltaTime);
                PhysicsEngine.instance.gameState.regions.UpdateRegions(obj);
                obj.ApplyTransform();
                prevObjPos = obj.Position;
            }
        }
    }
}
