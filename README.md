# BachelorResearch_public

## 概要 (Overview)
本リポジトリは、卒業研究「MR空間における視覚的イメージの傾向と視線運動の分析」のために独自開発した、Hololens2用の実験アプリケーションです。MR（複合現実）空間において、被験者が物体を記憶・想起する際の視線運動を計測し、視覚思考（ビジュアル・シンキング）の特性を分析することを目的としています。

実験参加者が初めてMRデバイスを触る場合でも問題なく実験が進められるよう実験機能のほか、初見のユーザーでも迷わず操作できるようにするキャリブレーション機能とチュートリアル機能を実装しています。

## 使用技術・環境 (Technologies & Environment)
- **Game Engine**: Unity 2021.3.45f2
- **Framework**: MRTK3 (Mixed Reality Toolkit 3)
- **Device**: Hololens 2
- **Base Package**: Local Joost MRTK3EyeTracking sample
- **Language**: C#

## 主な機能と実装の工夫 (Key Features)

### 1. 視線計測と関心領域（AOI）の動的判定
正面3箇所に関心領域（AOI）を設定し、被験者の視線が0.1秒以上停留したかを検知・記録します。チュートリアルでは、視線が当たると対象物の色が変化する機能も実装しています。

### 2. 被験者ごとのキャリブレーションUIの実装
被験者の身長（目線の高さ）に合わせてAOIの高さを最適化するため、空中にテンキーUIを実装しました。コントローラーを使わず、直感的な空間タップで身長を入力し、動的にオブジェクトの高さを調整します。

### 3. ハンドトラッキングによるジェスチャー入力
実験中の操作負荷を減らすため、物理コントローラーを廃止し、ハンドトラッキングを利用した「ピンチ（親指と人差し指を閉じる）」動作での入力を実装しました。
初見の被験者でも確実に入力できるよう、実験前に「ピンチ練習モード」を行うチュートリアル機能も構築しています。

### 4. MR空間での実物/仮想物のハイブリッド呈示
Hololens 2の描画限界範囲を画像でマスクする処理を組み込み、3Dモデル（仮想物）の呈示だけでなく、実験者が裏で「実物」を配置する際の動きを被験者に意識させないような工夫を施しています。

## 実験フロー (Experiment Flow)
アプリケーションは以下のフェーズで進行します。

1. **事前調整 (Preparation)**
   - 視線のキャリブレーション確認
   - テンキーを用いた身長入力によるAOIの高さ調整
   - ピンチ動作の練習モード（チュートリアル）
2. **Encode（記憶）フェーズ**
   - 呈示物のタイトル音声と共に、台座の上に3Dモデル（または実物）が約6秒間呈示され、被験者はそれを記憶します。
3. **Recall（想起）フェーズ**
   - **Generationタスク**: 再度、(呈示物が目の前にない状態で)呈示物タイトル音声を聞いて呈示物を心の中で思い出し、両手ピンチで合図します。
   - **Inspectionタスク**: 呈示物に関する文章の正誤を、右手（True）または左手（False）の片手ピンチで回答します。

## スクリーンショット (Screenshots)

### 実験環境と仮想物の呈示

<table>
  <tr>
    <td align="center" width="33%">
      <img src="https://github.com/user-attachments/assets/a670d906-67e4-4c60-9f17-a9d5946baa10" alt="実験の様子" /><br />
      <sub>実験の様子</sub>
    </td>
    <td align="center" width="33%">
      <img src="https://github.com/user-attachments/assets/7e629449-0207-4411-af1a-9306b5119b0f" alt="自転車の仮想物呈示" /><br />
      <sub>自転車の仮想物呈示</sub>
    </td>
    <td align="center" width="33%">
      <img src="https://github.com/user-attachments/assets/a688ba60-8330-4604-9581-2f15a3dae1c6" alt="バスケットボールの仮想物呈示" /><br />
      <sub>バスケットボールの仮想物呈示</sub>
    </td>
  </tr>
</table>

### キャリブレーション用UI

<table>
  <tr>
    <td align="center" valign="bottom" width="50%">
      <img src="https://github.com/user-attachments/assets/d8cef702-87e7-42d1-9e2d-068f42417bf9" alt="チュートリアルの初期状態" height="240" /><br />
      <sub>チュートリアルの初期状態</sub>
    </td>
    <td align="center" valign="bottom" width="50%">
      <img src="https://github.com/user-attachments/assets/fcc3c702-65fd-4fd5-8a82-50c624902948" alt="視線判定による立体色の変化" height="240" /><br />
      <sub>視線が関心領域に当たると色が変わる様子</sub>
    </td>
  </tr>
  <tr>
    <td align="center" valign="bottom" width="50%">
      <img src="https://github.com/user-attachments/assets/d6ca46d0-f69c-4785-9fec-33a06746800b" alt="高さ調整用のテンキーUI" height="270" /><br />
      <sub>テンキーでの身長入力（160cm設定例）</sub>
    </td>
    <td align="center" valign="bottom" width="50%">
      <img src="https://github.com/user-attachments/assets/6021fb01-cfbc-432b-9b01-22e889444f8f" alt="実験機能の実行ボタン" height="240" /><br />
      <sub>調整完了後に現れる実験開始ボタン</sub>
    </td>
  </tr>
</table>

### ハンドトラッキング（ピンチ入力）

- ピンチ動作検知

<img width="32%" alt="20260122_170631_HoloLens" src="https://github.com/user-attachments/assets/2777ab5b-5234-4baa-b127-33e998a79289" />

---
**Note:** 
- 本リポジトリは学術研究を目的とした実験用システムであり、一部のアセット（3Dモデル・音声等）は外部の無料アセットストアから利用しています。
- 3Dモデルが容量の関係でpushされていないため、実行自体はできません。
