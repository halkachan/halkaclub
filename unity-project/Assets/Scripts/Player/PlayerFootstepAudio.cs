using System;
using UnityEngine;

namespace Halka.Game.Player
{
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        [SerializeField] private PlayerMover mover;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip footstep;
        // ver3.0 shared world: steps come from SharedWorldSession.StepStarted through PlayStep,
        // so no PlayerMover is wired (the 2D game keeps mover and leaves this off).
        [SerializeField] private bool externallyDriven;
        // ver4.0 (the user's request after the second look: the same bright click hurt the ears
        // after a while): optional soft variants played in turn, each at a slightly different pitch.
        // Left empty (the 2D game), the one footstep plays at its own pitch, as before.
        [SerializeField] private AudioClip[] variants;
        [SerializeField, Range(0f, 0.2f)] private float pitchJitter;
        private int nextVariant;

        public int PlayCount { get; private set; }

        private void Awake()
        {
            if (mover == null && !externallyDriven || source == null || footstep == null)
                throw new InvalidOperationException("Footstep audio references are incomplete");
            footstep.LoadAudioData();
            if (variants != null)
                foreach (var variant in variants)
                    if (variant != null) variant.LoadAudioData();
        }

        private void OnEnable()
        {
            if (mover != null) mover.StepStarted += OnStepStarted;
        }

        private void OnDisable()
        {
            if (mover != null) mover.StepStarted -= OnStepStarted;
        }

        private void OnStepStarted(Vector2Int destinationCell) => PlayStep();

        // One footstep for one step that has started (a blocked step never starts).
        public void PlayStep()
        {
            var clip = footstep;
            if (variants != null && variants.Length > 0)
            {
                clip = variants[nextVariant % variants.Length] ?? footstep;
                nextVariant++;
            }
            if (pitchJitter > 0f) source.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            LastClip = clip;
            source.PlayOneShot(clip);
            PlayCount++;
        }

        public AudioClip LastClip { get; private set; }
    }
}
