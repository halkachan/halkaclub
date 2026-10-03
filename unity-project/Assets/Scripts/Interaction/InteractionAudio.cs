using System;
using UnityEngine;

namespace Halka.Game.Interaction
{
    // Optional sound played only after an examine action passes its normal checks.
    public sealed class InteractionAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clip;

        public int PlayRequestCount { get; private set; }

        public void Play()
        {
            if (audioSource == null || clip == null)
                throw new InvalidOperationException("Interaction sound is not configured");
            audioSource.PlayOneShot(clip);
            PlayRequestCount++;
        }
    }
}
