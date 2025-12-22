using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Microsoft.MixedReality.Toolkit.Input; // MRTK3のGazeInteractor
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Subsystems; // サブシステム管理
using System.IO;
using System;
using System.Runtime.InteropServices;
using UnityEngine.SceneManagement;
public class ExperimentController : MonoBehaviour
{
    [Header("中央の注視クロスと円錐")]
    public GameObject fixationCross;   // 正面の十字オブジェクト

    public GameObject centralCone;     // 半径60cm円錐

    // [Header("AOIオブジェクト（表示する順番）")]//実際には、表示するものと場所はランダムに並び替えたうえでAOIObjectとTargetObjectを対応させて記録する。
    // public GameObject[] encode_AOIObject;
    // public GameObject[] recall_AOIObject;
    public GameObject leftAOI;
    public GameObject centerAOI;
    public GameObject rightAOI; 

    [Header("対象オブジェクト（encodeとrecallで順番が変わる。その順番に登録する。）")]//実際には、表示するものと場所はランダムに並び替えたうえでAOIObjectとTargetObjectを対応させて記録する。
    public GameObject[] encode_TargetObject;
    public GameObject[] recall_TargetObject;

    [Header("音声クリップ（タイトル音声、yes/no質問音声）")]
    public AudioSource audioSource;
    public AudioClip[] EncodeInstructionClip; //エンコードフェーズの指示音声

    public AudioClip[] RecallInstructionClip; //リコールフェーズの指示音声

    // public AudioClip[] titleClips;     // 各オブジェクトのタイトル
    public AudioClip setupObjectSound;  //実オブジェクトを準備する合図
    public AudioClip removeObjectSound; //実体オブジェクトを下げる合図
    public AudioClip OkYesNoSound;
    // public AudioClip[] questionClips;  // 各オブジェクトの yes/no 質問
    // public string[] questionAnswer; //yes/No質問の正解
    public AudioClip[] thankClips; //終わりの説明
    private int currentIndex = 0;
    private bool okReceived = false;
    private bool yesReceived = false;
    private bool noReceived = false;
    private string lastResponse = "";

    public float minGazeTime = 0.1f;      // 100ms = 0.1秒
    private GameObject pastTarget = null;
    private float gazeStartTime = 0f;
    private float ActionTimer_StartTime = 0f;  //actionができるようになった時間
    private string logFilePath;
    private GameObject active_stim;
    private GameObject stim_position = null;

    private GazeInteractor gazeInteractor;
    private MRTKHandsAggregatorSubsystem handsAggregator;
    private string exp_phase = "";
    private string past_exp_phase = "";
    private string AppStartTime;
    private string sign_type;

    private float ok_choice_time;
    private float yes_choice_time;
    private float no_choice_time;

    // 前フレームのピンチ状態を保存
    private bool wasRightPinching = false;
    private float notRightTracked_time = 10000f;
    private bool wasLeftPinching = false;
    private float notLeftTracked_time = 10000f;

    private bool isAngleCounting = false;
    private float AngleStartTime = 0f;

    [SerializeField]
    private WhiteoutController whiteoutController;  // Inspector で割り当てる

    public Transform cameraTransform;
    public HeightDataSO data;


    void Start()
    {
        // 初期化：全オブジェクト不可視

        exp_phase = "not_exp_phase";
        
        leftAOI.transform.position = new Vector3(
            leftAOI.transform.position.x,
            leftAOI.transform.position.y + data.diff,
            leftAOI.transform.position.z
        );

        centerAOI.transform.position = new Vector3(
            centerAOI.transform.position.x,
            centerAOI.transform.position.y + data.diff,
            centerAOI.transform.position.z
        );

        rightAOI.transform.position = new Vector3(
            rightAOI.transform.position.x,
            rightAOI.transform.position.y + data.diff,
            rightAOI.transform.position.z
        );
        
        // Scene nextScene = SceneManager.GetSceneByName("eye_movement_previous_research_three_table");
        // SceneManager.SetActiveScene(nextScene);
        // SceneManager.UnloadSceneAsync("tutorial_eye_movement_previous_research_three_table");

        leftAOI.SetActive(false);
        centerAOI.SetActive(false);
        rightAOI.SetActive(false);

        foreach(var obj in encode_TargetObject){
            obj.SetActive(false);
        }
        
        fixationCross.SetActive(false);
        centralCone.SetActive(false);
        whiteoutController.notactiveWhiteout(); // 初期は非表示

        // シーンから GazeInteractor を探す
        gazeInteractor = FindObjectOfType<GazeInteractor>();

        // 現在実行中のHandAggregatorSubsystemを取得
        handsAggregator = XRSubsystemHelpers.GetFirstRunningSubsystem<MRTKHandsAggregatorSubsystem>();
        if (handsAggregator == null)
            Debug.LogError("HandsAggregatorSubsystemが見つかりません。MRTK Input サブシステムが有効か確認してください。");
    

        // 実行時刻をフォーマット
        AppStartTime = DateTime.Now.ToString("yyyyMMdd_HHmm_ss"); 
        // 例: 20251011_1930

        // 保存先フォルダ（例: Application.persistentDataPath は HoloLensでも有効）
        string folderPath = Application.persistentDataPath;

        // ファイル名に時刻を埋め込む
        logFilePath = Path.Combine(folderPath, $"{AppStartTime}_GazeLog_.csv");

        Debug.Log($"ログファイル作成: {logFilePath}");

        //ヘッダーの書き込み
        // オブジェクトの名前、見た秒数、対応AOIかどうか、
        File.WriteAllText(logFilePath, "app_start_time(ID), exp_phase, \"eye_data\", active_stim, stim_position, area_of_fix, bool_AOI, dwell_time, time_from_start(End_time)\n");
        File.AppendAllText(logFilePath, "app_start_time(ID), exp_phase, \"action_data\" , sign_type, answer(only_yes_no_action), time_to_action, time_from_start(End_time)\n");
        File.AppendAllText(logFilePath, "app_start_time(ID), exp_phase, \"eye_data(angle)\", active_stim, stim_position, HighAngle_dwell_time, time_from_start(End_time)");
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
        yield return StartCoroutine(AudioPlay(setupObjectSound));
        yield return StartCoroutine(whiteoutController.WhiteoutForSeconds(5.0f));
        Debug.Log("初期位置合わせ：完了");
        // 各オブジェクトを順番に処理

        for (currentIndex = 0; currentIndex < encode_TargetObject.Length; currentIndex++)
        {
            // 2. タイトル音声再生 & オブジェクト表示6秒
            yield return StartCoroutine(AudioPlay(encode_TargetObject[currentIndex].GetComponent<InformationHolder>().titleClip));
            encode_TargetObject[currentIndex].SetActive(true);
            exp_phase = "encode";
            active_stim = encode_TargetObject[currentIndex];
            stim_position = encode_TargetObject[currentIndex].transform.parent.gameObject;
            //File.AppendAllText(logFilePath, $"{TargetObject[currentIndex].name},,,,\n");
            Debug.Log($"{encode_TargetObject[currentIndex].name}");

            yield return EncodeObjectWait(stim_position);

            encode_TargetObject[currentIndex].SetActive(false);
            past_exp_phase = exp_phase;
            exp_phase = "not_exp_phase";

            //3. 注視再固定
            yield return StartCoroutine(AudioPlay(removeObjectSound));
            yield return StartCoroutine(FixateAndWait());
            Debug.Log("初期位置合わせ：完了");
            yield return StartCoroutine(AudioPlay(setupObjectSound));
            yield return StartCoroutine(whiteoutController.WhiteoutForSeconds(5.0f));
        }

        // リコールフェーズの説明
        for (currentIndex = 0; currentIndex < RecallInstructionClip.Length; currentIndex++)
        {
            yield return StartCoroutine(AudioPlay(RecallInstructionClip[currentIndex]));
        }

        //リコールフェーズの処理
        Debug.Log("リコールフェーズスタート");
        File.AppendAllText(logFilePath, $"\n\n<Recall_Phase>{Time.time}\n");

        yield return StartCoroutine(FixateAndWait());

        for (currentIndex = 0; currentIndex < recall_TargetObject.Length; currentIndex++)
        {

            // 5. タイトル音声 → OKサイン待ち
            Debug.Log("image generation task");
            stim_position = recall_TargetObject[currentIndex].transform.parent.gameObject;
            active_stim = recall_TargetObject[currentIndex];
            var info_holder = recall_TargetObject[currentIndex].GetComponent<InformationHolder>();
            File.AppendAllText(logFilePath, $"<Image_Generation_Task>{Time.time}_{recall_TargetObject[currentIndex].name}\n");

            yield return StartCoroutine(AudioPlay(info_holder.titleClip));
            ActionTimer_StartTime = Time.time;
            exp_phase = "recall -> Genaration";

            //File.AppendAllText(logFilePath, $"{TargetObject[currentIndex].name},,,,\n");
            Debug.Log($"{recall_TargetObject[currentIndex].name}");

            yield return StartCoroutine(WaitForOKAction());
            
            //okサインの記録
            //ボタンを押した瞬間にexp_phaseは"not_exp_phase"になるので、past_exp_phaseを使う必要あり
            string logEntry = $"{AppStartTime}, {past_exp_phase}, action_data, {sign_type}, {ok_choice_time - ActionTimer_StartTime},{ok_choice_time}\n";
            Debug.Log(logEntry);
            File.AppendAllText(logFilePath, logEntry);

            yield return new WaitForSeconds(0.1f); // 0.5秒待つ


            // 6. 質問音声 → yes/no ボタン押下待ち
            Debug.Log("image inspection task");
            File.AppendAllText(logFilePath, $"<Image_Inspection_Task>{Time.time}_{recall_TargetObject[currentIndex].name}\n");

            yield return StartCoroutine(AudioPlay(info_holder.questionClip));
            ActionTimer_StartTime = Time.time;
            exp_phase = "recall -> Inspection";

            yield return StartCoroutine(WaitForYesNoAction());
            

            // lastResponse に "yes" または "no" が入っている
            //Debug.Log($"回答 for index {currentIndex}: {lastResponse}");
            if (info_holder.questionAnswer == lastResponse)
            {
                if(lastResponse == "yes"){
                    logEntry = $"{AppStartTime}, {past_exp_phase}, action_data, {sign_type}, True, {yes_choice_time - ActionTimer_StartTime}, {yes_choice_time}\n";
                    File.AppendAllText(logFilePath, logEntry);
                    //Debug.Log($"answer:{logEntry}");
                    Debug.Log(logEntry);
                }else{
                    logEntry = $"{AppStartTime}, {past_exp_phase}, action_data, {sign_type}, True, {no_choice_time - ActionTimer_StartTime}, {no_choice_time}\n";
                    File.AppendAllText(logFilePath, logEntry);
                    Debug.Log($"answer:{logEntry}");
                }
            }
            else
            {
                if(lastResponse == "yes"){
                    logEntry = $"{AppStartTime}, {past_exp_phase}, action_data, {sign_type}, False, {yes_choice_time - ActionTimer_StartTime}, {yes_choice_time}\n";
                    File.AppendAllText(logFilePath, logEntry);
                    //Debug.Log($"answer:{logEntry}");
                    Debug.Log(logEntry);
                }else{
                    logEntry = $"{AppStartTime}, {past_exp_phase}, action_data, {sign_type}, False, {no_choice_time - ActionTimer_StartTime}, {no_choice_time}\n";
                    File.AppendAllText(logFilePath, logEntry);
                    Debug.Log($"answer:{logEntry}");
                }
            }
            File.AppendAllText(logFilePath, "\n");
            yield return new WaitForSeconds(0.1f); // 0.5秒待つ
            
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
        leftAOI.SetActive(false);
        centerAOI.SetActive(false);
        rightAOI.SetActive(false);
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
                Debug.Log("ok");
                fixationCross.SetActive(false);
                centralCone.SetActive(false);
                leftAOI.SetActive(true);
                centerAOI.SetActive(true);
                rightAOI.SetActive(true);
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
        ok_choice_time = Time.time;
        sign_type = "ok(timeout)";
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        okReceived = true;
        
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

    public IEnumerator OnOkActioned()
    {
        yield return StartCoroutine(AudioPlay(OkYesNoSound));
        ok_choice_time = Time.time;
        sign_type = "ok";
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        okReceived = true;
        //string logEntry = $"OKbutton,,,{Time.time:F3},\n";
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"OKボタン押下:{Time.time:F3}");

    }
    public IEnumerator OnYesActioned()
    {
        yield return StartCoroutine(AudioPlay(OkYesNoSound));
        yes_choice_time = Time.time;
        sign_type = "yes";
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        yesReceived = true;
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"YESボタン押下:{Time.time:F3}");
    }

    public IEnumerator OnNoActioned()
    {
        yield return StartCoroutine(AudioPlay(OkYesNoSound));
        no_choice_time = Time.time;
        sign_type = "no";
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        noReceived = true;
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
        //音楽を鳴らす
        //終了まで待機
        audioSource.clip = audioClip;
        audioSource.Play();
        yield return new WaitWhile(() => audioSource.isPlaying);
        yield return null; // 1フレーム待機

    }

    void Update(){
        // 視線Raycast
         // 視線の方向ベクトル（rayOriginTransform.forwardを使用）
        Vector3 gazeDirection = gazeInteractor.rayOriginTransform.forward;

        // 水平方向を基準にしたベクトル
        Vector3 horizontalForward = new Vector3(gazeDirection.x, 0, gazeDirection.z).normalized;

        // 上方向の角度（単位：度）を計算
        float angle = Vector3.SignedAngle(horizontalForward, gazeDirection, Vector3.Cross(horizontalForward, Vector3.up));
        
        if (angle > 30f && exp_phase != "not_exp_phase")
        {
            if (!isAngleCounting){
                isAngleCounting = true;
                AngleStartTime = Time.time;
                Debug.Log($"【カウント開始】角度={angle:F1}°");
            }
        }
        else
        {
            // カウント中に閾値を下回ったら、カウント終了
            if (isAngleCounting)
            {
                float AngleEndtime = Time.time;
                float AngleDuration = AngleEndtime - AngleStartTime;
                isAngleCounting = false;

                // 0.1秒未満の短い検出は無視
                if (AngleDuration >= 0.1f)
                {
                    string logEntry = $"{AppStartTime}, {exp_phase}, eye_data(angle), {active_stim.name}, {stim_position.name}, {AngleDuration}, {AngleEndtime}\n";
                    File.AppendAllText(logFilePath, logEntry);
                    Debug.Log("視線ログ: " + logEntry);
                }
            }
        }

        // var ray = new Ray(gazeInteractor.rayOriginTransform.position, gazeInteractor.rayOriginTransform.forward * 3);
        Vector3 cameraPos = cameraTransform.position;
        RaycastHit hit = new RaycastHit();
        var ray = new Ray(gazeInteractor.rayOriginTransform.position, gazeDirection * 3);


        if (Physics.Raycast(ray, out hit) || Vector3.Distance(leftAOI.GetComponent<Collider>().ClosestPoint(cameraPos), cameraPos) < 0.05 || Vector3.Distance(rightAOI.GetComponent<Collider>().ClosestPoint(cameraPos), cameraPos) < 0.05 || Vector3.Distance(centerAOI.GetComponent<Collider>().ClosestPoint(cameraPos), cameraPos) < 0.05)  //視線がオブジェクトに当たっている時、またはAOIの内側にいるとき
        {
            GameObject hitObject = null;
            // Debug.Log("111");
            // Debug.Log(hit.collider);
            // Debug.Log(centerAOI.GetComponent<Collider>().ClosestPoint(cameraPos));
            // Debug.Log(Vector3.Distance(centerAOI.GetComponent<Collider>().ClosestPoint(cameraPos), cameraPos));

            if(hit.collider == null)//視線は当たっていないがオブジェクト内にあるとき(厳密には、視線は当たっていないがオブジェクトの一番近い点とカメラの距離が5cm以内のとき)
            {
                if(Vector3.Distance(leftAOI.GetComponent<Collider>().ClosestPoint(cameraPos), cameraPos) < 0.05)
                {
                    hitObject = leftAOI;
                }else if(Vector3.Distance(rightAOI.GetComponent<Collider>().ClosestPoint(cameraPos), cameraPos) < 0.05)
                {
                    hitObject = rightAOI;
                }else if(Vector3.Distance(centerAOI.GetComponent<Collider>().ClosestPoint(cameraPos), cameraPos) < 0.05)
                {
                    hitObject = centerAOI;
                }
            }
            else //視線が当たっていたら、それを視線が当たっているオブジェクトとする。
            {
                hitObject = hit.collider.gameObject;
            }


            if(exp_phase != "not_exp_phase")
            {
                if (pastTarget == null)
                {
                    //以前物体を見ていなかった場合
                    pastTarget = hitObject;
                    gazeStartTime = Time.time;
                }
                else if (hitObject != pastTarget)
                {
                    // 新しいオブジェクトに視線が当たり始めた
                    float gazeEndTime = Time.time;
                    float duration = gazeEndTime - gazeStartTime;

                    if (duration >= minGazeTime && pastTarget.name != "vertical" && pastTarget.name != "horizontal" && pastTarget.name != "pedestal_front" && pastTarget.name != "pedestal_right" && pastTarget.name != "pedestal_left" && pastTarget.name != "pedestal_back")
                    {
                        // Debug.Log("pastTarget1");
                        // Debug.Log(pastTarget.name);
                        // Debug.Log("hitObject");
                        // Debug.Log(hitObject.name);

                        if (pastTarget == stim_position)
                        {
                            LogGaze(pastTarget.name, "corresponding AOI", duration, gazeEndTime, exp_phase);
                        }
                        else
                        {
                            LogGaze(pastTarget.name, "non-corresponding AOI", duration, gazeEndTime, exp_phase);
                        }
                    }
                    pastTarget = hitObject;
                    gazeStartTime = Time.time;
                }
            }
            else if(exp_phase == "not_exp_phase" && pastTarget != null)//視線がオブジェクトに当たっている状態で、exp_phaseがnot_exp_phaseになった時
            {
                float gazeEndTime = Time.time;
                float duration = gazeEndTime - gazeStartTime;

                if (duration >= minGazeTime && pastTarget.name != "vertical" && pastTarget.name != "horizontal" && pastTarget.name != "pedestal_front" && pastTarget.name != "pedestal_right" && pastTarget.name != "pedestal_left" && pastTarget.name != "pedestal_back")
                {
                    // Debug.Log("pastTarget2");
                    // Debug.Log(pastTarget.name);
                    // Debug.Log("hitObject");
                    // Debug.Log(hitObject.name);

                    if (pastTarget == stim_position)
                    {
                        LogGaze(pastTarget.name, "corresponding AOI", duration, gazeEndTime, past_exp_phase);
                    }
                    else
                    {
                        LogGaze(pastTarget.name, "non-corresponding AOI", duration, gazeEndTime, past_exp_phase);
                    }
                }
                pastTarget = null;
            }
        }
        else if (pastTarget != null && exp_phase != "not_exp_phase")// 視線がどのオブジェクトにも当たっていないとき
        {
            float gazeEndTime = Time.time;
            float duration = gazeEndTime - gazeStartTime;

            if (duration >= minGazeTime && pastTarget.name != "vertical" && pastTarget.name != "horizontal" && pastTarget.name != "pedestal_front" && pastTarget.name != "pedestal_right" && pastTarget.name != "pedestal_left" && pastTarget.name != "pedestal_back")
            {
                // Debug.Log("pastTarget3");
                // Debug.Log(pastTarget.name);
                // Debug.Log("hit.collider");
                // Debug.Log(hit.collider);

                if (pastTarget == stim_position)
                {
                    LogGaze(pastTarget.name, "corresponding AOI", duration, gazeEndTime, exp_phase);
                }
                else
                {
                    LogGaze(pastTarget.name, "non-corresponding AOI", duration, gazeEndTime, exp_phase);
                }
            }
            pastTarget = null;
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
                if (pinchRightAmount > 0.99 && pinchLeftAmount > 0.99  && (!wasRightPinching || !wasLeftPinching))
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
                if (pinchRightAmount > 0.99 && pinchLeftAmount > 0.99 && !wasRightPinching && !wasLeftPinching)
                {
                    //何もしない
                }
                // 過去：両手notピンチ → 今：右手ピンチ開始検出
                else if (pinchRightAmount > 0.99 && !wasRightPinching && !wasLeftPinching)
                {
                    wasRightPinching = true;
                    StartCoroutine(OnYesActioned());
                // 過去：両手notピンチ → 今：左手のピンチ開始検出
                }else if (pinchLeftAmount > 0.99 && !wasRightPinching && !wasLeftPinching)
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
    private void LogGaze(string objectName, string boolAOI, float duration, float gazeEndTime, string correct_exp_phase){
        //objectName : 視線が当たっているオブジェクト名
        //boolAOI : 視線が当たっているオブジェクトとアクティブなオブジェクトが一致しているかどうか
        string logEntry = $"{AppStartTime}, {correct_exp_phase}, eye_data, {active_stim.name}, {stim_position.name}, {objectName}, {boolAOI}, {duration}, {gazeEndTime}\n";
        File.AppendAllText(logFilePath, logEntry);
        Debug.Log("視線ログ: " + logEntry);
    }

}
