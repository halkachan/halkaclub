# はるかわーるど ver3.0 Phase 5 レポート — 正式ゲーム統合・2D演出改善・公開前品質検証

作成日: 2026-10-18 / 作業ブランチ: `claude/ver3.0-2d3d-phase5`（ローカルのみ・Pushなし）

## 結論

- ローカルの Release Candidate「はるかわーるど ver3.0 Release Candidate 1」を作りました。
  `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_RC1` の `start_rc1.bat` で起動します（48章）。
- 方針「できることは増えるが、操作方法は増やさない」を守りました。新しいボタンはありません。2D／3Dの切替は、今までどおりメニューの「ひょうじ」です。
- Phase 5 で直したこと:
  - 足音を入れました。2D本編の `PlayerFootstepAudio` と `footstep_one_step.wav` を、そのまま使っています。
  - 2Dの草を2D本編と同じ見た目にしました。足元が草に隠れ、カラスの足も隠れます。
  - 3Dの座る位置を直しました（ベンチの座面・端からの距離、クッション）。
  - クッション専用の2D座り画像を作りました（仮素材）。
  - 家から出た直後にキーを押したままでも、歩き続けられるようにしました（0.6秒で再開）。
  - 公開ページを再現したステージングで確認しました。
  - 旧版（ver2.9）の保存データが引き継がれることを確認しました。
- 検査の結果:
  - 新しい `Phase5ReleaseCandidateChecks` は 96件すべて合格しました。
  - 既存の検査もすべて合格しました。Phase 1 は102件、Phase 2 は130件、Phase 2.1 は60件、Phase 3 は88件、Phase 4 は134件、MAP EDITOR は181件です。Version30Checks と MapAuthoringChecks も合格しました。
  - 正式Map・Catalog・Player Sprite・Crow Sprite・音声など79ファイルの SHA-256 は、作業の前後で同じです。
- 確認していないもの（推測で書いていません）:
  - **物理スマートフォン**: 実機がないため、PC の Chrome でスマホを模擬して確認しました。
  - **音を耳で聞くこと**: ゲームが出した音の波形をデジタルで記録し、本編と比べて同じであることを確認しました。
  - **本番オリジン（halkaclub.com）上での保存データ**: ローカルで同じパスに置いて確認しました。
- 作業中に1件、報告することがあります。私のテスト用 Chrome（ポート9334）を別のセッションも使っていて、そのタブが途中で「みねはうす」（`http://localhost:8080/?debug=1`）に切り替わりました。私のスクリプトは、それに気づかないまま数回の操作と再読み込みを1回送りました。詳しくは 52章 にあります。それ以降は、専用のポートだけを使っています。
- `main` へのMerge、Push、GitHub Pages・公開HPの変更はしていません。Phase 6 には進んでいません。

---

## 1. 開始時Git状態

| 項目 | 値 |
|---|---|
| 作業ツリー | `C:\Users\owner\Documents\ChatGPT\HALKA_HP_2D3D_PHASE5`（新しく作成） |
| 作業ブランチ | `claude/ver3.0-2d3d-phase5`（`claude/ver3.0-2d3d-phase4` の `90b7383` から作成） |
| main | `d039acf`（変更なし） |
| origin/main | `3d6ea8c`（みねはうすの作業で進んだもの。Phase 5 では触っていません） |
| 元の作業ツリー `HALKA HP` | `codex/ver3.0-preview`、` M GrassDecoration.prefab`（改行の違いだけ）、`?? backup-v05-20261006-174557/` |

すべての作業ツリーとブランチの記録は、Backup の `git-refs.txt` と `git-status-all.txt` にあります。

## 2. Backup

場所: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE5_BACKUP_20261018`（337 MB）

| 内容 | ファイル |
|---|---|
| リポジトリの全ref（`git bundle verify` で確認済み） | `repo-all-refs.bundle` |
| ref・作業ツリーの状態 | `git-refs.txt`、`git-status-all.txt`、`git-diff-original.patch` |
| 作業前の SHA-256 | `official-map-sha256-before.txt`、`player-sprite-sha256-before.txt`、`crow-assets-sha256-before.txt`、`audio-and-benchsit-sha256-before.txt` |
| ソースのコピー | `files\`（POC2D3D、Scripts、WebGLTemplates、ProjectSettings、Blender、tools、Content、公開ページ） |
| Phase 4 Preview Build | `phase4-preview-build\` |

## 3. Phase 4からの変更一覧

コミット `68eec55`（レポートは別のコミット）。

| 範囲 | ファイル | 内容 |
|---|---|---|
| 本編（2D）共通 | `Scripts/Player/PlayerFootstepAudio.cs` | `externallyDriven`（外から呼ぶ場合）、`PlayStep()`、`PlayCount`。2D本編の動き（PlayerMoverの購読）は変えていません |
| 本編（2D）共通 | `Scripts/UI/GameHud.cs` | 右上の表示を差し替える項目（空なら今までどおり `GameVersion.Label`） |
| 共有世界 | `SharedWorldSession.cs` | `StepStarted`（歩き始め）と `StartedSteps` |
| 共有世界 | `PocGameplay.cs` | 歩き始めで足音を1回 |
| 共有世界 | `PocCrow.cs` | 草を揺らしたときの通知（`Rustled`） |
| 2D | `PocPixelView2D.cs`、`PocPixelSprite.shader`、`POC_PixelSprite.mat` | 本編の草Sprite（地面の層）、揺れ、足元を隠す、3Dの草の房を2Dで出さない |
| 2D | `PocViewSwitcher.cs` | 切替時に草の房の表示を合わせる |
| 座る | `PocActorView.cs`、`PocPresentationCatalog.cs`、`POC_PresentationCatalog.asset` | 座りの表示Metadata（腰の高さ・脚の角度・中央寄せ）、クッションの2D画像 |
| 仮素材 | `POC2D3D/Provisional/cushion_sit_provisional.png`（＋README） | クッション専用の2D座り画像（仮） |
| カメラ・入力 | `PocThirdPersonCamera.cs`、`PocInput.cs` | 退室時に押したままのキーを0.6秒後に再開 |
| 検証パネル | `PocDebugPanel.cs` | RCでは通常は停止。行を2つ追加（退室キー、2D草） |
| Scene | `HARUKA_WORLD_ver3.0_RC1.unity`（新規）、`HALKA_WORLD_2D3D_POC.unity`（再生成） | RC用Scene |
| Editor | `PocSceneBuilder.cs`、`PocBuilder.cs` | RC Scene と RC1 Build の作成 |
| Editor | `Phase5ReleaseCandidateChecks.cs`（新規）、`Phase5Capture.cs`（新規） | 検査、撮影 |
| Editor | `Phase4IntegrationChecks.cs`、`Phase21PresentationChecks.cs` | 前提が変わった検査を修正（38章） |
| tools | `tools/poc2d3d/rc1/`（新規）、`phase5_protected_sha256.txt`（新規） | RC1一式、保護ファイルの一覧 |

他の6つの `.mat` は、Unity が保存し直したときの改行の違いだけでした。コミットには差分として残っていません。

## 4. 本編統合方式の比較

| | A: POCのSceneを育ててRCにする | B: 旧本編のScene（FirstDay）へ移す |
|---|---|---|
| 中身 | 共有世界・2D/3D・AUTO・カラス・家具・Life Log・時間・保存は、Phase 1〜4 でPOCのSceneにすでに入っている | FirstDay は2D専用。共有世界と3D表示を最初から作り直すことになる |
| 本編の再利用 | POCは本編のコンポーネント（GameMenuController、AutoModeController、LifeLogController、WorldTimeController、PlayerFootstepAudio、TouchDpad など）をすでにそのまま使っている | 同じものを使うが、つなぎ直しが多い |
| 危険 | 小さい。Phase 1〜4 の検査（514件）がそのまま守ってくれる | 大きい。FirstDay は `Version30Checks` で再生成される本編のSceneで、変えると2D本編の検査に影響する |
| 公開版への影響 | なし（別のScene、別のBuild） | FirstDay の変更が ver2.x の作り方に影響する |

**A を採用しました。** 旧本編の FirstDay と ProjectBuilder（本番用Build）には、何も変更を加えていません。

## 5. 採用した正式起動構造

- `PocSceneBuilder.Build(scenePath, releaseCandidate)` が、同じ中身から2つのSceneを作ります。
  - 技術検証用: `HALKA_WORLD_2D3D_POC.unity`（今までどおり）
  - Release Candidate: `HARUKA_WORLD_ver3.0_RC1.unity`（新規）
- RC で違うのは起動の設定だけです。
  - 最初は 2D です。
  - 検証パネルは非表示で、部品ごと止めています（`enabled = false`）。毎フレームの OnGUI も、ProfilerRecorder も動きません。
  - `?debug=1` を付けると、検証パネルが「はるかわーるど ver3.0 RC1 (debug)」として出ます。
  - 右上の表示は「ver3.0 RC1」です。2D本編の `GameVersion.Label`（ver3.0 Preview）は変えていません。
- `PocBuilder.BuildReleaseCandidate()` は、RC Scene だけを repo の外に Build します。置き場所は公開サイトと同じパスの `HARUKA_WORLD_ver3.0_RC1\halkaworld\webgl\` です。そのとき `tools/poc2d3d/rc1/` の一式もコピーします。
- RC で使えること: 2D・3D、屋外・屋内、AUTO、カラス、家具、ベンチ・クッション、せいかつきろく、朝夜、保存。

## 6. POCからの移行内容

- POCの中身は、Phase 1〜4 で本編と同じ規則にそろえてあります。Grid、移動、衝突、調べる、AUTO、カラスAI、Life Log、時間、Menu です。Phase 5 では次を本編から移しました。
  - 足音: `PlayerFootstepAudio` と `footstep_one_step.wav`。設定は ProjectBuilder と同じで、音量0.45、2D音、ピッチ1です。
  - 2Dの草: `grass.png`、`grass_rustle/00〜04`（0.09秒×4、0.42秒）、`MapPlacementRules.HasGrass`。
  - 足元: `PlayerGrassOcclusion`（10px）、`CrowGrassOcclusion`（4px）の値と、どのマスで判定するかの規則。
- 技術検証用の機能（パネル、比較用の切替）は、RCでは `?debug=1` のときだけ使えます。

## 7. Gameplayの重複確認

| 確認 | 結果 |
|---|---|
| 足音の部品 | Scene に `PlayerFootstepAudio` は1つだけです。`PocGameplay` が `StepStarted` を1回だけ購読しています。本編の PlayerMover 経由の購読はありません（`externallyDriven`） |
| 足音の回数 | 1歩につき1回（Editor検査15件、WebGLでも確認。10章） |
| カラスの声 | 話しかけ1回につき `InteractionAudio.Play` が1回。声の部品は1つだけ（検査） |
| クッションの動作 | 座る処理は `SharedWorldSession.TrySit` の1か所だけ。2D・3D の表示はそれを読むだけ |
| メニュー・AUTO・Life Log・時間 | 本編のコンポーネントがそれぞれ1つ（Phase 4 の検査を継続） |
| 2D本編（FirstDay） | 変えていません。`Version30Checks` は合格 |

WebGL では、1回の `PlayOneShot` が `AudioBufferSourceNode.start` を**2回**呼びます。同じ時刻、同じ音です。これは Unity WebGL の仕組みで、元の2D本編（ver3.0 Preview）でもまったく同じでした。録音では、どちらも1歩ごとに音が1つで、音量も同じでした（10章）。

## 8. 足音の統合

- `SharedWorldSession.TryStep` は、Gridの判定に通って歩き始めた時だけ `StepStarted` を出します。ぶつかって歩けなかった時は出しません。
- `PocGameplay.OnStepStarted` が `PlayerFootstepAudio.PlayStep()` を呼び、`PlayOneShot(footstep)` で鳴らします。手動の歩きも AUTO も、同じ入口を通ります。
- 鳴らないこと（Editor検査で確認）:
  - ぶつかった時
  - 座る、座ったままキーを押す、立つ時
  - 2D/3Dの切替やカメラの動き
  - 家に出入りする時（扉へ踏み出す1歩だけ鳴り、マップの切替そのものでは鳴りません）
  - カラスが歩いたり飛んだりする時

## 9. StepStartedの実装

- `StepStarted` は歩き始めの瞬間に出ます。0.18秒かけて移動し、終わったときに今までの `StepCompleted` が出ます。Life Log が数えるのは `StepCompleted` のほうです。
- 検査の結果:
  - 足音は歩き始めのフレームで鳴ります（まだ移動中で、`CompletedSteps` は増えていない）。
  - 2つは別の出来事として、「始まり → 終わり → 始まり → 終わり…」の順に出ます。
  - キーを押したままだと、1歩が終わったフレームで次の1歩が始まります。それでも足音の数＝始まった歩数＝終わった歩数でした（3歩で3回）。
- 草が揺れるのも、2D本編と同じく歩き始めです。行き先のマスが揺れます。

## 10. 手動／AUTOの音声検証

**耳では聞いていません。** ページの中で、ゲームが出した音をデジタルで記録して確かめました。

- やり方:
  - 音を出す前の段階（AudioContextの出力）を `MediaStreamAudioDestinationNode` に分けて、`MediaRecorder` で録音しました。
  - 同時に、すべての `AudioBufferSourceNode.start` を記録しました。
  - headless Chrome は無音の間を録音しないため、出力には聞こえない一定値（1e-4）を加えて、実時間で録れるようにしました。この一定値はスピーカーへはつないでいません。

| 確認 | 結果 |
|---|---|
| 元の2D本編（8877）と RC1 で4歩 | 録音では、どちらも0.12秒おきに音が4つ。最大振幅はどちらも 10019 / 10049 / 10049 / 10005 で、**同じ波形の大きさ** |
| 手動の2D 4歩 | start 8回（4歩×2） |
| 手動の3D 6歩 | start 12回 |
| ベンチに座る・座ったままキー・立つ | 0回 |
| AUTO | 60秒後に歩き出し、10秒間に start 8〜34回（歩数×2）。キーを押すと止まり、その後3秒は0回 |
| カラスへの話しかけ | 声（0.813秒）が start 2回。これは PlayOneShot 1回分 |
| 動画の音 | 動画01・02・03・04・06・07に、実際に録音した音が入っています。動画02では、12回の音が12歩と一致しています |

- 録音したファイル:
  - `HARUKA_WORLD_ver3.0_PHASE5_CAPTURES\webgl\audio\`（元の本編とRC1の足音）
  - `webgl\pc\flow_audio.webm`
  - `webgl\pc\audio_starts.json`

## 11. 2D草の表現

- 比較した3つのやり方:
  - **A**: Phase 4 の3Dの草の房を増やす・大きくする。→ 2D本編の絵とは違ったままです。
  - **B**: 3Dの房のまま、足元を隠すマスクだけ足す。→ 草の見た目は本編と違ったままです。
  - **C**: 2D本編の草Sprite（`grass.png`）を、本編と同じマスに描く。→ **採用しました。**
- C の内容:
  - 草を描くマスは、2D本編と同じ `MapPlacementRules.HasGrass` で決めます。土・道、通れない物の下、草を生やさない物の下には描きません。
  - 地面の層として描きます。新しい shader 設定 `_GroundOnly` で、地面のピクセルの上だけに描くので、木・ベンチ・看板・家・岩・花・ﾊﾙｶﾁｬﾝ・カラスは、すべて草の手前に見えます。
  - 2Dでは、3Dの草の房を表示しません（表示だけの設定）。3Dでは今までどおり表示します。
  - Map JSON は変えていません。マップは二重に管理していません。草に当たり判定はありません。
  - 毎回同じマスに描きます（マップを切り替えても同じ）。
- 検査で確認したこと:
  - 屋外: 草のマスがすべて描かれる
  - 土・通れない物の下: 0マス
  - 屋内: 0マス（2D本編と同じ）
  - 全マスを地面の層の1つのまとまりで描いている
  - 草のマスも歩ける
  - 揺れた後、草は元に戻る
- `?debug=1` の「2D grass」で、Phase 4 の見た目（3Dの草の房）に戻して比べられます。

## 12. Player Grass Occlusion

- 2D本編の `PlayerGrassOcclusion` と同じです。草のマスでは、64pxの絵の**下10pxだけ**を隠します。顔と体は残ります（54px以上）。
- どのマスで判定するか: 移動の前半は出発したマス、後半（進み具合が0.5以上）は行き先のマスです。2D本編と同じで、検査では「土→草」の1歩の途中で、0から10に切り替わりました。
- 座っている間は隠しません。3Dでは使いません（3Dでは3Dの草の房が足元に見えます）。
- 絵の下の行を切る形で描きます。拡大の倍率、Spriteの取り込み設定、元の画像は変えていません。

## 13. Crow Grass Occlusion

- 2D本編の `CrowGrassOcclusion` と同じです。地上の草のマスでは、32pxの絵の**下4px**を隠します。
- 飛んでいる間と、木に止まっている間は隠しません（検査）。

## 14. 元の2Dとの比較

同じマップ・同じスタート位置で撮影しました（`sheets/01_2d_grass_original_p4_p5.png`、`02_2d_feet_player_crow.png`）。

| 項目 | 元の2D（ver3.0 Preview） | Phase 4 | Phase 5 |
|---|---|---|---|
| 草の密度・大きさ | 草のマスごとに草の絵 | 3Dの房がまばら | 元と同じ絵、同じマス |
| 道の境目 | マスで四角く分かれる | Phase 2 の自然な形の道 | Phase 2 の道。草は土の外側だけ |
| ﾊﾙｶﾁｬﾝの足元 | 草に隠れる | 隠れない | 元と同じく隠れる |
| カラスの足元 | 下4pxが隠れる | 隠れない | 元と同じ |
| 木の影・花・岩 | 見える | 見える | 見える（草の手前） |
| 1マスの画素 | 32px | 16px/セルを2倍 | 同じ |
| 輪郭 | なし | 1px #252B39 | 1px #252B39（草には輪郭を付けない） |

- 元の2D本編は時刻を固定できないため、撮影したときの実時間の夜になっています。Phase 5 は朝と夜の両方を撮りました。
- 違いが残るところ: 3Dの道・木・家などは、Phase 2〜3 の Blender モデルを上から見たものなので、絵の細かいところは本編のSpriteと違います（Phase 2 以来の方針どおり）。

## 15. OutlineとPixel品質

- 16px/セル、Object ID Outline、1px、#252B39、Point sampling は、そのままです（検査）。
- 草のSpriteには輪郭を付けません（`_Thickness 0`、本編と同じ見た目）。
- 拡大は整数倍のままです。非整数の拡大やSpriteの取り込み設定の変更は、元に戻していません（`grass.png` は Point のまま、ファイルも同じ）。

## 16. Bench Seat Anchor

Phase 4 の3Dの座り方を測りました。

| 測ったもの | 値 |
|---|---|
| SeatAnchor_L / R の高さ | 0.22 |
| 座面の高さ（メッシュで測定） | 0.18 |
| 太ももの下面（仮モデル） | 0.154。座面に 0.026 めり込んでいた |
| ベンチの端からアンカーまで | 0.22 |
| 仮モデルの幅 | 0.4。左の席では体の端がベンチの端とほぼ同じ位置で、「端に寄って見える」原因 |

直し方（表示だけ。アンカー、ベンチのGrid Root、Action Point、左右2席は変えていません）:

- `PocActorView.SeatDisplay` を追加しました。将来の正式モデルでは、この数値を差し替えます。
  - ベンチ: 腰を 0.03 上げます。脚は90°。ベンチの中央へ 0.08 寄せます。
  - クッション: 腰を 0.07 上げます。脚は62°で、床へ下ろします。
- 結果（検査）:
  - 太ももの下面は 0.184 で、座面 0.18 の上に乗りました。
  - 体の端とベンチの端の間は 0.093 です（左右とも）。
  - 腰は座面の上、背もたれより前です。
- 座ったまま2D/3Dを切り替えても座ったままです。Aで立ち、歩数は増えません。座っている間は AUTO も動きません（Phase 4 の検査を継続）。
- 比較画像: `sheets/04_3d_bench_cushion_old_new.png`

## 17. Cushion専用2D座り表示

- 新しいファイル `Assets/POC2D3D/Provisional/cushion_sit_provisional.png` を作りました。**仮素材**として README に書いてあります。
- 作り方: `bench_sit.png` のコピーを、ピクセル単位で編集しました。
  - 頭・体・ひざ（上から50行）は元のままです。
  - 下にぶら下がっていたブーツの筒を取り除き、ブーツをひざのすぐ下に置きました（床に脚を折って座る形）。
  - 配色・頭身・画素の密度は元の絵のままです。Blender からの撮影は使っていません。
- `bench_sit.png` と ﾊﾙｶﾁｬﾝの通常のSpriteは変えていません（SHA-256 が同じ）。
- 差し替えは `PocSpriteSet.cushionSit`（`PocSceneBuilder.CushionSitPath`）の1か所だけです。この画像がなければ `bench_sit.png` に戻ります。
- 比較画像: `sheets/03_2d_bench_cushion_sit.png`

## 18. Cushion Action

- Phase 4 と同じく、クッションに乗ってAで座り、もう一度Aで立ちます。2D/3Dを切り替えても座ったままです。RC1 でも、PC（クリック）とスマホ（Aボタン）で確認しました。
- **旧本編との違い**: 旧本編（ver2.x）にはクッションがなく、座れませんでした。この操作は ver3.0 で増えたものです。正式版の更新履歴に書く場合は、新しい操作として書いてください。
- 座る処理は1か所だけなので、2つ呼ばれることはありません（7章）。

## 19. Crow AIの維持

- カラスのAI（`CrowBehaviourCore`）は書き直していません。
- Phase 4 の Blender モデルと動き（地上で待つ・歩く・飛び立つ・飛ぶ・止まる・離れる・降りる）、飛ぶときに脚をたたむ動き（−85°、0.65倍）、翼の動きは、そのままです。
- 状態が1つであること: 飛んでいる途中で 2D/3D を8回切り替えても、状態と位置は変わりませんでした（検査）。WebGL でも、3Dで話しかけて飛ばし、2Dに戻ると同じ木にいました。

## 20. Crow Perch Anchorの比較

| | 止まる位置 |
|---|---|
| 元の2D | 木の絵の上から28%、中央から12%左（`TreePerchAnchor2D`） |
| Phase 4/5 の2D | 3Dの木の `PerchAnchor` を、2Dの見下ろしで投影した位置。木の上から約21%、中央から約10%左 |
| 3D | 同じ `PerchAnchor` の上（検査で一致を確認） |

- 違いは画面上で約2pxでした。木の葉の上に止まって見え、不自然ではないため、**2D専用の補正は入れていません**。2D専用の状態もありません。
- 止まっている間、2Dでは足元を隠さず、木の手前に描きます（検査）。

## 21. Crow Audio

- 話しかけ1回につき、声は1回です（`InteractionAudio.PlayRequestCount` が+1）。2D/3Dの表示から声を出すことはありません。
- WebGL（PC・スマホのAボタン）で、0.813秒の声が PlayOneShot 1回分だけ出ることを、start の記録で確認しました。

## 22. 3D Camera維持

- 北固定（North fixed）が標準です。距離は屋外3.4、屋内3.6です（Phase 4 で決めた値。検査あり）。
- 向きによって後ろへ回るカメラには戻していません（`?debug=1` の比較用の選択肢としてだけ残しています）。

## 23. Exit Camera

- 退室直後の家の正面の構図は、そのままです。
- **押したままのキー（Phase 5 で変更）**:
  - Phase 4 までは、扉を出るときに押していたキーを、一度離すまで受け付けませんでした。
  - Phase 5 では、キーの意味が変わらない場合に限り、家を **0.6秒** 見せた後、押したまま歩き続けます。条件は、北固定のカメラで、構図が北を向いていることです。
  - それ以外（後ろへ回るカメラの選択肢など）は、今までどおり離すまで待ちます。
- 検査で確認したこと:
  - 構図が先に出る
  - 0.60秒後に南へ1歩目が出る
  - 方向は逆にならない
  - 家に戻らない
  - カメラの動きはなめらか（1フレーム最大0.026）
  - 再開を切った場合や別のカメラの選択肢では、従来どおり待つ
  - 2Dの退室は変わらない
- WebGL（PC）: 屋内で S を押したまま出ると、家の正面が映った後、歩いて通常のカメラに戻りました（夜でも確認）。
- `?debug=1` の「3D exit held key」で、Phase 4 の動きに戻して比べられます。

## 24. Tree Fade

- 3Dの木の半透明（40%、0.25秒）は、そのままです（検査、WebGL）。
- WebGL で確認した場面:
  - PC 3Dで木(5,1)の向こう側に立つと、半透明になる（`webgl/pc_run7_noaudiosync/09_tree_fade_3d.png`）
  - 実時間の夜（`webgl/pc_run2_discarded/09_tree_fade_3d.png`）
  - スマホ（`m05_tree_fade_3d.png`）
  - 2D/3D切替や入室・退室の後にも、同じ場所で半透明になる（PC総合テストの流れの中）
- 2本の木の扱い・カラスが止まっているときの扱い（カラスは半透明にならない）は、Phase 4 の検査（Phase4IntegrationChecks の木の半透明の項目）が引き続き合格していることで確認しました。Phase 5 の WebGL では、カラスが止まった木を半透明にする場面は撮れていません。

## 25. Smartphone Landscape

DevTools のエミュレーション（844×390、DPR 2、スマホ・タッチ）で確認しました（`webgl/mobile/`、`MOBILE_SHEET.png`）。

| 項目 | 結果 |
|---|---|
| 十字キーの長押し | 1.3秒で8歩、足音8回 |
| Aボタン | 看板のメッセージ「ここは HALKA WORLD。」、カラス「カァ。」（声1回）、ベンチ・クッションに座る・立つ |
| Menu・ひょうじ | 2D↔3D |
| 入退室 | 3Dで入室、退室直後に家の正面 |
| 木の半透明 | 3Dで確認 |
| 朝夜 | 実時間の夜と、`?debug=1` で固定した朝・夜 |
| せいかつきろく・再読み込み | 歩数が残る |
| 16:9（640×360） | 2D・3Dとも表示され、ボタンは角に入る（`m25`・`m25b`） |
| FPS | 60（5秒、95%の時間16.8ms） |

- 気づいたこと（Phase 4 から同じ）: スマホ横画面の2Dでは、画面の右端にマップの外側（背景色 #101014）が見えます。右上の「ver3.0 RC1」の「RC1」は暗い背景の上に暗い文字で描かれ、読みにくくなっています。52章 にも書きました。

## 26. Orientation Guard

- 縦にすると「スマホを横向きにしてください」が出て、ゲームが止まります。横にすると消えて動きます。
- 縦→横→縦→横の後も、すぐに歩けました（`m21`〜`m23`、動画08）。
- iframe の中でも、iframe の縦横で判定します。ステージングの縦では案内が出ました（30章）。

## 27. Safe Area

- 左右47・下21 のSafe Areaを設定すると、キャンバスは x=47、幅750、高さ369 で、その内側に入りました（`m24_safe_area_47_21.png`）。
- 注意: ページを開いた後でSafe Areaの値だけが変わった場合は、次の画面サイズの変化（回転など）まで反映されません。実機では、回転と同時にSafe Areaも変わるので問題ないと考えています（未確認）。

## 28. 物理スマホ検証状況

**物理スマートフォンでは確認していません。** 実機での手順は `DEVICE_TEST_PROCEDURE_RC1.md` に書きました（同じWi-Fiで `start_rc1.bat lan`、確認19項目、記録表）。53章 も見てください。

## 29. Staging HP構造

`HARUKA_WORLD_ver3.0_RC1` の中に作りました。公開ページは変えていません。

```
HARUKA_WORLD_ver3.0_RC1\
  style.css, favicon.png             公開サイトからコピー
  halkaworld\index.html              公開ページ（halkaworld/index.html）と同じレイアウト・CSS
  halkaworld\index-mobilefix.html    同じページ＋スマホ表示の改善案CSS
  halkaworld\game-page.css           公開ページのCSS（コピー）
  halkaworld\game-page-rc1-proposal.css  改善案
  halkaworld\webgl\                  RC1（公開サイトと同じ /halkaworld/webgl/ のパス）
```

- ステージングページは、repo の `halkaworld/index.html` から `make_staging_pages.py` で作ります。これは origin/main と同じ内容でした（改行を除いて比較）。
- 公開ページから変えたところ:
  - 上に「ローカル検証用ステージング」の帯
  - `noindex`
  - canonical なし、アクセス解析のスクリプトなし
  - 見出しを「はるかわーるど」、版を「ver3.0 RC1」
  - iframe を `webgl/?v=3.0-rc1&build=<RC1のbuild>`
- 更新履歴の文章、操作説明、他のページへのリンクは、そのままです。

## 30. iframe検証

`webgl/staging/STAGING_*.txt` と画像（`st_*`）、動画09 に記録しました。

| 条件 | 公開ページと同じCSS | 改善案CSS |
|---|---|---|
| PC 1280×720 | 枠 892×636。ゲームは枠いっぱいに起動 | 同じ |
| スマホ横 844×390 | **枠の高さ452 > 画面390。スクロールしても全体が入らない** | 枠 346 で画面に収まる |
| スマホ縦 390×844 | 枠 306×570、iframe の中に回転案内 | 同じ |
| 枠の上でタッチして上へドラッグ | ページはスクロールしなかった（エミュレーション） | 同じ。CSS でも `touch-action: none` |
| fullscreen | iframe に `allow="fullscreen"` がある。ゲーム・ページに全画面ボタンはない（ボタンを増やさない方針） | 同じ |
| 保存先 | iframe の中でも、単体で開いても、同じ保存場所（`/idbfs/9c44…`） | 同じ |

- iframe の中のゲーム（キャンバスの大きさ、Safe Area の計算、回転案内）は、iframe の大きさを基準に動きます。親ページの Safe Area は、iframe の中には伝わりません。
- 横画面の公開ページでの問題は、`.unity-frame { height: clamp(420px, 54vw, 640px) }` が原因です。改善案は、スマホの横向きのときだけ枠を `100svh − 40px` にし、回転したら枠を画面の中央へスクロールするものです。

## 31. 正式HPへの移行時の注意

1. ゲームは今と同じ `halkaworld/webgl/` に置いてください。パスが変わると、保存データの場所が変わります（33・34章）。
2. スマホ横画面の枠の高さ（30章）。改善案CSSを使うかどうかは、ユーザーが決めてください。公開中の `game-page.css` は変えていません。
3. 見出し・タイトル・OGPの「HALKA WORLD」を「はるかわーるど」にするかどうか（35章）。
4. 更新履歴（ver3.0 の文章）と、操作説明（3Dとメニューの「ひょうじ」）を足すかどうか。
5. `?v=` と `build=` の値を新しい Build に合わせること（ブラウザのキャッシュ対策）。
6. Site Editor、みねはうす、ポイント、他のページには、Phase 5 では触れていません。

## 32. Save Key・Format

| キー | 形式 | 変更 |
|---|---|---|
| `HALKA_WORLD_life_log_v1` | `v1|歩数|秒` | なし |
| `HALKA_WORLD_time_cycle_anchor_v1` | ミリ秒の数字 | なし |
| AUTOのON/OFF | 保存しない | なし（本編と同じ） |

検査で確認したこと: キーの名前、旧形式の読み込みと書き戻し、知らない形式で上書きしないこと、歩数を足して保存すること、時間のアンカーを消さないこと。保存キーを消したり初期化したりする処理はありません。

## 33. 旧データ互換性

試験用の Chrome（新しいプロファイル。ユーザーの実際のデータは使っていません）で、**同じオリジン・同じパス**に、ver2.9 と RC1 を順に置きました。

1. repo の ver2.9（公開中と同じファイル）を `http://127.0.0.1:8896/halkaworld/webgl/?v=2.9&build=…` で遊ぶ → 保存は `v1|8|59`、場所は `/idbfs/9c4430914ca6a29f4fd84ab0428445ca/PlayerPrefs`。ver2.9 には時間のキーはありません。
2. 同じ 8896 に RC1 を置いて開く → **同じ場所**から `v1|8|89` を読み込みました（8歩はそのまま、プレイ時間も続いています）。時間のキーは新しく追加され、他のキーは消えていません。
3. RC1 で3歩 → `v1|11|118`。せいかつきろくの画面も「11」（`webgl/migration/m4_rc1_lifelog.png`）。
4. ステージングページ（iframe）から開いても同じ場所でした。最終の Build でも再確認しました。

| 区分 | 内容 |
|---|---|
| ローカルで実証 | 同じパスなら、ver2.9 → RC1 で歩数・プレイ時間が引き継がれる |
| 設計上の互換 | キー・形式が同じなので、v1 の値はすべて読める |
| 本番オリジンでは未確認 | halkaclub.com 上では試していない |
| 公開後に確認すること | 公開版で前の歩数が表示されること（54章） |

## 34. Browser Originの違い

- Unity WebGL の保存場所は、ブラウザのオリジン（`https://halkaclub.com`）ごとに分かれ、その中でページのパスごとに分かれます（`/idbfs/<パスから作るハッシュ>`）。
- ローカルの `127.0.0.1:8895`・`8896`、LAN の `192.168.x.x:8895` の保存データは、公開版とは別です。RC1 で遊んでも、公開版のデータは変わりません。
- 公開版と同じパス（`/halkaworld/webgl/`）に置くことが条件です。`?v=` などのクエリは保存場所に影響しませんでした（`v=2.9` と `v=3.0-rc1` で同じ場所）。

## 35. ゲーム名の表示

| 場所 | 表示 |
|---|---|
| RC1 のページタイトル | はるかわーるど ver3.0 RC1 |
| ゲーム内の右上 | ver3.0 RC1 |
| `?debug=1` の題 | はるかわーるど ver3.0 RC1 (debug) |
| ステージングの見出し・タイトル | はるかわーるど |
| URL、保存キー、ID、クラス名、repo | 今までどおり（HALKA_WORLD など） |
| 看板の文章「ここは HALKA WORLD。」 | **変えていません**。マップデータ（Map JSON）の文章なので、データも表示も変えていません。表示だけ置き換える方法も考えましたが、文章の内容はユーザーが決めることなので、記録にとどめました（54章） |

## 36. Map・Catalog保護

作業前後の SHA-256 の比較（`HARUKA_WORLD_ver3.0_PHASE5_LOGS\21-sha256-after-comparison.txt`）:

| 一覧 | ファイル数 | 変わったもの |
|---|---|---|
| 正式Map JSON・Catalog | 4 | 0 |
| Player Sprite（bench_sit.png を含む） | 50 | 0 |
| Crow Sprite・声 | 18 | 0 |
| 音声（足音を含む）・bench_sit | 7 | 0 |

草の画像（grass.png、grass_rustle 00〜04）も、Git の Phase 4 時点と同じです（`phase5_protected_sha256.txt` に追加して、検査でも確認）。

## 37. Phase5ReleaseCandidateChecks

`Halka.Poc2D3D.EditorTools.Phase5ReleaseCandidateChecks.RunBatch` — **96件、すべて合格**（`20i-phase5-checks.log`）

| 区分 | 件数 | 主な内容 |
|---|---|---|
| startup | 15 | RC Scene が別ファイル、最初は2D、パネルは止まる、ver3.0 RC1、足音・草・クッションの設定、北固定3.4/3.6、ボタンは増えない、RCページ、ステージングページ |
| save/time | 7 | キー・形式、旧データの読み込み、Life Log の保存、時間のアンカー |
| footsteps | 15 | 歩き始めで鳴る、1歩1回、始まり／終わりの順、ぶつかった時・切替・座る・入室・カラスでは鳴らない、AUTO |
| 2D presentation | 19 | 草のマス、土の上にない、地面の層、3Dの房、当たり判定なし、毎回同じ、屋内、足元10px／途中で切り替わる／54px残る／座ると隠さない、カラス4px、揺れ、16px・1px・#252B39、Point、比較用の切替 |
| seats | 19 | 左右のアンカー、太ももが座面の上、中央寄せ0.08、Metadata、2Dは bench_sit、立つと歩数は増えない、クッションの腰と脚、仮画像 |
| crow | 6 | 8回切り替えても状態が同じ、木に止まる、2Dで止まる位置、3Dモデル、声1回、脚をたたむ |
| camera | 8 | 北固定と距離、押したまま0.6秒、方向が逆にならない、なめらか、Phase 4 の動きの選択肢、2Dの退室は同じ、木の半透明 |
| mobile | 3 | 回転案内の部品、改善案CSS、公開CSSは変えていない |
| protection | 4 | 3つのSHA-256一覧、公開ページは ver2.9 のまま |

## 38. Phase 1～4 Regression

最終状態（`HARUKA_WORLD_ver3.0_PHASE5_LOGS\20*.log`）:

| 検査 | 結果 |
|---|---|
| PocChecks（Phase 1） | 102 合格 |
| Phase2EnvironmentChecks | 130 合格 |
| Phase21PresentationChecks | 60 合格 |
| Phase3BuildingFurnitureChecks | 88 合格 |
| Phase4IntegrationChecks | 134 合格 |

前提が変わった検査（削除はしていません。理由はコードのコメントにも書きました）:

| 検査 | 変更 | 理由 |
|---|---|---|
| Phase 2.1 の Sprite の画素比較（3件） | 草で隠れた下の行は比べない | Phase 5 で足元を草が隠すようにしたため。隠れ方そのものは Phase 5 の検査で確認しています |
| Phase 4「cushion 2D」 | bench_sit.png ではなく、クッション用の画像であること | Phase 5 で専用の仮画像にしたため |
| Phase 4「bench 3D: SeatAnchor_L に座る」 | 中央寄せの0.08を引いて比べる | 表示だけ中央へ寄せたため（アンカーは同じ） |

## 39. Version30Checks

合格です（`20f-version30checks.log`）。2D本編の FirstDay を作り直す検査なので、検査が書き換えた `FirstDay.unity`・`GrassDecoration.prefab`・`ProjectSettings.asset` は、毎回 Git の状態に戻しています（Phase 5 の作業ツリーだけ。元の `HALKA HP` には触れていません）。

## 40. MapAuthoringChecks

合格です。Standalone Map Contract v0.6 も合格しました（`20g-mapauthoringchecks.log`）。

## 41. MAP EDITOR Core Tests

181件、すべて合格です（`20h-map-editor-core-tests.txt`）。

## 42. PC WebGL

- 環境: repo 外の RC1 Build を、Phase 5 専用の headless Chrome（1280×720）で、**`?debug=1` なし**で操作しました。
- 操作: キーとクリックを送り、位置は2Dの画面から読み取りました。
- 記録: `HARUKA_WORLD_ver3.0_PHASE5_CAPTURES\webgl\pc\`（`PC_FLOW_SHEET.png`、`FLOW_LOG.txt`）と `pc_run7_noaudiosync\`（3Dで話しかけた回）。

| # | 手順 | 結果 |
|---|---|---|
| 1 | 起動 | 2D、ver3.0 RC1、検証パネルなし |
| 2〜4 | 2Dで移動、足音、草の重なり | 4歩で足音4回分、草のマスで足元が隠れる |
| 5 | カラス | 見える |
| 6〜8 | Menu → ひょうじ → 3D、3Dで歩く | 6歩、足音6回分 |
| 9 | 木の半透明 | (5,2)で木(5,1)が半透明 |
| 10〜12 | カラスに話しかける → 飛ぶ → 木に止まる | 3Dでカラスをクリック →「カァ。」、声1回、離陸・飛行・止まる（run7）。最終の回は2Dのクリックで会話 |
| 13・14 | 2Dで同じカラス | 同じ木に止まっている |
| 15〜18 | ベンチに座る → 3D → 座ったまま → Aで立つ | 座ったままキーを押しても歩かない。足音0回 |
| 19・20 | 入室、机・ベッド・棚 | 入口(0,−3)、机で「つくえ。」。ベッド・棚は画面で確認（調べる操作は Phase 4 の検査で確認済み） |
| 21〜23 | クッションに座る → 切替 → 立つ | 3D・2D（仮画像）とも座ったまま |
| 24〜27 | AUTO ON → 60秒 → 開始 → 手動で停止 | 55秒では動かず、65秒で歩く。キーで止まる |
| 28〜30 | 退室 → 家の正面 → 通常のカメラ | Sを押したまま、0.6秒後に歩き出す（夜） |
| 31 | Life Log | 表示される |
| 32 | 朝夜 | 実時間で夜になった回があり、`?debug=1` で朝・夜を固定して撮影（32a〜32d） |
| 33・34 | 再読み込み・保存 | 再読み込みの前後で `v1|593|2034` が同じ。画面の歩数も同じ |

- 3Dのクリック: PC では、ﾊﾙｶﾁｬﾝをクリックすると正面の家具を調べます。カラスは、2D本編と同じくカラスをクリックして話しかけます。操作の仕組みは変えていません。
- 自動操作の問題で、全体を何度か（7回）やり直しました（2D/3D の状態がずれた、髪の青と画架の絵の青が混ざって位置がずれた、52章のブラウザ共有、など）。どれも操作スクリプト側や環境の問題で、ゲームの不具合ではありませんでした。途中の回の画像は `pc_run1_discarded` などに残しています。

## 43. Mobile WebGL

- 25〜27章のとおり、844×390・DPR 2・タッチ・16:9（640×360）で確認しました。
- 指示書に挙がっている確認項目と結果:

| 項目 | 結果 |
|---|---|
| タッチ長押し | 1.3秒で8歩 |
| 2D／3D切替 | OK |
| カラス | Aボタンで会話、声1回 |
| 入退室 | OK |
| 木の半透明 | OK |
| クッション | OK |
| Menu | OK |
| Message | OK |
| 朝夜 | OK |
| 回転案内 | OK |

- 動画07（横画面のプレイ、音つき）、動画08（縦横の回転）があります。
- **物理スマホは未確認です**（28章）。

## 44. Performance

headless Chrome（AMD Radeon 内蔵GPU、60Hz）で、Phase 4 Preview と RC1 を同じ手順で続けて測りました。

- 条件: `?debug=1`、朝に固定、パネルは畳んだ状態
- FPS・フレーム時間: requestAnimationFrame の5秒間
- 数値: パネル（Unity ProfilerRecorder）の値
- 記録: `webgl/perf/`

**PC 1280×720、DPR 2（Unity画面 2048×1152）** — Phase 4 → RC1

| 条件 | FPS | p50/p95/最大 (RC1) | Draw Calls | SetPass | Tris | Memory |
|---|---|---|---|---|---|---|
| 屋外 2D | 60 → 60 | 16.7/16.8/16.8 | 69 → 75 | 65 → 58 | 46,352 → 37,866 | 16.6 → 16.6 MB |
| 屋外 3D | 60 → 60 | 16.7/16.8/16.8 | 108 → 95 | 61 → 53 | 35,818 → 34,668 | 16.7 → 16.7 MB |
| 3D Tree Fade | 60 → 60 | 16.7/16.8/16.9 | 103 → 95 | 67 → 59 | 29,108 → 29,092 | 16.7 → 16.8 MB |
| 3D AUTO中 | 60 → 60 | 16.7/16.8/16.9 | 101 → 102 | 61 → 53 | 35,752 → 35,484 | 16.8 → 16.8 MB |
| 屋内 2D | 60 → 60 | 16.7/16.8/16.8 | 70 → 62 | 64 → 56 | 9,208 → 9,192 | 16.7 → 16.8 MB |
| 屋内 3D | 60 → 60 | 16.7/16.8/16.8 | 76 → 68 | 61 → 53 | 12,130 → 12,114 | 16.6 → 16.8 MB |

**スマホ横 844×390、DPR 2（Unity画面 1247×576）** — Phase 4 → RC1

| 条件 | FPS | Draw Calls | SetPass | Tris | Memory |
|---|---|---|---|---|---|
| 屋外 2D | 60 → 60 | 79 → 85 | 75 → 68 | 46,434 → 37,948 | 16.7 → 16.8 MB |
| 屋外 3D | 60 → 60 | 109 → 104 | 71 → 63 | 34,342 → 35,386 | 16.7 → 16.7 MB |
| 3D Tree Fade | 60 → 60 | 121 → 114 | 77 → 69 | 31,080 → 31,636 | 16.6 → 16.7 MB |
| 3D AUTO中 | 59.8 → 60 | 119 → 114 | 71 → 63 | 39,302 → 36,740 | 16.9 → 16.9 MB |
| 屋内 2D | 60 → 60 | 80 → 72 | 74 → 66 | 9,290 → 9,274 | 16.7 → 16.9 MB |
| 屋内 3D | 60 → 59.8 | 86 → 78 | 71 → 63 | 12,212 → 12,196 | 16.7 → 16.8 MB |

- カラスが飛んでいる間:
  - Phase 4: PC の Tree Fade の計測中に飛んでいました（60fps）。
  - RC1: スマホで話しかけた直後の3秒（飛んでいる間を含む）が 60fps、最大18.7ms でした。飛んでいる間の Draw Calls などは、パネルを読んだ時にはもう止まっていたため、**測れていません**。
- 屋外2Dの三角形が約8,500減ったのは、2Dで3Dの草の房を描かなくなったためです。Draw Calls が+6なのは、草のSpriteをまとめて描く分です。
- SetPass は、どの条件でも約8少なくなっています。理由の内訳は調べていません。
- RC1 の通常ページ（`?debug=1` なし）では、パネルが止まるので数値は出ません。FPS は PC・スマホとも60でした。
- 測っていないもの: GPU・CPU の内訳、物理スマホでの値。大規模な最適化はしていません（必要のない値でした）。

## 45. Build Size

非圧縮の Build フォルダ（バイト）:

| | Phase 4 | RC1 | 差 |
|---|---|---|---|
| .data | 4,704,946 | 4,720,079 | +15,133（草Sprite、クッションの画像、足音、RC Scene） |
| .wasm | 24,633,147 | 24,661,499 | +28,352（コード） |
| .framework.js | 431,905 | 432,136 | +231 |
| .loader.js | 26,982 | 26,982 | 0 |
| 合計 | 29,796,980 | 29,840,696 | **+43,716（+0.15%）** |

RC1 フォルダ全体: 29 MB（ステージングページ・文書を含む）。

## 46. 比較画像

場所: `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE5_CAPTURES\`

| 指示書の項目 | 画像 |
|---|---|
| 2D: 旧本編・Phase 4・Phase 5 の草原、朝・夜 | `sheets/01_2d_grass_original_p4_p5.png`、`unity/grass2d_*` |
| 2D: Player・Crow の足元 | `sheets/02_2d_feet_player_crow.png` |
| 2D: Bench Sit・Cushion Sit | `sheets/03_2d_bench_cushion_sit.png` |
| 3D: Bench Sit 旧/新、左右、Cushion | `sheets/04_3d_bench_cushion_old_new.png`、`unity/bench_*`、`cushion_*`（夜あり） |
| 3D: 退室Camera、木の半透明、Crowの飛行・止まり木、屋内カメラ | `sheets/05_3d_exit_treefade_crow_indoor.png` |
| Mobile: 横画面2D/3D、回転案内、Safe Area、ステージングのiframe | `sheets/06_mobile_staging.png`、`webgl/mobile/`、`webgl/staging/` |
| RC: 起動画面、Menu、Life Log、2D／3D切替、Debug Panelなしの全景 | `sheets/07_rc1_start_menu_lifelog.png`、`webgl/pc/PC_FLOW_SHEET.png` |
| 旧データの引き継ぎ | `webgl/migration/m1〜m5` |

## 47. 動画

場所: `HARUKA_WORLD_ver3.0_PHASE5_CAPTURES\videos\`（画面の記録は約58〜60fps、H.264）

| # | ファイル | 長さ | 音 |
|---|---|---|---|
| 1 | `01_2d3d_overall_play.mp4`（2D/3D 総合プレイ） | 107秒 | 録音した実際の音 |
| 2 | `02_footsteps_walk_2d_3d_with_audio.mp4`（足音を伴う移動） | 15秒 | 実際の音（12回が12歩と一致） |
| 3 | `03_2d_grass_feet_layering.mp4`（草の前後関係、拡大） | 5秒 | 実際の音 |
| 4 | `04_sit_stand_bench_cushion.mp4`（座る・立つ） | 35秒 | 実際の音 |
| 5 | `05_crow_talk_takeoff_flight_perch_3d.mp4`（カラスの飛行） | 11秒 | **音なし**（この回は録音の無音部分が詰まってしまい、合わせられなかったため。声は動画01の中にあります） |
| 6 | `06_house_enter_exit.mp4`（入退室） | 39秒 | 実際の音 |
| 7 | `07_phone_landscape_play.mp4`（スマホ横画面） | 15秒 | 実際の音 |
| 8 | `08_phone_rotation_portrait_landscape.mp4`（縦横回転） | 9秒 | なし |
| 9 | `09_staging_page_startup.mp4`（ステージングでの起動） | 19秒 | なし（起動だけで音は出ていない） |

「実際の音」は、ページが出した音をデジタルで録音したものです。耳で聞いて確かめたものではありません（10章）。

## 48. RC1起動方法

1. `C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_RC1\start_rc1.bat` をダブルクリックします（Python 3 が必要です）。
2. ブラウザで `http://127.0.0.1:8895/halkaworld/`（ステージングページ）が開きます。
3. 他のページ:
   - ゲームだけ: `…/halkaworld/webgl/`
   - 改善案つきのページ: `…/halkaworld/index-mobilefix.html`
   - 検証パネル: `…/webgl/?debug=1`
4. スマホ実機で見るとき: `start_rc1.bat lan`（同じWi-Fiだけ。手順は `DEVICE_TEST_PROCEDURE_RC1.md`）。
5. 止めるときは、黒いウィンドウで Ctrl+C を押します。

文書: `README_RC1.txt`、`DEBUG_RC1.txt`、`VERIFICATION_SETTINGS_RC1.txt`、`DEVICE_TEST_PROCEDURE_RC1.md`、`BUILD_INFO.txt`（build `cb12fe67b80d425383ebd58c5fe18b7f`）

※ この報告の時点では、確認のためにローカルサーバー（8895）を動かしたままにしています。

## 49. Git Commit

| コミット | 内容 |
|---|---|
| `68eec55` | Phase 5: はるかわーるど ver3.0 Release Candidate 1（コード、Scene、検査、RC1一式） |
| （次のコミット） | このレポート |

どちらも `claude/ver3.0-2d3d-phase5` だけにあります。Push はしていません。

## 50. 最終Git状態

- `claude/ver3.0-2d3d-phase5`: Phase 4 の `90b7383` の上に、Phase 5 のコミットがあります。作業ツリーに変更は残っていません（Unity が書いた改行の違いだけの `.mat` を除く）。
- `main`: `d039acf`（変更なし）。`origin/main`: `3d6ea8c`（変更なし）。
- Phase 1〜4 のブランチと Preview は、上書きしていません。

## 51. 公開版の保護状況

| 項目 | 状態 |
|---|---|
| main へのMerge、origin へのPush、force push | していない |
| GitHub Pages・公開HP・公開中の ver2.9・本番のWebGL Build | 変えていない。repo の `halkaworld/` は ver2.9 のまま（検査でも確認） |
| `HALKA HP` の `GrassDecoration.prefab` の変更と `backup-v05-20261006-174557/` | そのまま（作業の最後に確認） |
| Site Editor、みねはうす、ポイント、他のページ | repo のファイルは変えていない（下の52章の件はブラウザの操作で、ファイルではありません） |
| 正式サイトの保存データ | 触れていない（ローカルの試験用プロファイルだけ） |
| 正式Map・Catalog・Player Sprite・Crow Sprite・音声・保存キー | 変えていない（36章） |
| 有料ツール・外部サービス | 使っていない（ffmpeg と Chrome はローカル） |

## 52. 未解決事項

1. **テスト用 Chrome の共有について（報告）**
   - 何が起きたか: 私が起動した headless Chrome（ポート9334、プロファイル `chrome-p5-desktop`）のタブが、PC 総合テストの途中で、別のセッションによって「みねはうす」（`http://localhost:8080/?debug=1`）に切り替わりました。
   - 私のスクリプトが送ったもの: 気づかないまま、そのページに次の操作を送りました。
     - キーの A（1回）
     - S の長押し（約1.8秒）
     - メニューの位置付近のクリック（数回〜十数回）
     - ページの再読み込み（1回）
   - その後: 保存データの読み取りが拒否されたところで止まりました。
   - 影響の範囲: そのブラウザのタブの中の操作です。ファイル、repo、公開サイトには何も書いていません。ただし、みねはうすのページがブラウザに何かを保存する作りなら、その状態が変わった可能性があります。
   - 対策: それ以降は、Phase 5 専用のポート（9371、9372、9336）だけを使っています。操作の前に毎回ページのオリジンを確かめ、違ったら止めるようにしました。9334 の Chrome は、他のセッションが使っているかもしれないため、止めていません。
2. **スマホ横画面の2Dでの表示**
   - 画面の端に、マップの外側の暗い色（背景色 #101014）が見えます。
   - 右上の「ver3.0 RC1」は、その上で読みにくくなっています（Phase 4 から同じ）。
   - 文字の色は2D本編の `GameHud` なので、変えていません。
3. **公開ページのスマホ横画面での枠の高さ**（30章）。改善案は作りましたが、採用するかどうかはまだ決まっていません。
4. **クッションの2D画像は仮**です。正式なイラストに差し替えられる構造にしてあります。
5. **カラスが飛んでいる間の Draw Calls など**は、RC1 では測れていません（FPS は測りました）。
6. **Safe Area の値だけが変わった場合**は、次の画面サイズの変化まで反映されません（27章）。

## 53. 物理実機で必要な確認

`DEVICE_TEST_PROCEDURE_RC1.md` の19項目です。特に次の点です。

- iPhone（Safari）と Android（Chrome）での横画面の操作、足音、カラスの声（iPhone のマナーモードの場合も）
- ノッチや角丸のある機種での Safe Area
- 回転したときの案内と、拡大が残らないこと
- 公開ページと同じ枠（index.html）で全体が見えるか（エミュレーションでは見えなかった）
- 10分ほど続けたときの動きの重さと、本体の熱
- タッチで操作するとき、ページがスクロールしないか
- 実機の FPS

## 54. 公開前にユーザーが確認すべき事項

1. RC1 を実際に遊んで確かめてください（足音、草、座る位置、クッションの仮画像、退室時のキー）。
2. クッションの2D画像を、仮のままにするか、正式なイラストを用意するか。
3. 退室時の「押したままのキーで0.6秒後に歩き続ける」を正式にするか（Phase 4 の動きにも戻せます）。
4. 公開ページのスマホ横画面の枠を、改善案のようにするか。
5. 公開ページの見出し・タイトル・OGPの「HALKA WORLD」を「はるかわーるど」にするか。
6. 看板の「ここは HALKA WORLD。」をそのままにするか（変えるならマップデータの文章を直すことになります）。
7. 更新履歴（ver3.0）と操作説明（「ひょうじ」で3D）の文章。
8. 物理スマホでの確認（53章）。
9. 公開後: halkaclub.com で、前の歩数・プレイ時間が引き継がれているか（33章の「公開後に確認すること」）。

## 55. Phase 6への提案

（承認があるまで着手しません）

1. 正式な3Dの ﾊﾙｶﾁｬﾝのモデルが届いたら、`PocActorPresentation` と座りのMetadata（`SeatDisplay`）を差し替えて調整する。
2. 物理スマホでの確認結果の反映（枠、Safe Area、音）。
3. 公開ページの更新案を、ユーザーの承認後にまとめる（見出し、ver3.0 の更新履歴、操作説明、スマホの枠）。
4. 本番の Build の作り方（ProjectBuilder との関係、build ID、キャッシュ）を決め、公開用のリハーサルを repo の外で行う。
5. 右上の表示を、背景が暗くても読めるようにする方法（GameHud の文字の縁取りなど。本編側の変更になるため要相談）。

---

### 付録: 主なログ

`C:\Users\owner\Documents\ChatGPT\HARUKA_WORLD_ver3.0_PHASE5_LOGS\`

| ログ | 内容 |
|---|---|
| `20a`〜`20h` | 最終の回帰検査 |
| `20i` | Phase 5 の検査 |
| `18-build-rc1-webgl.log` | RC1 の Build |
| `21-sha256-after-comparison.txt` | SHA-256 の比較 |
