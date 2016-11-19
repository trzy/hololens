using UnityEngine;
using UnityEngine.VR.WSA;
using System.Collections;
using System.Collections.Generic;

public class VoiceManager: HoloToolkit.Unity.Singleton<VoiceManager>
{
  public AudioClip voiceArmorHint;

  public enum Voice
  {
    ArmorHint
  }

  private AudioSource m_audio_source;

  public void Play(Voice voice)
  {
    m_audio_source.Stop();
    switch (voice)
    {
      case Voice.ArmorHint:
        m_audio_source.PlayOneShot(voiceArmorHint);
        break;
    }
  }

  private void Awake()
  {
    m_audio_source = GetComponent<AudioSource>();
  }
}
