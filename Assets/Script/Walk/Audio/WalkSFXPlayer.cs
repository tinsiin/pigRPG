using UnityEngine;

public interface IWalkSFXPlayer
{
    void Play(string sfxId);
}

public sealed class WalkSFXPlayer : IWalkSFXPlayer
{
    private readonly AudioSource audioSource;
    private readonly SFXDatabaseSO database;

    public WalkSFXPlayer(AudioSource audioSource, SFXDatabaseSO database = null)
    {
        this.audioSource = audioSource;
        this.database = database;
    }

    public void Play(string sfxId)
    {
        if (string.IsNullOrEmpty(sfxId)) return;
        if (audioSource == null)
        {
            Debug.LogWarning($"WalkSFXPlayer: AudioSource is null, cannot play '{sfxId}'.");
            return;
        }

        var clip = ResolveClip(sfxId);
        if (clip == null)
        {
            Debug.LogWarning($"WalkSFXPlayer: SFX not found: '{sfxId}'.");
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    private AudioClip ResolveClip(string sfxId)
    {
        if (database != null)
        {
            return database.GetClip(sfxId);
        }
        return Resources.Load<AudioClip>($"SFX/{sfxId}");
    }
}

public sealed class NullWalkSFXPlayer : IWalkSFXPlayer
{
    public static readonly NullWalkSFXPlayer Instance = new NullWalkSFXPlayer();
    public void Play(string sfxId) { }
}
