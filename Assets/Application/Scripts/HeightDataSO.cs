using UnityEngine;

[CreateAssetMenu(
    fileName = "HeightData",
    menuName = "Data/Height Data"
)]
public class HeightDataSO : ScriptableObject
{
    [Header("共通データ")]
    public float diff;

    public void ResetData()
    {
        diff = 0f;
    }
}