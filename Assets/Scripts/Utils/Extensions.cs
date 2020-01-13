
using UnityEngine;

public static class Extensions
{
    public static Vector3 ClampToMagnitude(this Vector3 vector, float maxMagnitude)
    {
        if(vector.sqrMagnitude > maxMagnitude * maxMagnitude)
        {
            return vector.normalized * maxMagnitude;
        }
        else
        {
            return vector;
        }
    }

    /// <summary>
    /// Gets the angle between (1, 0) and this vector. The angle is in radians.
    /// Returns 0 if this vector is zero (0, 0).
    /// </summary>
    public static float ToAngle(this Vector2 vector)
    {
        if (vector == Vector2.zero)
            return 0f;

        return Mathf.Atan2(vector.y, vector.x);
    }

    /// <summary>
    /// Returns this vector, clamped to a maximum magnitude.
    /// If the magnitude of this vector is less than the max value, then this vector is returned unmodified.
    /// </summary>
    public static Vector2 ClampToMagnitude(this Vector2 vector, float maxMagnitude)
    {
        if (maxMagnitude <= 0f)
            return Vector2.zero;

        if (vector.sqrMagnitude == 0f)
            return Vector2.zero;

        if (vector.sqrMagnitude > maxMagnitude * maxMagnitude)
            return vector.normalized * maxMagnitude;
        else
            return vector;
    }
}
