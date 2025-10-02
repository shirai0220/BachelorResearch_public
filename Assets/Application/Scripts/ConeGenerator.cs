using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ConeGenerator : MonoBehaviour
{
    public int segments = 40;        // 円周の分割数（なめらかさ）
    public float radius = 0.6f;      // 半径 60cm = 0.6m
    public float height = 3.0f;      // 高さ 3m
    public Material mat;

    void Start()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Cone";

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3 * 2];

        // 頂点定義
        vertices[0] = new Vector3(0, 0, 0);            // 底面の中心 (y=0)
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.Deg2Rad * (360f * i / segments);
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices[i + 1] = new Vector3(x, 0, z);    // 底面の円周
        }
        vertices[segments + 1] = new Vector3(0, height, 0); // 先端 (y=3)

        // 三角形 (底面)
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % segments + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        // 三角形 (側面)
        for (int i = 0; i < segments; i++)
        {
            triangles[segments * 3 + i * 3] = i + 1;
            triangles[segments * 3 + i * 3 + 1] = (i + 1) % segments + 1;
            triangles[segments * 3 + i * 3 + 2] = vertices.Length - 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;

        // // マテリアル設定（透明な青）
        // // Material mat = new Material(Shader.Find("Standard"));
        // mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        // mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        // mat.SetInt("_ZWrite", 0);
        // mat.DisableKeyword("_ALPHATEST_ON");
        // mat.EnableKeyword("_ALPHABLEND_ON");
        // mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        // mat.renderQueue = 3000;

        // mat.SetFloat("_Mode", 3); // 3 = Transparent
        // mat.color = new Color(0f, 0f, 1f, 0.3f);  // RGBA (透明度0.3の青)

        GetComponent<MeshRenderer>().material = mat;
        
    }
}
