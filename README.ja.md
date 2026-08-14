[中文](README.md) · [English](README.en.md) · [日本語](README.ja.md)

---

![OG$UI_JKGDD6QL21LEK)9DE](https://github.com/user-attachments/assets/04739895-457d-4806-8d93-6f3ed2b80bbf)
# 中文台球俱乐部桌 CBC Pool Table（中国ビリヤードクラブ）

VRChat ワールド **[中文台球俱乐部 Chinese Billiards Club](https://vrchat.com/home/launch?worldId=wrld_0a35397b-2e7d-4f01-8552-034ab8e76e2e)** で使われているビリヤード台プレハブです。

友人たちのサポートに感謝します：eijis-pan, RokaOvO, WangQAQ, COCOA GAME
キュー：Tempest, catte paling, tarsan, カボ
QQ グループ：780855553

> 元プロジェクト：[MS-VRCSA-Billiards](https://github.com/Sacchan-VRC/MS-VRCSA-Billiards)

---

## ゲームモード

| モード | 説明 |
|--------|------|
| チャイニーズエイトボール | フルルールとボールリターン付き |
| 15レッドスヌーカー | 6レッド / 15レッド選択可 |
| ロシアンビリヤード | 大きな球と狭いポケット、任意の球を打て、入れると得点 |
| 10ボール | 1–10 番、最小番号を先に当て、ポケット指定、WPA ルール選択可 |
| キャロム | 0/1/2/3クッションキャロム |
| 9ボール | 1–9 番、最小番号を先に当て、9 番を先に入れた方が勝ち |
| ラグ（比球） | ブレイク権を決めるショット。双方が 1 球ずつ打ち、反対側のクッションに近い方が勝ち |

---

## NPC AI 対戦相手 (PracticeManager)

ソロ練習モードでは AI 対戦相手と対戦できます。NPC は本格的なビリヤードの意思決定を備えています：

### ショット戦略（優先度の高い順）

| 優先度 | タイプ | 説明 |
|--------|--------|------|
| PASS 1 | ダイレクトショット | 直接ポケットするショット、ポジションプレイ評価付き |
| PASS 2 | シングルクッションバンク | 的球がクッションに1回当たってポケット |
| PASS 2b | ツークッションバンク | 的球がクッションに2回当たってポケット |
| PASS 2.5 | 薄いカット | 大きな角度の薄いカット |
| PASS 3 | シングルクッションキック | 手球がクッションに1回当たって的球にヒット |
| PASS 3b | ツークッションキック | 手球がクッションに2回当たって的球にヒット |
| — | Kボール | セーフティ、ポケットルートがないときの守備戦略 |

### 技術的特徴

- **クッション摩擦補正**：バンク時にクッション摩擦による跳ね返り角度のずれを考慮し、狙いを自動調整
- **軌道検証**：ショット前に対象球の経路をシミュレートし、ポケットを通過するか検証
- **経路遮蔽検出**：手球→的球、的球→クッション、クッション→ポケットの3区間で球の遮蔽をチェック
- **ファウル予測**：インパクト後の手球の軌道を予測し、不意のファウルを回避（手球のポケット、非対象球への接触など）
- **ポジションプレイ評価**：ショット後の手球の停止位置を評価し、次のショットに有利な位置を優先
- **パワー制御**：距離と球配置に応じてショットパワーを自動調整、バンク/キック時は最大パワーを制限
- **繰り返し検出**：同じ球・同じポケットが3回以上続くと強制的にセーフティへ

### ステートマシン

```
IDLE → CALCULATING → CHARGING → DELAYING → SHOOTING → OBSERVING → IDLE
 待機    計算中       チャージ中   待機中      ショット中    観察中
```

### テストモード

PracticeManager は自動テストモード（`testMode`）をサポートし、無人で複数ゲームを自動対戦できます：
- 自動ブレイク、交互ショット、毎ショットの結果を記録
- ログは `Assets/npc_log.txt` に出力
- 内容：球の位置、狙い方向、カット角、パワー、スピン、軌道検証結果
- `Editor/NpcLogExporter.cs` でログをエクスポート可能

---

## テーブル機能

### プレイヤーカスタマイズ (TableHook)

- キュー外観、球マテリアル、テーブルカラーを選択可能（キューはネットワーク同期、球とテーブルはローカル同期）
- キューのサイズ、太さ、滑らかさ、色オフセットを調整可能
- 設定の自動保存・読み込み（VRC PlayerData）
- Discord / QQ グループ経由で設定のアップロード・ダウンロード

### スコアシステム (ScoreManagerV4)

- 対局スコアを自動記録
- リーダーボードをバックエンドへアップロード（`wangqaq.com`、HMAC 認証）
- 45秒タイマー

### その他の機能

- 全自動翻訳システム（VRChat のローカル言語を検出、中/日/英）
- 専用の名前カラー機能
- 個人データとリーダーボードの永続化
- クッションカラーの切り替え
- コヨーテ連携対応（敗者が電気ショックを受ける）

### セットアップ

1. `BilliardsModule` をレイヤー22に設定し、物理を自分自身とのみ相互作用するように設定（上の MS ボタンで自動設定可）
2. シーン内に `TableHook` を配置（実行時に自動検出・追加されるので位置調整のみ）

![image](https://github.com/user-attachments/assets/f453ae11-0735-4885-b700-87101d5971c7)

![Q84OOB{37Q{XY946MTR$E`F](https://github.com/user-attachments/assets/6bf18499-5926-4ca2-8a8c-8f8e33fd9faa)

- カスタムキュー外観と球マテリアル：TableHook にテクスチャを追加、コード内に予約済みスロットがいくつかあります
- UdonChips 対応：BilliardsModule 内のボタンをクリック
- 既製パッケージを使用可；リポジトリをクローンした場合は VRCSDK（>=3.7.5）と UdonSharp を自身で追加してください

> [カスタムキューの設定方法 How to set custom cue](https://youtu.be/YnoQ9jsUg0k?si=EfdxX1FDMUZXM2RX)

---

## VRC Light Volumes (VRCLV) 対応

ビリヤード台の実体サーフェスシェーダーには VRC Light Volumes（VRCLV）対応が組み込まれています。VRCLV は Unity の静的なライトプローブを実行時の球面調和（SH）ライティングに置き換え、台が動的な照明に正しく反応できるようにします。

**現在の状態**: Unity 2022.3.22f1 と VRC Light Volumes 3.0.0-dev.14 の環境で、シェーダーのインポートとコンパイルを確認済みです。スイッチやマクロは不要で、有効な Light Volumes がある場合は VRCLV を使用し、未設定の場合は `LightVolumeSH()` が自動的に Unity ライトプローブへフォールバックします。

**対応シェーダー**:
- `metaphira/TableSurface`（台のクロス、PC。現在の台マテリアルに設定済み）
- `metaphira/VRC LV Standard`（ボール、キュー、脚などの実体パーツ。関連マテリアルは変換済み）
- `metaphira/TableSurface (Quest)`（台のクロス、Quest。シェーダーは利用可能ですが、現在のアセットからは自動参照されません）
- `metaphira/TableSurface (Glass)`（ガラス面。シェーダーは利用可能ですが、現在のアセットからは自動参照されません）

**有効化の手順**:
1. VCC から **VRC Light Volumes** パッケージをインストールします（VCC 依存関係として宣言済み）。
2. VRCLV の手順に従い、ワールドのシーンに `LightVolumeManager` と Light Volumes を作成・設定し、必要なベイクを行います。パッケージをインストールするだけでは LV ライティングは生成されません。
3. ビリヤード台を配置します。コードの変更は不要で、シェーダーはすでに `Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc` をインクルードしています。

**注意**: リポジトリ内のサンプル以外のシーンには Light Volume Manager/Volumes がまだ設定されていないため、そのまま実行した場合に確認できるのは Unity ライトプローブへのフォールバックです。また、Quest と Glass シェーダーは自動マテリアル置換フローに未接続のため、手動で割り当てるか、独自のプラットフォーム切り替え設定に追加してください。UI・ガイドライン・影などの unlit マテリアルはライティングの影響を受けないため、変更は不要です。

---

## 将来の可能性機能

- テーブルコントロールのさらなる機能
- より豊富な名前カラー
- 球の軌道ライン

---

## クレジット / メンバー

最終解釈権：
Roka：ロシアンテーブルとチャイニーズテーブルは、Sacc のビリヤード台の 10ft テーブルと 12ft スヌーカーテーブルのモデルを基に私が変更したものです。当初チャイニーズテーブルは私のワールド "FIVI Flight" 専用でしたが、その後、中国ビリヤードクラブ（中文台球俱乐部）での使用を許可し、開発にも協力しました。ただし使用権は私のワールドと中国ビリヤードクラブのワールドに限定するとは明示されていないため、使用は制限されません。最終解釈権は中国ビリヤードクラブに帰属します。

![image](https://github.com/user-attachments/assets/362abbc4-c159-4617-a6a2-23b64765709a)
![image](https://github.com/user-attachments/assets/8da69556-b526-488a-8127-5fc319de84a9)
![image](https://github.com/user-attachments/assets/f1ff2b1e-e0a0-49d5-becb-be3bf18a4ea8)
![9DH{L{LM 4~0@{)PZ4TD_tmb](https://github.com/cheesestudio/VRChat-Pool-table-with-15-red-snooker-Pyramid-Chinese-8-ball-based-on-MS-VRCSA-Billiards/assets/52149451/7f894791-cf72-473e-bbe6-20bec9804917)
![image](https://github.com/user-attachments/assets/969415da-7bda-4689-9e19-54c2f88e8d73)
![image](https://github.com/user-attachments/assets/36cfebe4-d929-4ac5-a14d-f71371f40442)
