using UnityEngine;
using System.IO;
using Microsoft.MixedReality.Toolkit.Input; // MRTK3のGazeInteractor
using UnityEngine.XR;

public class GazeLogger : MonoBehaviour
{
    private GazeInteractor gazeInteractor;
    private string filePath;

    void Start()
    {
        // シーンから GazeInteractor を探す
        gazeInteractor = FindObjectOfType<GazeInteractor>();

        // 保存先 (HoloLens2 でもアクセス可能)
        filePath = Path.Combine(Application.persistentDataPath, "GazeLog.csv");

        // ヘッダ行を作成
        File.WriteAllText(filePath, "Time,OriginX,OriginY,OriginZ,DirX,DirY,DirZ\n");
    }

    void Update()
    {
        if (gazeInteractor == null) return;

        // 視線の原点と方向を取得
        Vector3 origin = gazeInteractor.transform.position;
        Vector3 direction = gazeInteractor.transform.forward;

        // ログ出力 (Unityコンソール)
        Debug.Log($"[Gaze] Origin={origin}, Direction={direction}");

        // CSVに追記
        string line = $"{Time.time:F3},{origin.x:F4},{origin.y:F4},{origin.z:F4},{direction.x:F4},{direction.y:F4},{direction.z:F4}\n";
        File.AppendAllText(filePath, line);
    }
}
