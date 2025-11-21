using UnityEngine;
using System.Collections;

public class WhiteoutController : MonoBehaviour
{
    private GameObject whiteoutQuad;
    public Transform cameraTransform;

    void Start()
    {
        CreatewhiteoutQuad();
        whiteoutQuad.SetActive(false); // 初期は非表示
    }

    // 黒い板を作成
    private void CreatewhiteoutQuad()
    {
        whiteoutQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        whiteoutQuad.name = "whiteoutQuad";

        // カメラの子オブジェクトとして配置
        whiteoutQuad.transform.SetParent(cameraTransform);

        // カメラ前方 0.3m に固定
        whiteoutQuad.transform.localPosition = new Vector3(0, 0, 0.3f);
        whiteoutQuad.transform.localRotation = Quaternion.identity;

        // 画面いっぱいに広げる（視界を完全に覆うサイズ）
        whiteoutQuad.transform.localScale = new Vector3(5f, 5f, 1f);

        // 黒マテリアルを設定
        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = new Color(1f, 1f, 1f, 1f); // 完全白、不透明)
        mat.renderQueue = 5000; // 最前面に描画
        // mat.color = Color.black;

        whiteoutQuad.GetComponent<MeshRenderer>().material = mat;
    }

    // 外部から呼べるブラックアウト関数（秒数付き）
    public IEnumerator WhiteoutForSeconds(float duration)
    {
        yield return StartCoroutine(WhiteoutCoroutine(duration));
    }

    private IEnumerator WhiteoutCoroutine(float duration)
    {
        whiteoutQuad.SetActive(true);
        yield return new WaitForSeconds(duration);
        whiteoutQuad.SetActive(false);
    }

    public void activeWhiteout()
    {
        whiteoutQuad.SetActive(true);
    }
    public void notactiveWhiteout()
    {
        whiteoutQuad.SetActive(false);
    }
}
