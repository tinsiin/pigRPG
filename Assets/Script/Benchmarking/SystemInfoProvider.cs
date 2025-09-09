using System;
using UnityEngine;

/// <summary>
/// システム情報を一元管理するユーティリティクラス
/// 各フォーマッターで重複していたシステム情報取得処理を統合
/// </summary>
public static class SystemInfoProvider
{
    /// <summary>
    /// ベンチマーク実行時のシステム情報
    /// </summary>
    public struct SystemEnvironment
    {
        public string UnityVersion;
        public string Platform;
        public string DeviceModel;
        public string QualityLevel;
        public string Timestamp;
        public string DeviceType;
        public string ProcessorType;
        public int ProcessorCount;
        public int SystemMemorySize;
        public int GraphicsMemorySize;
        public string GraphicsDeviceName;
        
        /// <summary>
        /// CSV/JSONヘッダー用の文字列生成
        /// </summary>
        public string ToHeaderString()
        {
            return $"unity_version={UnityVersion},platform={Platform},device_model={DeviceModel},quality_level={QualityLevel}";
        }
        
        /// <summary>
        /// 詳細情報の文字列生成
        /// </summary>
        public string ToDetailedString()
        {
            return $"Unity: {UnityVersion}, Platform: {Platform}, Device: {DeviceModel}, " +
                   $"Quality: {QualityLevel}, CPU: {ProcessorType} x{ProcessorCount}, " +
                   $"Memory: {SystemMemorySize}MB, GPU: {GraphicsDeviceName} ({GraphicsMemorySize}MB)";
        }
    }
    
    /// <summary>
    /// 現在のシステム環境情報を取得
    /// </summary>
    public static SystemEnvironment GetCurrentEnvironment()
    {
        var env = new SystemEnvironment();
        
        env.Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        env.UnityVersion = Application.unityVersion ?? "-";
        env.Platform = Application.platform.ToString();
        env.DeviceModel = SystemInfo.deviceModel ?? "-";
        env.DeviceType = SystemInfo.deviceType.ToString();
        env.ProcessorType = SystemInfo.processorType ?? "-";
        env.ProcessorCount = SystemInfo.processorCount;
        env.SystemMemorySize = SystemInfo.systemMemorySize;
        env.GraphicsMemorySize = SystemInfo.graphicsMemorySize;
        env.GraphicsDeviceName = SystemInfo.graphicsDeviceName ?? "-";
        
        // Quality Level取得
        try
        {
            int qi = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            env.QualityLevel = (names != null && qi >= 0 && qi < names.Length) 
                ? names[qi] 
                : qi.ToString();
        }
        catch
        {
            env.QualityLevel = "-";
        }
        
        return env;
    }
    
    /// <summary>
    /// プリセット設定の文字列化（共通処理）
    /// </summary>
    public static string FormatPreset(WatchUIUpdate.IntroPreset preset)
    {
        return $"{preset.introYieldDuringPrepare.ToString().ToLower()} " +
               $"{preset.introYieldEveryN} " +
               $"{preset.introPreAnimationDelaySec} " +
               $"{preset.introSlideStaggerInterval}";
    }
    
    /// <summary>
    /// エラーメッセージのサニタイズ（CSV用）
    /// </summary>
    public static string SanitizeForCsv(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";
        
        return input
            .Replace(',', ' ')
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Replace('"', '\'');
    }
}