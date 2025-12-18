using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Microsoft.MixedReality.Toolkit.Input; // MRTK3のGazeInteractor
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Subsystems; // サブシステム管理
using System.IO; // ファイルI/Oのために追加
using System;    // DateTimeのために追加
using TMPro;
using UnityEngine.XR;
using UnityEngine.SceneManagement;

public class TutorialController : MonoBehaviour
{
    // === 公開変数（Inspectorで設定） ===
    // 判定対象のオブジェクト
    public GameObject leftAOI;
    public GameObject rightAOI;
    public GameObject centerAOI;

    // ClosestPointでの近接判定距離 (メートル)
    public float proximityThreshold = 0.05f; // 5cm
    
    // 視線が当たっている間の色
    public Color hoverColor = Color.yellow;

    private GazeInteractor gazeInteractor;
    
    // === プライベート変数 ===
    private Camera mainCamera;
    private GameObject currentlyHoveredObject = null;
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();
    private string heightInput = "";

    public Transform leftAOI_trans;
    public Transform centerAOI_trans;
    public Transform rightAOI_trans;
    public AudioSource audioSource;

    public HeightDataSO data;

    // 身長データを保存するファイルパス
    // Application.persistentDataPath は、デバイス上でアプリケーションがデータを永続的に保存できる安全な場所を指します。
    // HoloLens 2の実機では、このパスが適切に動作します。
    private const string FileName = "HeightData.csv";

    public TextMeshProUGUI displayHeightText;
    public TextMeshProUGUI displayPinchText;
    private string lastText = "";
    private string pinch_action = "";
    string folderPath;
    string logFilePath;
    private string exp_phase = "";
    private string past_exp_phase = "";
    private bool okReceived = false;
    private bool yesReceived = false;
    private bool noReceived = false;
    // 前フレームのピンチ状態を保存
    private bool wasRightPinching = false;
    private float notRightTracked_time = 10000f;
    private bool wasLeftPinching = false;
    private float notLeftTracked_time = 10000f;

    private bool isAngleCounting = false;
    private float AngleStartTime = 0f;
    public AudioClip OkYesNoSound;
    private MRTKHandsAggregatorSubsystem handsAggregator;
    public GameObject num_key;
    private Vector3 rightAOI_pos;
    private Vector3 centerAOI_pos;
    private Vector3 leftAOI_pos;
    private bool get_height_flag = false;
    private bool finish_pinch_tutorial_flag = false;

    public GameObject ExperimentStartButtom;
    void Start()
    {
        data.ResetData();
        num_key.SetActive(false);
        ExperimentStartButtom.SetActive(false);

        rightAOI_pos = rightAOI_trans.position;
        centerAOI_pos = centerAOI_trans.position;
        leftAOI_pos = leftAOI_trans.position;
        // メインカメラの取得
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("シーンに 'MainCamera' タグの付いたカメラがありません。");
            enabled = false;
            return;
        }

        // ファイルパスを初期化
        string folderPath = Application.persistentDataPath;

        // 3つのAOIをリストにまとめ、元の色を保存
        // シーンから GazeInteractor を探す
        gazeInteractor = FindObjectOfType<GazeInteractor>();

        // 現在実行中のHandAggregatorSubsystemを取得
        handsAggregator = XRSubsystemHelpers.GetFirstRunningSubsystem<MRTKHandsAggregatorSubsystem>();
        if (handsAggregator == null)
            Debug.LogError("HandsAggregatorSubsystemが見つかりません。MRTK Input サブシステムが有効か確認してください。");

        logFilePath = Path.Combine(folderPath, FileName);
        File.WriteAllText(logFilePath, "");

        InitializeAOIs(leftAOI, rightAOI, centerAOI);
    }

    void InitializeAOIs(params GameObject[] aois)
    {
        foreach (var aoi in aois)
        {
            if (aoi != null && aoi.GetComponent<Renderer>() != null)
            {
                // Colliderの確認
                if (aoi.GetComponent<Collider>() == null)
                {
                    Debug.LogError(aoi.name + " に Collider がありません。ClosestPoint判定に必要です。");
                }
                
                // 元の色の保存
                originalColors[aoi] = aoi.GetComponent<Renderer>().material.color;
            }
        }
    }

    void Update()
    {
        // 視線判定ロジックを実行
        GameObject newHoverObject = GetHitObject();
        
        // --- 色変更の処理 ---
        
        // 視線が外れたときの処理
        if (currentlyHoveredObject != null && currentlyHoveredObject != newHoverObject)
        {
            // 元の色に戻す
            if (originalColors.ContainsKey(currentlyHoveredObject))
            {
                currentlyHoveredObject.GetComponent<Renderer>().material.color = originalColors[currentlyHoveredObject];
            }
            currentlyHoveredObject = null;
        }
        
        // 新しいオブジェクトに視線が当たったときの処理
        if (newHoverObject != null && newHoverObject != currentlyHoveredObject)
        {
            // 新しい色に変更
            if (originalColors.ContainsKey(newHoverObject))
            {
                newHoverObject.GetComponent<Renderer>().material.color = hoverColor;
            }
            currentlyHoveredObject = newHoverObject;
        }

        if (exp_phase == "recall -> Genaration" || exp_phase == "recall -> Inspection")
        {
            // 現在のピンチ状態を取得
            // bool isRightPinching = handsAggregator.TryGetPinchProgress(XRNode.RightHand);
            // bool isLeftPinching = handsAggregator.TryGetPinchProgress(XRNode.LeftHand);
            bool isRightTracked = handsAggregator.TryGetPinchProgress(XRNode.RightHand, out bool isReadyToRightPinch, out bool isRightPinching, out float pinchRightAmount);
            bool isLeftTracked = handsAggregator.TryGetPinchProgress(XRNode.LeftHand, out bool isReadyToLeftPinch, out bool isLeftPinching, out float pinchLeftAmount);
            
        

            if (exp_phase == "recall -> Genaration")
            {
                // 両手同時ピンチ検出
                if (pinchRightAmount > 0.95 && pinchLeftAmount > 0.95  && (!wasRightPinching || !wasLeftPinching))
                {
                    // 状態を更新
                    wasRightPinching = true;
                    wasLeftPinching = true;
                    StartCoroutine(OnOkActioned());
                }

                //　手のトラッキングが切れたときの処理
                if (!isRightTracked && notRightTracked_time == 10000f)
                {
                    notRightTracked_time = Time.time;
                }
                else if(isRightTracked)
                {
                    notRightTracked_time = 10000f;
                }

                if (!isLeftTracked && notLeftTracked_time == 10000f)
                {
                    notLeftTracked_time = Time.time;
                }else if (isLeftTracked)
                {
                    notLeftTracked_time = 10000f;
                }

                // 状態を更新
                // 手が検知できていてピンチを解除しているか、手が検知できていない状態が続いていたらfalseになる。
                if((!isRightPinching && isRightTracked) || (!isRightTracked && (Time.time - notRightTracked_time)>0.5f))
                {
                    wasRightPinching = false;
                }

                if((!isLeftPinching && isLeftTracked) || (!isLeftTracked && (Time.time - notLeftTracked_time)>0.5f))
                {
                    wasLeftPinching = false;
                }

            }else if (exp_phase == "recall -> Inspection"){
                // 今：両手ピンチ開始検出
                if (pinchRightAmount > 0.95 && pinchLeftAmount > 0.95 && !wasRightPinching && !wasLeftPinching)
                {
                    //何もしない
                }
                // 過去：両手notピンチ → 今：右手ピンチ開始検出
                else if (pinchRightAmount > 0.95 && !wasRightPinching && !wasLeftPinching)
                {
                    wasRightPinching = true;
                    StartCoroutine(OnYesActioned());


                // 過去：両手notピンチ → 今：左手のピンチ開始検出
                }else if (pinchLeftAmount > 0.95 && !wasRightPinching && !wasLeftPinching)
                {
                    wasLeftPinching = true;
                    StartCoroutine(OnNoActioned());
                }

                //　手のトラッキングが切れたときの処理
                if (!isRightTracked && notRightTracked_time == 10000f)
                {
                    notRightTracked_time = Time.time;
                }
                else if(isRightTracked)
                {
                    notRightTracked_time = 10000f;
                }

                if (!isLeftTracked && notLeftTracked_time == 10000f)
                {
                    notLeftTracked_time = Time.time;
                }else if (isLeftTracked)
                {
                    notLeftTracked_time = 10000f;
                }

                // 状態を更新
                // 手が検知できていてピンチを解除しているか、手が検知できていない状態が続いていたらfalseになる。
                if((!isRightPinching && isRightTracked) || (!isRightTracked && (Time.time - notRightTracked_time)>0.5f))
                {
                    wasRightPinching = false;
                }

                if((!isLeftPinching && isLeftTracked) || (!isLeftTracked && (Time.time - notLeftTracked_time)>0.5f))
                {
                    wasLeftPinching = false;
                }
            }
        }
    }

    private void SavediffToFile(float diff)
    {
        data.diff = diff;
    }



    /// <summary>
    /// ご提示いただいた視線判定ロジックを実装します。
    /// </summary>
    private GameObject GetHitObject()
    {
        if (mainCamera == null) return null;

        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 cameraForward = mainCamera.transform.forward;
        var ray = new Ray(gazeInteractor.rayOriginTransform.position, gazeInteractor.rayOriginTransform.forward * 3);
        
        RaycastHit hit;
        GameObject hitObject = null;

        // 1. Raycast判定 (真っ直ぐな視線が当たっているか)
        bool isRaycastHit = Physics.Raycast(ray, out hit);
        
        if (isRaycastHit)
        {
            // Raycastが当たっていたら、そのオブジェクトを優先する
            hitObject = hit.collider.gameObject;
        }
        // else // 2. Raycastが当たっていない場合（近接判定）
        // {
        //     // 視線は当たっていないが、オブジェクトが近接閾値内にあるかチェック
        //     if (IsClosestPointNear(leftAOI, cameraPos, proximityThreshold))
        //     {
        //         hitObject = leftAOI;
        //     }
        //     else if (IsClosestPointNear(rightAOI, cameraPos, proximityThreshold))
        //     {
        //         hitObject = rightAOI;
        //     }
        //     else if (IsClosestPointNear(centerAOI, cameraPos, proximityThreshold))
        //     {
        //         hitObject = centerAOI;
        //     }
        // }
        
        return hitObject;
    }

    // / <summary>
    // / オブジェクトのColliderからカメラに最も近い点までの距離が閾値内にあるかを判定します。
    // / </summary>
    // private bool IsClosestPointNear(GameObject aoi, Vector3 cameraPosition, float threshold)
    // {
    //     if (aoi == null) return false;
        
    //     Collider collider = aoi.GetComponent<Collider>();
    //     if (collider == null) return false;
        
    //     // Collider上のカメラに最も近い点の座標
    //     Vector3 closestPoint = collider.ClosestPoint(cameraPosition);
        
    //     // その点とカメラの距離
    //     float distance = Vector3.Distance(closestPoint, cameraPosition);
        
    //     return distance < threshold;
    // }

    public void StartPinchTutorialCoroutine()
    {
        StartCoroutine(pinch_tutorial());
    }
    public IEnumerator pinch_tutorial()
    {
        
        for (int i = 0; i < 3; i++)
        {
            displayPinchText.text = $"action({i+1}/3)";
            exp_phase = "recall -> Genaration";

            yield return StartCoroutine(WaitForOKAction());
            
            yield return new WaitForSeconds(0.1f); // 0.5秒待つ

            exp_phase = "recall -> Inspection";
            yield return StartCoroutine(WaitForYesNoAction());
            
            yield return new WaitForSeconds(1f); // 0.5秒待つ
            
            // データを初期化
            okReceived = false;
            yesReceived = false;
            noReceived = false;
        }
        displayPinchText.text = "finish";
        
        finish_pinch_tutorial_flag = true;

        if(get_height_flag && finish_pinch_tutorial_flag){
            ExperimentStartButtom.SetActive(true);
        }
    }
        
    private IEnumerator WaitForOKAction(){
        Debug.Log("OKサイン待ち");

        float limit = 30f;
        float timer = 0f;
        while (timer < limit)
        {
            if (okReceived)
            {
                yield break;  // コルーチン終了
            }
            timer += Time.deltaTime;
            yield return null;
        }
        okReceived = true;
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        
    }

    private IEnumerator WaitForYesNoAction(){
        // 押されるまで待つ
        while (!yesReceived && !noReceived)
        {
            yield return null;
        }
    }

    // ボタン押下時に呼ばれるメソッド（Inspector の OnClicked に登録）

    public IEnumerator OnOkActioned()
    {
        yield return StartCoroutine(AudioPlay(OkYesNoSound));
        okReceived = true;
        past_exp_phase = exp_phase;
        exp_phase = "recall -> Inspection";
        displayPinchText.text = "ok";
        //string logEntry = $"OKbutton,,,{Time.time:F3},\n";
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"OKボタン押下:{Time.time:F3}");

    }
    public IEnumerator OnYesActioned()
    {
        yield return StartCoroutine(AudioPlay(OkYesNoSound));
        yesReceived = true;
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        displayPinchText.text = "yes";
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"YESボタン押下:{Time.time:F3}");
    }

    public IEnumerator OnNoActioned()
    {
        yield return StartCoroutine(AudioPlay(OkYesNoSound));
        noReceived = true;
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        displayPinchText.text = "no";
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"NOボタン押下:{Time.time:F3}");
    }
    private IEnumerator AudioPlay(AudioClip audioClip)
    {
        //音楽を鳴らす
        //終了まで待機
        audioSource.clip = audioClip;
        audioSource.Play();
        yield return new WaitWhile(() => audioSource.isPlaying);
        yield return null; // 1フレーム待機

    }

    public void get_height()
    {
        num_key.SetActive(true);
    }

    public void press_1()
    {
        lastText += "1";
        displayHeightText.text = lastText;
    }
    public void press_2()
    {
        lastText += "2";
        displayHeightText.text = lastText;
    }
    public void press_3()
    {
        lastText += "3";
        displayHeightText.text = lastText;
    }
    public void press_4()
    {
        lastText += "4";
        displayHeightText.text = lastText;
    }
    public void press_5()
    {
        lastText += "5";
        displayHeightText.text = lastText;
    }
    public void press_6()
    {
        lastText += "6";
        displayHeightText.text = lastText;
    }
    public void press_7()
    {
        lastText += "7";
        displayHeightText.text = lastText;
    }
    public void press_8()
    {
        lastText += "8";
        displayHeightText.text = lastText;
    }
    public void press_9()
    {
        lastText += "9";
        displayHeightText.text = lastText;
    }
    public void press_0()
    {
        lastText += "0";
        displayHeightText.text = lastText;
    }
    public void press_enter()
    {
        float diff = 170 - int.Parse(lastText);
        diff = diff/100;
        Vector3 tmp_rightAOI_pos = rightAOI_pos;
        Vector3 tmp_centerAOI_pos = centerAOI_pos;
        Vector3 tmp_leftAOI_pos = leftAOI_pos;

        tmp_rightAOI_pos.y = rightAOI_pos.y + diff;
        tmp_centerAOI_pos.y = centerAOI_pos.y + diff;
        tmp_leftAOI_pos.y = leftAOI_pos.y + diff;

        rightAOI_trans.transform.position = tmp_rightAOI_pos;
        centerAOI_trans.transform.position = tmp_centerAOI_pos;
        leftAOI_trans.transform.position = tmp_leftAOI_pos;

        num_key.SetActive(false);

        SavediffToFile(diff);
        Debug.Log($"rightAOI_pos.y = {rightAOI_pos.y}");

        get_height_flag = true;

        if(get_height_flag && finish_pinch_tutorial_flag){
            ExperimentStartButtom.SetActive(true);
        }
    }
    public void press_backspace()
    {
        if(lastText.Length > 0)
        {
            lastText = lastText.Remove(lastText.Length - 1, 1);
            displayHeightText.text = lastText;
        }
    }
    public void press_experiment_start(){

        // ② Update を止める
        enabled = false;
        SceneManager.LoadScene("eye_movement_previous_research_three_table");
    }

}