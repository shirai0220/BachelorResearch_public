using UnityEngine;

public class InvisibleCollider : MonoBehaviour
{
    void Start()
    {
        // Renderer を無効にする（見えなくする）
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }

        // BoxCollider を有効にする（当たり判定は残す）
        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.enabled = true;
        }
    }
}
