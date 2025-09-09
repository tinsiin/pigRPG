using System;
using System.Collections.Generic;

/// <summary>
/// ジェネリックリングバッファ実装
/// MetricsHubの重複コードを削減するための共通実装
/// </summary>
public sealed class RingBuffer<T> where T : class
{
    private readonly T[] _buffer;
    private readonly int _capacity;
    private int _index = -1;
    private int _count = 0;
    
    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be positive", nameof(capacity));
        
        _capacity = capacity;
        _buffer = new T[capacity];
    }
    
    /// <summary>
    /// 新しい要素を追加（古い要素を上書き）
    /// </summary>
    public void Add(T item)
    {
        if (item == null) return;
        
        _index = (_index + 1) % _capacity;
        _buffer[_index] = item;
        
        if (_count < _capacity)
            _count++;
    }
    
    /// <summary>
    /// 最新の要素を取得
    /// </summary>
    public T GetLatest()
    {
        if (_index < 0 || _count == 0)
            return null;
        
        return _buffer[_index];
    }
    
    /// <summary>
    /// 最近の要素をコピー（新しい順）
    /// </summary>
    public int CopyRecent(List<T> dest, int max)
    {
        if (dest == null || max <= 0)
            return 0;
        
        int copyCount = Math.Min(max, _count);
        int startIndex = 0;
        
        for (int i = 0; i < copyCount; i++)
        {
            int idx = (_index - i + _capacity) % _capacity;
            var item = _buffer[idx];
            if (item == null) break;
            dest.Add(item);
            startIndex++;
        }
        
        return startIndex;
    }
    
    /// <summary>
    /// 条件に一致する最近の要素を取得
    /// </summary>
    public List<T> GetRecentWhere(Predicate<T> predicate, int maxCount)
    {
        var result = new List<T>(Math.Min(maxCount, _count));
        
        for (int i = 0; i < _count && result.Count < maxCount; i++)
        {
            int idx = (_index - i + _capacity) % _capacity;
            var item = _buffer[idx];
            if (item != null && predicate(item))
            {
                result.Add(item);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// バッファをクリア
    /// </summary>
    public void Clear()
    {
        Array.Clear(_buffer, 0, _capacity);
        _index = -1;
        _count = 0;
    }
    
    /// <summary>
    /// 現在の要素数
    /// </summary>
    public int Count => _count;
    
    /// <summary>
    /// バッファの容量
    /// </summary>
    public int Capacity => _capacity;
}