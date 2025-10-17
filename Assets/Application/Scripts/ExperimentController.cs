using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Microsoft.MixedReality.Toolkit.Input; // MRTK3のGazeInteractor
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Subsystems; // サブシステム管理
using System.IO;
using System;

public class ExperimentController : MonoBehaviour
{
    [Header("中央の注視クロスと円錐")]
    public GameObject fixationCross;   // 正面の十字オブジェクト

    public GameObject centralCone;     // 半径60cm円錐

    [Header("AOIオブジェクト（前, 後, 左, 右 の順にセット）")]//実際には、表示するものと場所はランダムに並び替えたうえでAOIObjectとTargetObjectを対応させて記録する。
    public GameObject[] AOIObject;

    [Header("対象オブジェクト（前, 後, 左, 右 の順にセットしてループさせる。）")]//実際には、表示するものと場所はランダムに並び替えたうえでAOIObjectとTargetObjectを対応させて記録する。
    public GameObject[] TargetObject;

    [Header("音声クリップ（タイトル音声、yes/no質問音声）")]
    public AudioSource audioSource;
    public AudioClip[] EncodeInstructionClip; //エンコードフェーズの指示音声

    public AudioClip[] RecallInstructionClip; //リコールフェーズの指示音声

    public AudioClip[] titleClips;     // 各オブジェクトのタイトル
    public AudioClip[] questionClips;  // 各オブジェクトの yes/no 質問
    public string[] questionAnswer; //yes/No質問の正解

    public AudioClip[] thankClips; //終わりの説明
    private int currentIndex = 0;
    private bool okReceived = false;
    private bool yesReceived = false;
    private bool noReceived = false;
    private string lastResponse = "";

    public float minGazeTime = 0.1f;      // 100ms = 0.1秒
    private GameObject currentTarget = null;
    private float gazeStartTime = 0f;
    private float ActionTimer_StartTime = 0f;  //actionができるようになった時間
    private string logFilePath;
    private GameObject active_stim;
    private GameObject stim_position = null;

    private GazeInteractor gazeInteractor;
    private MRTKHandsAggregatorSubsystem handsAggregator;
    private string exp_phase = "";
    private string past_exp_phase = "";
    private string StartTime;
    private string sign_type;

    private float ok_choice_time;
    private float yes_choice_time;
    private float no_choice_time;

    // 前フレームのピンチ状態を保存
    private bool wasRightPinching = false;
    private bool wasLeftPinching = false;

    private bool AudioAStopFlag = false;

    void Start()
    {
        // 初期化：全オブジェクト不可視

        exp_phase = "not_exp_phase";

        foreach (var obj in TargetObject)
            obj.SetActive(false);
        fixationCross.SetActive(false);
        centralCone.SetActive(false);
        // AOIだけはアクティブにする。(ただし、オブジェクトに付随しているスクリプトにより、非表示でも衝突判定はできるようにした。)
        foreach (var obj in AOIObject)
            obj.SetActive(true);

        // シーンから GazeInteractor を探す
        gazeInteractor = FindObjectOfType<GazeInteractor>();

        // 現在実行中のHandAggregatorSubsystemを取得
        handsAggregator = XRSubsystemHelpers.GetFirstRunningSubsystem<MRTKHandsAggregatorSubsystem>();
        if (handsAggregator == null)
            Debug.LogError("HandsAggregatorSubsystemが見つかりません。MRTK Input サブシステムが有効か確認してください。");
    

        // 実行時刻をフォーマット
        StartTime = DateTime.Now.ToString("yyyyMMdd_HHmm_ss"); 
        // 例: 20251011_1930

        // 保存先フォルダ（例: Application.persistentDataPath は HoloLensでも有効）
        string folderPath = Application.persistentDataPath;

        // ファイル名に時刻を埋め込む
        logFilePath = Path.Combine(folderPath, $"{StartTime}_GazeLog_.csv");

        Debug.Log($"ログファイル作成: {logFilePath}");

        //ヘッダーの書き込み
        // オブジェクトの名前、見た秒数、対応AOIかどうか、
        File.WriteAllText(logFilePath, "app_start_time(ID), exp_phase, \"eye_data\", active_stim, stim_position, area_of_fix, bool_AOI, dwell_time, time_from_start\n");
        File.AppendAllText(logFilePath, "app_start_time(ID), exp_phase, \"action_data\" , sign_type, answer(only_yes_no_action), time_to_action, time_from_start\n");
        Debug.Log("ログの保存先とヘッダの書き込み：完了");

        //ライセンス表示をしたい！！！！！
        //↑同意書に書けばよさそう


        // 実験開始
        StartCoroutine(RunExperiment());
    }


    private IEnumerator RunExperiment()
    {
        //エンコードフェーズの説明音声
        for (currentIndex = 0; currentIndex < EncodeInstructionClip.Length; currentIndex++)
        {
            yield return StartCoroutine(AudioPlay(EncodeInstructionClip[currentIndex]));
        }

        //エンコードフェーズの処理
        Debug.Log("エンコードフェーズスタート");

        File.AppendAllText(logFilePath, $"\n<Encode_Phase>{Time.time}\n");
        // 0. 注視と中央円錐内滞在（0.5秒）
        yield return StartCoroutine(FixateAndWait());
        Debug.Log("初期位置合わせ：完了");
        // 各オブジェクトを順番に処理

        for (currentIndex = 0; currentIndex < TargetObject.Length; currentIndex++)
        {
            // 2. タイトル音声再生 & オブジェクト表示6秒
            yield return StartCoroutine(AudioPlay(titleClips[currentIndex]));
            TargetObject[currentIndex].SetActive(true);
            exp_phase = "encode";
            active_stim = TargetObject[currentIndex];
            stim_position = AOIObject[currentIndex];
            //File.AppendAllText(logFilePath, $"{TargetObject[currentIndex].name},,,,\n");
            Debug.Log($"{TargetObject[currentIndex].name}");

            yield return EncodeObjectWait(AOIObject[currentIndex]);

            TargetObject[currentIndex].SetActive(false);
            past_exp_phase = exp_phase;
            exp_phase = "not_exp_phase";

            //3. 注視再固定
            yield return StartCoroutine(FixateAndWait());
            Debug.Log("初期位置合わせ：完了");
        }

        // // 5~8: Yes/No 質問パート
        // for (currentIndex = 0; currentIndex < AOIObject.Length; currentIndex++)
        // {
        //     // 5. タイトル音声 → OKサイン待ち
        //     audioSource.clip = titleClips[currentIndex];
        //     audioSource.Play();
        //     yield return StartCoroutine(WaitForOKAction());

        //     // 6. 質問音声 → Yes/No 待ち
        //     audioSource.clip = questionClips[currentIndex];
        //     audioSource.Play();
        //     yield return StartCoroutine(WaitForYesNo());


        //リコールフェーズの説明
        for (currentIndex = 0; currentIndex < RecallInstructionClip.Length; currentIndex++)
        {
            yield return StartCoroutine(AudioPlay(RecallInstructionClip[currentIndex]));
        }

        //リコールフェーズの処理
        Debug.Log("リコールフェーズスタート");
        File.AppendAllText(logFilePath, $"\n\n<Recall_Phase>{Time.time}\n");

        yield return StartCoroutine(FixateAndWait());

        for (currentIndex = 0; currentIndex < TargetObject.Length; currentIndex++)
        {

            // 5. タイトル音声 → OKサイン待ち
            Debug.Log("image generation task");
            stim_position = AOIObject[currentIndex];
            File.AppendAllText(logFilePath, $"<Image_Generation_Task>{Time.time}\n");

            exp_phase = "recall -> Genaration";
            yield return StartCoroutine(AudioPlay(titleClips[currentIndex]));
            ActionTimer_StartTime = Time.time;

            //File.AppendAllText(logFilePath, $"{TargetObject[currentIndex].name},,,,\n");
            Debug.Log($"{TargetObject[currentIndex].name}");

            yield return StartCoroutine(WaitForOKAction());
            
            //okサインの記録
            //ボタンを押した瞬間にexp_phaseは"not_exp_phase"になるので、past_exp_phaseを使う必要あり
            string logEntry = $"{StartTime}, {past_exp_phase}, action_data, {sign_type}, {ActionTimer_StartTime - ok_choice_time},{ok_choice_time}\n";
            Debug.Log(logEntry);
            File.AppendAllText(logFilePath, logEntry);


            // 6. 質問音声 → yes/no ボタン押下待ち
            Debug.Log("image inspection task");
            File.AppendAllText(logFilePath, $"<Image_Inspection_Task>{Time.time}\n");

            exp_phase = "recall -> Inspection";
            yield return StartCoroutine(AudioPlay(questionClips[currentIndex]));
            ActionTimer_StartTime = Time.time;

            yield return StartCoroutine(WaitForYesNoAction());
            

            // lastResponse に "yes" または "no" が入っている
            //Debug.Log($"回答 for index {currentIndex}: {lastResponse}");
            if (questionAnswer[currentIndex] == lastResponse)
            {
                logEntry = $"{StartTime}, {past_exp_phase}, action_data, {sign_type}, True, {ActionTimer_StartTime - yes_choice_time}, {yes_choice_time}\n";
                File.AppendAllText(logFilePath, logEntry);
                //Debug.Log($"answer:{logEntry}");
                Debug.Log(logEntry);
            }
            else
            {
                logEntry = $"{StartTime}, {past_exp_phase}, action_data, {sign_type}, False, {ActionTimer_StartTime - no_choice_time}, {no_choice_time}\n";
                File.AppendAllText(logFilePath, logEntry);
                Debug.Log($"answer:{logEntry}");
            }
            File.AppendAllText(logFilePath, "\n");
            
            // データを初期化
            okReceived = false;
            yesReceived = false;
            noReceived = false;
            lastResponse = "";

            // 7. 再度注視固定
            yield return StartCoroutine(FixateAndWait());
        }

        Debug.Log("全タスク終了");
        for (currentIndex = 0; currentIndex < thankClips.Length; currentIndex++)
        {
            yield return StartCoroutine(AudioPlay(thankClips[currentIndex]));
        }
    }

    // ---- 補助コルーチン ----

    private IEnumerator FixateAndWait()
    {
        float timer = 0f;
        fixationCross.SetActive(true);
        centralCone.SetActive(true);
        foreach (var obj in AOIObject)
            obj.SetActive(false);
        while (true)
        {
            if (IsFixatingCentralCross())
            {
                Debug.Log("視線成功");
                if (IsInsideCentralCone())
                {
                    Debug.Log("位置成功");
                    timer += Time.deltaTime;
                }
                else
                {
                    Debug.Log("位置失敗");
                    timer = 0f;
                }
            }
            else
                timer = 0f;


            if (timer >= 0.5f)
            {
                fixationCross.SetActive(false);
                centralCone.SetActive(false);
                foreach (var obj in AOIObject)
                    obj.SetActive(true);
                break;
            }
            yield return null;
        }
    }

    private bool IsFixatingCentralCross()
    {
        // GazeInteractor (XR Ray Interactor with Eye Tracking)
        //var cam = Camera.main;
        var ray = new Ray(gazeInteractor.rayOriginTransform.position, gazeInteractor.rayOriginTransform.forward * 3);
        if (Physics.Raycast(ray, out var hit))
        {
            GameObject hitObject = hit.collider.gameObject;
            Debug.Log(hitObject.name);

            if (hitObject.transform.parent != null)
            {
                // 直近の親を返す
                hitObject = hitObject.transform.parent.gameObject;
            }
            else
            {
                // 親が無いなら自分自身を返す
            }
            if (hitObject == fixationCross)
            {
                return true;
            }
            return false;
            //if (hit.collider.gameObject.transform.parent.gameObject == fixationCross)
            //{
            //     return true;
            //}
            //     return false;
        }
        return false;
    }

    private bool IsInsideCentralCone(){

        // 中央円錐のCollider内にユーザーの頭（カメラ）が入っているか判定
        var gazePos = gazeInteractor.rayOriginTransform.position;
        var col = centralCone.GetComponent<Collider>();

        if (col.bounds.Contains(gazePos))
        {
            Debug.Log("位置ok");
        }
        else
        {
            Debug.Log("位置NO");
        }


        return col.bounds.Contains(gazePos);
    }

    private IEnumerator WaitForOKAction(){
        Debug.Log("OKサイン待ち");

        while (!okReceived)
            yield return null;
    }

    private IEnumerator WaitForYesNoAction(){
        // 押されるまで待つ
        while (!yesReceived && !noReceived)
        {
            yield return null;
        }

        if (yesReceived)
        {
            lastResponse = "yes";
        }
        else if (noReceived)
        {
            lastResponse = "no";
        }
    }

    // ボタン押下時に呼ばれるメソッド（Inspector の OnClicked に登録）

    public void OnOkActioned()
    {
        AudioAStopFlag = true;
        okReceived = true;
        ok_choice_time = Time.time;
        sign_type = "ok";
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        //string logEntry = $"OKbutton,,,{Time.time:F3},\n";
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"OKボタン押下:{Time.time:F3}");

    }
    public void OnYesActioned()
    {
        AudioAStopFlag = true;
        yesReceived = true;
        yes_choice_time = Time.time;
        sign_type = "yes";
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        AudioAStopFlag = true;
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"YESボタン押下:{Time.time:F3}");
    }

    public void OnNoActioned()
    {
        AudioAStopFlag = true;
        noReceived = true;
        no_choice_time = Time.time;
        sign_type = "no";
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"NOボタン押下:{Time.time:F3}");
    }

    // ---- エンコードフェーズのオブジェクトを見たら6秒待つ----
    private IEnumerator EncodeObjectWait(GameObject AOI_object)
    {
        while (true)
        {
            if (IsFixingEncodeObject(AOI_object))
            {
                yield return new WaitForSeconds(6f);
                break;
            }

            yield return null;
        }
    }

    private bool IsFixingEncodeObject(GameObject AOI_object)
    {
        var ray = new Ray(gazeInteractor.rayOriginTransform.position, gazeInteractor.rayOriginTransform.forward * 3);
        if (Physics.Raycast(ray, out var hit))
        {

            // 子オブジェクトに当たった場合でも親を取得する
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject.transform.parent != null)
            {
                // 直近の親を返す
                hitObject = hitObject.transform.parent.gameObject;
            }
            else
            {
                // 親が無いなら自分自身を返す
            }

            if (hitObject == AOI_object)
            {
                return true;
            }
            return false;
        }
        return false;
    }

    private IEnumerator AudioPlay(AudioClip audioClip)
    {
        AudioAStopFlag = false;
        //音楽を鳴らす
        audioSource.PlayOneShot(audioClip);
        //終了まで待機
        while (audioSource.isPlaying)
        {
            // 変数が変化したかチェック
            if (AudioAStopFlag)
            {
                Debug.Log("変数変更を検知 → 音声を停止");
                audioSource.Stop(); // 再生中断
                yield break;        // コルーチン終了
            }

            yield return null; // 1フレーム待機
        }

    }

    void Update()
    {
        // 視線Raycast
        var ray = new Ray(gazeInteractor.rayOriginTransform.position, gazeInteractor.rayOriginTransform.forward * 3);
        if (Physics.Raycast(ray, out var hit) && exp_phase != "not_exp_phase")  //視線がオブジェクトに当たっている時
        {
            GameObject hitObject = hit.collider.gameObject;

            if (currentTarget == null || currentTarget != hitObject)
            {
                // 新しいオブジェクトに視線が当たり始めた
                currentTarget = hitObject;
                gazeStartTime = Time.time;
            }
        }
        else if (currentTarget != null && exp_phase != "not_exp_phase")// 視線がどのオブジェクトにも当たっていないとき
        {
            float gazeEndTime = Time.time;
            float duration = gazeEndTime - gazeStartTime;

            if (duration >= minGazeTime && currentTarget.name != "vertical" && currentTarget.name != "horizontal" && currentTarget.name != "pedestal_front" && currentTarget.name != "pedestal_right" && currentTarget.name != "pedestal_left" && currentTarget.name != "pedestal_back")
            {
                Debug.Log("currentTarget");
                Debug.Log(currentTarget.name);

                if (currentTarget == stim_position)
                {
                    LogGaze(currentTarget.name, "corresponding AOI", duration, gazeStartTime, exp_phase);
                }
                else
                {
                    LogGaze(currentTarget.name, "non-corresponding AOI", duration, gazeStartTime, exp_phase);
                }
            }
            currentTarget = null;
        }
        else if (currentTarget != null && exp_phase == "not_exp_phase")  //視線が当たっていたのに、exp_phaseが"not_exp_phase"になった場合
        {
            float gazeEndTime = Time.time;
            float duration = gazeEndTime - gazeStartTime;

            if (duration >= minGazeTime && currentTarget.name != "vertical" && currentTarget.name != "horizontal" && currentTarget.name != "pedestal_front" && currentTarget.name != "pedestal_right" && currentTarget.name != "pedestal_left" && currentTarget.name != "pedestal_back")
            {
                Debug.Log("currentTarget");
                Debug.Log(currentTarget.name);

                if (currentTarget == stim_position)
                {
                    LogGaze(currentTarget.name, "corresponding AOI", duration, gazeStartTime, past_exp_phase);
                }
                else
                {
                    LogGaze(currentTarget.name, "non-corresponding AOI", duration, gazeStartTime, past_exp_phase);
                }
            }
            currentTarget = null;
        }

        if (exp_phase == "recall -> Genaration" || exp_phase == "recall -> Inspection")
        {
            // 現在のピンチ状態を取得
            // bool isRightPinching = handsAggregator.TryGetPinchProgress(XRNode.RightHand);
            // bool isLeftPinching = handsAggregator.TryGetPinchProgress(XRNode.LeftHand);
            handsAggregator.TryGetPinchProgress(XRNode.RightHand, out bool isReadyToRightPinch, out bool isRightPinching, out float pinchRightAmount);
            handsAggregator.TryGetPinchProgress(XRNode.LeftHand, out bool isReadyToLeftPinch, out bool isLeftPinching, out float pinchLeftAmount);
            
        

            if (exp_phase == "recall -> Genaration")
            {
                // 両手同時ピンチ検出
                if (isRightPinching && isLeftPinching && (!wasRightPinching || !wasLeftPinching))
                {
                    OnOkActioned();
                    // データを初期化
                    wasRightPinching = false;
                    wasLeftPinching = false;
                }
                else
                {
                    // 状態を更新
                    wasRightPinching = isRightPinching;
                    wasLeftPinching = isLeftPinching;
                }



            }else if (exp_phase == "recall -> Inspection"){
                //OnOkActioned()を実行した直後に、人間の認知スピードでは両手のピンチを解除することはできない。そのため、両手のピンチを解除した後、片手のピンチを検知できるようにしたい。
                if (!isRightPinching && !isLeftPinching && (!wasRightPinching || !wasLeftPinching))
                {
                    
                }
                // 右手のピンチ開始検出
                else if (isRightPinching && !wasRightPinching)
                {
                    OnYesActioned();
                    wasRightPinching = false;

                }else if (isLeftPinching && !wasLeftPinching)
                // 左手のピンチ開始検出
                {
                    OnNoActioned();
                    wasLeftPinching = false;
                }
                else
                {
                    // 状態を更新
                    wasRightPinching = isRightPinching;
                    wasLeftPinching = isLeftPinching;
                }
            }
        }
    }
    private void LogGaze(string objectName, string boolAOI, float duration, float gazeStartTime, string correct_exp_phase){
        //objectName : 視線が当たっているオブジェクト名
        //boolAOI : 視線が当たっているオブジェクトとアクティブなオブジェクトが一致しているかどうか
        string logEntry = $"{StartTime}, {correct_exp_phase}, eye_data, {active_stim.name}, {stim_position.name}, {objectName}, {boolAOI}, {duration}, {gazeStartTime}\n";
        File.AppendAllText(logFilePath, logEntry);
        Debug.Log("視線ログ: " + logEntry);
    }

}
