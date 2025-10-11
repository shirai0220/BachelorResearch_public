using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.Speech; // Yes/No 音声認識
using UnityEngine.XR.Interaction.Toolkit;
using Microsoft.MixedReality.Toolkit.Input; // MRTK3のGazeInteractor
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

    public GameObject OkButton;
    public GameObject YesButton;
    public GameObject NoButton;

    private int currentIndex = 0;
    private string lastResponse = "";
    private bool okReceived = false;
    private bool yesPressed = false;
    private bool noPressed = false;

    public float minGazeTime = 0.1f;      // 100ms = 0.1秒
    private GameObject currentTarget = null;
    private float gazeStartTime = 0f;
    private float ButtonStartTime = 0f;  //ボタンが現れた時間
    private string logFilePath;
    private GameObject active_stim;
    private GameObject stim_position = null;

    private GazeInteractor gazeInteractor;
    private string exp_phase = "";
    private string past_exp_phase = "";
    private string StartTime;
    private string pressed_button;

    private float ok_press_time;
    private float yes_press_time;
    private float no_press_time;

    void Start()
    {
        // 初期化：全オブジェクト不可視

        exp_phase = "not_exp_phase";

        foreach (var obj in TargetObject)
            obj.SetActive(false);
        fixationCross.SetActive(false);
        centralCone.SetActive(false);
        OkButton.SetActive(false);
        YesButton.SetActive(false);
        NoButton.SetActive(false);
        // AOIだけはアクティブにする。(ただし、オブジェクトに付随しているスクリプトにより、非表示でも衝突判定はできるようにした。)
        foreach (var obj in AOIObject)
            obj.SetActive(true);

        // シーンから GazeInteractor を探す
        gazeInteractor = FindObjectOfType<GazeInteractor>();

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
        File.AppendAllText(logFilePath, "app_start_time(ID), exp_phase, \"button_data\" , pressed_button, answer(only_yes_no_press), time_to_press, time_from_start\n");
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
            yield return StartCoroutine(FullAudioPlay(EncodeInstructionClip[currentIndex]));
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
            yield return StartCoroutine(FullAudioPlay(titleClips[currentIndex]));
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
        //     yield return StartCoroutine(WaitForOKSign());

        //     // 6. 質問音声 → Yes/No 待ち
        //     audioSource.clip = questionClips[currentIndex];
        //     audioSource.Play();
        //     yield return StartCoroutine(WaitForYesNo());


        //リコールフェーズの説明
        for (currentIndex = 0; currentIndex < RecallInstructionClip.Length; currentIndex++)
        {
            yield return StartCoroutine(FullAudioPlay(RecallInstructionClip[currentIndex]));
        }

        //リコールフェーズの処理
        Debug.Log("リコールフェーズスタート");
        File.AppendAllText(logFilePath, $"\n\n<Recall_Phase>{Time.time}\n");

        for (currentIndex = 0; currentIndex < TargetObject.Length; currentIndex++)
        {

            // 5. タイトル音声 → OKサイン待ち
            Debug.Log("image generation task");
            stim_position = AOIObject[currentIndex];
            File.AppendAllText(logFilePath, $"<Image_Generation_Task>{Time.time}\n");

            exp_phase = "recall -> genaration";
            yield return StartCoroutine(FullAudioPlay(titleClips[currentIndex]));
            OkButton.SetActive(true);
            ButtonStartTime = Time.time;

            //File.AppendAllText(logFilePath, $"{TargetObject[currentIndex].name},,,,\n");
            Debug.Log($"{TargetObject[currentIndex].name}");

            yield return StartCoroutine(WaitForOKSign());
            OkButton.SetActive(false);
            

            //okサインの記録
            //ボタンを押した瞬間にexp_phaseは"not_exp_phase"になるので、past_exp_phaseを使う必要あり
            string logEntry = $"{StartTime}, {past_exp_phase}, button_data, {pressed_button}, {ButtonStartTime - ok_press_time},{ok_press_time}\n";
            Debug.Log(logEntry);
            File.AppendAllText(logFilePath, logEntry);


            // 6. 質問音声 → yes/no ボタン押下待ち
            Debug.Log("image inspection task");
            File.AppendAllText(logFilePath, $"<Image_Inspection_Task>{Time.time}\n");

            exp_phase = "recall -> Inspection";
            yield return StartCoroutine(FullAudioPlay(questionClips[currentIndex]));
            YesButton.SetActive(true);
            NoButton.SetActive(true);
            ButtonStartTime = Time.time;

            yield return StartCoroutine(WaitForYesNoButton());
            
            YesButton.SetActive(false);
            NoButton.SetActive(false);

            // lastResponse に "yes" または "no" が入っている
            //Debug.Log($"回答 for index {currentIndex}: {lastResponse}");
            if (questionAnswer[currentIndex] == lastResponse)
            {
                logEntry = $"{StartTime}, {past_exp_phase}, button_data, {pressed_button}, True, {ButtonStartTime - yes_press_time}, {yes_press_time}\n";
                File.AppendAllText(logFilePath, logEntry);
                //Debug.Log($"answer:{logEntry}");
                Debug.Log(logEntry);
            }
            else
            {
                logEntry = $"{StartTime}, {past_exp_phase}, button_data, {pressed_button}, False, {ButtonStartTime - no_press_time}, {no_press_time}\n";
                File.AppendAllText(logFilePath, logEntry);
                Debug.Log($"answer:{logEntry}");
            }
            File.AppendAllText(logFilePath, "\n");
            // 7. 再度注視固定
            yield return StartCoroutine(FixateAndWait());
        }

        Debug.Log("全タスク終了");
        for (currentIndex = 0; currentIndex < thankClips.Length; currentIndex++)
        {
            yield return StartCoroutine(FullAudioPlay(thankClips[currentIndex]));
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

    private IEnumerator WaitForOKSign(){
        okReceived = false;
        Debug.Log("OKサイン待ち");

        while (!okReceived)
            yield return null;
    }

    private IEnumerator WaitForYesNoButton(){
        yesPressed = false;
        noPressed = false;
        lastResponse = "";

        // 押されるまで待つ
        while (!yesPressed && !noPressed)
        {
            yield return null;
        }

        if (yesPressed)
        {
            lastResponse = "yes";
        }
        else if (noPressed)
        {
            lastResponse = "no";
        }
    }

    // ボタン押下時に呼ばれるメソッド（Inspector の OnClicked に登録）

    public void OnOkButtonClicked()
    {
        okReceived = true;
        ok_press_time = Time.time;
        pressed_button = "ok_button";
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        //string logEntry = $"OKbutton,,,{Time.time:F3},\n";
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"OKボタン押下:{Time.time:F3}");

    }
    public void OnYesButtonClicked()
    {
        yesPressed = true;
        yes_press_time = Time.time;
        pressed_button = "yes_button";
        past_exp_phase = exp_phase;
        exp_phase = "not_exp_phase";
        //File.AppendAllText(logFilePath, logEntry);
        //Debug.Log($"YESボタン押下:{Time.time:F3}");
    }

    public void OnNoButtonClicked()
    {
        noPressed = true;
        no_press_time = Time.time;
        pressed_button = "no_button";
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

    private IEnumerator FullAudioPlay(AudioClip audioClip)
    {
        //音楽を鳴らす
        audioSource.PlayOneShot(audioClip);
        //終了まで待機
        yield return new WaitWhile(() => audioSource.isPlaying);
    }

    void Update(){
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
            return;
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
            return;
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
            return;
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
