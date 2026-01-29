using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 敵UIの配置・管理を担当するコントローラー。
/// Phase 3c: WatchUIUpdateから敵配置機能を分離。
/// </summary>
public sealed class EnemyPlacementController : IEnemyPlacementController
{
    private readonly EnemyPlacementConfig _config;
    private readonly Func<Vector2> _getGotoPos;
    private readonly Func<Vector2> _getGotoScaleXY;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="config">敵配置設定</param>
    /// <param name="getGotoPos">ズーム目標位置取得デリゲート</param>
    /// <param name="getGotoScaleXY">ズーム目標スケール取得デリゲート</param>
    public EnemyPlacementController(
        EnemyPlacementConfig config,
        Func<Vector2> getGotoPos,
        Func<Vector2> getGotoScaleXY)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _getGotoPos = getGotoPos ?? throw new ArgumentNullException(nameof(getGotoPos));
        _getGotoScaleXY = getGotoScaleXY ?? throw new ArgumentNullException(nameof(getGotoScaleXY));
    }

    /// <inheritdoc/>
    public RectTransform SpawnArea => _config.SpawnArea;

    /// <inheritdoc/>
    public Transform BattleLayer => _config.BattleLayer;

    /// <inheritdoc/>
    public async UniTask PlaceEnemiesAsync(BattleGroup enemyGroup)
    {
        if (enemyGroup?.Ours == null || _config.BattleLayer == null) return;

        if (_config.EnableVerboseLogs)
        {
            Debug.Log($"PlaceEnemiesFromBattleGroup開始: 敵数={enemyGroup.Ours.Count}");
        }

        var placedWorldPositions = new List<Vector2>();

        if (_config.ThrottleSpawns)
        {
            await PlaceEnemiesThrottled(enemyGroup, placedWorldPositions);
        }
        else
        {
            await PlaceEnemiesParallel(enemyGroup, placedWorldPositions);
        }
    }

    /// <summary>
    /// スロットル有り: バッチ単位かつフレーム分散で逐次生成
    /// </summary>
    private async UniTask PlaceEnemiesThrottled(BattleGroup enemyGroup, List<Vector2> placedWorldPositions)
    {
        int batchCounter = 0;
        var batchCreated = new List<BattleIconUI>();

        foreach (var character in enemyGroup.Ours)
        {
            if (character is NormalEnemy enemy)
            {
                var preZoomLocal = ComputePreZoomLocalPosition(enemy, placedWorldPositions, out var chosenWorldPos);
                placedWorldPositions.Add(chosenWorldPos);

                var ui = await PlaceEnemyUI(enemy, preZoomLocal);
                if (ui != null) batchCreated.Add(ui);

                batchCounter++;
                if (batchCounter >= Mathf.Max(1, _config.SpawnBatchSize))
                {
                    batchCounter = 0;
                    ActivateBatch(batchCreated);
                    batchCreated.Clear();

                    for (int f = 0; f < Mathf.Max(0, _config.SpawnInterBatchFrames); f++)
                    {
                        await UniTask.NextFrame();
                    }
                }
            }
        }

        if (batchCreated.Count > 0)
        {
            ActivateBatch(batchCreated);
        }
    }

    /// <summary>
    /// 旧挙動: 並列生成（スパイクが発生しやすい）
    /// </summary>
    private async UniTask PlaceEnemiesParallel(BattleGroup enemyGroup, List<Vector2> placedWorldPositions)
    {
        var tasks = new List<UniTask<BattleIconUI>>();

        foreach (var character in enemyGroup.Ours)
        {
            if (character is NormalEnemy enemy)
            {
                var preZoomLocal = ComputePreZoomLocalPosition(enemy, placedWorldPositions, out var chosenWorldPos);
                placedWorldPositions.Add(chosenWorldPos);
                tasks.Add(PlaceEnemyUI(enemy, preZoomLocal));
            }
        }

        var results = await UniTask.WhenAll(tasks);
        ActivateBatch(results);
    }

    /// <summary>
    /// バッチをまとめて有効化
    /// </summary>
    private void ActivateBatch(IEnumerable<BattleIconUI> batch)
    {
        foreach (var ui in batch)
        {
            if (ui != null) ui.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 敵のサイズを考慮してズーム前のローカル座標を計算
    /// </summary>
    private Vector2 ComputePreZoomLocalPosition(NormalEnemy enemy, List<Vector2> placedWorldPositions, out Vector2 chosenWorldPos)
    {
        Vector2 iconSize = (enemy.EnemyGraphicSprite != null)
            ? enemy.EnemyGraphicSprite.rect.size
            : new Vector2(100f, 100f);

        float iconW = iconSize.x;
        float barH = iconW * _config.HpBarSizeRatio.y;
        float vSpace = iconW * _config.HpBarSizeRatio.y * 0.5f;
        float totalBarHeight = barH * 2f + vSpace;
        Vector2 combinedSize = new Vector2(iconW, iconSize.y + vSpace + totalBarHeight);

        return GetRandomPreZoomLocalPosition(
            combinedSize,
            placedWorldPositions,
            _config.Margin,
            out chosenWorldPos);
    }

    /// <summary>
    /// ズーム後のワールド座標をズーム前のenemyBattleLayerローカル座標に変換
    /// </summary>
    private Vector2 WorldToPreZoomLocal(Vector2 targetWorldPos)
    {
        if (_config.BattleLayer == null) return Vector2.zero;

        var gotoPos = _getGotoPos();
        var gotoScaleXY = _getGotoScaleXY();

        var local = ((RectTransform)_config.BattleLayer).InverseTransformPoint(targetWorldPos);
        local = new Vector2(
            (local.x - gotoPos.x) / gotoScaleXY.x,
            (local.y - gotoPos.y) / gotoScaleXY.y
        );

        return local;
    }

    /// <summary>
    /// ズーム後にspawnArea内に収まるズーム前のローカル座標を取得
    /// </summary>
    private Vector2 GetRandomPreZoomLocalPosition(Vector2 enemySize, List<Vector2> existingWorldPositions, float marginSize, out Vector2 chosenWorldPos)
    {
        if (_config.SpawnArea == null)
        {
            Debug.LogWarning("enemySpawnAreaが設定されていません。");
            chosenWorldPos = Vector2.zero;
            return Vector2.zero;
        }

        var rect = _config.SpawnArea.rect;
        var halfEnemySize = enemySize / 2 + Vector2.one * marginSize;

        Debug.Log($"SpawnArea rect: xMin={rect.xMin:F2}, xMax={rect.xMax:F2}, yMin={rect.yMin:F2}, yMax={rect.yMax:F2} | enemySize={enemySize}, half+margin={halfEnemySize}, existingCount={existingWorldPositions?.Count ?? 0}");

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
            var targetWorldPos = _config.SpawnArea.TransformPoint(spawnAreaLocal);

            if (attempt < 3)
            {
                Debug.Log($"Attempt#{attempt}: local={spawnAreaLocal}, world={targetWorldPos}");

                Vector2 halfSize = enemySize / 2 + Vector2.one * marginSize;
                Rect candidateRect = new Rect(spawnAreaLocal - halfSize, enemySize + Vector2.one * marginSize * 2);
                int toShow = Mathf.Min(existingWorldPositions.Count, 2);
                for (int i = 0; i < toShow; i++)
                {
                    var existingWorld = existingWorldPositions[i];
                    var existingLocal = (Vector2)_config.SpawnArea.InverseTransformPoint(new Vector3(existingWorld.x, existingWorld.y, 0));
                    Rect existingRect = new Rect(existingLocal - halfSize, enemySize + Vector2.one * marginSize * 2);
                    bool overlap = candidateRect.Overlaps(existingRect);
                    Debug.Log($"  vs existing[{i}]: localPos={existingLocal}, overlap={overlap}, candRect(local)={candidateRect}, existRect(local)={existingRect}");
                }
            }

            bool validLocal = true;
            Vector2 half = enemySize / 2 + Vector2.one * marginSize;
            Rect cand = new Rect(spawnAreaLocal - half, enemySize + Vector2.one * marginSize * 2);
            for (int i = 0; i < existingWorldPositions.Count; i++)
            {
                var existWorld = existingWorldPositions[i];
                var existLocal = (Vector2)_config.SpawnArea.InverseTransformPoint(new Vector3(existWorld.x, existWorld.y, 0));
                Rect exist = new Rect(existLocal - half, enemySize + Vector2.one * marginSize * 2);
                if (cand.Overlaps(exist)) { validLocal = false; break; }
            }

            if (validLocal)
            {
                chosenWorldPos = targetWorldPos;
                if (attempt > 0)
                {
                    Debug.Log($"Valid position found at attempt#{attempt}: local={spawnAreaLocal}, world={targetWorldPos}");
                }
                return WorldToPreZoomLocal(targetWorldPos);
            }
        }

        var fallbackWorld = _config.SpawnArea.TransformPoint(rect.center);
        Debug.LogWarning($"GetRandomPreZoomLocalPosition: fallback to center. enemySize={enemySize}, margin={marginSize}");
        chosenWorldPos = fallbackWorld;
        return WorldToPreZoomLocal(fallbackWorld);
    }

    /// <summary>
    /// 個別の敵UIを配置（ズーム前座標で即座に配置）
    /// </summary>
    private UniTask<BattleIconUI> PlaceEnemyUI(NormalEnemy enemy, Vector2 preZoomLocalPosition)
    {
        if (_config.EnemyUIPrefab == null)
        {
            Debug.LogWarning("enemyUIPrefab が設定されていません。敵UIを生成できません。");
            return UniTask.FromResult<BattleIconUI>(null);
        }

        if (_config.BattleLayer == null)
        {
            Debug.LogWarning("enemyBattleLayerが設定されていません。");
            return UniTask.FromResult<BattleIconUI>(null);
        }

        BattleIconUI uiInstance = null;

        if (_config.EnableVerboseLogs)
        {
            Debug.Log($"[Prefab ref] enemyUIPrefab.activeSelf={_config.EnemyUIPrefab.gameObject.activeSelf}", _config.EnemyUIPrefab);
            Debug.Log($"[Parent] enemyBattleLayer activeSelf={_config.BattleLayer.gameObject.activeSelf}, inHierarchy={_config.BattleLayer.gameObject.activeInHierarchy}", _config.BattleLayer);
        }

#if UNITY_EDITOR
        if (_config.EnableVerboseLogs)
        {
            Debug.Log($"[Prefab path] {AssetDatabase.GetAssetPath(_config.EnemyUIPrefab)}", _config.EnemyUIPrefab);
            if (!_config.EnemyUIPrefab.gameObject.activeSelf)
            {
                Debug.LogWarning($"[Detect] Prefab asset inactive at call. path={AssetDatabase.GetAssetPath(_config.EnemyUIPrefab)}\n{new System.Diagnostics.StackTrace(true)}", _config.EnemyUIPrefab);
            }
        }
#endif

        var uiInstanceSpawn = UnityEngine.Object.Instantiate(_config.EnemyUIPrefab, _config.BattleLayer, false);
        uiInstanceSpawn.gameObject.SetActive(false);
        uiInstance = uiInstanceSpawn;

        if (_config.EnableVerboseLogs)
        {
            Debug.Log($"[Instantiated] {uiInstance.name} activeSelf={uiInstance.gameObject.activeSelf}, inHierarchy={uiInstance.gameObject.activeInHierarchy}", uiInstance);
        }

        var rectTransform = (RectTransform)uiInstance.transform;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = preZoomLocalPosition;

        SetupIcon(uiInstance, enemy);
        SetupHPBar(uiInstance, enemy, rectTransform);

        enemy.BindBattleIconUI(uiInstance);

        return UniTask.FromResult(uiInstance);
    }

    /// <summary>
    /// アイコンのセットアップ
    /// </summary>
    private void SetupIcon(BattleIconUI uiInstance, NormalEnemy enemy)
    {
        if (uiInstance.Icon == null) return;

        uiInstance.Icon.preserveAspect = true;

        if (enemy.EnemyGraphicSprite != null)
        {
            uiInstance.Icon.sprite = enemy.EnemyGraphicSprite;
            uiInstance.Icon.type = UnityEngine.UI.Image.Type.Simple;
            uiInstance.Icon.useSpriteMesh = true;
            uiInstance.Icon.color = Color.white;
            uiInstance.Icon.material = null;

            if (_config.EnableVerboseLogs)
            {
                var spr = uiInstance.Icon.sprite;
                Debug.Log($"Enemy Icon sprite assigned: name={spr.name}, tex={(spr.texture != null ? spr.texture.name : "<no texture>")}, rect={spr.rect}, ppu={spr.pixelsPerUnit}");
            }
        }
        else
        {
            uiInstance.Icon.enabled = false;
            Debug.LogWarning($"Enemy Icon sprite is NULL for enemy: {enemy.CharacterName}. Icon will be hidden to avoid white box.\nCheck: NormalEnemy.EnemyGraphicSprite assignment and Texture import type (Sprite [2D and UI]).");
        }

        var iconRT = (RectTransform)uiInstance.Icon.transform;
        if (uiInstance.Icon.sprite != null)
        {
            iconRT.sizeDelta = uiInstance.Icon.sprite.rect.size;
            uiInstance.Icon.SetNativeSize();
            uiInstance.Icon.enabled = true;
        }
        else
        {
            iconRT.sizeDelta = new Vector2(100f, 100f);
        }

        uiInstance.Init();
    }

    /// <summary>
    /// HPバーのセットアップ
    /// </summary>
    private void SetupHPBar(BattleIconUI uiInstance, NormalEnemy enemy, RectTransform rectTransform)
    {
        if (uiInstance.HPBar == null) return;

        float iconW = (uiInstance.Icon != null)
            ? ((RectTransform)uiInstance.Icon.transform).sizeDelta.x
            : rectTransform.sizeDelta.x;

        float barW = iconW * _config.HpBarSizeRatio.x;
        float barH = iconW * _config.HpBarSizeRatio.y;
        uiInstance.HPBar.SetSize(barW, barH);

        float verticalSpacing = iconW * _config.HpBarSizeRatio.y * 0.5f;
        uiInstance.HPBar.VerticalSpacing = verticalSpacing;

        var barRT = (RectTransform)uiInstance.HPBar.transform;
        barRT.pivot = new Vector2(0.5f, 1f);
        barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, -verticalSpacing);

        uiInstance.HPBar.SetBothBarsImmediate(
            enemy.HP / enemy.MaxHP,
            enemy.MentalHP / enemy.MaxHP,
            enemy.GetMentalDivergenceThreshold());

        float totalBarHeight = barH * 2f + uiInstance.HPBar.VerticalSpacing;
        float iconHeight = (uiInstance.Icon != null) ? ((RectTransform)uiInstance.Icon.transform).sizeDelta.y : 0f;
        float totalHeight = iconHeight + verticalSpacing + totalBarHeight;
        rectTransform.sizeDelta = new Vector2(Mathf.Max(rectTransform.sizeDelta.x, iconW), totalHeight);

        if (_config.EnableVerboseLogs)
        {
            Debug.Log($"敵UI配置完了: {enemy.GetHashCode()} at IconW: {iconW}, Bar: {barW}x{barH}");
        }
    }

    /// <inheritdoc/>
    public void ClearEnemyUI()
    {
        if (_config.BattleLayer == null) return;

        for (int i = _config.BattleLayer.childCount - 1; i >= 0; i--)
        {
            var child = _config.BattleLayer.GetChild(i);
            if (child != null)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}
