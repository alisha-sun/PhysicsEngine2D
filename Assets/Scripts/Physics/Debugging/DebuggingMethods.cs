using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Debug2
{
    private const float RATIO = .2f;
    public static void DrawArrow(Vector2 start, Vector2 direction, Color color, float duration = 0f)
    {
        Vector2 end = start + direction;
        Debug.DrawLine(start, end, color, duration);

        Debug.DrawLine(end, end - direction*RATIO + new Vector2(direction.y,-direction.x)*RATIO, color, duration);
        Debug.DrawLine(end, end - direction*RATIO + new Vector2(-direction.y, direction.x)*RATIO, color, duration);
    }
}
