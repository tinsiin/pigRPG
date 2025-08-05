using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WatchUIUpdateクラスに戦闘画面用のレイヤー分離システムを追加しました。
/// 背景と敵は一緒にズームし、味方アイコンは独立してスライドインします。
/// 戦闘エリアはズーム後の座標系で直接デザイン可能です。
/// </summary>
public class WatchUIUpdate : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI StagesString; //ステージとエリア名のテキスト
    [SerializeField] private TenmetuNowImage MapImg; //直接で現在位置表示する簡易マップ
    [SerializeField] private RectTransform bgRect; //背景のRectTransform
    [SerializeField] private DarkWaveManager _waveManager;
    /// <summary>
    /// 敵キャラの参照用HPバー
    /// </summary>
    public CombinedStatesBar EnemyHPBars;

    //ズーム用変数
    [SerializeField] private AnimationCurve _firstZoomAnimationCurve;
    [SerializeField] private float _firstZoomSpeedTime;
    [SerializeField] private Vector2 _gotoPos;
    [SerializeField] private Vector2 _gotoScaleXY;
    // ズーム前の初期状態を自動保存（実行時に設定）
    private Vector2 _originalBackPos;
    private Vector2 _originalBackScale;
    private Vector2 _originalFrontPos;
    private Vector2 _originalFrontScale;

    GameObject[] TwoObjects;//サイドオブジェクトの配列
    SideObjectMove[] SideObjectMoves = new SideObjectMove[2];//サイドオブジェクトのスクリプトの配列
    List<SideObjectMove>[] LiveSideObjects = new List<SideObjectMove>[2] { new List<SideObjectMove>(), new List<SideObjectMove>() };//生きているサイドオブジェクトのリスト 間引き用

    // 戦闘画面レイヤー構成
    [Header("戦闘画面レイヤー構成")]
    [SerializeField] private Transform backgroundZoomLayer;  // 背景ズームレイヤー
    [SerializeField] private Transform enemyBattleLayer;     // 敵配置レイヤー（背景と一緒にズーム）
    [SerializeField] private Transform allyBattleLayer;      // 味方アイコンレイヤー（独立アニメーション）
    [SerializeField] private Transform fixedUILayer;         // 固定UIレイヤー（ズーム対象外）

    // HPバーサイズ設定
    [Header("敵HPバー設定")]
    [SerializeField] private Vector2 hpBarSizeRatio = new Vector2(1.0f, 0.15f); // x: バー幅/アイコン幅, y: バー高/アイコン幅
    
    // 戦闘エリア設定（ズーム後座標系）
    [Header("戦闘エリア設定（ズーム後座標系）")]
    [SerializeField] private RectTransform enemySpawnArea;   // 敵ランダム配置エリア（単一・ズーム対象外）
    [SerializeField] private Transform[] allySpawnPositions; // 味方出現位置
    [SerializeField] private Vector2 allySlideStartOffset = new Vector2(0, -200); // 味方スライドイン開始オフセット

    // 複数階層ZoomContainer方式
    [Header("ズーム対象コンテナ")]
    [SerializeField] private Transform zoomBackContainer;  // 背景用ズームコンテナ
    [SerializeField] private Transform zoomFrontContainer; // 敵用ズームコンテナ

    private void Start()
    {
        TwoObjects = new GameObject[2];//サイドオブジェクト二つ分の生成
        LiveSideObjects = new List<SideObjectMove>[2];//生きているサイドオブジェクトのリスト
        LiveSideObjects[0] = new List<SideObjectMove>();//左右二つ分
        LiveSideObjects[1] = new List<SideObjectMove>();
    }

    RectTransform _rect;
    RectTransform Rect
    {
        get {
            if (_rect == null)
            {
                _rect = GetComponent<RectTransform>();
            }
            return _rect;
        }
    }

    /// <summary>
    ///     歩行時のEYEAREAのUI更新
    /// </summary>
    public void WalkUIUpdate(StageData sd, StageCut sc, PlayersStates pla)
    {
        StagesString.text = sd.StageName + "・\n" + sc.AreaName;
        NowImageCalc(sc, pla);
        SideObjectManage(sc, SideObject_Type.Normal);//サイドオブジェクト
    }

    /// <summary>
    /// エンカウントしたら最初にズームする処理（改良版）
    /// </summary>
    public async UniTask FirstImpressionZoomImproved()
    {
        // 背景+敵レイヤーのズーム、味方アイコンのスライドイン、敵UI生成を同時実行
        var zoomTask = ZoomBackgroundAndEnemies();
        var slideTask = SlideInAllyIcons();
        
        // 敵UI生成も同時に開始（ズーム開始と同タイミング）
        // 注意: 敵UI生成は別途PlaceEnemiesFromBattleGroupで呼び出されることを前提
        
        // 両方のアニメーションが完了するまで待機
        await UniTask.WhenAll(zoomTask, slideTask);
        
        Debug.Log("ズームアニメーション完了。敵UIが正しい位置に配置されているはずです。");
    }

    /// <summary>
    /// 背景と敵コンテナを同時にズーム（新方式）
    /// </summary>
    private async UniTask ZoomBackgroundAndEnemies()
    {
        var tasks = new List<UniTask>();
        
        Debug.Log($"複数コンテナズーム開始: 目標スケール={_gotoScaleXY}, 目標位置={_gotoPos}");
        
        // ズーム前の初期状態を自動保存
        SaveOriginalTransforms();
        
        // ★ ズームと同時に敵UIを生成（並行実行でレスポンシブに）
        var currentBattleManager = Walking.bm;
        if (currentBattleManager?.EnemyGroup != null)
        {
            Debug.Log("ズームと同時に敵UI生成を開始");
            PlaceEnemiesFromBattleGroup(currentBattleManager.EnemyGroup).Forget();
        }
        
        // ZoomBackContainer（背景）のズーム
        if (zoomBackContainer != null)
        {
            var backRect = zoomBackContainer as RectTransform;
            if (backRect != null)
            {
                var nowScale = new Vector2(backRect.localScale.x, backRect.localScale.y);
                var nowPos = new Vector2(backRect.anchoredPosition.x, backRect.anchoredPosition.y);
                
                Debug.Log($"ZoomBackContainer: 現在スケール={nowScale}, 現在位置={nowPos}");
                
                var scaleTask = LMotion.Create(nowScale, _gotoScaleXY, _firstZoomSpeedTime)
                    .WithEase(_firstZoomAnimationCurve)
                    .BindToLocalScaleXY(backRect)
                    .ToUniTask();
                    
                var posTask = LMotion.Create(nowPos, _gotoPos, _firstZoomSpeedTime)
                    .WithEase(_firstZoomAnimationCurve)
                    .BindToAnchoredPosition(backRect)
                    .ToUniTask();
                    
                tasks.Add(scaleTask);
                tasks.Add(posTask);
            }
        }
        
        // ZoomFrontContainer（敵）のズーム（同じパラメータ）
        if (zoomFrontContainer != null)
        {
            var frontRect = zoomFrontContainer as RectTransform;
            if (frontRect != null)
            {
                var nowScale = new Vector2(frontRect.localScale.x, frontRect.localScale.y);
                var nowPos = new Vector2(frontRect.anchoredPosition.x, frontRect.anchoredPosition.y);
                
                Debug.Log($"ZoomFrontContainer: 現在スケール={nowScale}, 現在位置={nowPos}");
                
                var scaleTask = LMotion.Create(nowScale, _gotoScaleXY, _firstZoomSpeedTime)
                    .WithEase(_firstZoomAnimationCurve)
                    .BindToLocalScaleXY(frontRect)
                    .ToUniTask();
                    
                var posTask = LMotion.Create(nowPos, _gotoPos, _firstZoomSpeedTime)
                    .WithEase(_firstZoomAnimationCurve)
                    .BindToAnchoredPosition(frontRect)
                    .ToUniTask();
                    
                tasks.Add(scaleTask);
                tasks.Add(posTask);
            }
        }
        
        Debug.Log($"MiddleFixedLayer, FixedUILayer, EnemySpawnAreaはズーム対象外です。");
        
        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
            Debug.Log("複数コンテナズーム完了");
        }
        else
        {
            Debug.LogWarning("ズーム対象コンテナが設定されていません。zoomBackContainer/zoomFrontContainerを設定してください。");
        }
    }
    
    /// <summary>
    /// RectTransformのワールド座標を取得
    /// </summary>
    private Vector2 GetWorldPosition(RectTransform rectTransform)
    {
        // rect の中心をローカル → ワールド変換
        return rectTransform.TransformPoint(rectTransform.rect.center);
    }
    
    /// <summary>
    /// RectTransformのpivotのワールド座標を取得
    /// </summary>
    private Vector2 GetWorldPivotPosition(RectTransform rectTransform)
    {
        // pivot がローカル原点なので、TransformPointで正確なワールド座標を取得
        return rectTransform.TransformPoint(Vector3.zero);
    }
    
    /// <summary>
    /// 親レイヤー全体ズームと同じ結果になる位置を計算
    /// 数式: targetWorld = (originalWorld - parentPivot) * scale + parentPivot + move
    /// </summary>
    private Vector2 CalculateParentZoomLikePosition(Vector2 originalWorldPos, Vector2 parentPivotWorld, Vector2 scale, Vector2 move)
    {
        return (originalWorldPos - parentPivotWorld) * scale + parentPivotWorld + move;
    }

    /// <summary>
    /// ズーム前の初期状態を保存
    /// </summary>
    private void SaveOriginalTransforms()
    {
        if (zoomBackContainer != null)
        {
            var backRect = zoomBackContainer as RectTransform;
            if (backRect != null)
            {
                _originalBackPos = backRect.anchoredPosition;
                _originalBackScale = backRect.localScale;
                Debug.Log($"ZoomBackContainer初期状態保存: pos={_originalBackPos}, scale={_originalBackScale}");
            }
        }
        
        if (zoomFrontContainer != null)
        {
            var frontRect = zoomFrontContainer as RectTransform;
            if (frontRect != null)
            {
                _originalFrontPos = frontRect.anchoredPosition;
                _originalFrontScale = frontRect.localScale;
                Debug.Log($"ZoomFrontContainer初期状態保存: pos={_originalFrontPos}, scale={_originalFrontScale}");
            }
        }
    }
    
    /// <summary>
    /// ズーム前の状態に戻す
    /// </summary>
    public async UniTask RestoreOriginalTransforms(float duration = 1.0f)
    {
        var tasks = new List<UniTask>();
        
        Debug.Log("ズーム状態を初期状態に戻します");
        
        if (zoomBackContainer != null)
        {
            var backRect = zoomBackContainer as RectTransform;
            if (backRect != null)
            {
                var currentPos = backRect.anchoredPosition;
                var currentScale = backRect.localScale;
                
                var posTask = LMotion.Create(currentPos, _originalBackPos, duration)
                    .WithEase(Ease.OutQuart)
                    .BindToAnchoredPosition(backRect)
                    .ToUniTask();
                    
                var scaleTask = LMotion.Create((Vector3)currentScale, (Vector3)_originalBackScale, duration)
                    .WithEase(Ease.OutQuart)
                    .BindToLocalScale(backRect)
                    .ToUniTask();
                    
                tasks.Add(posTask);
                tasks.Add(scaleTask);
            }
        }
        
        if (zoomFrontContainer != null)
        {
            var frontRect = zoomFrontContainer as RectTransform;
            if (frontRect != null)
            {
                var currentPos = frontRect.anchoredPosition;
                var currentScale = frontRect.localScale;
                
                var posTask = LMotion.Create(currentPos, _originalFrontPos, duration)
                    .WithEase(Ease.OutQuart)
                    .BindToAnchoredPosition(frontRect)
                    .ToUniTask();
                    
                var scaleTask = LMotion.Create((Vector3)currentScale, (Vector3)_originalFrontScale, duration)
                    .WithEase(Ease.OutQuart)
                    .BindToLocalScale(frontRect)
                    .ToUniTask();
                    
                tasks.Add(posTask);
                tasks.Add(scaleTask);
            }
        }
        
        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
            Debug.Log("ズーム状態の復元完了");
        }
        else
        {
            Debug.LogWarning("復元対象のコンテナが見つかりません");
        }
    }

    /// <summary>
    /// ワールド座標をRectTransformのanchoredPosition座標系へ変換
    /// </summary>
    private Vector2 WorldToAnchoredPosition(RectTransform rectTransform, Vector2 worldPos)
    {
        var parent = rectTransform.parent as RectTransform;
        if (parent == null)
        {
            // 親がRectTransformでない場合はそのままlocalへ変換
            return rectTransform.InverseTransformPoint(worldPos);
        }
        Vector2 localPoint = parent.InverseTransformPoint(worldPos);
        return localPoint;
    }

    /// <summary>
    /// 味方アイコンを下からスライドイン
    /// </summary>
    private async UniTask SlideInAllyIcons()
    {
        Debug.Log($"SlideInAllyIcons: allyBattleLayer={allyBattleLayer?.name}, allySpawnPositions={allySpawnPositions?.Length}");
        
        if (allyBattleLayer == null)
        {
            Debug.LogWarning("allyBattleLayerがnullです。設定してください。");
            return;
        }
        
        if (allySpawnPositions == null)
        {
            Debug.LogWarning("allySpawnPositionsがnullです。設定してください。");
            return;
        }
        //味方アイコンレイヤーを表示する
        allyBattleLayer.gameObject.SetActive(true);
        
        var tasks = new List<UniTask>();
        
        for (int i = 0; i < allySpawnPositions.Length; i++)
        {
            var allyIcon = allySpawnPositions[i];
            if (allyIcon != null)
            {
                var rect = allyIcon as RectTransform;
                if (rect != null)
                {
                    // 開始位置を下にオフセット
                    var startPos = rect.anchoredPosition + allySlideStartOffset;
                    var endPos = rect.anchoredPosition;
                    
                    rect.anchoredPosition = startPos;
                    
                    // スライドインアニメーション（少しずつ遅延）
                    var delay = i * 0.1f;
                    var slideTask = UniTask.Delay(TimeSpan.FromSeconds(delay))
                        .ContinueWith(() => 
                            LMotion.Create(startPos, endPos, 0.5f)
                                .WithEase(Ease.OutBack)
                                .BindToAnchoredPosition(rect)
                                .ToUniTask()
                        );
                    
                    tasks.Add(slideTask);
                }
            }
        }
        
        await UniTask.WhenAll(tasks);
    }


    
    // エディタプレビュー設定
    [Header("エディタプレビュー設定")]
    [SerializeField] private bool enableEditorPreview = false;
    [SerializeField] private Color previewBoxColor = Color.red;

    /// <summary>
    /// 敵をランダムエリア内に配置（安定したローカル座標系ベース）
    /// </summary>
    private Vector2 GetRandomEnemyPosition(Vector2 enemySize, List<Vector2> existingPositions, float marginSize)
    {
        if (enemySpawnArea == null)
        {
            Debug.LogWarning("enemySpawnAreaが設定されていません。");
            return Vector2.zero;
        }
        
        // enemySpawnAreaのローカル矩形を取得（pivot/anchor無関係）
        var rect = enemySpawnArea.rect;
        var halfEnemySize = enemySize / 2;
        
        // ローカル座標でのランダム範囲（中央pivot前提）
        var minX = rect.xMin + halfEnemySize.x;
        var maxX = rect.xMax - halfEnemySize.x;
        var minY = rect.yMin + halfEnemySize.y;
        var maxY = rect.yMax - halfEnemySize.y;
        
        var maxAttempts = 50;
        Debug.Log($"ローカル座標でのスポーン範囲: Rect={rect}, Bounds=({minX},{minY})-({maxX},{maxY})");
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // ローカル座標でランダム位置を生成
            var randomX = UnityEngine.Random.Range(minX, maxX);
            var randomY = UnityEngine.Random.Range(minY, maxY);
            var localPos = new Vector2(randomX, randomY);
            
            // 重複チェック（ローカル座標で実施）
            if (IsPositionValidLocal(localPos, enemySize, existingPositions, marginSize))
            {
                Debug.Log($"ローカル座標で生成位置決定: {localPos}");
                return localPos;
            }
        }
        
        // フォールバック: スポーンエリア中央を使用
        var fallbackPos = rect.center;
        Debug.Log($"フォールバック位置（ローカル座標）: {fallbackPos}");
        return fallbackPos;
    }

    /// <summary>
    /// enemySpawnArea 内でランダムにワールド座標を取得（ズーム後基準）。
    /// ズーム前にこのワールド座標を逆変換して配置することで、ズーム完了後に正しい位置へ収まる。
    /// </summary>
    private Vector2 GetRandomEnemyWorldPosition(Vector2 enemySize, List<Vector2> existingWorldPositions, float marginSize)
    {
        if (enemySpawnArea == null)
        {
            Debug.LogWarning("enemySpawnAreaが設定されていません。");
            return Vector2.zero;
        }

        var rect = enemySpawnArea.rect;
        var halfEnemySize = enemySize / 2;

        var minX = rect.xMin + halfEnemySize.x;
        var maxX = rect.xMax - halfEnemySize.x;
        var minY = rect.yMin + halfEnemySize.y;
        var maxY = rect.yMax - halfEnemySize.y;

        const int maxAttempts = 50;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var randomX = UnityEngine.Random.Range(minX, maxX);
            var randomY = UnityEngine.Random.Range(minY, maxY);
            var localPos = new Vector2(randomX, randomY);
            var worldPos = enemySpawnArea.TransformPoint(localPos);

            if (IsWorldPositionValid(worldPos, enemySize, existingWorldPositions, marginSize))
            {
                return worldPos;
            }
        }

        // フォールバック: スポーンエリア中央
        return enemySpawnArea.TransformPoint(rect.center);
    }

    /// <summary>
    /// 他の敵と重複しないかワールド座標でチェック（矩形ベース）
    /// </summary>
    private bool IsWorldPositionValid(Vector2 candidateWorldPos, Vector2 enemySize, List<Vector2> existingWorldPositions, float marginSize)
    {
        // 候補の矩形を作成（中心座標 + サイズ + マージン）
        Vector2 halfSize = enemySize / 2 + Vector2.one * marginSize;
        Rect candidateRect = new Rect(candidateWorldPos - halfSize, enemySize + Vector2.one * marginSize * 2);
        
        foreach (var existingPos in existingWorldPositions)
        {
            // 既存の敵も同じサイズと仮定して矩形を作成
            Rect existingRect = new Rect(existingPos - halfSize, enemySize + Vector2.one * marginSize * 2);
            
            if (candidateRect.Overlaps(existingRect))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 位置が有効かチェック（他の敵と重複しないか）
    /// </summary>
    private bool IsPositionValidLocal(Vector2 candidatePos, Vector2 enemySize, List<Vector2> existingPositions, float marginSize)
    {
        foreach (var existingPos in existingPositions)
        {
            var distance = Vector2.Distance(candidatePos, existingPos);
            var minDistance = (enemySize.magnitude + marginSize) / 2;
            
            if (distance < minDistance)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// ズーム後のワールド座標をズーム前のenemyBattleLayerローカル座標に変換
    /// ズームパラメータ(_gotoPos, _gotoScaleXY)を考慮した逆算処理
    /// </summary>
    private Vector2 WorldToPreZoomLocal(Vector2 targetWorldPos)
    {
        if (enemyBattleLayer == null) return Vector2.zero;
        
        // ① まずワールド座標をenemyBattleLayerローカル座標に変換
        var local = ((RectTransform)enemyBattleLayer).InverseTransformPoint(targetWorldPos);
        
        // ② ズームで掛かるスケールと平行移動分を逆算
        // enemyBattleLayerは(_gotoScaleXY, _gotoPos)でズームする想定
        // pivot(0.5,0.5)なら「中心からの差分」にスケールが掛かる
        local = new Vector2(
            (local.x - _gotoPos.x) / _gotoScaleXY.x,
            (local.y - _gotoPos.y) / _gotoScaleXY.y
        );
        
        return local;
    }
    

    /// <summary>
    /// ズーム後にspawnArea内に収まるズーム前のローカル座標を取得
    /// 逆算ロジックでズーム後の目標位置を先に決めてからズーム前座標を算出
    /// </summary>
    private Vector2 GetRandomPreZoomLocalPosition(Vector2 enemySize, List<Vector2> existingWorldPositions, float marginSize)
    {
        if (enemySpawnArea == null)
        {
            Debug.LogWarning("enemySpawnAreaが設定されていません。");
            return Vector2.zero;
        }

        var rect = enemySpawnArea.rect;
        var halfEnemySize = enemySize / 2 + Vector2.one * marginSize;

        var minX = rect.xMin + halfEnemySize.x;
        var maxX = rect.xMax - halfEnemySize.x;
        var minY = rect.yMin + halfEnemySize.y;
        var maxY = rect.yMax - halfEnemySize.y;

        const int maxAttempts = 50;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var randomX = UnityEngine.Random.Range(minX, maxX);
            var randomY = UnityEngine.Random.Range(minY, maxY);
            var spawnAreaLocal = new Vector2(randomX, randomY);
            var targetWorldPos = enemySpawnArea.TransformPoint(spawnAreaLocal);

            if (IsWorldPositionValid(targetWorldPos, enemySize, existingWorldPositions, marginSize))
            {
                return WorldToPreZoomLocal(targetWorldPos);
            }
        }

        // フォールバック: スポーンエリア中央
        var fallbackWorld = enemySpawnArea.TransformPoint(rect.center);
        return WorldToPreZoomLocal(fallbackWorld);
    }

    /// <summary>
    /// BattleGroupの敵リストに基づいて敵UIを配置（戦闘参加敵のみ）
    /// </summary>
    public async UniTask PlaceEnemiesFromBattleGroup(BattleGroup enemyGroup)
    {
        if (enemyGroup?.Ours == null || enemyBattleLayer == null) return;
        
        Debug.Log($"PlaceEnemiesFromBattleGroup開始: 敵数={enemyGroup.Ours.Count}");
        
        var placedWorldPositions = new List<Vector2>();
        var tasks = new List<UniTask>();
        
        foreach (var character in enemyGroup.Ours)
        {
            // BaseStatesをNormalEnemyにキャスト
            if (character is NormalEnemy enemy)
            {
                // アイコン+HPバーを含めたUI合計サイズを事前計算
                Vector2 iconSize = enemy.EnemySize;
                if (iconSize == Vector2.zero && enemy.EnemyUIObjects?.EnemyGraphicSprite != null)
                {
                    // Sprite サイズから推測（幅のみ使用）
                    iconSize = enemy.EnemyUIObjects.EnemyGraphicSprite.rect.size;
                }
                float iconW = iconSize.x;
                float barH  = iconW * hpBarSizeRatio.y;
                float vSpace = iconW * hpBarSizeRatio.y * 0.5f;
                float totalBarHeight = barH * 2f + vSpace; // 2段バー + 内部余白
                Vector2 combinedSize = new Vector2(iconW, iconSize.y + vSpace + totalBarHeight);

                var preZoomLocal = GetRandomPreZoomLocalPosition(
                    combinedSize,
                    placedWorldPositions,
                    enemy.MarginSize);

                // ワールド座標も記録（重複チェック用）
                var worldPos = ((RectTransform)enemyBattleLayer).TransformPoint(preZoomLocal);
                placedWorldPositions.Add(worldPos);
                
                var placeTask = PlaceEnemyUI(enemy, preZoomLocal);
                tasks.Add(placeTask);
            }
        }
        
        await UniTask.WhenAll(tasks);
    }

    /// <summary>
    /// 個別の敵UIを配置（ズーム前座標で即座に配置）
    /// </summary>
    private async UniTask PlaceEnemyUI(NormalEnemy enemy, Vector2 preZoomLocalPosition)
    {
        if (enemy.EnemyUIObjects?.EnemyGraphicSprite == null)
        {
            Debug.LogWarning($"敵 '{enemy.GetHashCode()}' のEnemyGraphicSpriteが設定されていません。");
            return;
        }

        if (enemyBattleLayer == null)
        {
            Debug.LogWarning("enemyBattleLayerが設定されていません。");
            return;
        }

        // 敵グラフィックUIを生成（enemyBattleLayer直下）
        var graphicGO = new GameObject($"EnemyGraphic_{enemy.GetHashCode()}");
        graphicGO.transform.SetParent(enemyBattleLayer, false);

        // RectTransform設定
        var rectTransform = graphicGO.AddComponent<RectTransform>();
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = enemy.EnemySize;

        // ズーム前のローカル座標で配置（ズーム後に正しい位置に収まる）
        rectTransform.anchoredPosition = preZoomLocalPosition;
        
        // Imageコンポーネント追加と設定
        var image = graphicGO.AddComponent<Image>();
        image.sprite = enemy.EnemyUIObjects.EnemyGraphicSprite;
        image.preserveAspect = true;
        
        // サイズを画像から自動取得（オプション）
        if (enemy.EnemySize == Vector2.zero && image.sprite != null)
        {
            var spriteSize = image.sprite.rect.size;
            rectTransform.sizeDelta = spriteSize;
        }
        
        // HPバーを生成し、アイコンの子に設定
        var hpBarGO = new GameObject($"HPBar_{enemy.GetHashCode()}");
        hpBarGO.transform.SetParent(graphicGO.transform, false);
        var bar = hpBarGO.AddComponent<CombinedStatesBar>();
        
        // バーサイズと余白をアイコンから算出
        float iconW = rectTransform.sizeDelta.x;
        float barW = iconW * hpBarSizeRatio.x;
        float barH = iconW * hpBarSizeRatio.y;
        bar.SetSize(barW, barH);
        float verticalSpacing = iconW * hpBarSizeRatio.y * 0.5f;
        bar.VerticalSpacing = verticalSpacing;
        
        // HPバーをアイコン直下に配置
        var barRT = (RectTransform)hpBarGO.transform;
        barRT.pivot = new Vector2(0.5f, 1f);
        barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 0f); // 下側基準
        barRT.anchoredPosition = new Vector2(0f, -verticalSpacing);
        
        // データバインド
        enemy.HPBar = bar;
        bar.SetBothBarsImmediate(
            enemy.HP / enemy.MaxHP,
            enemy.MentalHP / enemy.MaxHP,
            enemy.GetMentalDivergenceThreshold());
        
        // 敵GameObjectのサイズをアイコン+HPバーの合計サイズに更新
        float totalBarHeight = barH * 2f + bar.VerticalSpacing; // 2段バー + 内部余白
        float totalHeight = rectTransform.sizeDelta.y + verticalSpacing + totalBarHeight;
        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, totalHeight);
        
        Debug.Log($"敵UI配置完了: {enemy.GetHashCode()} at preZoomLocal={preZoomLocalPosition}, IconSize: {iconW}x{rectTransform.sizeDelta.y}, BarSize: {barW}x{barH}");
        
        // フェード無し、即座に表示
        image.color = Color.white;
        await UniTask.Yield();
    }

    
/// <summary>
    /// 歩行の度に更新されるSideObjectの管理
    /// </summary>
    private void SideObjectManage(StageCut nowStageCut, SideObject_Type type)
    {

        var GetObjects = nowStageCut.GetRandomSideObject();//サイドオブジェクトLEFTとRIGHTを取得

        //サイドオブジェクト二つ分の生成
        for(int i =0; i < 2; i++)
        {
            if (TwoObjects[i] != null)
            {
                SideObjectMoves[i].FadeOut().Forget();//フェードアウトは待たずに処理をする。
            }

            TwoObjects[i] = Instantiate(GetObjects[i], bgRect);//サイドオブジェクトを生成、配列に代入
            var LineObject = TwoObjects[i].GetComponent<UILineRenderer>();
            LineObject.sideObject_Type = type;//引数のタイプを渡す。
            SideObjectMoves[i] = TwoObjects[i].GetComponent<SideObjectMove>();//スクリプトを取得
            SideObjectMoves[i].boostSpeed=3.0f;//スピードを初期化
            LiveSideObjects[i].Add(SideObjectMoves[i]);//生きているリスト(左右どちらか)に追加
            //Debug.Log("サイドオブジェクト生成[" + i +"]");

            //数が多くなりだしたら
            /*if (LiveSideObjects[i].Count > 2) {
                SideObjectMoves[i].boostSpeed = 3.0f;//スピードをブースト

            }*/

        }
    }

    /// <summary>
    ///     簡易マップ現在地のUI更新とその処理
    /// </summary>
    private void NowImageCalc(StageCut sc, PlayersStates player)
    {
        //進行度自体の割合を計算
        var Ratio = (float)player.NowProgress / (sc.AreaDates.Count - 1);
        //進行度÷エリア数(countだから-1) 片方キャストしないと整数同士として小数点以下切り捨てられる。
        //Debug.Log("現在進行度のエリア数に対する割合"+Ratio);

        //lerpがベクトルを設定してくれる、調整された位置を渡す
        MapImg.LocationSet(Vector2.Lerp(sc.MapLineS, sc.MapLineE, Ratio));
    }
}