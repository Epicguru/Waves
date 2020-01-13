
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;

public class MuzzleFlash : MonoBehaviour
{
    public Light2D Light;
    public SpriteRenderer Sprite;

    public float Duration = 0.1f;
    public float BaseLightIntensity = 0.2f;

    private float timer = 0f;

    private void UponSpawn()
    {
        timer = 0f;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if(timer >= Duration)
        {
            PoolObject.Despawn(this);
        }

        float alpha = 1f - Mathf.Clamp01(timer / Duration);

        var c = Sprite.color;
        c.a = alpha;
        Sprite.color = c;

        Light.intensity = BaseLightIntensity * alpha;
    }
}