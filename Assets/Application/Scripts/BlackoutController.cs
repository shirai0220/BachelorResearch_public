using UnityEngine;
using System.Collections;

public class BlackoutController : MonoBehaviour
{
    private GameObject blackoutQuad;

    void Start()
    {
        CreateBlackoutQuad();
        blackoutQuad.SetActive(false); // 初期は非表示
    }

    // 黒い板を作成
    private void CreateBlackoutQuad()
    {
        blackoutQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        blackoutQuad.name = "BlackoutQuad";

        // カメラの子オブジェクトとして配置
        blackoutQuad.transform.SetParent(Camera.main.transform);

        // カメラ前方 0.3m に固定
        blackoutQuad.transform.localPosition = new Vector3(0, 0, 0.3f);
        blackoutQuad.transform.localRotation = Quaternion.identity;

        // 画面いっぱいに広げる（視界を完全に覆うサイズ）
        blackoutQuad.transform.localScale = new Vector3(5f, 5f, 1f);

        // 黒マテリアルを設定
        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = Color.black;

        blackoutQuad.GetComponent<MeshRenderer>().material = mat;
    }

    // 外部から呼べるブラックアウト関数（秒数付き）
    public IEnumerator BlackoutForSeconds(float duration)
    {
        yield return StartCoroutine(BlackoutCoroutine(duration));
    }

    private IEnumerator BlackoutCoroutine(float duration)
    {
        blackoutQuad.SetActive(true);
        yield return new WaitForSeconds(duration);
        blackoutQuad.SetActive(false);
    }

    public void activeBlackOut()
    {
        blackoutQuad.SetActive(true);
    }
    public void notactiveBlackOut()
    {
        blackoutQuad.SetActive(false);
    }
}
